namespace Common.Entities;

/// <summary>
/// Abstract base class for web/browser-related alerts.
/// Contains shared fields between UrlAlert and TrackUrlAlert.
/// </summary>
public abstract class WebAlertEntity : DeviceAlertEntity
{
    protected WebAlertEntity()
    {
        // Parameterless constructor for EF Core
    }

    /// <summary>
    /// The URL being visited/analyzed
    /// </summary>
    public string Url { get; set; } = string.Empty;
    
    /// <summary>
    /// User agent string from the browser
    /// </summary>
    public string UserAgent { get; set; } = string.Empty;
    
    /// <summary>
    /// Browser tab identifier
    /// </summary>
    public string TabId { get; set; } = string.Empty;
}
