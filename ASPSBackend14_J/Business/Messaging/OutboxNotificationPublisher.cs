using Business.Messaging.Abstractions;
using Common.Entities;
using Common.Generated.Messaging.V1;
using Interface.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Business.Messaging;

/// <summary>
/// Outbox-aware wrapper around <see cref="INotificationEgress"/>.
/// For every notification:
///   1. Persist to <see cref="INotificationOutboxRepository"/> (outbox pattern).
///   2. Increment delivery attempts.
///   3. Send over the egress transport.
///
/// ASPS-620: Durable notification delivery — no message is sent without a DB record.
/// ASPS-683: Depends on the transport-agnostic <see cref="INotificationEgress"/> abstraction.
/// Uses <see cref="IServiceScopeFactory"/> because the outbox repository is Scoped
/// while this publisher is Singleton.
/// </summary>
public class OutboxNotificationPublisher
{
    private readonly INotificationEgress _inner;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxNotificationPublisher> _logger;

    private static readonly JsonSerializerSettings _jsonSettings = new()
    {
        TypeNameHandling = TypeNameHandling.None,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.None
    };

    public OutboxNotificationPublisher(
        INotificationEgress inner,
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxNotificationPublisher> logger)
    {
        _inner = inner;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public wrappers — one per notification type
    // ─────────────────────────────────────────────────────────────────────────

    public async Task PublishImmediateDangerEventAsync(
        string? deviceUid,
        string? userKeyField,
        Business.DomainEvents.ImmediateDangerEvent? evt)
    {
        if (evt is null) return;
        await PersistAsync(Guid.NewGuid(), "ImmediateDangerNotification", deviceUid, userKeyField, new
        {
            Type = "ImmediateDangerNotification",
            Timestamp = DateTime.UtcNow,
            DeviceUid = deviceUid ?? string.Empty,
            Data = evt
        });
        await _inner.PublishImmediateDangerEventAsync(deviceUid, userKeyField, evt);
    }

    // Synchronous overload for callers that cannot await (fire-and-forget with logged error).
    public virtual void PublishImmediateDangerEvent(
        string? deviceUid,
        string? userKeyField,
        Business.DomainEvents.ImmediateDangerEvent? evt)
        => _ = PublishImmediateDangerEventAsync(deviceUid, userKeyField, evt);

    public async Task PublishImmediateDangerEndedAsync(
        string? deviceUid,
        string? userKeyField,
        Business.DomainEvents.ImmediateDangerEnded? evt)
    {
        if (evt is null) return;
        await PersistAsync(Guid.NewGuid(), "ImmediateDangerEndedNotification", deviceUid, userKeyField, new
        {
            Type = "ImmediateDangerEndedNotification",
            Timestamp = DateTime.UtcNow,
            DeviceUid = deviceUid ?? string.Empty,
            Data = evt
        });
        await _inner.PublishImmediateDangerEndedAsync(deviceUid, userKeyField, evt);
    }

    public virtual void PublishImmediateDangerEnded(
        string? deviceUid,
        string? userKeyField,
        Business.DomainEvents.ImmediateDangerEnded? evt)
        => _ = PublishImmediateDangerEndedAsync(deviceUid, userKeyField, evt);

    public async Task PublishSetTrackedDomainsAsync(
        IEnumerable<string> deviceUids,
        string? userKeyField,
        Business.DomainEvents.SetTrackedDomains? evt)
    {
        if (evt is null) return;
        var deviceList = (deviceUids ?? Enumerable.Empty<string>())
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct()
            .ToList();

        var payload = new
        {
            Type = "SetTrackedDomainsNotification",
            Timestamp = DateTime.UtcNow,
            Data = evt
        };
        // Fan out an outbox entry per device — each row needs its own PK (MessageId).
        foreach (var d in deviceList)
            await PersistAsync(Guid.NewGuid(), "SetTrackedDomainsNotification", d, userKeyField, payload);

        if (deviceList.Count == 0 && !string.IsNullOrEmpty(userKeyField))
            await PersistAsync(Guid.NewGuid(), "SetTrackedDomainsNotification", null, userKeyField, payload);

        await _inner.PublishSetTrackedDomainsAsync(deviceList, userKeyField, evt);
    }

    public virtual void PublishSetTrackedDomains(
        IEnumerable<string> deviceUids,
        string? userKeyField,
        Business.DomainEvents.SetTrackedDomains? evt)
        => _ = PublishSetTrackedDomainsAsync(deviceUids, userKeyField, evt);

    public async Task PublishAnalysisResultAsync(
        string? deviceUid,
        string? userKeyField,
        AnalysisResultNotification? notification,
        MessageIdentityV1? messagingIdentity = null)
    {
        if (notification is null) return;
        await PersistAsync(Guid.NewGuid(), "AnalysisResult", deviceUid, userKeyField, new
        {
            Type = "AnalysisResult",
            Timestamp = DateTime.UtcNow,
            DeviceUid = deviceUid ?? string.Empty,
            Data = notification
        });
        await _inner.PublishAnalysisResultAsync(deviceUid, userKeyField, notification, messagingIdentity);
    }

    public virtual void PublishAnalysisResult(
        string? deviceUid,
        string? userKeyField,
        AnalysisResultNotification? notification)
        => _ = PublishAnalysisResultAsync(deviceUid, userKeyField, notification, null);

    public virtual void PublishAnalysisResult(
        string? deviceUid,
        string? userKeyField,
        AnalysisResultNotification? notification,
        MessageIdentityV1? messagingIdentity)
        => _ = PublishAnalysisResultAsync(deviceUid, userKeyField, notification, messagingIdentity);

    public async Task PublishSetBrowserTabsPolicyAsync(
        string? deviceUid,
        string? userKeyField,
        string mode,
        DateTime? validUntil)
    {
        await PersistAsync(Guid.NewGuid(), "SetBrowserTabsPolicyNotification", deviceUid, userKeyField, new
        {
            Type = "SetBrowserTabsPolicyNotification",
            Timestamp = DateTime.UtcNow,
            DeviceUid = deviceUid ?? string.Empty,
            Data = new { deviceUid, userKeyField, mode, validUntil }
        });
        await _inner.PublishSetBrowserTabsPolicyAsync(deviceUid, userKeyField, mode, validUntil);
    }

    public void PublishSetBrowserTabsPolicy(
        string? deviceUid,
        string? userKeyField,
        string mode,
        DateTime? validUntil)
        => _ = PublishSetBrowserTabsPolicyAsync(deviceUid, userKeyField, mode, validUntil);

    // ─────────────────────────────────────────────────────────────────────────
    // Internal — persist outbox entry (DB write completes before returning)
    // ─────────────────────────────────────────────────────────────────────────

    private async Task PersistAsync(Guid messageId, string notificationType, string? deviceUid, string? userKeyField, object payload)
    {
        try
        {
            var json = JsonConvert.SerializeObject(payload, _jsonSettings);
            var entry = new OutboxNotificationEntity
            {
                MessageId = messageId,
                DeviceUid = deviceUid ?? string.Empty,
                UserKeyField = userKeyField ?? string.Empty,
                NotificationType = notificationType,
                PayloadJson = json,
                CreatedAt = DateTime.UtcNow,
                DeliveryAttempts = 1
            };

            // DB write MUST complete before the ZMQ send (outbox guarantee).
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<INotificationOutboxRepository>();
            await repo.AddAsync(entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Outbox] Failed to persist notification {MessageId} type={Type}",
                messageId, notificationType);
            // Re-throw so the caller (ZMQ send) knows the persist failed.
            throw;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Metrics
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the current pending notification backlog size.
    /// Used for logging/metrics (AC8).
    /// </summary>
    public async Task<int> GetBacklogSizeAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<INotificationOutboxRepository>();
            return await repo.GetPendingCountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Outbox] Failed to retrieve backlog size");
            return -1;
        }
    }
}
