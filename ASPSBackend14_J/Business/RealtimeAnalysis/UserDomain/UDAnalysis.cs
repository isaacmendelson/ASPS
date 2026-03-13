#nullable enable

using Business.DomainEvents;
using Business.RealtimeAnalysis;
using Business.RealtimeAnalysis.Indicators;
using Business.Views;
using Common.Entities;
using Common.Enums;
using Common.Interfaces;
using Common.Models;
using Common.Models.Alerts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Runtime.Serialization;
using DeviceAlert = Common.Models.DeviceAlert;

namespace Business.RealtimeAnalysis.UserDomain;


// Analysis result class
[Serializable]
[DataContract]
public class UDAnalysisResult
{
    private readonly IConfiguration _configuration;
    public UDAnalysisResult(AnalysisLevel analysis, Severity overallSeverity, Dictionary<string, Tuple<AnalysisResult, IIndicator[], 
        IProtectiveAction[]>> analyzerResults, DateTime analysisTimestamp, UDUser user, IConfiguration configuration)
    {
        OverallSeverity = overallSeverity;
        AnalyzerResults = analyzerResults;
        //GeneratedFlags = generatedFlags;
        AnalysisTimestamp = analysisTimestamp;
        User = user;
        _configuration = configuration;
    }

    [DataMember]
    public Severity OverallSeverity { get; set; }
    [DataMember]
    public Dictionary<string, Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>> AnalyzerResults { get; set; } = new();
    [DataMember]
    public DateTime AnalysisTimestamp { get; set; } = DateTime.UtcNow;

    [DataMember]
    public UDUser User { get; set; }

    [DataMember]
    public AnalysisLevel AnalysisLevel { get; set; } = AnalysisLevel.Unknown;
}

// UDAnalysis class
public class UDAnalysis : IBackgroundTask
{
    private readonly List<ISpecificAnalyzer> _analyzers;
    private readonly ILogger<UDAnalysis> _logger;
    private readonly int _alertExpiryDays;
    private readonly int _alertDeletionDays;
    private readonly List<IDomainEventHandler> _eventHandlers = new();
    private readonly UDUserAnalyzer  _userAnalyzer;
    private bool _isRunning;

    public UDUser _udUser { get; private set; }
    private List<ActiveDeviceAlert> _activeDeviceAlerts = new();
    private List<ActiveDeviceAlert> _expiredDeviceAlerts = new();
    private IIndicatorFactory _indicatorFactory;
    private IProtectiveActionsFactory _protectiveActionsFactory;
    private readonly IConfiguration _configuration;
    private ASView _aSView;

    public IReadOnlyList<ActiveDeviceAlert> ActiveDeviceAlerts => _activeDeviceAlerts.AsReadOnly();
    public IReadOnlyList<ActiveDeviceAlert> ExpiredDeviceAlerts => _expiredDeviceAlerts.AsReadOnly();

    public UDAnalysis(
        UDUser udUser, 
        ASView aSView,
        List<ISpecificAnalyzer> analyzers, 
        ILoggerFactory _loggerFactory,
        IndicatorFactory indicatorFactory,
        ProtectiveActionsFactory protectiveActionsFactory,
        IConfiguration configuration, 
        int alertExpiryDays = 30, 
        int alertDeletionDays = 90)
    {
        _udUser = udUser;
        _aSView = aSView;
        _analyzers = analyzers;
        _logger = _loggerFactory.CreateLogger<UDAnalysis>();
        _configuration = configuration;
        _alertExpiryDays = alertExpiryDays;
        _alertDeletionDays = alertDeletionDays;
        _indicatorFactory = indicatorFactory;
        _protectiveActionsFactory = protectiveActionsFactory;
        
        _userAnalyzer = new UDUserAnalyzer(udUser, aSView, _alertExpiryDays, _alertDeletionDays, _loggerFactory);
        _userAnalyzer.Start();

    }

    /// <summary>
    /// Register the internal UDUserAnalyzer as the last event handler.
    /// Call this after all external handlers have been registered.
    /// </summary>
    public void RegisterUserAnalyzer()
    {
        RegisterEventHandler(_userAnalyzer);
    }

