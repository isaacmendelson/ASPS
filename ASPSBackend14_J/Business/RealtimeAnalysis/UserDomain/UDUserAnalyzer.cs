using Business.DomainEvents;
using Business.RealtimeAnalysis.Indicators;
using Business.RealtimeAnalysis.ProtectivActions;
using Business.Views;
using Common.Entities;
using Common.Enums;
using Common.Interfaces;
using Common.Models;
using Common.Models.Alerts;
using Common.ViewModels;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis.UserDomain
{
    public class UDUserAnalyzer : IDomainEventHandler   //, IBackgroundTask
    {
        private readonly ILogger<UDUserAnalyzer> _logger;
        private bool _isRunning;
        private bool _isInitialized;
        private readonly ASView _asView;
        private readonly int _alertExpiryDays;
        private readonly int _alertDeletionDays;
        
        private List<DeviceAlertView> _activeDeviceAlerts = new();
        //private KeyValuePair<string, DeviceAlertEntity>[] _activeDeviceAlertMap = Array.Empty<KeyValuePair<string, DeviceAlertEntity>>();
        //private KeyValuePair<string, AnalysisResultContainer>[] _analysisResultMap = Array.Empty<KeyValuePair<string, AnalysisResultContainer>>();
        private List<AnalysisResultView> _analysisResultViews = new();
        private List<RemoteAccessAnalysisResultView> _remoteAccessAnalysisResults = new();
        private List<UrlAnalysisResultView> _urlAnalysisResults = new();
        private List<UserDeviceView> _devices = new();
        //private IEnumerable<IEnumerable<IIndicator>?> _indicators = Enumerable.Empty<IEnumerable<Indicator>?>();
        //private IEnumerable<IEnumerable<IProtectiveAction>?> _protectiveActions = Enumerable.Empty<IEnumerable<IProtectiveAction>?>();
        private List<IIndicator?> _indicators = new();
        private List<IProtectiveAction?> _protectiveActions = new();
        private KeyValuePair<string, Indicator>[] _activeIndicatorMap = Array.Empty<KeyValuePair<string, Indicator>>();
        //private KeyValuePair<string, IProtectiveAction>[] _protectiveActions = Array.Empty<KeyValuePair<string, IProtectiveAction>>();

        private List<KeyValuePair<string,RemoteAccessStatusObject>> _remoteAccessStatus = new();
        private List<ImmediateDangerDto> _immediateDangers = new();
        //private List<BrowserTab> _browserTabs = new();
        private readonly List<IDomainEventHandler> _eventHandlers = new();
        private readonly DomainEventPublisher _domainEventPublisher;

        private IProtectiveActionsFactory _protectiveActionsFactory;

        public UDUserAnalyzer(
            UDUser udUser,
            //ILoggerFactory loggerFactory
            ASView asView,
            int alertExpiryDays,
            int alertDeletionDays,
            ILoggerFactory loggerFactory,
            ProtectiveActionsFactory protectiveActionsFactory,
            IEnumerable<IDomainEventHandler>? eventHandlers = null
            )
        {
            this.UDUser = udUser;
            this._asView = asView;
            this._logger = loggerFactory.CreateLogger<UDUserAnalyzer>();
            this._alertDeletionDays = alertDeletionDays;
            this._alertExpiryDays = alertExpiryDays;
            if (eventHandlers != null)
            {
                _eventHandlers.AddRange(eventHandlers);
            }
            _domainEventPublisher = new DomainEventPublisher(_eventHandlers);
            _protectiveActionsFactory = protectiveActionsFactory;

            _logger.LogInformation($"UDUserAnalyzer created for user {udUser.Key.Value} with {_eventHandlers.Count} event handlers");
        }
        public string Name => nameof(UDUserAnalyzer);
        //public ExternalAnalyzer[] ExternalAnalyzers { get; }

        public UDUser UDUser { get; private set; }


        public void RegisterEventHandler(IDomainEventHandler handler)
        {
            _eventHandlers.Add(handler);
            _domainEventPublisher.Subscribe(handler);
            _logger.LogInformation($"Registered event handler: {handler.GetType().Name}");
        }


        public async Task AnalyzeAsync(DeviceAlert alert)
        {

            switch (alert)
            {
                case RemoteAccessAlert r:

                    // When RemoteAccessAlert is received:
                    // 1. Update RemoteAccessStatus for user
                    // 2. Update BrowserTabs for User (if provided)

                    this._remoteAccessStatus.Add(new KeyValuePair<string, RemoteAccessStatusObject>(r.DeviceInfo.DeviceUid, new RemoteAccessStatusObject(r.Timestamp, r.DeviceInfo.DeviceUid, r.RemoteAccessDirection, r.RemoteAccessApp, 
                        r.ConnectionStatus == ConnectionStatus.Open, r.SessionStatus == (int)SessionStatus.Open)));

                    if (r.BrowserTabs is not null)  // && r.BrowserTabs.Length > 0)
                    {
                        this.UDUser.SetBrowserTabs(r.DeviceInfo.DeviceUid, r.BrowserTabs);
                    }
                    break;
            }

            Key? key = alert.AlertId is not null ? new Key(alert.GetType().Name, alert.AlertId) : null;
            // With every device alert - detect  immediate danger
            var isImmediateDanger = this.DetectImmediateDanger(key);

            if (isImmediateDanger)
            {
                // RaiseImmediateDangerAlert
            }

        }
        public async Task AnalyzeAsync(Dictionary<string, Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>> analysisResult)  //AnalysisResult analysisResult )
        {

            string analyzerName = analysisResult.FirstOrDefault().Key;
            var firstResult = analysisResult.FirstOrDefault().Value;
            var key = firstResult.Item1.ResultId is not null ? new Key(firstResult.Item1.GetType().Name, firstResult.Item1.ResultId) : null;
            var isImmediateDanger = this.DetectImmediateDanger(key);
            
            var analysisResultViews = this._asView.GetAnalysisResultsByUserKey(this.UDUser.Key)
                .OrderByDescending(I => I.Timestamp)
                .ToList();

            var urlAnalysisResultViews = this._asView.GetUrlAnalysisResultsByUserKey(this.UDUser.Key)
                .OrderByDescending(I => I.Timestamp)
                .ToList();

            var trackUrlAnalysisResultViews = this._asView.GetTrackUrlAnalysisResultsByUserKey(this.UDUser.Key)
               .OrderByDescending(I => I.Timestamp)
               .ToList();

            var remoteAccessAnalysisResultViews = this._asView.GetRemoteAccessAnalysisResultsByUserKey(this.UDUser.Key)
               .OrderByDescending(I => I.Timestamp)
               .ToList();

            var riskyUserUrlSurfData = _asView.GetRiskyUrlSurfingByUserKey(this.UDUser.Key);

            this._analysisResultViews = analysisResultViews;

            this._analysisResultViews.AddRange(urlAnalysisResultViews.Where(i => !_analysisResultViews.Contains(i)));
            var userRiskProfile = this.UDUser.RiskProfile;
            var userBrowserTabs = this.UDUser.BrowserTabs;
            var userRemoteAccessStatus = this.UDUser.RemoteAccessStatus;
            var userRiskAsessment = this.UDUser.RiskAssessment;
            var userUrlSurfDataByDevice = this.UDUser.UserUrlSurfDataByDevice;


            if (isImmediateDanger)
            {
                // RaiseImmediateDangerAlert
            }

        }

        public Type[] GetHandleableEvents()
        {
            return new[] {
                typeof(AnalysisResultAdded),
                typeof(AnalysisResultReceived),
                typeof(ImmediateDangerDetected),
                typeof(ImmediateDangerAdded),
                typeof(ImmediateDangerEnded)
            };
        }

        public async Task Handle(IDomainEvent evt)
        {
            Key? key = null;

            switch(evt)
            {
                case AnalysisResultReceived analysisEvent:
                    key = analysisEvent.DeviceAlertKeyField is not null ? new Key(analysisEvent.GetType().Name, analysisEvent.DeviceAlertKeyField) : null;
                    // Handle the analysis result received event
                    await this.HandleAnalysisResultReceivedAsync(analysisEvent);
                    break;
                case AnalysisResultAdded analysisEvent:
                    key = analysisEvent.DeviceAlertKey is not null ? new Key(analysisEvent.GetType().Name, analysisEvent.DeviceAlertKey.Value) : null;
                    // Handle the analysis result received event
                    await this.HandleAnalysisResultAddedAsync(analysisEvent);
                    break;
                case ImmediateDangerDetected immediateDangerDetected:
                    this.HandleImmediateDangerDetected(immediateDangerDetected);
                    break;
                case ImmediateDangerAdded immediateDangerAdded:
                    this.HandleImmediateDangerAdded(immediateDangerAdded);
                    break;
                case ImmediateDangerEnded immediateDangerEnded:
                    this.HandleImmediateDangerEnded(immediateDangerEnded);
                    break;
            }

            var isImmediateDanger = this.DetectImmediateDanger(key);
        }

        public void HandleImmediateDangerAdded(ImmediateDangerAdded immediateDangerAdded)
        {
            var protectiveActions = this._protectiveActionsFactory.CreateProtectiveActions(immediateDangerAdded.ImmediateDanger);

            var immediateDangerEvent = new ImmediateDangerEvent(this.UDUser.Key, immediateDangerAdded.DeviceUid, immediateDangerAdded.ImmediateDanger, protectiveActions);
            this._domainEventPublisher.Register(immediateDangerEvent);
            this._domainEventPublisher.RaiseAll();

        }

        public void HandleImmediateDangerDetected(ImmediateDangerDetected immediateDangerDetected)
        {

        }

        public void HandleImmediateDangerEnded(ImmediateDangerEnded immediateDangerEnded)
        {
            // Reflect the persisted EndTime in the in-memory list so subsequent
            // DetectImmediateDanger passes don't republish ImmediateDangerEnded
            // for the same record.
            var keyValue = immediateDangerEnded.ImmediateDangerKey?.Value;
            if (string.IsNullOrEmpty(keyValue))
            {
                _logger.LogWarning("ImmediateDangerEnded received without ImmediateDangerKey — skipping local update");
                return;
            }

            var match = this._immediateDangers.FirstOrDefault(i => i.Key?.Value == keyValue);
            if (match != null && match.EndTime == null)
            {
                match.EndTime = DateTime.UtcNow;
                _logger.LogInformation(
                    "[UDUserAnalyzer] ImmediateDangerEnded acknowledged: Key={Key}, User={UserKey}",
                    keyValue, this.UDUser.Key.Value);
            }
        }
        private async Task HandleAnalysisResultAddedAsync(AnalysisResultAdded analysisEvent)
        {
            if (analysisEvent.AnalyzerResults.TryGetValue(nameof(UDRemoteAccessAnalyzer), out var raResult))
            {
                var browserTabsOfUser = this.UDUser.BrowserTabs;
                var browserTabsOfDevice = (raResult.Item1 as RemoteAccessAnalysisResult)?.BrowserTabs;
                if (browserTabsOfUser is not null && browserTabsOfDevice is not null)
                {
                    browserTabsOfUser[analysisEvent.DeviceUid] = browserTabsOfDevice;
                }

                if (browserTabsOfDevice is not null && browserTabsOfDevice.Length > 0)
                {
                    this.UDUser.SetBrowserTabs(analysisEvent.DeviceUid, browserTabsOfDevice);
                }
            }


            if (!analysisEvent.AnalyzerResults.Any())
                return;

            var firstResult = analysisEvent.AnalyzerResults.First().Value;
            var evAnalysisResult = firstResult.Item1;
            var evIndicators = firstResult.Item2;
            var evProtectiveActions = firstResult.Item3;
            this.CleanupExpiredAlerts();
            this.GetLatestAnalysisResults();

            var alerts = this._activeDeviceAlerts.OrderByDescending(i => i.Timestamp).Take(5);
            var results = this._analysisResultViews.OrderByDescending(i => i.Timestamp).Take(5);
            var remoteAccessAnalysisResults = this._asView.GetRemoteAccessAnalysisResultsByUserKey(this.UDUser.Key)?
                .Where(i => i.Timestamp > DateTime.UtcNow.Subtract(new TimeSpan(24, 0, 0))).OrderByDescending(i => i.Timestamp).Take(5);
            var urlAnalysisResults = this._asView.GetUrlAnalysisResultsByUserKey(this.UDUser.Key)?
                .Where(i => i.Timestamp > DateTime.UtcNow.Subtract(new TimeSpan(24, 0, 0))).OrderByDescending(i => i.Timestamp).Take(5);
            var indicators = new List<IIndicator>();
            //var x = remoteAccessAnalysisResults.Select(i => indicators.AddRange(i.Indicators.ToList()  )); 
            foreach (var ind in remoteAccessAnalysisResults.Where(i => i.Indicators is not null).Select(i => i.Indicators))
            {
                if (ind is not null)
                {
                    indicators.AddRange(ind);
                }
            }
            foreach (var ind in urlAnalysisResults.Where(i => i.Indicators is not null).Select(i => i.Indicators))
            {
                if (ind is not null)
                {
                    indicators.AddRange(ind);
                }
            }

            var remoteAccessStatus = this.GetRemoteAccessStatus();
            var firstAnalyzerResult = analysisEvent.AnalyzerResults.FirstOrDefault();
            if (firstAnalyzerResult.Value.Item1 == null)
            {
                return; // No analyzer result to process
            }
            switch (firstAnalyzerResult.Value.Item1)
            {
                case RemoteAccessAnalysisResult r:
                    break;
                case UrlAnalysisResult u:
                    var websitePurpose = u.Purpose;

                    //if (remoteAccessStatus.IsRemoteAccessAppActive && remoteAccessStatus.isRemoteAccessSessionActive)
                    //{
                    //    if ((u.Purpose?.CategoryName == "banking") || (u.Purpose?.CategoryName == "exchange"))
                    //    {

                    //    }
                    //}
                    break;
                case TrackUrlAnalysisResult t:
                    HandleTrackUrlAnalysisResultReceived(t, remoteAccessStatus?? Enumerable.Empty<KeyValuePair<string, RemoteAccessStatusObject>>());
                    break;
            }

        }

        private async Task HandleAnalysisResultReceivedAsync(AnalysisResultReceived analysisEvent)
        {
            if (analysisEvent.AnalyzerResults.TryGetValue(nameof(UDRemoteAccessAnalyzer), out var raResult))
            {
                var browserTabsOfUser = this.UDUser.BrowserTabs;
                var browserTabsOfDevice = (raResult.Item1 as RemoteAccessAnalysisResult)?.BrowserTabs;
                if (browserTabsOfUser is not null && browserTabsOfDevice?.Length > 0)
                {
                    browserTabsOfUser[analysisEvent.DeviceUid] = browserTabsOfDevice;
                    this.UDUser.SetBrowserTabs(analysisEvent.DeviceUid, browserTabsOfDevice);
                }
            }


            if (!analysisEvent.AnalyzerResults.Any())
                return;

            var firstResult = analysisEvent.AnalyzerResults.First().Value;
            var evAnalysisResult = firstResult.Item1;
            var evIndicators = firstResult.Item2;
            var evProtectiveActions = firstResult.Item3;
            this.CleanupExpiredAlerts();
            this.GetLatestAnalysisResults();
            
            var alerts = this._activeDeviceAlerts.OrderByDescending(i => i.Timestamp).Take(5);
            var results = this._analysisResultViews.OrderByDescending(i => i.Timestamp).Take(5);
            var remoteAccessAnalysisResults = this._asView.GetRemoteAccessAnalysisResultsByUserKey(this.UDUser.Key)?
                .Where(i => i.Timestamp > DateTime.UtcNow.Subtract(new TimeSpan(24, 0, 0))).OrderByDescending(i => i.Timestamp).Take(5);
            var urlAnalysisResults = this._asView.GetUrlAnalysisResultsByUserKey(this.UDUser.Key)?
                .Where(i => i.Timestamp > DateTime.UtcNow.Subtract(new TimeSpan(24, 0, 0))).OrderByDescending(i => i.Timestamp).Take(5);
            var indicators = new List<IIndicator>();
            //var x = remoteAccessAnalysisResults.Select(i => indicators.AddRange(i.Indicators.ToList()  )); 
            foreach (var ind in remoteAccessAnalysisResults.Where(i => i.Indicators is not null).Select(i => i.Indicators))
            {
                if(ind is not null)
                {
                    indicators.AddRange(ind);
                }
            }
            foreach (var ind in urlAnalysisResults.Where(i => i.Indicators is not null).Select(i => i.Indicators))
            {
                if (ind is not null)
                {
                    indicators.AddRange(ind);
                }
            }

            var remoteAccessStatus = this.GetRemoteAccessStatus();
            //var x = this._asView.GetUrlAnalysisResultsByUserKey().Select(i => i.Indicators) ?? 
            // SECURITY FIX ASPS-70: Safe null handling for FirstOrDefault
            var firstAnalyzerResult = analysisEvent.AnalyzerResults.FirstOrDefault();
            if (firstAnalyzerResult.Value.Item1 == null)
            {
                return; // No analyzer result to process
            }
            switch (firstAnalyzerResult.Value.Item1)
            {
                case RemoteAccessAnalysisResult r:
                    break;
                case UrlAnalysisResult u:
                    var websitePurpose = u.Purpose;

                    //if (remoteAccessStatus.IsRemoteAccessAppActive && remoteAccessStatus.isRemoteAccessSessionActive)
                    //{
                    //    if ((u.Purpose?.CategoryName == "banking") || (u.Purpose?.CategoryName == "exchange"))
                    //    {

                    //    }
                    //}
                    break;
                case TrackUrlAnalysisResult t:
                    HandleTrackUrlAnalysisResultReceived(t, remoteAccessStatus);
                    break;
            }
            
        }

        private IEnumerable<KeyValuePair<string, RemoteAccessStatusObject>>? GetRemoteAccessStatus()
        {
            if (this._remoteAccessAnalysisResults.Count ==0)
            {
                return null;
            }
            List<KeyValuePair<string, RemoteAccessStatusObject>> res = new();


            foreach(var d in this.UDUser.UserDevices)
            {
                var latestRemoteAccessResultForDevice = this._remoteAccessAnalysisResults.OrderByDescending(i => i.Timestamp).FirstOrDefault(i => i.Alert.DeviceInfo.DeviceUid == d.DeviceUid);
                var deviceInfo = latestRemoteAccessResultForDevice?.Alert?.DeviceInfo;
                var anaylisisResult = latestRemoteAccessResultForDevice?.AnalysisResult;
                var timestamp = latestRemoteAccessResultForDevice?.Timestamp ?? DateTime.UtcNow;
                if (anaylisisResult is null || deviceInfo is null)
                {
                    continue;
                }

                var isRemoteAccessAppActive = anaylisisResult.RunningProcesses > 0;
                var isRemoteAccessSessionActive = anaylisisResult.SessionStatus > 0;
                var remoteAccessDirection = anaylisisResult.RemoteAccessDirection;
                var connectionStatus = anaylisisResult.ConnectionStatus;
                var statusForDevice = new RemoteAccessStatusObject(timestamp, deviceInfo.DeviceUid, remoteAccessDirection, anaylisisResult.RemoteAccessApp, isRemoteAccessAppActive, isRemoteAccessSessionActive);
                res.Add( new KeyValuePair<string, RemoteAccessStatusObject>(d.DeviceUid, statusForDevice));
            }
            //var deviceInfo = this._remoteAccessAnalysisResults.FirstOrDefault()?.Alert.DeviceInfo;
            //var anaylisisResult = this._remoteAccessAnalysisResults.FirstOrDefault()?.AnalysisResult;
            //if (anaylisisResult is null || deviceInfo is null)
            //{
            //    return null;
            //}

            //var isRemoteAccessAppActive = anaylisisResult.RunningProcesses > 0;
            //var isRemoteAccessSessionActive = anaylisisResult.SessionStatus > 0;
            //var remoteAccessDirection = anaylisisResult.RemoteAccessDirection;
            //var connectionStatus = anaylisisResult.ConnectionStatus;
            //return new RemoteAccessStatusObject(anaylisisResult.analyzed_at, deviceInfo.DeviceUid, remoteAccessDirection,  anaylisisResult.RemoteAccessApp,isRemoteAccessAppActive, isRemoteAccessSessionActive);
            return res;
        }

        private void HandleTrackUrlAnalysisResultReceived(TrackUrlAnalysisResult trackUrlResult, IEnumerable<KeyValuePair<string, RemoteAccessStatusObject>> remoteAccessStatus)
        {
            _logger.LogInformation($"Handling TrackUrlAnalysisResult: URL={trackUrlResult.Url}, Duration={trackUrlResult.Duration}s, IsSafe={trackUrlResult.IsSafeDomain}");

            // Log scam-in-progress scenarios
            if (!string.IsNullOrWhiteSpace(trackUrlResult.ScamInProgressKey))
            {
                _logger.LogWarning($"Scam-in-progress detected for user {this.UDUser.Key}: {trackUrlResult.ScamInProgressKey}");
            }

            //// Check for high-risk scenarios when remote access is active
            //if (remoteAccessStatus.IsRemoteAccessAppActive && remoteAccessStatus.isRemoteAccessSessionActive)
            //{
            //    if (!trackUrlResult.IsSafeDomain && trackUrlResult.Duration > 300)
            //    {
            //        _logger.LogWarning($"High-risk: User {this.UDUser.Key} spending extended time on non-safe domain {trackUrlResult.Domain} while remote access is active");
            //    }
            //}
        }


        private void CleanupExpiredImmediateDangers()
        {
            // Remove expired _immediateDangers from active list
            this._immediateDangers = this._immediateDangers.OrderByDescending(i => i.Timestamp).ToList();
            foreach (var item in this._immediateDangers.Where(i => i.EndTime != null).OrderByDescending(i => i.Timestamp))
            {
                this._immediateDangers.Remove(item);
            }

        }

        private void CleanupExpiredAlerts()
        {
            var expiryDate = DateTime.UtcNow.AddDays(-_alertExpiryDays);
            var deletionDate = DateTime.UtcNow.AddDays(-_alertDeletionDays);
            // Remove expired alerts from active list
            _activeDeviceAlerts = _activeDeviceAlerts
                .Where(alert => alert.Timestamp >= expiryDate)
                .ToList();
            // Additional logic to permanently delete alerts older than deletionDate can be added here
        }
        private void GetLatestAnalysisResults()
        {
            // Logic to fetch the latest analysis results from ASView

            this._devices = this._asView.GetUserDevices(this.UDUser.Key);
            this._remoteAccessAnalysisResults = this._asView.GetRemoteAccessAnalysisResultsByUserKey(UDUser.Key)
                .Where(i => i.AnalysisResult is not null && i.AnalysisResult.Success)
                .OrderByDescending(i => i.Timestamp).Take(5).ToList();

            this._urlAnalysisResults = this._asView.GetUrlAnalysisResultsByUserKey(UDUser.Key)
                .Where(i => i.AnalysisResult is not null && i.AnalysisResult.Success)
                .OrderByDescending(i => i.Timestamp).Take(5).ToList();
            //this._indicators = this._urlAnalysisResults.Select(i => i.Indicators?.Where(j => j is not null))
            //   .Union(this._remoteAccessAnalysisResults.Select(i => i.Indicators?.Where(j => j is not null)));

            //this._protectiveActions = this._urlAnalysisResults.Select(i => i.ProtectiveActions?.Where(j => j is not null))
            //    .Union(this._remoteAccessAnalysisResults.Select(i => i.ProtectiveActions?.Where(j => j is not null)));
            foreach( var r in this._remoteAccessAnalysisResults)
            {
                var ra = (r.Alert as RemoteAccessAlert);
                var y = new RemoteAccessStatusObject(DateTime.UtcNow, ra.DeviceInfo.DeviceUid, ra.RemoteAccessDirection, 
                    ra.RemoteAccessApp, ra.ConnectionStatus == ConnectionStatus.Open, ra.SessionStatus == (int)SessionStatus.Open);
            }

            foreach (var ind in this._remoteAccessAnalysisResults.Where(i => i.Indicators is not null).Select(i => i.Indicators))
            {
                if(ind is not null)
                {
                    this._indicators.AddRange(ind);
                }
            }
            foreach (var ind in this._urlAnalysisResults.Where(i => i.Indicators is not null).Select(i => i.Indicators))
            {
                if (ind is not null)
                {
                    this._indicators.AddRange(ind);
                }
            }

            foreach (var pra in this._remoteAccessAnalysisResults.Where(i => i.ProtectiveActions is not null).Select(i => i.ProtectiveActions))
            {
                if (pra is not null)
                {
                    this._protectiveActions.AddRange(pra);
                }
            }
            foreach (var pra in this._urlAnalysisResults.Where(i => i.ProtectiveActions is not null).Select(i => i.ProtectiveActions))
            {
                if (pra is not null)
                {
                    this._protectiveActions.AddRange(pra);
                }
            }

        }
        public void Start()
        {
            if (!_isInitialized)
            {
                Initialize();
            }

            _isRunning = true;
            _logger.LogInformation($"UDAnalysis started for user: {this.UDUser.Key}");
        }

        public void Stop()
        {
            _isRunning = false;
            _logger.LogInformation($"UDAnalysis stopped for user: {UDUser.Key}");
        }

        public void Initialize()
        {
            this.GetLatestAnalysisResults();

            _logger.LogInformation($"UDUserAnalyzer initialized for user: {UDUser.Key}");

        }

        private bool DetectImmediateDanger(Key? alertKey)
        {
            if (alertKey == null)
            {
                return false;
            }
            bool res = false;
            
            this.GetLatestAnalysisResults();
            
            var remoteAccessStatus = this.GetRemoteAccessStatus();
            this._remoteAccessStatus = this.GetRemoteAccessStatus()?.ToList() ?? new List<KeyValuePair<string, RemoteAccessStatusObject>>();
            //if (remoteAccessStatus is not null && !_remoteAccessStatus.Any(i => i.Timestamp >= remoteAccessStatus.Timestamp))
            //{
            //    this._remoteAccessStatus.Add(remoteAccessStatus);
            //}
            if (!this._remoteAccessStatus.Any(i => i.Value.isRemoteAccessSessionActive && i.Value.RemoteAccessDirection == RemoteAccessDirection.Incoming))
            {
                //return false;
            }


            var remoteAccessObjectsWithActiveSession = this._remoteAccessStatus.OrderByDescending(i => i.Value.Timestamp).Where(i => i.Value.isRemoteAccessSessionActive && i.Value.RemoteAccessDirection == RemoteAccessDirection.Incoming);
            //remoteAccessObjectsWithActiveSession = this._remoteAccessStatus.OrderByDescending(i => i.Timestamp).Where(i => i.isRemoteAccessSessionActive);
            if (!remoteAccessObjectsWithActiveSession.Any())
            {
                //No remoteAccess Objects With ActiveSession found. Close all _immediateDangers by setting EndTime
                
                foreach (var item in this._immediateDangers.Where(i => i.EndTime == null))
                {
                    item.EndTime = DateTime.UtcNow;
                    var evt = new ImmediateDangerEnded(item.Key, this.UDUser.Key);
                    this._domainEventPublisher.Register(evt);
                }
            }

            else
            {
                var activeDeviceUids = remoteAccessObjectsWithActiveSession.Select(i => i.Value.DeviceUid).ToHashSet();
                var urlAnalysisResultViews = this._asView.GetUrlAnalysisResultsByUserKey(this.UDUser.Key)
                    .OrderByDescending(I => I.Timestamp)
                    .ToList();
                var userDervices = this.UDUser.UserDevices.Select(i => i.DeviceUid);
                foreach (var deviceUid in userDervices)
                {
                    if (activeDeviceUids?.Count == 0 || !(activeDeviceUids?.Contains(deviceUid) == true))
                    {
                        foreach (var item in this._immediateDangers.Where(i => i.DeviceUid == deviceUid && i.EndTime == null))
                        {
                            item.EndTime = DateTime.UtcNow;
                            var evt = new ImmediateDangerEnded(item.Key, this.UDUser.Key);
                            this._domainEventPublisher.Register(evt);
                        }
                    }
                    else if (this.HasSensitiveBrowserPages(deviceUid))
                    {
                        res = true;
                        var remoteAccessApp = remoteAccessObjectsWithActiveSession.OrderByDescending(i => i.Value.Timestamp).FirstOrDefault(i => i.Key == deviceUid).Value.RemoteAccessApp;
                        _logger.LogWarning($"Immediate danger detected for user {this.UDUser.Key} on device {deviceUid} with active remote access session and sensitive website open.");
                        var sUrl = this.UDUser.BrowserTabs?[deviceUid]?.FirstOrDefault(i => this.IsSensitiveWebsite(i.Url))?.Url;
                        var urls = this.UDUser.BrowserTabs?[deviceUid]?.Where(i => this.IsSensitiveWebsite(i.Url)).ToList();
                        if (this._immediateDangers is null)
                        {
                            this._immediateDangers = new();
                        }
                        //if (alertKey is not null && !this._immediateDangers.OfType<ImmediateDangerByRemoteAccessDto>().Any(i => i.DeviceAlertKey == alertKey && i.EndTime == null && i.RemoteAccessApp == remoteAccessApp && i.DeviceUid == deviceUid && i.SensitiveUrl?.ToLower() == sUrl))
                        if (alertKey is not null && !this._immediateDangers.OfType<ImmediateDangerByRemoteAccessDto>().Any(i => i.EndTime == null && i.RemoteAccessApp == remoteAccessApp && i.DeviceUid == deviceUid && i.SensitiveUrl?.ToLower() == sUrl))
                        {
                            // Create new immediate danger instance and add to the list
                            var immediateDanger = new ImmediateDangerByRemoteAccessDto(new Key(nameof(ImmediateDangerByRemoteAccess), Guid.NewGuid().ToString()), remoteAccessApp, sUrl, deviceUid, this.UDUser.Key.Value,
                                this.UDUser.UserDevices.FirstOrDefault(i => i.DeviceUid == deviceUid)?.Key, alertKey, null, []);
                            this._immediateDangers.Add(immediateDanger);
                        }
                        //Publish event ImmediateDanderDetected
                        var domainEvent = new ImmediateDangerDetected(this._immediateDangers.OrderByDescending(i => i.Timestamp).OfType<ImmediateDangerByRemoteAccessDto>()
                            .First(i => i.EndTime == null && i.RemoteAccessApp == remoteAccessApp && i.DeviceUid == deviceUid && i.SensitiveUrl?.ToLower() == sUrl));
                        this._domainEventPublisher.Register(domainEvent);


                    }
                }

            }
            this.CleanupExpiredImmediateDangers();
            this._domainEventPublisher.RaiseAll();
            return res;
        }

        private bool HasSensitiveBrowserPages(string deviceUid)
        {
            return this.UDUser.BrowserTabs?[deviceUid]?.Select(i => i.Url).Any(i => this.IsSensitiveWebsite(i)) ?? false;
        }

        private bool IsSensitiveWebsite(string url)
        {
            if (url is null || String.IsNullOrEmpty(url))
            {
                return false;
            }

            var domain = KnownPhishingWebsite.GetDomainFromUrl(url).ToLower();
            if (url.IndexOf("localhost") >=7 && url.IndexOf("localhost") <= 9)
            {
                return false;
            }

            string[] sensitiveWebsiteCategories = "crypto_exchange,bank,banking".Split(',');
            List<UrlAnalysisResultView> urlAnalysisResultViews = this._asView.GetUrlAnalysisResultsByUserKey(this.UDUser.Key)
                .OrderByDescending(I => I.Timestamp)
                .ToList();
            var view = urlAnalysisResultViews.FirstOrDefault(i => i.AnalysisResult is UrlAnalysisResult uv && uv.Domain.ToLower() == domain.ToLower());
            if (sensitiveWebsiteCategories.Contains(view?.AnalysisResult?.website_category?.category?.ToLower()))
            {
                return true;
            }
            
            return false;
        }
    }

    //public class RemoteAccessStatusObject
    //{
    //    public RemoteAccessStatusObject(DateTime timestamp, string deviceUid, RemoteAccessDirection remoteAccessDirection, RemoteAccessApp remoteAccessApp, 
    //        bool isRemoteAccessAppActive, bool isRemoteAccessSessionActive)
    //    {
    //        Timestamp = timestamp;
    //        DeviceUid = deviceUid;
    //        RemoteAccessDirection = remoteAccessDirection;
    //        RemoteAccessApp = remoteAccessApp;
    //        IsRemoteAccessAppActive = isRemoteAccessAppActive;
    //        this.isRemoteAccessSessionActive = isRemoteAccessSessionActive;
    //    }

    //    public DateTime Timestamp { get; set; }
    //    public string DeviceUid { get; set; }
    //    public RemoteAccessDirection RemoteAccessDirection { get; set; }
    //    public RemoteAccessApp RemoteAccessApp { get; set; }
    //    public bool IsRemoteAccessAppActive { get; set; }
    //    public bool isRemoteAccessSessionActive { get; set; }

    //}
}
