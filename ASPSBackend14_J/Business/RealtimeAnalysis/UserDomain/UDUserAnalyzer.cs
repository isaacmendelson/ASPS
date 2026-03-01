using Business.DomainEvents;
using Business.RealtimeAnalysis.Indicators;
using Business.Views;
using Common.Entities;
using Common.Enums;
using Common.Interfaces;
using Common.Models;
using Common.Models.Alerts;
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
            return new[] { typeof(AnalysisResultReceived) };
        }

        public async Task Handle(IDomainEvent evt)
        {
            if (evt is AnalysisResultReceived analysisEvent)
            {
                // Handle the analysis result received event
                this.HandleAnalysisResultReceivedAsync(analysisEvent);
            }
        }

        public async Task HandleAnalysisResultReceivedAsync(AnalysisResultReceived analysisEvent)
        {
            if (analysisEvent.AnalyzerResults.TryGetValue(nameof(UDRemoteAccessAnalyzer), out var raResult))
            {
                this.UDUser.BrowserTabs[analysisEvent.DeviceUid] = (raResult.Item1 as RemoteAccessAnalysisResultVm)?.BrowserTabs;
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
            switch (analysisEvent.AnalyzerResults.FirstOrDefault().Value.Item1)
            {
                case RemoteAccessAnalysisResultVm r:
                    break;
                case UrlAnalysisResultVm u:
                    var websitePurpose = u.Purpose;

                    if (remoteAccessStatus.IsRemoteAccessAppActive && remoteAccessStatus.isRemoteAccessSessionActive)
                    {
                        if ((u.Purpose?.Category == WebsiteType.Banking) || (u.Purpose?.Category == WebsiteType.Exchange))
                        {

                        }
                    }
                    break;
            }
            
        }


        private RemoteAccessStatusObject GetRemoteAccessStatus()
        {
            var isRemoteAccessAppActive = this._remoteAccessAnalysisResults.FirstOrDefault()?.AnalysisResult?.RunningProcesses > 0;
            var isRemoteAccessSessionActive = this._remoteAccessAnalysisResults.FirstOrDefault()?.AnalysisResult?.SessionStatus > 0;
            return new RemoteAccessStatusObject(isRemoteAccessAppActive, isRemoteAccessSessionActive);
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
    }

    public class RemoteAccessStatusObject
    {
        public RemoteAccessStatusObject(bool isRemoteAccessAppActive, bool isRemoteAccessSessionActive)
        {
            this.IsRemoteAccessAppActive = isRemoteAccessAppActive;
            this.isRemoteAccessSessionActive = isRemoteAccessSessionActive;
        }

        public bool IsRemoteAccessAppActive { get; set; }
        public bool isRemoteAccessSessionActive { get; set; }

    }
}
