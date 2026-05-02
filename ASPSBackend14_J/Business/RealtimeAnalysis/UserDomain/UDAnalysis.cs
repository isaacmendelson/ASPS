#nullable enable

using Business.DomainEvents;
using Business.RealtimeAnalysis.Indicators;
using Business.RealtimeAnalysis.ProtectivActions;
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

// UDAnalysis class
public class UDAnalysis : IBackgroundTask, IDomainEventHandler
{
    private readonly List<ISpecificAnalyzer> _analyzers;
    private readonly List<IDomainEvent> _events = new();
    private readonly ILogger<UDAnalysis> _logger;
    private int _alertExpiryDays;
    private int _alertDeletionDays;
    private readonly List<IDomainEventHandler> _eventHandlers = new();
    private readonly UDUserAnalyzer  _userAnalyzer;
    private bool _isRunning;

    public UDUser _udUser { get; private set; }
    private List<ActiveDeviceAlert> _activeDeviceAlerts = new();
    private List<ActiveDeviceAlert> _expiredDeviceAlerts = new();
    private IIndicatorFactory _indicatorFactory;
    private IProtectiveActionsFactory _protectiveActionsFactory;
    private IConfiguration _configuration;
    private ASView _aSView;
    private readonly DomainEventPublisher _domainEventPublisher;

    private float _riskThresholdEnableTracking;

    private float _severityScoreThresholdCritical;
    private float _severityScoreThresholdHigh;
    private float _severityScoreThresholdMedium;

    public IReadOnlyList<ActiveDeviceAlert> ActiveDeviceAlerts => _activeDeviceAlerts.AsReadOnly();
    public IReadOnlyList<ActiveDeviceAlert> ExpiredDeviceAlerts => _expiredDeviceAlerts.AsReadOnly();

    public UDAnalysis(
        UDUser udUser,
        UDUserAnalyzer userAnalyzer,
        ASView aSView,
        List<ISpecificAnalyzer> analyzers, 
        ILoggerFactory _loggerFactory,
        IndicatorFactory indicatorFactory,
        ProtectiveActionsFactory protectiveActionsFactory,
        IConfiguration configuration)
    {
        _udUser = udUser;
        _userAnalyzer = userAnalyzer;
        _aSView = aSView;
        _analyzers = analyzers;
        _logger = _loggerFactory.CreateLogger<UDAnalysis>();

        this.LoadConfiguration(configuration);

        _indicatorFactory = indicatorFactory;
        _protectiveActionsFactory = protectiveActionsFactory;
        _domainEventPublisher = new DomainEventPublisher(_eventHandlers);
        //_userAnalyzer = new UDUserAnalyzer(udUser, aSView, _alertExpiryDays, _alertDeletionDays, _loggerFactory);
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
        _logger.LogInformation($"Registered event handler: {handler.GetType().Name}, user={this._udUser.Key.Value}");
        
        _domainEventPublisher.Subscribe(handler);
        _logger.LogInformation($"Subscribed event handler to DomainEventPublisher: {handler.GetType().Name}, user={this._udUser.Key.Value}");
    }

    //public async Task AnalyzeAsync(AnalysisResult analysisResult, string deviceUid, Key deviceAlertEntityKey)
    //{
    //    _logger.LogInformation($"AnalyzeAsync {analysisResult.GetType().Name} {deviceUid}. Total active alerts: {_activeDeviceAlerts.Count}");
    //    this._userAnalyzer.AnalyzeAsync(analysisResult);
    //}

    public async Task<Dictionary<string, Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>>> AnalyzeAsync(DeviceAlert deviceAlert, string deviceUid, string deviceAlertEntityKey)
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
            if (!analyzer.CanAnalyze(deviceAlert))
            {
                continue;
            }
            var analyzerResult = await analyzer.AnalyzeAsync(deviceAlert, allAlerts, _configuration);
            analysisResults[analyzer.GetType().Name] = new Tuple<string, AnalyzerResult>(deviceAlert.AlertId, analyzerResult);

            AnalysisResult[]? firstAnalysisResult = null;
            KeyValuePair<string, AnalysisResult[]> resultsEntry = analyzerResult.Details
                .Where(i => i.Key == "results" && i.Value is AnalysisResult[])
                .Select(i => new KeyValuePair<string, AnalysisResult[]>(i.Key, i.Value as AnalysisResult[]))
                .FirstOrDefault();