    /// <summary>
    /// Register an event handler to receive events from this analysis
    /// </summary>
    public void RegisterEventHandler(IDomainEventHandler handler)
    {
        _eventHandlers.Add(handler);
        _logger.LogInformation($"Registered event handler: {handler.GetType().Name}");
    }

    public async Task AnalyzeAsync(DeviceAlert deviceAlert, string deviceUid, string deviceAlertEntityKey)
    {
        var activeAlert = new ActiveDeviceAlert(deviceUid, deviceAlert, deviceAlertEntityKey);
        _activeDeviceAlerts.Add(activeAlert);
        _logger.LogInformation($"Added alert from device {deviceUid}. Total active alerts: {_activeDeviceAlerts.Count}");
        
        // Run analysis
        var analysisResults = new Dictionary<string, Tuple<string, AnalyzerResult>>();
        var flags = new List<AlertFlag>();
        var allAlerts = _activeDeviceAlerts.Select(a => a.Alert).ToList();

        foreach (var analyzer in _analyzers)
        {
            if (analyzer.CanAnalyze(deviceAlert))
            {
                var result = await analyzer.AnalyzeAsync(deviceAlert, allAlerts, _configuration);
                analysisResults[analyzer.GetType().Name] = new Tuple<string, AnalyzerResult>(deviceAlert.AlertId, result);

                object firstResult = null;
                switch (analyzer)
                {
                    case UDUrlAnalyzer:
                        _logger.LogInformation($"URL Analyzer result for device {deviceUid}: Severity={result.Severity}, Message={result.Message}");
                        // SECURITY FIX ASPS-70: Safe null handling for FirstOrDefault
                        var resultsEntry = result.Details.FirstOrDefault(i => i.Key == "results");
                        firstResult = resultsEntry.Key != null ? resultsEntry.Value : null;
                        switch (firstResult)
                        {
                            case null:
                                _logger.LogWarning($"URL Analyzer result for device {deviceUid} has no results in details.");
                                break;
                            case UrlAnalysisResultVm[] uarVm:
                                _logger.LogInformation($"UrlAnalyzer: analyzer results for device {deviceUid}: Severity={result.Severity}, Message={result.Message}");
                                break;

                            default:
                                _logger.LogWarning($"Cannot get Indicators & ProtectiveActions for type {firstResult.GetType().Name}.");
                                break;
                        }
                        break;
                    case UDRemoteAccessAnalyzer:
                        firstResult = result;
                        _logger.LogInformation($"RemoteAccessAnalyzer: analyzer results for device {deviceUid}: Severity={result.Severity}, Message={result.Message}");
                        break;
                }
                if (firstResult is AnalysisResult[] resultsCollection && resultsCollection.Length > 0)
                {
                    var indicators = this._indicatorFactory.CreateIndicators(resultsCollection.First());
                    if (indicators?.Length > 0)
                    {
                        result.Indicators?.AddRange(indicators.ToList());
                    }

                    var protectiveActions = this._protectiveActionsFactory.CreateProtectiveActions(resultsCollection.First(), result, deviceAlert.AlertId);
                    if (protectiveActions?.Length > 0)
                    {
                        result.ProtectiveActions?.AddRange(protectiveActions.ToList());
                    }
                }
                analysisResults[analyzer.GetType().Name] = new Tuple<string, AnalyzerResult>(deviceAlert.AlertId, result);
                FireSpecificAnalyzerResultReceivedEvent(activeAlert, analyzer.GetType().Name, result);
            }
        }

        var urlAnalyzerResults = new List<KeyValuePair<string, Tuple<string, AnalyzerResult>>>();
        var udAnalysisResults = new Dictionary<string, Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>>();
        switch (deviceAlert)
        {
            case UrlAlert:
            case TrackUrlAlert:
                urlAnalyzerResults = analysisResults.Where(i => i.Key == nameof(UDUrlAnalyzer)).ToList();

                foreach (var item in urlAnalyzerResults)
                {
                    var r = item.Value.Item2.Details["results"];
                    if (r is UrlAnalysisResultVm[] vms)
                    {
                        foreach (var result in vms)
                        {
                            udAnalysisResults.Add(item.Key, new Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>(result, item.Value.Item2.Indicators.ToArray(), item.Value.Item2.ProtectiveActions.ToArray()));
                        }
                    }
                }
                break;
            case RemoteAccessAlert:
                
                urlAnalyzerResults = analysisResults.Where(i => i.Key == nameof(UDRemoteAccessAnalyzer)).ToList();
                foreach (var item in urlAnalyzerResults)
                {
                    if (item.Key == nameof(UDRemoteAccessAnalyzer))
                    {
                        var r = item.Value.Item2.Details["results"];
                        if (r is AnalysisResult[] raar)
                        {
                            foreach (var result in raar)
                            {
                                udAnalysisResults.Add(item.Key, new Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>(result, item.Value.Item2.Indicators.ToArray(), item.Value.Item2.ProtectiveActions.ToArray()));
                            }
                        }
                    }
                }
                    break;
        }

        var analysisResult = new UDAnalysisResult(AnalysisLevel.Device, DetermineOverallSeverity(analysisResults), udAnalysisResults, DateTime.UtcNow, this._udUser, _configuration);

        // Update the AnalysisResult field in the ActiveDeviceAlert
        activeAlert.AnalysisResult = analysisResult;
        FireAnalysisResultEvent(activeAlert, analysisResult);

        _logger.LogInformation($"Analysis completed for device {deviceUid}. Overall severity: {analysisResult.OverallSeverity}");
        
        // Clean up old alerts
        CleanupOldAlerts();
    }

