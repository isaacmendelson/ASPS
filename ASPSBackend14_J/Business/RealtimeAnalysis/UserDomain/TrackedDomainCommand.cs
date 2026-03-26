namespace Business.RealtimeAnalysis.UserDomain;

/// <summary>
/// Information about a domain to track across user devices
/// ASPS-371: SetTrackedDomains Event
/// </summary>
public class TrackedDomainCommand
{
    public string Domain { get; set; } = string.Empty;
    public string ScamInProgressKey { get; set; } = string.Empty;
    public TrackMode TrackMode { get; set; }
    public ReportType ReportType { get; set; }
    public DateTime AddedTimestamp { get; set; }
    public string Reason { get; set; } = string.Empty;

    public TrackedDomainCommand()
    {
        AddedTimestamp = DateTime.UtcNow;
    }

    public TrackedDomainCommand(
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
    Monitor = 0,
    Warn = 1,
    Block = 2,
    HighAlert = 3
}

/// <summary>
/// Type of report to generate
/// </summary>
public enum ReportType
{
    None = 0,
    OnVisit = 1,
    OnFormSubmit = 2,
    OnAnyAction = 3
}
