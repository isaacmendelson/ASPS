using Business.DomainEvents;
using Business.Views;
using Common.Entities;
using Common.Interfaces;
using Common.Models;
using Interface.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Business.RealtimeAnalysis.UserDomain;

public class UDAnalysisManager : IDomainEventHandler, IBackgroundTask
{
    private readonly UDUser _udUser;
    private readonly List<ISpecificAnalyzer> _analyzers;
    private readonly UDAnalysis _analysis;
    private readonly ILogger<UDAnalysisManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;
    public UDUser UDUser => _udUser;
    public UDAnalysis Analysis => _analysis;
    private bool _isRunning;
    //private UDUserAnalyzer _userAnalyzer;
    private readonly ASView _aSView;

    public UDAnalysisManager(
        UDUser udUser, 
        //ILogger<UDAnalysisManager> logger, 
        ILoggerFactory loggerFactory, 
        ASView aSView,
        IConfiguration configuration,
        List<IDomainEventHandler> eventHandlers,
        IKnownPhishingWebsiteRepository phishingRepo)
    {
        _udUser = udUser;
        _aSView = aSView;
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<UDAnalysisManager>();
        _configuration = configuration;

        // Initialize analyzers
        _analyzers = new List<ISpecificAnalyzer>
        {
            new UDRemoteAccessAnalyzer(loggerFactory.CreateLogger<UDRemoteAccessAnalyzer>()),
            new UDPhishingAnalyzer(loggerFactory.CreateLogger<UDPhishingAnalyzer>()),
            new UDUrlAnalyzer(loggerFactory.CreateLogger<UDUrlAnalyzer>(), configuration, phishingRepo, aSView)
        };
        //_userAnalyzer = new UDUserAnalyzer(_udUser, loggerFactory.CreateLogger<UDUserAnalyzer>());

        // Get configuration values for alert lifecycle
        var alertExpiryDays = configuration.GetValue<int>("Analysis:DeviceAlertExpiryDays", 30);
        var alertDeletionDays = configuration.GetValue<int>("Analysis:DeviceAlertDeletionDays", 90);
        
        // Create single UDAnalysis for this user
        var analysisLogger = loggerFactory.CreateLogger<UDAnalysis>();
        _analysis = new UDAnalysis(_udUser, aSView, _analyzers, _loggerFactory, new IndicatorFactory(), new ProtectiveActionsFactory(), _configuration, alertExpiryDays, alertDeletionDays);
        
        // Register external event handlers first (includes ASView)
        foreach (var handler in eventHandlers)
        {
            _analysis.RegisterEventHandler(handler);
        }

        // Register internal UDUserAnalyzer last so it runs after ASView
        _analysis.RegisterUserAnalyzer();
        
        _logger.LogInformation($"UDAnalysisManager created for user {_udUser.Key} with {eventHandlers.Count} event handlers, expiry={alertExpiryDays}d, deletion={alertDeletionDays}d");
    }

    public void Start()
    {
        _isRunning = true;
        _logger.LogInformation($"UDAnalysisManager started for user: {_udUser.Key}");
    }

    public void Stop()
    {
        _isRunning = false;
        _logger.LogInformation($"UDAnalysisManager stopped for user: {_udUser.Key}");
    }

    public async Task Handle(IDomainEvent evt)
    {
        if (!_isRunning) return;
        switch(evt)
        {
            case DeviceAlertReceived alertEvent:
                HandleDeviceAlertAdded(alertEvent);
                break;
            case AnalysisResultReceived analysisResultEvent:
                HandleAnalysisResultReceived(analysisResultEvent);
                break;
        }
    }

    public Type[] GetHandleableEvents()
    {
        return new[]
        {
            typeof(DeviceAlertReceived),
            typeof(AnalysisResultReceived)
        };
    }

    private async void HandleDeviceAlertAdded(DeviceAlertReceived alertEvent)
    {
        var deviceUid = alertEvent.DeviceUid;
        
        try
        {
            // Pass alert to the single analysis instance with entity key
            await _analysis.AnalyzeAsync(alertEvent.Alert, deviceUid, alertEvent.DeviceAlertEntityKey);

            //_logger.LogInformation($"Alert from device {deviceUid} analyzed. Severity: {_analysis.Result?.OverallSeverity}, Active alerts: {_analysis.ActiveDeviceAlerts.Count}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error handling alert from device {deviceUid} for user {_udUser.Key}");
        }
    }

    private async Task HandleAnalysisResultReceived(AnalysisResultReceived analysisResultEvent)
    {

    }
}