    /// <summary>
    /// Fire AnalysisResultReceived event for registered handlers
    /// </summary>
    private void FireSpecificAnalyzerResultReceivedEvent(ActiveDeviceAlert activeAlert, string analyzerName, AnalyzerResult result)
    {
        var analysisEvent = new SpecificAnalyzerResultReceived
        {
            UserKeyField = _udUser.Key.Value,
            DeviceAlertKeyField = activeAlert.DeviceAlertEntityKey,  // Use entity key from DB
            DeviceUid = activeAlert.DeviceUid,
            AnalyzerName = analyzerName,
            Severity = result.Severity,
            AnalysisTimestamp = activeAlert.Timestamp,
            
        };

        // Notify all registered event handlers
        foreach (var handler in _eventHandlers)
        {
            if (handler.GetHandleableEvents().Contains(typeof(SpecificAnalyzerResultReceived)))
            {
                try
                {
                    handler.Handle(analysisEvent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error in event handler {handler.GetType().Name} for {analyzerName}");
                }
            }
        }

        _logger.LogDebug($"Fired SpecificAnalyzerResultReceived event: {analyzerName}, Severity: {result.Severity}");
    }

    /// <summary>
    /// Fire AnalysisResultReceived event for registered handlers
    /// </summary>
    private void FireAnalysisResultEvent(ActiveDeviceAlert activeAlert, UDAnalysisResult result)
    {
        var analysisEvent = new AnalysisResultReceived
        {
            UserKeyField = _udUser.Key.Value,
            DeviceAlertKeyField = activeAlert.DeviceAlertEntityKey,  // Use entity key from DB
            DeviceUid = activeAlert.DeviceUid,
            AnalyzerResults = result.AnalyzerResults,
            Severity = result.OverallSeverity,
            AnalysisTimestamp = result.AnalysisTimestamp,
            AlertType = activeAlert.Alert.AlertType
        };

        // Notify all registered event handlers
        foreach (var handler in _eventHandlers)
        {
            if (handler.GetHandleableEvents().Contains(typeof(AnalysisResultReceived)))
            {
                try
                {
                    handler.Handle(analysisEvent); 
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error in event handler {handler.GetType().Name} for device  {activeAlert.DeviceUid}, at {activeAlert.Timestamp} Severity: {result.OverallSeverity}");
                }
            }
        }

        _logger.LogDebug($"Fired AnalysisResultReceived event for device: {activeAlert.DeviceUid}, at {activeAlert.Timestamp} Severity: {result.OverallSeverity}");
    }

    /// <summary>
    /// Move expired alerts to expired list and delete old expired alerts
    /// </summary>
    private void CleanupOldAlerts()
    {
        var now = DateTime.UtcNow;
        var expiryThreshold = now.AddDays(-_alertExpiryDays);
        var deletionThreshold = now.AddDays(-_alertDeletionDays);
        
        // Move active alerts older than expiry threshold to expired list
        var toExpire = _activeDeviceAlerts.Where(a => a.Timestamp < expiryThreshold).ToList();
        foreach (var alert in toExpire)
        {
            _activeDeviceAlerts.Remove(alert);
            _expiredDeviceAlerts.Add(alert);
        }
        
        if (toExpire.Any())
        {
            _logger.LogInformation($"Moved {toExpire.Count} alerts to expired list. Active: {_activeDeviceAlerts.Count}, Expired: {_expiredDeviceAlerts.Count}");
        }
        
        // Remove expired alerts older than deletion threshold
        var toDelete = _expiredDeviceAlerts.Where(a => a.Timestamp < deletionThreshold).ToList();
        foreach (var alert in toDelete)
        {
            _expiredDeviceAlerts.Remove(alert);
        }
        
        if (toDelete.Any())
        {
            _logger.LogInformation($"Deleted {toDelete.Count} old alerts. Expired: {_expiredDeviceAlerts.Count}");
        }
    }

    private Severity DetermineOverallSeverity(Dictionary<string, Tuple<string, AnalyzerResult>> results)
    {
        var maxSeverity = Severity.Low;
        
        foreach (var result in results.Values)
        {
            if (result.Item2 is AnalyzerResult ar && ar.Severity > maxSeverity)
            {
                maxSeverity = ar.Severity;
            }
        }
        
        return maxSeverity;
    }

    public void Start()
    {
        _isRunning = true;
        _logger.LogInformation($"UDAnalysis started for user: {this._udUser.Key}");
    }

    public void Stop()
    {
        _isRunning = false;
        _logger.LogInformation($"UDAnalysis stopped for user: {_udUser.Key}");
    }
}

// Analyzer result class
[Serializable]
public class AnalyzerResult
{
    public AnalyzerResult(Severity severity, string message, List<IIndicator>? indicators, List<IProtectiveAction>? protectiveActions, Dictionary<string, object> details)
    {
        Severity = severity;
        Message = message;
        Indicators = indicators;
        ProtectiveActions = protectiveActions;
        Details = details;
    }

