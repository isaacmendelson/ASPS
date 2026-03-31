using Business.RealtimeAnalysis.Indicators;
using Business.Views;
using Common.Entities;
using Common.Enums;
using Common.Models;
using Common.Models.Alerts;
using Interface.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Business.RealtimeAnalysis.UserDomain;

/// <summary>
/// Analyzer specifically for TrackUrlAlert (ongoing URL monitoring).
/// Separated from UDUrlAnalyzer which handles initial UrlAlert.
/// </summary>
public class UDTrackUrlAnalyzer : ISpecificAnalyzer
{
    private readonly ILogger<UDTrackUrlAnalyzer> _logger;
    private readonly IConfiguration _configuration;
    private readonly ASView _asView;

    private int _riskThreshold;
    private int _trackingDurationMinutes;

    private float _severityScoreThresholdCritical;
    private float _severityScoreThresholdHigh;
    private float _severityScoreThresholdMedium;
    public ExternalAnalyzer[] ExternalAnalyzers { get; } = Array.Empty<ExternalAnalyzer>();

    public UDTrackUrlAnalyzer(
        ILogger<UDTrackUrlAnalyzer> logger,
        IConfiguration configuration,
        ASView asView)
    {
        _logger = logger;
        _configuration = configuration;
        _asView = asView;

        _riskThreshold = _configuration.GetValue<int>("TrackUrl:RiskThresholdToEnableTracking", 40);
        _trackingDurationMinutes = _configuration.GetValue<int>("TrackUrl:TrackingDurationMinutes", 3000);

        _severityScoreThresholdCritical = _configuration.GetValue<float>("Analysis:SeverityScoreThresholdCritical", 80);
        _severityScoreThresholdHigh = _configuration.GetValue<float>("Analysis:SeverityScoreThresholdHigh", 80);
        _severityScoreThresholdMedium = _configuration.GetValue<float>("Analysis:SeverityScoreThresholdMedium", 80);

        _logger.LogInformation("UDTrackUrlAnalyzer initialized for TrackUrlAlert monitoring");
    }

    public bool CanAnalyze(DeviceAlert alert)
    {
        return alert is TrackUrlAlert;
    }

    public async Task<AnalyzerResult> AnalyzeAsync(DeviceAlert alert, List<DeviceAlert> historicalAlerts, IConfiguration configuration)
    {
        if (alert is not TrackUrlAlert trackUrlAlert)
        {
            return new AnalyzerResult(Severity.Low, "Alert is not a TrackUrlAlert");
        }

        _logger.LogInformation($"Analyzing TrackUrlAlert: URL={trackUrlAlert.Url}, Duration={trackUrlAlert.Duration}");

        var results = new List<TrackUrlAnalysisResult>();
        var errors = new List<string>();

        var url = trackUrlAlert.Url;
        var domain = Common.Entities.KnownPhishingWebsite.GetDomainFromUrl(url);
        
        // Calculate risk score based on TrackUrlAlert-specific logic
        var riskScore = CalculateRiskScore(trackUrlAlert, domain);

        // Determine severity based on risk score
        var severity = this.GetSeverityFromRiskScore(riskScore);

        var riskAssessment = new RiskAssessment(riskScore, severity.ToString(), severity > Severity.Medium, 1);

        var indicators = new List<IIndicator>();
        var protectiveActions = new List<IProtectiveAction>();

        // Add protective action if risk is high
        //if (riskScore >= _severityScoreThresholdHigh)
        //{
        //    var notificationAction = new ProtectiveAction(
        //        trackUrlAlert.DeviceInfo.Key,
        //        ProtectiveActionType.UserDisplayNotification,
        //        AnalysisLevel.Device,
        //        $"Suspicious activity detected on tracked URL: {domain}",
        //        alert.AlertId
        //    );
        //    protectiveActions.Add(notificationAction);
        //}

        var message = $"TrackUrlAlert analyzed: {domain}, Risk={riskScore}, Duration={trackUrlAlert.Duration}";

        var isSafeDomain = !string.IsNullOrEmpty(domain) && _asView.IsSafeDomain(domain);

        //return new AnalyzerResult(
        //    severity,
        //    message,
        //    indicators,
        //    protectiveActions,
        //    new Dictionary<string, object>
        //    {
        //        ["url"] = url,
        //        ["domain"] = domain,
        //        ["from_url"] = trackUrlAlert.FromUrl ?? "N/A",
        //        ["duration_ms"] = trackUrlAlert.Duration,
        //        ["scam_in_progress_key"] = trackUrlAlert.ScamInProgressKey ?? "N/A",
        //        ["timezone"] = trackUrlAlert.Timezone ?? "N/A",
        //        ["risk_score"] = riskScore,
        //        ["risk_reason"] = GetRiskReason(trackUrlAlert, domain)
        //    }
        //);
        var result =  new TrackUrlAnalysisResult(
            trackUrlAlert.Url,
            trackUrlAlert.FromUrl ?? "", 
            trackUrlAlert.Duration,
            trackUrlAlert.ScamInProgressKey,
            trackUrlAlert.IPAddress,
            trackUrlAlert.UserAgent,
            trackUrlAlert.TabId,
            trackUrlAlert.Timezone,
            KnownPhishingWebsite.GetDomainFromUrl(trackUrlAlert.Url),
            isSafeDomain, false, riskAssessment
            );
        results.Add(result);

        var analyzerResult = new AnalyzerResult
            (
                severity,
                results.Any()
                    ? $"TrackUrl analysis completed: {results.Count}/{ExternalAnalyzers.Length} analyzers succeeded"
                    : "All Track Url analyzers failed",
                indicators,  // Pass indicators list
                protectiveActions,
                new Dictionary<string, object>
                {
                    ["results"] = results.ToArray(),
                    ["errors"] = errors.ToArray(),
                    ["url"] = trackUrlAlert.Url,
                    ["from_url"] = trackUrlAlert.FromUrl ?? "",
                    ["duration"] = trackUrlAlert.Duration,
                    ["device_uid"] = trackUrlAlert.DeviceInfo.DeviceUid,
                    ["alert_id"] = trackUrlAlert.AlertId ?? "",
                    ["risk_score"] = riskScore,
                    ["ip_address"] = trackUrlAlert.IPAddress,
                    ["tab_id"] = trackUrlAlert.TabId,
                    ["user_key"] = trackUrlAlert.DeviceInfo.UserKey,
                    ["timestamp"] = DateTime.UtcNow,
                    ["analyzers_run"] = results.Count,
                    ["external_analyzers_total"] = ExternalAnalyzers.Length
                }
            );

        return analyzerResult;
    }

