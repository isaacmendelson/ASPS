namespace WebApi.Services;

/// <summary>
/// Abstraction over the ZMQ bridge to Backend, so <see cref="AgentConnection"/> is
/// unit-testable without real sockets. Implemented by <see cref="AgentGatewayService"/>.
/// </summary>
public interface IAgentBackendGateway
{
    /// <summary>Forward a request payload to Backend's ROUTER (localhost:50001) and await the response.</summary>
    Task<AgentBackendResult> ForwardRequestAsync(string payloadJson, CancellationToken ct);

    /// <summary>Subscribe to Backend's PUB (localhost:50002) on topic <c>device:{deviceUid}</c>.</summary>
    IAgentNotificationSubscription Subscribe(string deviceUid, Func<string, string, Task> onNotification);
}

/// <summary>Handle for an active device-topic subscription; disposing tears down the ZMQ SUB socket.</summary>
public interface IAgentNotificationSubscription : IDisposable
{
    string DeviceUid { get; }
}
