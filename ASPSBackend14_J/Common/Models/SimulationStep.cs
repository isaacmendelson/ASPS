using System.ComponentModel.DataAnnotations;
using Common.Enums;

namespace Common.Models;

/// <summary>
/// Represents a single step in a device alert simulation.
/// Each step defines a delay period, target user/device, alert type, and alert details.
/// </summary>
public class SimulationStep
{
    /// <summary>
    /// Sequence number (order) of this step in the simulation
    /// </summary>
    [Required]
    public int Sequence { get; set; }

    /// <summary>
    /// Delay (in milliseconds) after the previous step before executing this step.
    /// For the first step, this is the delay before the simulation starts.
    /// </summary>
    [Required]
    public long DelayMs { get; set; }

    /// <summary>
    /// Target device UID
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string DeviceUid { get; set; } = string.Empty;

    /// <summary>
    /// Target User Key value (the User's KeyField)
    /// </summary>
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Type of alert to simulate (UrlAlert, RemoteAccessAlert, TrackUrlAlert)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string AlertType { get; set; } = string.Empty;

    /// <summary>
    /// Serialized JSON representation of the DeviceAlert (UrlAlert, RemoteAccessAlert, etc.)
    /// This will be deserialized based on AlertType when the simulation runs.
    /// </summary>
    [Required]
    public string AlertJson { get; set; } = string.Empty;

    /// <summary>
    /// Optional: Priority level for the alert
    /// </summary>
    public Priority Priority { get; set; } = Priority.High;
}
