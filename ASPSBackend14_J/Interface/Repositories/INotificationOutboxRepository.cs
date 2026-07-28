using Common.Entities;

namespace Interface.Repositories;

/// <summary>
/// Repository for the notification outbox (ASPS-620).
/// </summary>
public interface INotificationOutboxRepository
{
    /// <summary>Persist a new outbox entry before it is sent over ZMQ.</summary>
    Task AddAsync(OutboxNotificationEntity notification);

    /// <summary>
    /// Mark a notification as acknowledged by a device.
    /// Updates both the outbox row and the per-device cursor.
    /// </summary>
    Task AcknowledgeAsync(string deviceUid, Guid messageId);

    /// <summary>
    /// Retrieve all un-acknowledged notifications for a device, ordered oldest-first.
    /// Used to decide whether a snapshot replay is needed on reconnect.
    /// </summary>
    Task<IReadOnlyList<OutboxNotificationEntity>> GetPendingForDeviceAsync(string deviceUid);

    /// <summary>Total number of un-acknowledged notifications (backlog metric).</summary>
    Task<int> GetPendingCountAsync();

    /// <summary>
    /// Get the per-device cursor for a device. Returns null if the device
    /// has never ACK'd any notification.
    /// </summary>
    Task<DeviceNotificationCursorEntity?> GetCursorAsync(string deviceUid);

    /// <summary>
    /// Delete notifications older than <paramref name="olderThan"/> that have
    /// already been acknowledged, to keep the table bounded.
    /// </summary>
    Task PruneAcknowledgedAsync(TimeSpan olderThan);
}
