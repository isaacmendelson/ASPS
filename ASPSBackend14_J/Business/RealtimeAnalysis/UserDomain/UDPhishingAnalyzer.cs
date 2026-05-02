#nullable enable

using Business.RealtimeAnalysis.Indicators;
using Common.Entities;
using Common.Enums;
using Common.Interfaces;
using Common.Models;
using Common.Models.Alerts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DeviceAlert = Common.Models.DeviceAlert;

namespace Business.RealtimeAnalysis.UserDomain;

// Phishing Analyzer
public class UDPhishingAnalyzer : ISpecificAnalyzer
{
    private readonly ILogger<UDPhishingAnalyzer> _logger;
    private readonly HashSet<string> _knownPhishingDomains = new()
    {
        "suspicious-bank.com", "fake-paypal.net", "phishing-site.org"
    };

    public ExternalAnalyzer[] ExternalAnalyzers => Array.Empty<ExternalAnalyzer>();

    public UDPhishingAnalyzer(ILogger<UDPhishingAnalyzer> logger)
    {
        _logger = logger;
    }

    public bool CanAnalyze(DeviceAlert alert)
    {
        return alert is UrlAlert;
    }

    public async Task<AnalyzerResult> AnalyzeAsync(DeviceAlert alert, List<DeviceAlert> historicalAlerts, IConfiguration _configuration)
    {
        //await Task.CompletedTask;

        var urlAlert = alert as UrlAlert;
        if (urlAlert == null)
        {
            return new AnalyzerResult(Severity.Low, "Invalid alert type");
        }

        var severity = Severity.Low;
        var flags = new List<AlertFlag>();

        // Check URL against known phishing domains
        foreach (var domain in _knownPhishingDomains)
        {
            if (urlAlert.Url.Contains(domain, StringComparison.OrdinalIgnoreCase))
            {
                severity = Severity.Critical;
                _logger.LogWarning($"Phishing URL detected: {urlAlert.Url}");
                break;
            }
        }

        // Check for suspicious trackers
        if (urlAlert.Trackers.Length > 5)
        {
            severity = severity < Severity.Medium ? Severity.Medium : severity;
        }

        // Check for iframes (potential clickjacking)
        if (urlAlert.IFrameDomains.Length > 0)
        {
            severity = severity < Severity.Medium ? Severity.Medium : severity;
        }

        return new AnalyzerResult(
            severity,
            $"URL analysis: {urlAlert.Url}",
            new List<IIndicator>(),
            new List<IProtectiveAction>(),
            new Dictionary<string, object>
            {
                ["Url"] = urlAlert.Url,
                ["TrackerCount"] = urlAlert.Trackers.Length,
                ["IFrameCount"] = urlAlert.IFrameDomains.Length
            });
    }
}
