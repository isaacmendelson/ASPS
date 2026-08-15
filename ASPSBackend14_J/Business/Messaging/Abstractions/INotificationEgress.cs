using Business.DomainEvents;
using Common.Generated.Messaging.V1;

namespace Business.Messaging.Abstractions;

/// <summary>
/// Abstraction for notification egress — publishes outbound notifications to subscribed
/// clients (devices / admin UI). Transport-agnostic: implementations may use NetMQ PUB,
/// the outbox-wrapped publisher, or any other transport.
/// Method names mirror the existing <see cref="OutboxNotificationPublisher"/> async wrappers.
/// </summary>
public interface INotificationEgress
{
    /// <summary>
    /// Publish an analysis result notification. <paramref name="messagingIdentity"/> is optional —
    /// when supplied, the notification is wrapped in a correlated <c>MessageEnvelopeV1</c>.
    /// </summary>
    Task PublishAnalysisResultAsync(
        string? deviceUid,
        string? userKeyField,
        AnalysisResultNotification? analysisResultNotification,
        MessageIdentityV1? messagingIdentity = null,
        CancellationToken ct = default);

    Task PublishImmediateDangerEventAsync(
        string? deviceUid,
        string? userKeyField,
        ImmediateDangerEvent? immediateDangerEvent,
        CancellationToken ct = default);

    Task PublishImmediateDangerEndedAsync(
        string? deviceUid,
        string? userKeyField,
        ImmediateDangerEnded? immediateDangerEnded,
        CancellationToken ct = default);

    /// <summary>
    /// Fan out a SetTrackedDomains event to every listed device (and optionally the user topic).
    /// </summary>
    Task PublishSetTrackedDomainsAsync(
        IEnumerable<string> deviceUids,
        string? userKeyField,
        SetTrackedDomains? evt,
        CancellationToken ct = default);

    Task PublishSetBrowserTabsPolicyAsync(
        string? deviceUid,
        string? userKeyField,
        string mode,
        DateTime? validUntil,
        CancellationToken ct = default);

    /// <summary>
    /// Publish raw pre-serialized JSON on the device topic — used for reconnect snapshot replay
    /// (ASPS-620), where the payload is already serialized and must not be re-serialized.
    /// </summary>
    Task PublishSnapshotAsync(
        string deviceUid,
        string json,
        CancellationToken ct = default);
}
