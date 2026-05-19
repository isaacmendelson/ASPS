using Common.Enums;

namespace Business.RealtimeAnalysis.UserDomain;

/// <summary>
/// Information about a domain to track across user devices
/// ASPS-371: SetTrackedDomains Event
/// </summary>
public class TrackedDomainCommand
{
    public string Domain { get; set; } = string.Empty;
    public string? ScamInProgressKey { get; set; }
    public string? AnalysisKey { get; set; }
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
        string analysisKey,
        TrackMode trackMode,
        ReportType reportType,
        string reason)
    {
        Domain = domain;
        ScamInProgressKey = scamInProgressKey;
        AnalysisKey = analysisKey;
        TrackMode = trackMode;
        ReportType = reportType;
        AddedTimestamp = DateTime.UtcNow;
        Reason = reason;
    }
}

// NOTE: The TrackMode enum is intentionally NOT redefined here.
// The single source of truth is Common.Enums.TrackMode (None=0, Surf=1,
// Click=2) which mirrors the Chrome extension's TrackMode. A second
// divergent enum used to live here (Monitor/Warn/Block/HighAlert) and
// caused a silent serialization mismatch when SetTrackedDomains reached
// the extension. Do not reintroduce a local TrackMode.

/// <summary>
/// Type of report to generate
/// </summary>
public enum ReportType
{
    None = 0,
    Backend = 1,
    User = 2,
    All = 3
}
