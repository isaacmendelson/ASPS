using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WebApi.Services;

/// <summary>
/// Per-connection state for a <c>/ws/agent</c> WebSocket: the authentication state
/// machine (WS-AGENT-PROTOCOL.md section 4.2), request/response forwarding, and
/// notification-subscription lifetime. IO-agnostic — the caller supplies a
/// <c>send</c> delegate, which keeps this class unit-testable without a real
/// WebSocket or ZMQ socket (see <see cref="IAgentBackendGateway"/>).
///
/// The gateway MUST NOT log token values or payload content (WS-AGENT-PROTOCOL.md
/// section 14) — only deviceUid, frame types, and error codes are logged here.
/// </summary>
public sealed class AgentConnection : IDisposable
{
    private readonly IAgentBackendGateway _gateway;
    private readonly Func<string, CancellationToken, Task> _send;
    private readonly ILogger _logger;
    private readonly string _connectionId;
    private readonly object _subscriptionLock = new();
    private IAgentNotificationSubscription? _subscription;

    public AgentConnectionState State { get; private set; } = AgentConnectionState.Unauthenticated;
    public string? DeviceUid { get; private set; }
    public string? Token { get; private set; }

    public AgentConnection(
        IAgentBackendGateway gateway,
        Func<string, CancellationToken, Task> send,
        ILogger logger,
        string connectionId)
    {
        _gateway = gateway;
        _send = send;
        _logger = logger;
        _connectionId = connectionId;
    }

    /// <summary>Dispatch a parsed inbound frame. Unrecognized-but-parseable frame types (e.g. future additions) are ignored.</summary>
    public Task HandleFrameAsync(AgentFrame frame, CancellationToken ct)
    {
        return frame.Frame switch
        {
            AgentFrameType.Request => HandleRequestAsync(frame, ct),
            AgentFrameType.Subscribe => HandleSubscribeAsync(frame, ct),
            AgentFrameType.Pong => Task.CompletedTask, // informational only — no liveness action required
            _ => Task.CompletedTask
        };
    }

    private async Task HandleRequestAsync(AgentFrame frame, CancellationToken ct)
    {
        var validation = ValidateRequest(frame.Payload);
        if (validation == AgentRequestValidation.NotAuthenticated)
        {
            _logger.LogWarning("Agent {ConnectionId} request rejected: not authenticated", _connectionId);
            await SendError(null, AgentErrorCode.NotAuthenticated,
                "Connection is not authenticated. Send RequestToken first.", ct);
            return;
        }

        if (validation == AgentRequestValidation.DeviceMismatch)
        {
            _logger.LogWarning("Agent {ConnectionId} request rejected: device mismatch", _connectionId);
            await SendError(null, AgentErrorCode.DeviceMismatch,
                "Request device/token does not match the authenticated device.", ct);
            return;
        }

        var payloadJson = frame.Payload!.ToString(Formatting.None);
        var requestDeviceUid = ExtractDeviceUid(frame.Payload);

        var result = await _gateway.ForwardRequestAsync(payloadJson, ct);
        if (!result.Success)
        {
            _logger.LogWarning("Agent {ConnectionId} request {RequestId} failed: {ErrorCode}",
                _connectionId, frame.Id, result.ErrorCode);

            // gateway.timeout is request-level (echoes id); everything else here
            // (gateway.backend_unavailable) is connection-level (id null) per
            // WS-AGENT-PROTOCOL.md section 9.
            var idForError = result.ErrorCode == AgentErrorCode.Timeout ? frame.Id : null;
            await SendError(idForError, result.ErrorCode!, result.ErrorMessage!, ct);
            return;
        }

        ApplyBackendResponse(result.ResponseJson!, requestDeviceUid);
        await _send(AgentFrameBuilder.BuildResponseFrame(frame.Id!, result.ResponseJson!), ct);
    }

