namespace Business.RealtimeAnalysis.UserDomain;

/// <summary>
/// Information about a domain to track across user devices
/// ASPS-371: SetTrackedDomains Event
/// </summary>
public class TrackedDomainInfo
{
    public string Domain { get; set; } = string.Empty;
    public string ScamInProgressKey { get; set; } = string.Empty;
    public TrackMode TrackMode { get; set; }
    public ReportType ReportType { get; set; }
    public DateTime AddedTimestamp { get; set; }
    public string Reason { get; set; } = string.Empty;

    public TrackedDomainInfo()
    {
        AddedTimestamp = DateTime.UtcNow;
    }

    public TrackedDomainInfo(
        string domain,
        string scamInProgressKey,
        TrackMode trackMode,
        ReportType reportType,
        string reason)
    {
        Domain = domain;
        ScamInProgressKey = scamInProgressKey;
        TrackMode = trackMode;
        ReportType = reportType;
        AddedTimestamp = DateTime.UtcNow;
        Reason = reason;
    }
}

/// <summary>
/// Tracking mode for domain monitoring
/// </summary>
public enum TrackMode
{
    /// <summary>
    /// Monitor but don't block
    /// </summary>
    Monitor = 0,

    /// <summary>
    /// Show warning before access
    /// </summary>
    Warn = 1,

    /// <summary>
    /// Block access completely
    /// </summary>
    Block = 2,

    /// <summary>
    /// High alert - notify immediately on any access
    /// </summary>
    HighAlert = 3
}

/// <summary>
/// Type of report when domain is accessed
/// </summary>
public enum ReportType
{
    /// <summary>
    /// Don't report
    /// </summary>
    None = 0,

    /// <summary>
    /// Report to backend only
    /// </summary>
    Backend = 1,

    /// <summary>
    /// Report to user/guardian
    /// </summary>
    User = 2,

    /// <summary>
    /// Report to both backend and user
    /// </summary>
    All = 3
}
