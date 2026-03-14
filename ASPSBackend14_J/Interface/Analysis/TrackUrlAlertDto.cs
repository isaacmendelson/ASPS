using System;

namespace Interface.Analysis;

/// <summary>
/// Data Transfer Object for TrackUrlAlert.
/// Used for API responses and data serialization.
/// </summary>
public class TrackUrlAlertDto
{
    /// <summary>
    /// The current URL being visited
    /// </summary>
    public string Url { get; set; } = string.Empty;
    
    /// <summary>
    /// The previous URL (referrer)
    /// </summary>
    public string FromUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Duration spent on the page in seconds
    /// </summary>
    public int Duration { get; set; }
    
    /// <summary>
    /// Key for identifying scam-in-progress scenarios
    /// </summary>
    public string ScamInProgressKey { get; set; } = string.Empty;
    
    /// <summary>
    /// IP address of the request
    /// </summary>
    public string IPAddress { get; set; } = string.Empty;
    
    /// <summary>
    /// User agent string from the browser
    /// </summary>
    public string UserAgent { get; set; } = string.Empty;
    
    /// <summary>
    /// Browser tab identifier
    /// </summary>
    public string TabId { get; set; } = string.Empty;
    
    /// <summary>
    /// User's timezone
    /// </summary>
    public string Timezone { get; set; } = string.Empty;
    
    /// <summary>
    /// Timestamp of the alert
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Alert priority
    /// </summary>
    public string Priority { get; set; } = string.Empty;
    
    /// <summary>
    /// Device UID
    /// </summary>
    public string DeviceUid { get; set; } = string.Empty;

    /// <summary>
    /// Default constructor for serialization
    /// </summary>
    public TrackUrlAlertDto()
    {
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Constructor with required fields
    /// </summary>
    public TrackUrlAlertDto(
        string url,
        string fromUrl,
        int duration,
        string deviceUid,
        string ipAddress,
        string userAgent,
        string tabId,
        string timezone)
    {
        Url = url ?? throw new ArgumentNullException(nameof(url));
        FromUrl = fromUrl ?? string.Empty;
        Duration = duration;
        DeviceUid = deviceUid ?? throw new ArgumentNullException(nameof(deviceUid));
        IPAddress = ipAddress ?? string.Empty;
        UserAgent = userAgent ?? string.Empty;
        TabId = tabId ?? string.Empty;
        Timezone = timezone ?? string.Empty;
        Timestamp = DateTime.UtcNow;
    }
}
