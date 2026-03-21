using Business.RealtimeAnalysis.Indicators;
using Business.Views;
using Common.Entities;
using Common.Enums;
using Common.Interfaces;
using Common.Models;
using Common.Models.Alerts;
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

    public ExternalAnalyzer[] ExternalAnalyzers => Array.Empty<ExternalAnalyzer>();

    public TrackUrlAnalyzer(
        ILogger<TrackUrlAnalyzer> logger,
        ASView asView)
    {
        _logger = logger;
        _asView = asView;
    }

    public bool CanAnalyze(DeviceAlert alert)
    {
        return alert is TrackUrlAlert;
    }

    public async Task<AnalyzerResult> AnalyzeAsync(DeviceAlert alert, List<DeviceAlert> historicalAlerts, IConfiguration configuration)
    {
        await Task.CompletedTask; // Placeholder for async operations

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

        // Check for safe domain
        var domain = KnownPhishingWebsite.GetDomainFromUrl(trackUrlAlert.Url);
        var isSafeDomain = !string.IsNullOrEmpty(domain) && _asView.IsSafeDomain(domain);

        if (isSafeDomain)
        {
            _logger.LogInformation($"Domain '{domain}' is whitelisted (SafeDomains)");
            riskScore = 0;
        }
        else
        {
            // Basic risk assessment based on duration
            // Check longer duration first!
            if (trackUrlAlert.Duration > 600) // > 10 minutes
            {
                riskScore = 40;
                severity = Severity.High;
                _logger.LogInformation($"Very long session detected: {trackUrlAlert.Duration}s on {trackUrlAlert.Url}");
            }
            else if (trackUrlAlert.Duration > 300) // > 5 minutes
            {
                riskScore = 20;
                severity = Severity.Medium;
                _logger.LogInformation($"Long session detected: {trackUrlAlert.Duration}s on {trackUrlAlert.Url}");
            }

            // Check for scam-in-progress scenario
            if (!string.IsNullOrWhiteSpace(trackUrlAlert.ScamInProgressKey))
            {
                riskScore = 60;
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
            riskAssessment);

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
                ["analyzers_run"] = 1,
                ["analyzers_total"] = 1
            });
    }
}
