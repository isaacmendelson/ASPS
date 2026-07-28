using Business.DomainEvents;
using Business.Messaging;
using Business.RealtimeAnalysis.Indicators;
using Business.RealtimeAnalysis.ProtectivActions;
using Business.Views;
using Common.Entities;
using Common.Enums;
using Common.Interfaces;
using Common.Models;
using Common.Models.Alerts;
using Interface.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Business.RealtimeAnalysis.UserDomain;

public class UDAnalysisManager : IDomainEventHandler, IBackgroundTask
{
    private readonly UDUser _udUser;
    private List<UserDeviceView> _userDevices;
    private List<ISpecificAnalyzer> _analyzers;
    private readonly UDAnalysis _analysis;
    private readonly ILogger<UDAnalysisManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;
    public UDUser UDUser => _udUser;
    public UDAnalysis Analysis => _analysis;
    private bool _isRunning;
    private UDUserAnalyzer _userAnalyzer;
    private readonly ASView _aSView;
    private bool isInitialized = false;
    private readonly OutboxNotificationPublisher? _notificationPublisher;

    public UDAnalysisManager(
        UDUser udUser,
        UDUserAnalyzer userAnalyzer,
        ILoggerFactory loggerFactory,
        ASView aSView,
        IConfiguration configuration,
        List<IDomainEventHandler> eventHandlers,
        IServiceScopeFactory serviceScopeFactory,
        OutboxNotificationPublisher? notificationPublisher = null)
    {
        _udUser = udUser;
        _userAnalyzer = userAnalyzer;
        _aSView = aSView;
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<UDAnalysisManager>();
        _configuration = configuration;
        _notificationPublisher = notificationPublisher;

        // Initialize analyzers
        _analyzers = new List<ISpecificAnalyzer>
        {
            new UDRemoteAccessAnalyzer(loggerFactory.CreateLogger<UDRemoteAccessAnalyzer>()),
            new UDPhishingAnalyzer(loggerFactory.CreateLogger<UDPhishingAnalyzer>()),
            new UDUrlAnalyzer(loggerFactory.CreateLogger<UDUrlAnalyzer>(), configuration, serviceScopeFactory, aSView),
            new UDTrackUrlAnalyzer(loggerFactory.CreateLogger<UDTrackUrlAnalyzer>(), configuration, aSView)
        };
        

        // Get configuration values for alert lifecycle
        var alertExpiryDays = configuration.GetValue<int>("Analysis:DeviceAlertExpiryDays", 30);
        var alertDeletionDays = configuration.GetValue<int>("Analysis:DeviceAlertDeletionDays", 90);

        // Create single UDAnalysis for this user
        var analysisLogger = loggerFactory.CreateLogger<UDAnalysis>();
        this._analysis = new UDAnalysis(this._udUser, this._userAnalyzer, this._aSView, this._analyzers, this._loggerFactory, new IndicatorFactory(), new ProtectiveActionsFactory(), this._configuration);

        // Register external event handlers first (includes ASView)
        foreach (var handler in eventHandlers)
        {
            _analysis.RegisterEventHandler(handler);
        }
        _analysis.RegisterEventHandler(this);
        // Register internal UDUserAnalyzer last so it runs after ASView
        _analysis.RegisterUserAnalyzer();

        _logger.LogInformation($"UDAnalysisManager created for user {_udUser.Key} with {eventHandlers.Count} event handlers");
    }

    public void Start()
    {
        _isRunning = true;
        this.Initialize();
        _logger.LogInformation($"UDAnalysisManager started for user: {_udUser.Key}");
    }

    public void Stop()
    {
        _isRunning = false;
        _logger.LogInformation($"UDAnalysisManager stopped for user: {_udUser.Key}");
    }

    private void Initialize()
    {
        if (isInitialized) return;

        this.LoadUserDevices();
        // Load active alerts
        this.LoadActiveAlerts();
        // Load Bad Url Visits
        this.LoadRiskyUserUrlSurfData();

        isInitialized = true;
        _logger.LogInformation($"UDAnalysisManager initialized for user: {_udUser.Key}");
    }

    private void LoadUserDevices()
    {
        // This method can be used to fetch user devices from the ASView or database if needed
        this._userDevices = _aSView.GetUserDevices
            (_udUser.Key);
        this.UDUser.UserDevices = this._userDevices;
    }

    private void LoadActiveAlerts()
    {
        this._udUser.ActiveAlerts = _aSView.GetActiveDeviceAlertsByUserKey(_udUser.Key);
    }

    private void LoadRiskyUserUrlSurfData()
    {
        var riskyUserUrlSurfData = _aSView.GetRiskyUrlSurfingByUserKey(_udUser.Key);

        //_udUser.UserUrlSurfDataByDevice = riskyUserUrlSurfData
        //.ToDictionary(g => g.DeviceUid, g => g.SurfHistory);
        //.ToDictionary(g => g.DeviceUid, g => g);

        _udUser.UserUrlSurfDataByDevice = riskyUserUrlSurfData.GroupBy(d => d.DeviceUid)
                .ToDictionary(g => g.Key, g => g.AsEnumerable());
    }

