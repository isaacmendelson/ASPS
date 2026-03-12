using System.ComponentModel.DataAnnotations.Schema;
using Common.Enums;
using Common.Models;

namespace Common.Entities;

/// <summary>
/// Track URL alert stored in database - detects tracker URLs in visited sites
/// </summary>
public class TrackUrlAlertEntity : DeviceAlertEntity
{
    public TrackUrlAlertEntity()
    {
        // Parameterless constructor for EF Core
    }

    /// <summary>
    /// The URL that contains tracking elements
    /// </summary>
    public string Url { get; set; } = string.Empty;
    
    /// <summary>
    /// JSON array of tracker keys detected in the URL
    /// </summary>
    public string TrackerKeys { get; set; } = string.Empty;
    
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
    
    [NotMapped]
    public override string TypeName => "TrackUrlAlert";
}