            firstAnalysisResult = resultsEntry.Key != null ? resultsEntry.Value : null;
            _logger.LogInformation($"{nameof(analyzer)} result for device {deviceUid}: Severity={analyzerResult.Severity}, Message={analyzerResult.Message}");

            if (firstAnalysisResult is null)
            {
                _logger.LogWarning($"{nameof(analyzer)} result for device {deviceUid} has no results in details.");
            }
            else
            {
                _logger.LogInformation($"{nameof(analyzer)} : analyzer results for device {deviceUid}: Severity={analyzerResult.Severity}, Message={analyzerResult.Message}");
            }

            if (firstAnalysisResult is AnalysisResult[] resultsCollection && resultsCollection.Length > 0)
            {
                var indicators = this._indicatorFactory.CreateIndicators(resultsCollection.First());
                if (indicators?.Length > 0)
                {
                    analyzerResult.Indicators?.AddRange(indicators.ToList());
                }

                var protectiveActions = this._protectiveActionsFactory.CreateProtectiveActions(resultsCollection.First(), analyzerResult, deviceAlert.AlertId, deviceAlert.DeviceInfo, this._riskThresholdEnableTracking);
                if (protectiveActions?.Length > 0)
                {
                    analyzerResult.ProtectiveActions?.AddRange(protectiveActions.ToList());
                }
            }
            analysisResults[analyzer.GetType().Name] = new Tuple<string, AnalyzerResult>(deviceAlert.AlertId, analyzerResult);
            
