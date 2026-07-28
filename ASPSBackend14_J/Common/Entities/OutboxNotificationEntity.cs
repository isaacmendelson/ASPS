using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Common.Entities;

/// <summary>
/// Outbox table — every notification persisted here before being emitted on ZMQ PUB.
/// ASPS-620: Durable notification delivery with ACK and replay (snapshot approach).
/// </summary>
[Table("OutboxNotifications")]
public class OutboxNotificationEntity
{
    [Key]
    public Guid MessageId { get; set; }

    /// <summary>Device UID this notification targets. Empty means user-topic only.</summary>
    [MaxLength(255)]
    public string DeviceUid { get; set; } = string.Empty;

    /// <summary>User KeyField this notification targets. Empty means device-topic only.</summary>
    [MaxLength(36)]
    public string UserKeyField { get; set; } = string.Empty;

    /// <summary>Notification type string (e.g. "ImmediateDangerNotification", "AnalysisResult").</summary>
    [MaxLength(100)]
    [Required]
    public string NotificationType { get; set; } = string.Empty;

    /// <summary>Full serialized JSON payload sent over the wire.</summary>
    [Column(TypeName = "LONGTEXT")]
    [Required]
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>UTC time the notification was created/published.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// UTC time the device ACK'd this notification.
    /// Null = not yet acknowledged (pending delivery).
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>Number of delivery attempts (for metrics/backlog reporting).</summary>
    public int DeliveryAttempts { get; set; }
}
