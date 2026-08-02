using Common.Enums;

namespace Common.Models;

/// <summary>
/// DTO for device list and detail responses (Angular admin API).
/// </summary>
public class DeviceDto
{
    public Key Key { get; set; } = new Key();
    public string DeviceUid { get; set; } = string.Empty;
    public DeviceType DeviceType { get; set; }
    public OperatingSystemType OperatingSystem { get; set; }
    public DeviceMonitoringStatus MonitoringStatus { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? MAC { get; set; }
    public DateTime DateCreated { get; set; }

    /// <summary>Key of the owning user (null if device is unregistered).</summary>
    public Key? UserKey { get; set; }

    /// <summary>Display name of the owning user (first + last, or "Unregistered").</summary>
    public string UserName { get; set; } = "Unregistered";
}

/// <summary>
/// DTO for alert list and detail responses (Angular admin API).
/// </summary>
public class AlertDto
{
    public Key Key { get; set; } = new Key();
    public string AlertType { get; set; } = string.Empty;
    public Priority Priority { get; set; }
    public AlertFlagStatus Status { get; set; }
    public DateTime Timestamp { get; set; }
    public string DeviceUid { get; set; } = string.Empty;
    public DeviceType DeviceType { get; set; }
    public OperatingSystemType OperatingSystem { get; set; }

    /// <summary>Key of the owning device (null when device not registered).</summary>
    public Key? DeviceKey { get; set; }

    /// <summary>Key of the owning user (null when alert is from unregistered device).</summary>
    public Key? UserKey { get; set; }

    /// <summary>Display name of the owning user.</summary>
    public string? UserName { get; set; }

    /// <summary>Key of the linked analysis result (null when no analysis was run).</summary>
    public Key? AnalysisKey { get; set; }
}

/// <summary>
/// DTO for analysis result list and detail responses (Angular admin API).
/// </summary>
public class AnalysisResultDto
{
    public Key Key { get; set; } = new Key();
    public string Discriminator { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsFromCache { get; set; }
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>Raw JSON analysis payload.</summary>
    public string? JsonValue { get; set; }

    /// <summary>Key of the user this analysis belongs to.</summary>
    public Key? UserKey { get; set; }

    /// <summary>Key of the device alert that triggered this analysis (null when not triggered by an alert).</summary>
    public Key? DeviceAlertKey { get; set; }

    /// <summary>URL analyzed (populated for Url and TrackUrl analysis types).</summary>
    public string? Url { get; set; }
}