    public AnalyzerResult(Severity severity, string message)
    {
        Severity = severity;
        Message = message;
        Details = new Dictionary<string, object>();
    }

    public Severity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<IIndicator>? Indicators { get; set; }

    public List<IProtectiveAction>? ProtectiveActions { get; set; }

    public Dictionary<string, object> Details { get; set; } = new();
}

// Analyzer interface
public interface ISpecificAnalyzer
{
    ExternalAnalyzer[] ExternalAnalyzers { get; }
    bool CanAnalyze(DeviceAlert alert);
    Task<AnalyzerResult> AnalyzeAsync(DeviceAlert alert, List<DeviceAlert> historicalAlerts, IConfiguration configuration);
}

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
            return new AnalyzerResult (Severity.Low, "Invalid alert type" );
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
            var action = new ProtectiveAction(ProtectiveActionSubject.Device, ProtectiveActionType.DisplayNotification, AnalysisLevel.Device, msg, remoteAlert.AlertId); // "Remote access detected", "A remote access application has been detected running on your device. Please verify if this activity is authorized.", DateTime.UtcNow);
            protectiveActions.Add(action);
        }

        var riskAssesment = new RiskAssessment(score.Value, "", isScam, 1);

        var res = new RemoteAccessAnalysisResultVm(remoteAlert.RemoteAccessApp, remoteAlert.RunningProcesses, remoteAlert.ConnectionUrl, 
            remoteAlert.ConnectionStatus, remoteAlert.ConnectionsCount, remoteAlert.SessionStatus, remoteAlert.BrowserTabs, riskAssesment);
        var results = new List<RemoteAccessAnalysisResultVm>() { res};

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
            return new AnalyzerResult (Severity.Low, "Invalid alert type" );
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