            FireAnalyzerResultReceivedEvent(activeAlert, analyzer.GetType().Name, analyzerResult);
        }

        var urlAnalyzerResults = new List<KeyValuePair<string, Tuple<string, AnalyzerResult>>>();
        var udAnalysisResults = new Dictionary<string, Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>>();
        switch (deviceAlert)
        {
            case UrlAlert:
            
                urlAnalyzerResults = analysisResults.Where(i => i.Key == nameof(UDUrlAnalyzer)).ToList();

                foreach (var item in urlAnalyzerResults)
                {
                    var r = item.Value.Item2.Details["results"];
                    if (r is UrlAnalysisResult[] vms)
                    {
                        foreach (var result in vms)
                        {
                            udAnalysisResults.Add(item.Key, new Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>(result, item.Value.Item2.Indicators.ToArray(), item.Value.Item2.ProtectiveActions.ToArray()));
                        }
                    }
                }
                break;

                case TrackUrlAlert t:
                    urlAnalyzerResults = analysisResults.Where(i => i.Key == nameof(UDTrackUrlAnalyzer)).ToList();

                
                foreach (var item in urlAnalyzerResults)
                {
                    var r = item.Value.Item2.Details["results"];
                    if (r is TrackUrlAnalysisResult[] vms)
                    {
                        foreach (var result in vms)
                        {
                            udAnalysisResults.Add(item.Key, new Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>(result, item.Value.Item2.Indicators.ToArray(), item.Value.Item2.ProtectiveActions.ToArray()));
                        }
                    }
                    //var trackUrlResult = new TrackUrlAnalysisResult(
                    //    t, false, new RiskAssessment((float)item.Value.Item2.Details["risk_score"], (float)item.Value.Item2.Details["risk_score"] > _riskThreshold ? "High" : "", true, 1)
                    //);

                    //udAnalysisResults.Add(item.Key, new Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>(trackUrlResult, item.Value.Item2.Indicators?.ToArray(), item.Value.Item2.ProtectiveActions?.ToArray()));
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

        return analysisResult.AnalyzerResults;
    }

    /// <summary>
    /// Fire AnalysisResultReceived event for registered handlers
    /// </summary>
    private void FireAnalyzerResultReceivedEvent(ActiveDeviceAlert activeAlert, string analyzerName, AnalyzerResult result)
    {
        var analysisEvent = new AnalyzerResultReceived
        {
            UserKeyField = _udUser.Key.Value,
            DeviceAlertKeyField = activeAlert.DeviceAlertEntityKey, 
            DeviceUid = activeAlert.DeviceUid,
            AnalyzerName = analyzerName,
            Severity = result.Severity,
            AnalysisTimestamp = activeAlert.Timestamp,
        };
        this._domainEventPublisher.Register(analysisEvent);
        this._domainEventPublisher.RaiseAll();

        // Notify all registered event handlers
        //foreach (var handler in _eventHandlers)
        //{
        //    if (handler.GetHandleableEvents().Contains(typeof(AnalyzerResultReceived)))
        //    {
        //        try
        //        {
        //            handler.Handle(analysisEvent);
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(ex, $"Error in event handler {handler.GetType().Name} for {analyzerName}");
        //        }
        //    }
        //}

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
            AlertType = activeAlert.Alert.AlertType,
            Timestamp = activeAlert.Alert.Timestamp
        };

        this._domainEventPublisher.Register(analysisEvent);
        this._domainEventPublisher.RaiseAll();

        //// Notify all registered event handlers
        //_logger.LogInformation($"[DEBUG] FireAnalysisResultEvent: {_eventHandlers.Count} handlers registered");
        //foreach (var handler in _eventHandlers)
        //{
        //    _logger.LogInformation($"[DEBUG] Checking handler: {handler.GetType().Name}, handles AnalysisResultReceived: {handler.GetHandleableEvents().Contains(typeof(AnalysisResultReceived))}");
        //    if (handler.GetHandleableEvents().Contains(typeof(AnalysisResultReceived)))
        //    {
        //        try
        //        {
        //            _logger.LogInformation($"[DEBUG] Calling handler.Handle for {handler.GetType().Name}");
        //            handler.Handle(analysisEvent); 
        //            _logger.LogInformation($"[DEBUG] Handler.Handle completed for {handler.GetType().Name}");
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(ex, $"Error in event handler {handler.GetType().Name} for device  {activeAlert.DeviceUid}, at {activeAlert.Timestamp} Severity: {result.OverallSeverity}");
        //        }
        //    }
        //}

        _logger.LogInformation($"Fired AnalysisResultReceived event for device: {activeAlert.DeviceUid}, at {activeAlert.Timestamp} Severity: {result.OverallSeverity}");
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

    public Task Handle(IDomainEvent evt)
    {

        switch(evt)
        {
            case SystemConfigurationChanged sysConfigChanged:
                this.HandleSystemConfigurationChanged(sysConfigChanged);
                break;
            case ImmediateDangerDetected immediateDangerDetected:
                this.HandleImmediateDangerDetected(immediateDangerDetected);
                break;
            case ImmediateDangerAdded immediateDangerAdded:
                this.HandleImmediateDangerAdded(immediateDangerAdded);
                break;
        }
        return Task.CompletedTask;
    }

    public Type[] GetHandleableEvents()
    {
        return [typeof(SystemConfigurationChanged), typeof(ImmediateDangerDetected), typeof(ImmediateDangerAdded)];
    }

    private void HandleImmediateDangerAdded(ImmediateDangerAdded immediateDangerAdded)
    {
        var protectiveActions = this._protectiveActionsFactory.CreateProtectiveActions(immediateDangerAdded.ImmediateDanger);

        var immediateDangerEvent = new ImmediateDangerEvent(this._udUser.Key, immediateDangerAdded.DeviceUid, immediateDangerAdded.ImmediateDanger, protectiveActions);
        this._domainEventPublisher.Register(immediateDangerEvent);
        this._domainEventPublisher.RaiseAll();

    }

    private void HandleImmediateDangerDetected(ImmediateDangerDetected immediateDangerDetected)
    {

    }
    private void HandleSystemConfigurationChanged(SystemConfigurationChanged sysConfigChanged)
    {
        this._configuration = sysConfigChanged.NewConfiguration;
        this.LoadConfiguration(sysConfigChanged.NewConfiguration);
    }

    private void LoadConfiguration(IConfiguration configuration)
    {
        this._configuration = configuration;
        this._riskThresholdEnableTracking = _configuration.GetValue<int>("TrackUrl:RiskThresholdToEnableTracking", 30);
        this._alertExpiryDays = configuration.GetValue<int>("Analysis:DeviceAlertExpiryDays", 30);
        this._alertDeletionDays = configuration.GetValue<int>("Analysis:DeviceAlertDeletionDays", 90);

        this._severityScoreThresholdCritical = _configuration.GetValue<float>("Analysis:SeverityScoreThresholdCritical", 80);
        this._severityScoreThresholdHigh = _configuration.GetValue<float>("Analysis:SeverityScoreThresholdHigh", 80);
        this._severityScoreThresholdMedium = _configuration.GetValue<float>("Analysis:SeverityScoreThresholdMedium", 80);

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

    //private 
}