    private async Task HandleSubscribeAsync(AgentFrame frame, CancellationToken ct)
    {
        if (State != AgentConnectionState.Authenticated)
        {
            _logger.LogWarning("Agent {ConnectionId} subscribe rejected: not authenticated", _connectionId);
            await SendError(null, AgentErrorCode.NotAuthenticated,
                "Connection is not authenticated. Send RequestToken first.", ct);
            return;
        }

        if (!string.Equals(frame.DeviceUid, DeviceUid, StringComparison.Ordinal))
        {
            _logger.LogWarning("Agent {ConnectionId} subscribe rejected: device mismatch (requested={Requested}, authenticated={Authenticated})",
                _connectionId, frame.DeviceUid, DeviceUid);
            await SendError(null, AgentErrorCode.DeviceMismatch,
                "Subscribe deviceUid does not match the authenticated device.", ct);
            return;
        }

        try
        {
            var newSubscription = _gateway.Subscribe(frame.DeviceUid!,
                (topic, json) => _send(AgentFrameBuilder.BuildNotificationFrame(topic, json), ct));

            IAgentNotificationSubscription? previous;
            lock (_subscriptionLock)
            {
                previous = _subscription;
                _subscription = newSubscription;
            }
            previous?.Dispose();

            _logger.LogInformation("Agent {ConnectionId} subscribed to device {DeviceUid}", _connectionId, frame.DeviceUid);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Agent {ConnectionId} failed to subscribe to device {DeviceUid}", _connectionId, frame.DeviceUid);
            await SendError(null, AgentErrorCode.SubscribeFailed, "Failed to create notification subscription.", ct);
        }
    }

    private Task SendError(string? id, string code, string message, CancellationToken ct) =>
        _send(AgentFrameBuilder.BuildErrorFrame(id, code, message), ct);

    // ─────────────────────────────────────────────────────────────────────────
    // Pure state-machine logic — unit-testable without IO.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>WS-AGENT-PROTOCOL.md section 4.2, rules 1 and 4.</summary>
    public AgentRequestValidation ValidateRequest(JObject? payload)
    {
        if (payload is null)
            return AgentRequestValidation.NotAuthenticated;

        var messageType = payload["MessageType"]?.ToString();

        if (State == AgentConnectionState.Unauthenticated)
        {
            return messageType is "RequestToken" or "RefreshToken" or "RegisterDevice"
                ? AgentRequestValidation.Allowed
                : AgentRequestValidation.NotAuthenticated;
        }

        var token = payload["Token"]?.ToString();
        var deviceInfoUid = payload["DeviceInfo"]?["DeviceUid"]?.ToString();
        var topLevelDeviceUid = payload["DeviceUid"]?.ToString();

        var tokenMatches = !string.IsNullOrEmpty(token) && string.Equals(token, Token, StringComparison.Ordinal);
        var deviceMatches =
            (!string.IsNullOrEmpty(deviceInfoUid) && string.Equals(deviceInfoUid, DeviceUid, StringComparison.Ordinal)) ||
            (!string.IsNullOrEmpty(topLevelDeviceUid) && string.Equals(topLevelDeviceUid, DeviceUid, StringComparison.Ordinal));

        if (tokenMatches || deviceMatches)
            return AgentRequestValidation.Allowed;

        // No identifying field present at all (e.g. a payload with neither Token nor
        // DeviceUid) — nothing to validate against at the gateway layer; Backend's own
        // AlertProcessor token check remains authoritative.
        if (string.IsNullOrEmpty(token) && string.IsNullOrEmpty(deviceInfoUid) && string.IsNullOrEmpty(topLevelDeviceUid))
            return AgentRequestValidation.Allowed;

        return AgentRequestValidation.DeviceMismatch;
    }

    /// <summary>WS-AGENT-PROTOCOL.md section 4.2 state diagram.</summary>
    public void ApplyBackendResponse(string responseJson, string? requestDeviceUid)
    {
        JObject payload;
        try
        {
            using var stringReader = new StringReader(responseJson);
            using var jsonReader = new JsonTextReader(stringReader) { DateParseHandling = DateParseHandling.None };
            payload = (JObject)JToken.ReadFrom(jsonReader);
        }
        catch (JsonException)
        {
            return;
        }

        var status = payload["status"]?.ToString();
        switch (status)
        {
            case "TokenCreated":
            case "TokenRefreshed":
            case "Registered":
                var deviceUid = payload["deviceUid"]?.ToString() ?? requestDeviceUid;
                if (!string.IsNullOrEmpty(deviceUid))
                {
                    DeviceUid = deviceUid;
                    Token = payload["token"]?.ToString();
                    State = AgentConnectionState.Authenticated;
                }
                break;

            case "TokenExpired":
            case "InvalidToken":
                State = AgentConnectionState.Unauthenticated;
                DeviceUid = null;
                Token = null;
                break;
        }
    }

    private static string? ExtractDeviceUid(JObject payload) =>
        payload["DeviceUid"]?.ToString() ?? payload["DeviceInfo"]?["DeviceUid"]?.ToString();

    public void Dispose()
    {
        lock (_subscriptionLock)
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }
}
