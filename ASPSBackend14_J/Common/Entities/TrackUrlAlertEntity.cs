using System.ComponentModel.DataAnnotations.Schema;

namespace Common.Entities;

/// <summary>
/// Track URL alert stored in database - tracks URL navigation and time spent on pages
/// </summary>
public class TrackUrlAlertEntity : WebAlertEntity
{
    public TrackUrlAlertEntity()
    {
        // Parameterless constructor for EF Core
    }

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
    /// User's timezone
    /// </summary>
    public string Timezone { get; set; } = string.Empty;
    
    [NotMapped]
    public override string TypeName => "TrackUrlAlert";
}
