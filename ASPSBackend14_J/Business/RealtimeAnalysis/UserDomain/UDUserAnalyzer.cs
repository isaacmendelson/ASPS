using Business.DomainEvents;
using Business.RealtimeAnalysis.Indicators;
using Business.Views;
using Common.Entities;
using Common.Enums;
using Common.Interfaces;
using Common.Models;
using Common.Models.Alerts;
using Common.ViewModels;
using Microsoft.Extensions.Logging;
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
        private List<IAnalysisResultView> _analysisResults = new();
        private List<RemoteAccessAnalysisResultView> _remoteAccessAnalysisResults = new();
        private List<UrlAnalysisResultView> _urlAnalysisResults = new();
        private List<UserDeviceView> _devices = new();
        //private IEnumerable<IEnumerable<IIndicator>?> _indicators = Enumerable.Empty<IEnumerable<Indicator>?>();
        //private IEnumerable<IEnumerable<IProtectiveAction>?> _protectiveActions = Enumerable.Empty<IEnumerable<IProtectiveAction>?>();
        private List<IIndicator?> _indicators = new();
        private List<IProtectiveAction?> _protectiveActions = new();
        private KeyValuePair<string, Indicator>[] _activeIndicatorMap = Array.Empty<KeyValuePair<string, Indicator>>();
        //private KeyValuePair<string, IProtectiveAction>[] _protectiveActions = Array.Empty<KeyValuePair<string, IProtectiveAction>>();

        private List<RemoteAccessStatusObject> _remoteAccessStatus = new();

        //private List<BrowserTab> _browserTabs = new();

        public UDUserAnalyzer(
            UDUser udUser,
            //ILoggerFactory loggerFactory
            ASView asView,
            int alertExpiryDays,
            int alertDeletionDays,
            ILoggerFactory loggerFactory
            ) 
        {
            this.UDUser = udUser;
            this._asView = asView;
            this._logger = loggerFactory.CreateLogger<UDUserAnalyzer>();
            this._alertDeletionDays = alertDeletionDays;
            this._alertExpiryDays = alertExpiryDays;
        }
        public string Name => "UDUserAnalyzer";
        //public ExternalAnalyzer[] ExternalAnalyzers { get; }

        public UDUser UDUser { get; private set; }

        

        public Task AnalyzeAsync()
        {
            // Placeholder for user-specific analysis logic
            return Task.CompletedTask;
        }

        public Type[] GetHandleableEvents()
        {
            return new[] { 
                typeof(AnalysisResultAdded) ,
                //typeof(AnalysisResultReceived) ,
            };
        }

        public async Task Handle(IDomainEvent evt)
        {
            switch(evt)
            {
                case AnalysisResultReceived analysisEvent:
                    // Handle the analysis result received event
                    await this.HandleAnalysisResultReceivedAsync(analysisEvent);
                    break;
                case AnalysisResultAdded analysisEvent:
                    // Handle the analysis result received event
                    await this.HandleAnalysisResultAddedAsync(analysisEvent);
                    break;
            }

            var isImmediateDanger = this.CheckImmediateDanger();
        }

        public async Task HandleAnalysisResultAddedAsync(AnalysisResultAdded analysisEvent)
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
            var results = this._analysisResults.OrderByDescending(i => i.Timestamp).Take(5);
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
                    //    if ((u.Purpose?.Category == WebsiteType.Banking) || (u.Purpose?.Category == WebsiteType.Exchange))
                    //    {

                    //    }
                    //}
                    break;
                case TrackUrlAnalysisResult t:
                    HandleTrackUrlAnalysisResultReceived(t, remoteAccessStatus);
                    break;
            }

        }

        public async Task HandleAnalysisResultReceivedAsync(AnalysisResultReceived analysisEvent)
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
            var results = this._analysisResults.OrderByDescending(i => i.Timestamp).Take(5);
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
                    //    if ((u.Purpose?.Category == WebsiteType.Banking) || (u.Purpose?.Category == WebsiteType.Exchange))
                    //    {

                    //    }
                    //}
                    break;
                case TrackUrlAnalysisResult t:
                    HandleTrackUrlAnalysisResultReceived(t, remoteAccessStatus);
                    break;
            }
            
        }


        private RemoteAccessStatusObject? GetRemoteAccessStatus()
        {
            if (this._remoteAccessAnalysisResults.Count ==0)
            {
                return null;
            }
            
            var deviceInfo = this._remoteAccessAnalysisResults.FirstOrDefault()?.Alert.DeviceInfo;
            var anaylisisResult = this._remoteAccessAnalysisResults.FirstOrDefault()?.AnalysisResult;
            if (anaylisisResult is null)
            {
                return null;
            }
            
            var isRemoteAccessAppActive = anaylisisResult.RunningProcesses > 0;
            var isRemoteAccessSessionActive = anaylisisResult.SessionStatus > 0;
            var remoteAccessDirection = anaylisisResult.RemoteAccessDirection;
            var connectionStatus = anaylisisResult.ConnectionStatus;
            return new RemoteAccessStatusObject(DateTime.UtcNow, deviceInfo.DeviceUid, remoteAccessDirection,  anaylisisResult.RemoteAccessApp,isRemoteAccessAppActive, isRemoteAccessSessionActive);
        }

        private void HandleTrackUrlAnalysisResultReceived(TrackUrlAnalysisResult trackUrlResult, RemoteAccessStatusObject remoteAccessStatus)
        {
            _logger.LogInformation($"Handling TrackUrlAnalysisResult: URL={trackUrlResult.Url}, Duration={trackUrlResult.Duration}s, IsSafe={trackUrlResult.IsSafeDomain}");

            // Log scam-in-progress scenarios
            if (!string.IsNullOrWhiteSpace(trackUrlResult.ScamInProgressKey))
            {
                _logger.LogWarning($"Scam-in-progress detected for user {this.UDUser.Key}: {trackUrlResult.ScamInProgressKey}");
            }

            // Check for high-risk scenarios when remote access is active
            if (remoteAccessStatus.IsRemoteAccessAppActive && remoteAccessStatus.isRemoteAccessSessionActive)
            {
                if (!trackUrlResult.IsSafeDomain && trackUrlResult.Duration > 300)
                {
                    _logger.LogWarning($"High-risk: User {this.UDUser.Key} spending extended time on non-safe domain {trackUrlResult.Domain} while remote access is active");
                }
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

        private bool CheckImmediateDanger()
        {
            bool res = false;
            var remoteAccessStatus = this.GetRemoteAccessStatus();
            if (!this._remoteAccessStatus.Any(i => i.isRemoteAccessSessionActive && i.RemoteAccessDirection == RemoteAccessDirection.In))
            {
                return false;
            }
            var remoteAccessObjectsWithActiveRemoteAccess = this._remoteAccessStatus.Where(i => i.isRemoteAccessSessionActive && i.RemoteAccessDirection == RemoteAccessDirection.In);
                //.Select(i => i.DeviceUid).ToHashSet();  

            foreach (var obj in remoteAccessObjectsWithActiveRemoteAccess.Where(i => i is not null))
            {
                if (this.UDUser.BrowserTabs is not null && this.UDUser.BrowserTabs[obj.DeviceUid].Any(i => this.IsSensitiveWebsite(i.Url)))
                {
                    res = true;
                    _logger.LogWarning($"Immediate danger detected for user {this.UDUser.Key} on device {obj.DeviceUid} with active remote access session and sensitive website open.");
                    var sUrl = this.UDUser.BrowserTabs[obj.DeviceUid].FirstOrDefault(i => this.IsSensitiveWebsite(i.Url))?.Url;
                    var immeidateDanger = new ImmediateDanger(obj.RemoteAccessApp, sUrl, obj.DeviceUid, this.UDUser.Key.Value, 
                        this.UDUser.UserDevices.FirstOrDefault(i => i.DeviceUid == obj.DeviceUid)?.Key.Value, null, new ProtectiveAction[] { });
                    //{
                    //    UserKey = this.UDUser.Key,
                    //    DeviceUid = deviceUid,
                    //    Timestamp = DateTime.UtcNow,
                    //    Description = "Active remote access session detected with sensitive website open."
                    //};

                }
            }
            return res;
        }

        private bool IsSensitiveWebsite(string url)
        {
            // Placeholder for logic to determine if a website is sensitive (e.g., banking, exchange)
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
