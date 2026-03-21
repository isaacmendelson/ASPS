using Business.RealtimeAnalysis.Indicators;
using Business.Views;
using Common.Entities;
using Common.Enums;
using Common.Interfaces;
using Common.Models;
using Common.Models.Alerts;
using Interface.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis.UserDomain;

/// <summary>
/// Analyzer for TrackUrlAlert - analyzes URL navigation patterns and time spent on pages
/// </summary>
public class TrackUrlAnalyzer : ISpecificAnalyzer
{
    private readonly ILogger<TrackUrlAnalyzer> _logger;
    private readonly ASView _asView;
    private readonly ITrackedDomainRepository _trackedDomainRepository;

    public ExternalAnalyzer[] ExternalAnalyzers => Array.Empty<ExternalAnalyzer>();

    public TrackUrlAnalyzer(
        ILogger<TrackUrlAnalyzer> logger,
        ASView asView,
        ITrackedDomainRepository trackedDomainRepository)
    {
        _logger = logger;
        _asView = asView;
        _trackedDomainRepository = trackedDomainRepository;
    }

    public bool CanAnalyze(DeviceAlert alert)
    {
        return alert is TrackUrlAlert;
    }

    /// <summary>
    /// Extract the full domain (with subdomains) from a URL
    /// </summary>
    /// <param name="url">The URL to parse</param>
    /// <returns>Full domain (e.g., "ads.google.com") or empty string if invalid</returns>
    private string GetFullDomainFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        try
        {
            // Ensure scheme exists (Uri requires it)
            if (!url.Contains("://"))
                url = "http://" + url;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return string.Empty;

            return uri.Host.ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Check if a URL domain matches a TrackedDomain (exact or subdomain match)
    /// </summary>
    /// <param name="urlDomain">Domain from the URL (e.g., "ads.google.com")</param>
    /// <param name="trackedDomains">List of active tracked domains</param>
    /// <returns>Tuple of (matched TrackedDomain, isExactMatch) or (null, false) if no match</returns>
    private (TrackedDomain? matchedDomain, bool isExactMatch) FindMatchingTrackedDomain(
        string urlDomain, 
        IEnumerable<TrackedDomain> trackedDomains)
    {
        if (string.IsNullOrWhiteSpace(urlDomain))
            return (null, false);

        var normalizedUrlDomain = urlDomain.ToLowerInvariant().Trim();

        // Try exact match first
        var exactMatch = trackedDomains.FirstOrDefault(td => td.Domain == normalizedUrlDomain);
        if (exactMatch != null)
        {
            _logger.LogInformation($"Exact TrackedDomain match: {urlDomain} -> {exactMatch.Domain} (Category: {exactMatch.Category})");
            return (exactMatch, true);
        }

        // Try subdomain match (e.g., "ads.google.com" matches tracked domain "google.com")
        var subdomainMatch = trackedDomains.FirstOrDefault(td =>
            normalizedUrlDomain.EndsWith("." + td.Domain, StringComparison.OrdinalIgnoreCase));

        if (subdomainMatch != null)
        {
            _logger.LogInformation($"Subdomain TrackedDomain match: {urlDomain} -> {subdomainMatch.Domain} (Category: {subdomainMatch.Category})");
            return (subdomainMatch, false);
        }

        return (null, false);
    }

    public async Task<AnalyzerResult> AnalyzeAsync(DeviceAlert alert, List<DeviceAlert> historicalAlerts, IConfiguration configuration)
    {
        var trackUrlAlert = alert as TrackUrlAlert;
        if (trackUrlAlert == null)
        {
            return new AnalyzerResult(Severity.Low, "Invalid alert type");
        }

        // Validation
        if (string.IsNullOrWhiteSpace(trackUrlAlert.Url))
        {
            _logger.LogWarning($"TrackUrlAlert {trackUrlAlert.AlertId} has empty URL");
            return new AnalyzerResult(Severity.Low, "Invalid TrackUrlAlert: URL is empty");
        }

        _logger.LogInformation($"Analyzing TrackUrlAlert: URL={trackUrlAlert.Url}, Duration={trackUrlAlert.Duration}s, From={trackUrlAlert.FromUrl}");

        var indicators = new List<IIndicator>();
        var protectiveActions = new List<IProtectiveAction>();
        var severity = Severity.Low;
        var riskScore = 0;

        // Extract both full domain and root domain from URL
        var domain = KnownPhishingWebsite.GetDomainFromUrl(trackUrlAlert.Url);
        var fullDomain = GetFullDomainFromUrl(trackUrlAlert.Url);
        var isSafeDomain = !string.IsNullOrEmpty(domain) && _asView.IsSafeDomain(domain);

        // Load active tracked domains and check for match (using full domain for matching)
        var trackedDomains = await _trackedDomainRepository.GetAllActiveAsync();
        var (matchedTrackedDomain, isExactMatch) = FindMatchingTrackedDomain(fullDomain, trackedDomains);
        
        TrackedDomainInfo? trackedDomainInfo = null;
        if (matchedTrackedDomain != null)
        {
            trackedDomainInfo = new TrackedDomainInfo(
                matchedTrackedDomain.Id,
                matchedTrackedDomain.Domain,
                matchedTrackedDomain.Category,
                isExactMatch);

            _logger.LogInformation($"TrackedDomain detected: {matchedTrackedDomain.Category} - {matchedTrackedDomain.Domain}");
        }

        if (isSafeDomain)
        {
            _logger.LogInformation($"Domain '{domain}' is whitelisted (SafeDomains)");
            riskScore = 5; // LOW risk - safe domain
        }
        else
        {
            // Basic risk assessment based on duration
            // Check longer duration first!
            if (trackUrlAlert.Duration > 600) // > 10 minutes
            {
                riskScore = 60; // HIGH risk - very long session
                severity = Severity.High;
                _logger.LogInformation($"Very long session detected: {trackUrlAlert.Duration}s on {trackUrlAlert.Url}");
            }
            else if (trackUrlAlert.Duration > 300) // > 5 minutes
            {
                riskScore = 40; // MEDIUM risk - long session
                severity = Severity.Medium;
                _logger.LogInformation($"Long session detected: {trackUrlAlert.Duration}s on {trackUrlAlert.Url}");
            }

            // Check for scam-in-progress scenario
            if (!string.IsNullOrWhiteSpace(trackUrlAlert.ScamInProgressKey))
            {
                riskScore = 90; // HIGH risk - scam in progress
                severity = Severity.High;
                _logger.LogWarning($"Scam-in-progress key detected: {trackUrlAlert.ScamInProgressKey}");

                var action = new ProtectiveAction(
                    ProtectiveActionSubject.Device,
                    ProtectiveActionType.DisplayNotification,
                    AnalysisLevel.Device,
                    $"Potential scam detected on {domain}. Please verify this activity.",
                    trackUrlAlert.AlertId);
                protectiveActions.Add(action);
            }
        }

        var riskAssessment = new RiskAssessment(riskScore, isSafeDomain ? "Safe" : "Monitor", false, 1);

        var result = new TrackUrlAnalysisResultVm(
            trackUrlAlert.Url,
            trackUrlAlert.FromUrl,
            trackUrlAlert.Duration,
            trackUrlAlert.ScamInProgressKey,
            trackUrlAlert.IPAddress,
            trackUrlAlert.UserAgent,
            trackUrlAlert.TabId,
            trackUrlAlert.Timezone,
            domain,
            isSafeDomain,
            riskAssessment,
            trackedDomainInfo);

        var results = new List<TrackUrlAnalysisResultVm> { result };

        return new AnalyzerResult(
            severity,
            $"TrackUrl analysis completed: {trackUrlAlert.Url} (Duration: {trackUrlAlert.Duration}s)",
            indicators,
            protectiveActions,
            new Dictionary<string, object>
            {
                ["results"] = results.ToArray(),
                ["url"] = trackUrlAlert.Url,
                ["from_url"] = trackUrlAlert.FromUrl,
                ["duration"] = trackUrlAlert.Duration,
                ["domain"] = domain,
                ["is_safe_domain"] = isSafeDomain,
                ["scam_in_progress_key"] = trackUrlAlert.ScamInProgressKey ?? string.Empty,
                ["tracked_domain"] = trackedDomainInfo != null ? new Dictionary<string, object>
                {
                    ["id"] = trackedDomainInfo.Id,
                    ["domain"] = trackedDomainInfo.Domain,
                    ["category"] = trackedDomainInfo.Category,
                    ["is_exact_match"] = trackedDomainInfo.IsExactMatch
                } : null!,
                ["is_tracked_domain"] = trackedDomainInfo != null,
                ["analyzers_run"] = 1,
                ["analyzers_total"] = 1
            });
    }
}
