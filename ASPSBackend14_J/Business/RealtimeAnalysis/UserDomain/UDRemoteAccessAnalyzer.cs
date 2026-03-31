#nullable enable

using Business.RealtimeAnalysis.Indicators;
using Common.Entities;
using Common.Enums;
using Common.Models;
using Common.Models.Alerts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DeviceAlert = Common.Models.DeviceAlert;

namespace Business.RealtimeAnalysis.UserDomain;

// Remote Access Analyzer
public class UDRemoteAccessAnalyzer : ISpecificAnalyzer
{
    private readonly ILogger<UDRemoteAccessAnalyzer> _logger;

    public ExternalAnalyzer[] ExternalAnalyzers => Array.Empty<ExternalAnalyzer>();

    public UDRemoteAccessAnalyzer(ILogger<UDRemoteAccessAnalyzer> logger)
    {
        _logger = logger;
    }

    public bool CanAnalyze(DeviceAlert alert)
    {
        return alert is RemoteAccessAlert;
    }

    public async Task<AnalyzerResult> AnalyzeAsync(DeviceAlert alert, List<DeviceAlert> historicalAlerts, IConfiguration configuration)
    {
        await Task.CompletedTask; // Placeholder for async operations

        var remoteAlert = alert as RemoteAccessAlert;
        if (remoteAlert == null)
        {
            return new AnalyzerResult(Severity.Low, "Invalid alert type");
        }

        var flags = new List<AlertFlag>();
        var severity = Severity.Low;

        // Analyze running processes
        if (remoteAlert.RunningProcesses > 0)
        {
            flags.Add(new AlertFlag
            {
                AlertFlagType = AlertFlagType.RemoteAccess_AppRunning,
                Status = AlertFlagStatus.Open,
                Created = DateTime.UtcNow
            });
            severity = Severity.Medium;
        }

        // Analyze connection status
        if (remoteAlert.ConnectionStatus == ConnectionStatus.Open)
        {
            flags.Add(new AlertFlag
            {
                AlertFlagType = AlertFlagType.RemoteAccess_ConnectionOpen,
                Status = AlertFlagStatus.Open,
                Created = DateTime.UtcNow
            });
            severity = Severity.High;
        }

        // Check for active session
        if (remoteAlert.SessionStatus > 0)
        {
            flags.Add(new AlertFlag
            {
                AlertFlagType = AlertFlagType.RemoteAccess_SessionActive,
                Status = AlertFlagStatus.Open,
                Created = DateTime.UtcNow
            });
            severity = Severity.Critical;
        }

        _logger.LogInformation($"Remote access analysis: Severity={severity}, Flags={flags.Count}");


        var indicators = new List<IIndicator>();
        var protectiveActions = new List<IProtectiveAction>();
        var score = new NumericScore(0, 1, true);
        bool isScam = false;

        if (remoteAlert.RunningProcesses > 0 && remoteAlert.ConnectionStatus == ConnectionStatus.Open)
        {

            score = new NumericScore(10, 1, true);


            if (remoteAlert.ConnectionsCount > 2)
            {
                score.Value = 20;
            }

            if (remoteAlert.SessionStatus == 1)
            {
                score.Value = 30;
            }

            string msg = $"Remote access application {remoteAlert.RemoteAccessApp} detected with {remoteAlert.ConnectionsCount} connections.";
            var action = new ProtectiveAction(
                //ProtectiveActionSubject.Device, 
                remoteAlert.DeviceInfo.Key,
                ProtectiveActionType.DisplayNotification, AnalysisLevel.Device, msg, remoteAlert.AlertId); // "Remote access detected", "A remote access application has been detected running on your device. Please verify if this activity is authorized.", DateTime.UtcNow);
            protectiveActions.Add(action);
        }

        var riskAssesment = new RiskAssessment(score.Value, "", isScam, 1);

        var res = new RemoteAccessAnalysisResult(remoteAlert.RemoteAccessApp, remoteAlert.RunningProcesses, remoteAlert.ConnectionUrl,
            remoteAlert.ConnectionStatus, remoteAlert.RemoteAccessDirection, remoteAlert.ConnectionsCount, remoteAlert.SessionStatus, remoteAlert.BrowserTabs, riskAssesment);
        var results = new List<RemoteAccessAnalysisResult>() { res };

        return new AnalyzerResult
        (
            severity,
            $"Remote access detected: {remoteAlert.RemoteAccessApp}",
            indicators,
            protectiveActions,
            new Dictionary<string, object>
            {
                ["results"] = results.ToArray(),
                ["App"] = remoteAlert.RemoteAccessApp.ToString(),
                ["ConnectionUrl"] = remoteAlert.ConnectionUrl,
                ["ConnectionsCount"] = remoteAlert.ConnectionsCount
            }
        );
    }
}

