using Interface.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Business.Messaging;

/// <summary>
/// On client reconnect, builds a notification-state snapshot and emits it
/// over the ZMQ PUB socket.
///
/// ASPS-620 AC5/AC6 — Snapshot approach:
///   Rather than replaying historical notification events verbatim (which risks
///   re-triggering side effects and requires the client to deduplicate), the
///   backend sends a snapshot that includes:
///     • The PendingNotificationCount — how many outbox entries are still
///       awaiting ACK for this device.
///     • Idempotent resend of each pending payload, in oldest-first order.
///
///   The client processes each resent payload and ACKs it once applied, exactly
///   as it would for a newly delivered notification.  Because the payloads are
///   the same deterministic JSON that was originally sent, a second delivery
///   produces the same state as the first (idempotent).
/// </summary>
public class ReconnectSnapshotService
{
    private readonly NotificationPublisher _publisher;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReconnectSnapshotService> _logger;

    private static readonly JsonSerializerSettings _jsonSettings = new()
    {
        TypeNameHandling = TypeNameHandling.None,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.None
    };

    public ReconnectSnapshotService(
        NotificationPublisher publisher,
        IServiceScopeFactory scopeFactory,
        ILogger<ReconnectSnapshotService> logger)
    {
        _publisher = publisher;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Send a reconnect snapshot to the device.
    /// Called by <see cref="RealTimeAlertListener"/> when a device re-authenticates.
    /// The snapshot header is sent first; then each pending payload is re-sent
    /// in-order over the same device topic so the client can replay and ACK them.
    /// </summary>
    public async Task SendSnapshotAsync(string deviceUid)
    {
        if (string.IsNullOrEmpty(deviceUid)) return;

        try
        {
            IReadOnlyList<Common.Entities.OutboxNotificationEntity> pending =
                Array.Empty<Common.Entities.OutboxNotificationEntity>();

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var outboxRepo = scope.ServiceProvider.GetRequiredService<INotificationOutboxRepository>();
                pending = await outboxRepo.GetPendingForDeviceAsync(deviceUid);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[ReconnectSnapshot] Could not read outbox for device={Device}; sending empty snapshot",
                    deviceUid);
            }

            _logger.LogInformation(
                "[ReconnectSnapshot] device={Device} pendingCount={Count}",
                deviceUid, pending.Count);

            // 1. Send snapshot header so client knows how many pending items to expect.
            var header = new
            {
                Type = "ReconnectSnapshot",
                MessageId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                DeviceUid = deviceUid,
                PendingNotificationCount = pending.Count
            };
            _publisher.PublishSnapshot(deviceUid, JsonConvert.SerializeObject(header, _jsonSettings));

            // 2. Re-send each pending notification in-order.
            //    The payload is the original serialized JSON stored in the outbox,
            //    so the client sees exactly the same message it would have on first
            //    delivery. Processing it again is idempotent (AC7).
            foreach (var entry in pending)
            {
                _publisher.PublishSnapshot(deviceUid, entry.PayloadJson);
                _logger.LogDebug(
                    "[ReconnectSnapshot] Replayed {MessageId} type={Type} to device={Device}",
                    entry.MessageId, entry.NotificationType, deviceUid);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ReconnectSnapshot] Failed to send snapshot to device={Device}", deviceUid);
        }
    }
}
