using Business.DomainEvents;
using Common.Enums;

namespace Business.RealtimeAnalysis.UserDomain;

/// <summary>
/// Distributes tracked domains to all user devices for cross-platform protection
/// ASPS-371: SetTrackedDomains Event
/// </summary>
public class TrackedDomainDistributor
{
    /// <summary>
    /// Create domain tracking event for user's all devices
    /// </summary>
    /// <param name="userKeyField">User key</param>
    /// <param name="domains">List of domains to track</param>
    /// <param name="isCrossPlatformLock">Whether to enable cross-platform lock</param>
    /// <param name="reason">Reason for tracking</param>
    /// <returns>SetTrackedDomains event</returns>
    public SetTrackedDomains CreateTrackingEvent(
        string userKeyField,
        List<TrackedDomainCommand> domains,
        bool isCrossPlatformLock,
        string reason)
    {
        return new SetTrackedDomains(
            userKeyField: userKeyField,
            trackedDomains: domains,
            isCrossPlatformLock: isCrossPlatformLock,
            reason: reason
        );
    }

    /// <summary>
    /// Create tracked domain from suspicious URL
    /// </summary>
    /// <param name="url">Suspicious URL</param>
    /// <param name="scamKey">Scam in progress key</param>
    /// <param name="trackMode">Track mode (Monitor/Warn/Block/HighAlert)</param>
    /// <param name="reportType">Report type</param>
    /// <param name="reason">Reason for tracking</param>
    /// <returns>TrackedDomainCommand</returns>
    public TrackedDomainCommand CreateTrackedDomain(
        string url,
        string scamKey,
        TrackMode trackMode,
        ReportType reportType,
        string reason)
    {
        var domain = ExtractDomain(url);

        return new TrackedDomainCommand(
            domain: domain,
            scamInProgressKey: scamKey,
            analysisKey: string.Empty,
            trackMode: trackMode,
            reportType: reportType,
            reason: reason
        );
    }

    /// <summary>
    /// Extract domain from URL
    /// </summary>
    /// <param name="url">Full URL</param>
    /// <returns>Domain name</returns>
    public string ExtractDomain(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        try
        {
            // Remove protocol
            var cleaned = url.Replace("http://", "").Replace("https://", "");

            // Extract domain (before first /)
            var parts = cleaned.Split('/');
            var domain = parts[0];

            // Remove port if present
            if (domain.Contains(':'))
                domain = domain.Split(':')[0];

            return domain.ToLowerInvariant();
        }
        catch
        {
            return url.ToLowerInvariant();
        }
    }

    /// <summary>
    /// Determine track mode based on risk score.
    /// Maps to the canonical Common.Enums.TrackMode (extension-facing):
    ///   • risk ≥ 60  → Click  (aggressive — TrackUrlAlert on every navigation)
    ///   • risk ≥ 40  → Surf   (passive — UrlAlert once per domain)
    ///   • otherwise  → None   (below the tracking threshold)
    /// The 40 floor aligns with ProtectiveActionsFactory's trackUrlThreshold.
    /// </summary>
    /// <param name="riskScore">Risk score (0-100)</param>
    /// <returns>Appropriate track mode</returns>
    public TrackMode DetermineTrackMode(double riskScore)
    {
        if (riskScore >= 60)
            return TrackMode.Click;
        if (riskScore >= 40)
            return TrackMode.Surf;
        return TrackMode.None;
    }

    /// <summary>
    /// Determine report type based on risk score and user status
    /// </summary>
    /// <param name="riskScore">Risk score (0-100)</param>
    /// <param name="isTargeted">Whether user is targeted</param>
    /// <returns>Appropriate report type</returns>
    public ReportType DetermineReportType(double riskScore, bool isTargeted)
    {
        if (riskScore >= 80 || isTargeted)
            return ReportType.All; // High risk or targeted = report everything

        if (riskScore >= 60)
            return ReportType.User; // Significant risk = notify user

        if (riskScore >= 40)
            return ReportType.Backend; // Moderate risk = backend only

        return ReportType.None; // Low risk = no report
    }

    /// <summary>
    /// Merge multiple tracked domain lists (deduplicate by domain)
    /// </summary>
    /// <param name="lists">Multiple lists of tracked domains</param>
    /// <returns>Merged list with unique domains</returns>
    public List<TrackedDomainCommand> MergeDomainLists(params List<TrackedDomainCommand>[] lists)
    {
        var merged = new Dictionary<string, TrackedDomainCommand>();

        foreach (var list in lists)
        {
            foreach (var domain in list)
            {
                if (!merged.ContainsKey(domain.Domain))
                {
                    merged[domain.Domain] = domain;
                }
                else
                {
                    // Keep the higher track mode
                    if (domain.TrackMode > merged[domain.Domain].TrackMode)
                        merged[domain.Domain] = domain;
                }
            }
        }

        return merged.Values.ToList();
    }
}