    /// <summary>
    /// Calculate risk score for TrackUrlAlert based on specific criteria:
    /// - ScamInProgressKey exists → HIGH risk (90)
    /// - Duration > 10 minutes → HIGH risk (60)
    /// - Duration > 5 minutes → MEDIUM risk (40)
    /// - SafeDomain → LOW risk (5)
    /// </summary>
    private int CalculateRiskScore(TrackUrlAlert alert, string domain)
    {
        // Check if domain is whitelisted (SafeDomains)
        if (!string.IsNullOrEmpty(domain) && _asView.IsSafeDomain(domain))
        {
            _logger.LogInformation($"Domain '{domain}' is whitelisted (SafeDomains). Risk: LOW");
            return 5;
        }

        // Check if scam is in progress (highest priority)
        if (!string.IsNullOrEmpty(alert.ScamInProgressKey))
        {
            _logger.LogWarning($"ScamInProgressKey detected: {alert.ScamInProgressKey}. Risk: HIGH");
            return 90;
        }

        // Convert duration from milliseconds to minutes
        var durationMinutes = alert.Duration / (1000.0 * 60);

        // Check duration thresholds
        if (durationMinutes > 10)
        {
            _logger.LogWarning($"Duration exceeds 10 minutes: {durationMinutes:F2}min. Risk: HIGH");
            return 60;
        }

        if (durationMinutes > 5)
        {
            _logger.LogInformation($"Duration exceeds 5 minutes: {durationMinutes:F2}min. Risk: MEDIUM");
            return 40;
        }

        // Default: low risk for short-duration tracking
        _logger.LogInformation($"Duration below threshold: {durationMinutes:F2}min. Risk: LOW");
        return 20;
    }

    /// <summary>
    /// Get human-readable reason for risk assessment
    /// </summary>
    private string GetRiskReason(TrackUrlAlert alert, string domain)
    {
        if (!string.IsNullOrEmpty(domain) && _asView.IsSafeDomain(domain))
            return "Whitelisted domain (SafeDomains)";

        if (!string.IsNullOrEmpty(alert.ScamInProgressKey))
            return $"Scam in progress detected (Key: {alert.ScamInProgressKey})";

        var durationMinutes = alert.Duration / (1000.0 * 60);

        if (durationMinutes > 10)
            return $"Prolonged visit duration ({durationMinutes:F2} minutes)";

        if (durationMinutes > 5)
            return $"Extended visit duration ({durationMinutes:F2} minutes)";

        return $"Short visit duration ({durationMinutes:F2} minutes)";
    }

    private Severity GetSeverityFromRiskScore(float val)
    {
        Severity severity = Severity.Unknown;
        if (val >= _severityScoreThresholdCritical)
            severity = Severity.Critical;  // risk score >= 80 = very dangerous
        else if (val >= _severityScoreThresholdHigh)
            severity = Severity.High;      // risk score >= 61 = dangerous
        else if (val >= _severityScoreThresholdMedium)
            severity = Severity.Medium;    // risk score >= 31 = moderate ris
        else
            severity = Severity.Low;       // risk score < 31 = low risk    

        return severity;
    }
}
