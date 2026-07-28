using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Common.Entities;

/// <summary>
/// Per-device cursor tracking the last ACK'd outbox notification.
/// ASPS-620: Enables the backend to know which notifications a device has processed.
/// </summary>
[Table("DeviceNotificationCursors")]
public class DeviceNotificationCursorEntity
{
    [Key]
    [MaxLength(255)]
    public string DeviceUid { get; set; } = string.Empty;

    /// <summary>
    /// MessageId of the most recently acknowledged notification for this device.
    /// Null = device has never ACK'd any notification.
    /// </summary>
    public Guid? LastAcknowledgedMessageId { get; set; }

    /// <summary>UTC time of the last ACK received from this device.</summary>
    public DateTime? LastAcknowledgedAt { get; set; }

    /// <summary>Total ACKs received from this device (for metrics).</summary>
    public long TotalAcknowledged { get; set; }
}
