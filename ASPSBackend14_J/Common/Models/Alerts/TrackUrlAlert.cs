namespace Common.Models.Alerts;

/// <summary>
/// Track URL alert - detects tracker URLs in visited sites
/// </summary>
public class TrackUrlAlert : DeviceAlert
{
    public TrackUrlAlert()
    {
        // Parameterless constructor for deserialization
    }

    /// <summary>
    /// The URL that contains tracking elements
    /// </summary>
    public string Url { get; set; } = string.Empty;
    
    /// <summary>
    /// Array of tracker keys detected in the URL
    /// </summary>
    public Key[] Trackers { get; set; } = Array.Empty<Key>();
    
    /// <summary>
    /// Number of trackers detected
    /// </summary>
    public int TrackerCount { get; set; }
    
    /// <summary>
    /// Type of tracking detected (e.g., "Analytics", "Advertising", "Social")
    /// </summary>
    public string TrackingType { get; set; } = string.Empty;
    
    /// <summary>
    /// User agent string from the browser
    /// </summary>
    public string UserAgent { get; set; } = string.Empty;
}