    public async Task Handle(IDomainEvent evt)
    {
        if (!_isRunning) return;
        switch (evt)
        {
            case DeviceAlertReceived alertEvent:
                await HandleDeviceAlertReceived(alertEvent);
                break;
            case AnalysisResultReceived analysisResultEvent:
                await HandleAnalysisResultReceived(analysisResultEvent);
                break;
            case AnalysisResultAdded analysisResultEvent:
                await HandleAnalysisResultAdded(analysisResultEvent);
                break;
            case ImmediateDangerDetected immediateDangerDetected:
                _logger.LogInformation(
                    "[UDAnalysisManager] ImmediateDangerDetected: User={UserKey}, Device={DeviceUid}",
                    _udUser.Key.Value, immediateDangerDetected.DeviceUid);
                this._userAnalyzer.HandleImmediateDangerDetected(immediateDangerDetected);
                break;
            case ImmediateDangerAdded immediateDangerAdded:
                _logger.LogInformation(
                    "[UDAnalysisManager] ImmediateDangerAdded: User={UserKey}, Device={DeviceUid}",
                    _udUser.Key.Value, immediateDangerAdded.DeviceUid);
                this._userAnalyzer.HandleImmediateDangerAdded(immediateDangerAdded);
                break;
            case ImmediateDangerEnded immediateDangerEnded:
                _logger.LogInformation(
                    "[UDAnalysisManager] ImmediateDangerEnded: User={UserKey}, Key={Key}",
                    _udUser.Key.Value, immediateDangerEnded.ImmediateDangerKey?.Value);
                this._userAnalyzer.HandleImmediateDangerEnded(immediateDangerEnded);
                break;
        }
    }

    public Type[] GetHandleableEvents()
    {
        return new[]
        {
            typeof(DeviceAlertReceived),
            typeof(AnalysisResultReceived),
            typeof(AnalysisResultAdded),
            typeof(ImmediateDangerDetected),
            typeof(ImmediateDangerAdded),
            typeof(ImmediateDangerEnded)
        };
    }

    private async Task HandleDeviceAlertReceived(DeviceAlertReceived alertEvent)
    {
        var deviceUid = alertEvent.DeviceUid;

        try
        {
            if (alertEvent.Alert is RemoteAccessAlert ra)
            {
                _ = this._userAnalyzer.AnalyzeAsync(ra);

                // When AnyDesk/TeamViewer connects inbound but the agent didn't include
                // browser tabs (race: extension hadn't reported yet), request the agent to
                // always attach tabs so the next alert re-triggers DetectImmediateDanger
                // with real tab data.
                if (ra.BrowserTabs is null
                    && ra.RemoteAccessDirection == RemoteAccessDirection.Incoming
                    && ra.ConnectionStatus == ConnectionStatus.Open)
                {
                    _notificationPublisher?.PublishSetBrowserTabsPolicy(
                        deviceUid,
                        _udUser.Key.Value,
                        "always",
                        validUntil: DateTime.UtcNow.AddMinutes(10));
                    _logger.LogInformation(
                        "[UDAnalysisManager] RemoteAccessAlert with null BrowserTabs (Incoming+Open) on device {DeviceUid} — requested tab policy 'always'",
                        deviceUid);
                }
            }
            else if (alertEvent.Alert is TabChangedAlert tc)
            {
                _ = this._userAnalyzer.AnalyzeAsync(tc);
            }
            else if (alertEvent.Alert is TabClosedAlert tca)
            {
                _ = this._userAnalyzer.AnalyzeAsync(tca);
            }

            this._analysis.AnalyzeAsync(alertEvent.Alert, deviceUid, alertEvent.DeviceAlertEntityKey);
            //// Pass alert to the single analysis instance with entity key
            //var analysisResultForDeviceAlert = await this._analysis.AnalyzeAsync(alertEvent.Alert, deviceUid, alertEvent.DeviceAlertEntityKey);
            //if (analysisResultForDeviceAlert?.FirstOrDefault() is not null && analysisResultForDeviceAlert?.FirstOrDefault().Value is Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>)
            //{
            //    await this._userAnalyzer.AnalyzeAsync(analysisResultForDeviceAlert);
            //}
            //_logger.LogInformation($"Alert from device {deviceUid} analyzed. Severity: {_analysis.Result?.OverallSeverity}, Active alerts: {_analysis.ActiveDeviceAlerts.Count}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error handling alert from device {deviceUid} for user {_udUser.Key}");
        }
    }

    private async Task HandleAnalysisResultReceived(AnalysisResultReceived analysisResultEvent)
    {
        var deviceUid = analysisResultEvent.DeviceUid;

        try
        {
            // Pass the analysis result to the single analysis instance with entity key
            //await this._analysis.AnalyzeAsync(analysisResultEvent.AnalyzerResults.FirstOrDefault().Value.Item1, deviceUid, new Key(analysisResultEvent.AlertType, analysisResultEvent.DeviceAlertKeyField));
            this._userAnalyzer.AnalyzeAsync(analysisResultEvent.AnalyzerResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error handling alert from device {deviceUid} for user {_udUser.Key}");
        }
    }
    private async Task HandleAnalysisResultAdded(AnalysisResultAdded analysisResultEvent)
    {
        var deviceUid = analysisResultEvent.DeviceUid;

        try
        {
            // Pass the analysis result to the single analysis instance with entity key
            //await this._analysis.AnalyzeAsync(analysisResultEvent.AnalyzerResults.FirstOrDefault().Value.Item1, deviceUid, new Key(analysisResultEvent.AlertType, analysisResultEvent.DeviceAlertKeyField));
            if (analysisResultEvent.AnalyzerResults.FirstOrDefault().Value is not null)
            {
                await this._userAnalyzer.AnalyzeAsync(analysisResultEvent.AnalyzerResults);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error handling alert from device {deviceUid} for user {_udUser.Key}");
        }
    }
}
