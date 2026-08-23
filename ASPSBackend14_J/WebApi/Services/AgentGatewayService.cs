using System.Collections.Concurrent;
using Business.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetMQ;
using NetMQ.Sockets;

namespace WebApi.Services;

/// <summary>Bound from the "AgentGateway" configuration section. See appsettings.json.</summary>
public sealed class AgentGatewayOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxConnectionsPerIp { get; set; } = 10;
    public int MaxFrameSize { get; set; } = 262144;
    public int UnauthenticatedTimeoutSeconds { get; set; } = 30;
    public int AuthenticatedIdleTimeoutSeconds { get; set; } = 300;
    public string BackendHost { get; set; } = "localhost";
    public int BackendReqPort { get; set; } = 50001;
    public int BackendPubPort { get; set; } = 50002;
    public int RequestTimeoutSeconds { get; set; } = 10;
    public int PingIntervalSeconds { get; set; } = 60;
}

/// <summary>
/// Singleton hosted service bridging the <c>/ws/agent</c> WebSocket gateway to
/// Backend's ZMQ sockets on localhost — ROUTER (request/response, port 50001) and
/// PUB (notifications, port 50002). Zero Backend changes: this is just another ZMQ
/// client on localhost, exactly like a desktop agent, using
/// <see cref="CurveKeyManager.ApplyClientCurve"/> for CURVE (ADR-004 / ASPS-718 / ASPS-720).
/// </summary>
public sealed class AgentGatewayService : IAgentBackendGateway, IAgentConnectionLimiter, IHostedService
{
    private readonly CurveKeyManager _curveKeyManager;
    private readonly ILogger<AgentGatewayService> _logger;
    private readonly string _reqEndpoint;
    private readonly string _subEndpoint;
    private readonly ConcurrentDictionary<string, int> _connectionsPerIp = new();

    public AgentGatewayOptions Options { get; }

    public AgentGatewayService(IOptions<AgentGatewayOptions> options, CurveKeyManager curveKeyManager, ILogger<AgentGatewayService> logger)
    {
        _curveKeyManager = curveKeyManager;
        _logger = logger;
        Options = options.Value;
        _reqEndpoint = $"tcp://{Options.BackendHost}:{Options.BackendReqPort}";
        _subEndpoint = $"tcp://{Options.BackendHost}:{Options.BackendPubPort}";
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "AgentGatewayService started — REQ endpoint {ReqEndpoint}, SUB endpoint {SubEndpoint}, enabled={Enabled}",
            _reqEndpoint, _subEndpoint, Options.Enabled);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AgentGatewayService stopped");
        return Task.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IAgentConnectionLimiter — per-IP connection limiting, used by AgentWebSocketMiddleware.
    // ─────────────────────────────────────────────────────────────────────────

    public bool TryAcquireConnectionSlot(string clientIp)
    {
        var updated = _connectionsPerIp.AddOrUpdate(clientIp, 1, (_, count) => count + 1);
        if (updated <= Options.MaxConnectionsPerIp)
            return true;

        _connectionsPerIp.AddOrUpdate(clientIp, 0, (_, count) => Math.Max(0, count - 1));
        return false;
    }

    public void ReleaseConnectionSlot(string clientIp)
    {
        _connectionsPerIp.AddOrUpdate(clientIp, 0, (_, count) => Math.Max(0, count - 1));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IAgentBackendGateway
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Option A per WS-AGENT-PROTOCOL.md section 12.2: a new ZMQ REQ socket per
    /// request, closed after the response. Matches the Python agent's current
    /// pattern; adequate for expected load.
    /// </summary>
    public Task<AgentBackendResult> ForwardRequestAsync(string payloadJson, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            try
            {
                using var socket = new RequestSocket();
                socket.Options.Linger = TimeSpan.Zero;
                _curveKeyManager.ApplyClientCurve(socket);
                socket.Connect(_reqEndpoint);

                var timeout = TimeSpan.FromSeconds(Options.RequestTimeoutSeconds);
                if (!socket.TrySendFrame(timeout, payloadJson))
                    return AgentBackendResult.BackendUnavailable("Failed to send request to Backend within timeout");

                if (!socket.TryReceiveFrameString(timeout, out var response) || response is null)
                    return AgentBackendResult.Timeout();

                return AgentBackendResult.Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding agent request to Backend ROUTER");
                return AgentBackendResult.BackendUnavailable(ex.Message);
            }
        }, ct);
    }

    public IAgentNotificationSubscription Subscribe(string deviceUid, Func<string, string, Task> onNotification) =>
        new AgentNotificationSubscription(deviceUid, _subEndpoint, _curveKeyManager, onNotification, _logger);

    // ─────────────────────────────────────────────────────────────────────────
    // ZMQ SUB socket lifetime for one WS connection's notification subscription.
    // WS-AGENT-PROTOCOL.md section 6: created on subscribe, destroyed on disconnect
    // (or replaced when a new subscribe frame arrives on the same connection).
    // ─────────────────────────────────────────────────────────────────────────

    private sealed class AgentNotificationSubscription : IAgentNotificationSubscription
    {
        private readonly SubscriberSocket _socket;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _pumpTask;
        private readonly ILogger _logger;

        public string DeviceUid { get; }

        public AgentNotificationSubscription(
            string deviceUid,
            string subEndpoint,
            CurveKeyManager curveKeyManager,
            Func<string, string, Task> onNotification,
            ILogger logger)
        {
            DeviceUid = deviceUid;
            _logger = logger;

            _socket = new SubscriberSocket();
            _socket.Options.Linger = TimeSpan.Zero;
            curveKeyManager.ApplyClientCurve(_socket);
            _socket.Connect(subEndpoint);
            _socket.Subscribe($"device:{deviceUid}");

            _pumpTask = Task.Run(() => PumpAsync(onNotification, _cts.Token));
        }

        private async Task PumpAsync(Func<string, string, Task> onNotification, CancellationToken ct)
        {
            var pollTimeout = TimeSpan.FromMilliseconds(500);
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var msg = new NetMQMessage();
                    if (!_socket.TryReceiveMultipartMessage(pollTimeout, ref msg))
                        continue;
                    if (msg.FrameCount < 2)
                        continue;

                    var topic = msg[0].ConvertToString();
                    var json = msg[1].ConvertToString();
                    await onNotification(topic, json);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "Error pumping notification for device {DeviceUid}", DeviceUid);
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            try
            {
                _pumpTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // best effort — connection is closing regardless
            }
            _socket.Dispose();
            _cts.Dispose();
        }
    }
}
