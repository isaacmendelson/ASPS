using Common.Entities;
using Interface.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Business.Data.EF.Repositories;

/// <summary>
/// EF Core implementation of <see cref="INotificationOutboxRepository"/>.
/// ASPS-620: Durable notification delivery with ACK and snapshot replay.
/// </summary>
public class NotificationOutboxRepository : INotificationOutboxRepository
{
    private readonly AppDbContext _db;
    private readonly ILogger<NotificationOutboxRepository> _logger;

    public NotificationOutboxRepository(AppDbContext db, ILogger<NotificationOutboxRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task AddAsync(OutboxNotificationEntity notification)
    {
        _db.OutboxNotifications.Add(notification);
        await _db.SaveChangesAsync();
        _logger.LogDebug(
            "[Outbox] Persisted notification {MessageId} type={Type} device={Device}",
            notification.MessageId, notification.NotificationType, notification.DeviceUid);
    }

    public async Task AcknowledgeAsync(string deviceUid, Guid messageId)
    {
        // Mark the outbox row acknowledged — filter by DeviceUid too to prevent
        // Device A from ACKing Device B's notifications (Major-2 fix, ASPS-620).
        var notification = await _db.OutboxNotifications
            .FirstOrDefaultAsync(n => n.MessageId == messageId && n.DeviceUid == deviceUid);

        if (notification is not null && notification.AcknowledgedAt is null)
        {
            notification.AcknowledgedAt = DateTime.UtcNow;
        }

        // Upsert the per-device cursor
        var cursor = await _db.DeviceNotificationCursors
            .FirstOrDefaultAsync(c => c.DeviceUid == deviceUid);

        if (cursor is null)
        {
            cursor = new DeviceNotificationCursorEntity
            {
                DeviceUid = deviceUid,
                LastAcknowledgedMessageId = messageId,
                LastAcknowledgedAt = DateTime.UtcNow,
                TotalAcknowledged = 1
            };
            _db.DeviceNotificationCursors.Add(cursor);
        }
        else
        {
            cursor.LastAcknowledgedMessageId = messageId;
            cursor.LastAcknowledgedAt = DateTime.UtcNow;
            cursor.TotalAcknowledged += 1;
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "[Outbox] ACK received — device={Device} messageId={MessageId} totalAcked={Total}",
            deviceUid, messageId, cursor.TotalAcknowledged);
    }

    public async Task<IReadOnlyList<OutboxNotificationEntity>> GetPendingForDeviceAsync(string deviceUid)
    {
        return await _db.OutboxNotifications
            .Where(n => n.DeviceUid == deviceUid && n.AcknowledgedAt == null)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> GetPendingCountAsync()
    {
        return await _db.OutboxNotifications
            .CountAsync(n => n.AcknowledgedAt == null);
    }

    public async Task<DeviceNotificationCursorEntity?> GetCursorAsync(string deviceUid)
    {
        return await _db.DeviceNotificationCursors
            .FirstOrDefaultAsync(c => c.DeviceUid == deviceUid);
    }

    public async Task PruneAcknowledgedAsync(TimeSpan olderThan)
    {
        var cutoff = DateTime.UtcNow - olderThan;
        // ExecuteDeleteAsync issues a single DELETE statement — avoids loading
        // all rows into memory (Minor fix, ASPS-620).
        var deleted = await _db.OutboxNotifications
            .Where(n => n.AcknowledgedAt != null && n.AcknowledgedAt < cutoff)
            .ExecuteDeleteAsync();

        if (deleted > 0)
            _logger.LogInformation("[Outbox] Pruned {Count} acknowledged notifications older than {Cutoff:o}", deleted, cutoff);
    }
}
