using Newtonsoft.Json.Linq;

namespace WebApi.Services;

/// <summary>
/// Frame type discriminators for the <c>/ws/agent</c> WebSocket protocol.
/// See <c>docs/architecture/WS-AGENT-PROTOCOL.md</c> section 3 (ASPS-720 / ASPS-722).
/// </summary>
public static class AgentFrameType
{
    public const string Request = "request";
    public const string Subscribe = "subscribe";
    public const string Pong = "pong";
    public const string Response = "response";
    public const string Notification = "notification";
    public const string Ping = "ping";
    public const string Error = "error";
}

/// <summary>Error codes — WS-AGENT-PROTOCOL.md section 9.</summary>
public static class AgentErrorCode
{
    public const string NotAuthenticated = "auth.not_authenticated";
    public const string DeviceMismatch = "auth.device_mismatch";
    public const string Timeout = "gateway.timeout";
    public const string BackendUnavailable = "gateway.backend_unavailable";
    public const string InvalidFrame = "gateway.invalid_frame";
    public const string FrameTooLarge = "gateway.frame_too_large";
    public const string RateLimited = "gateway.rate_limited";
    public const string UnknownFrameType = "gateway.unknown_frame_type";
    public const string SubscribeFailed = "gateway.subscribe_failed";
}

/// <summary>WS close codes — WS-AGENT-PROTOCOL.md section 9 ("Connection-level vs request-level errors").</summary>
public static class AgentCloseCode
{
    public const int AuthenticationRequired = 4001;
    public const int DeviceMismatch = 4002;
    public const int BackendUnavailable = 4003;
    public const int RateLimited = 4008;
    public const int InvalidFrame = 4009;
}

/// <summary>Gateway authentication state machine — WS-AGENT-PROTOCOL.md section 4.2.</summary>
public enum AgentConnectionState
{
    Unauthenticated,
    Authenticated
}

/// <summary>A parsed, valid inbound frame from the agent.</summary>
public sealed class AgentFrame
{
    public required string Frame { get; init; }
    public string? Id { get; init; }
    public JObject? Payload { get; init; }
    public string? DeviceUid { get; init; }
    public string? Ts { get; init; }
}

/// <summary>Result of validating/parsing a raw inbound WS text frame. Pure — no IO.</summary>
public sealed class AgentFrameParseResult
{
    public bool Success { get; private init; }
    public AgentFrame? Frame { get; private init; }
    public string? ErrorCode { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static AgentFrameParseResult Ok(AgentFrame frame) =>
        new() { Success = true, Frame = frame };

    public static AgentFrameParseResult Fail(string errorCode, string errorMessage) =>
        new() { Success = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}

/// <summary>Outcome of validating a "request" frame's payload against the auth state machine.</summary>
public enum AgentRequestValidation
{
    Allowed,
    NotAuthenticated,
    DeviceMismatch
}

/// <summary>Result of forwarding a request payload to Backend via ZMQ REQ (localhost:50001).</summary>
public sealed class AgentBackendResult
{
    public bool Success { get; private init; }
    public string? ResponseJson { get; private init; }
    public string? ErrorCode { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static AgentBackendResult Ok(string responseJson) =>
        new() { Success = true, ResponseJson = responseJson };

    public static AgentBackendResult Timeout() =>
        new()
        {
            ErrorCode = AgentErrorCode.Timeout,
            ErrorMessage = "Backend did not respond within the configured timeout"
        };

    public static AgentBackendResult BackendUnavailable(string? detail = null) =>
        new()
        {
            ErrorCode = AgentErrorCode.BackendUnavailable,
            ErrorMessage = detail ?? "Gateway cannot reach Backend on localhost"
        };
}
