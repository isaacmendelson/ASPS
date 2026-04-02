namespace Common.Models.Alerts;

/// <summary>
/// Track URL alert - tracks URL navigation and time spent on pages
/// </summary>
public class TrackUrlAlert : DeviceAlert
{
    public TrackUrlAlert()
    {
        // Parameterless constructor for deserialization
    }

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

    public string IP { get; set; } = string.Empty;

    //public string IPAddress
    //{
    //    get
    //    {
    //        return this.IP;
    //    }
    //}

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
}
