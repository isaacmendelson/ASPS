using Business.Data.EF.Repositories;
using Business.DomainEvents;
using Business.RealtimeAnalysis.UserDomain;
using Common.Entities;
using Common.Enums;
using Common.Interfaces;
using Common.Models;
using Common.Models.Alerts;
using Interface.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Business.Views;

public class ASView : IDomainEventHandler, IBackgroundTask
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ASView> _logger;
    private readonly object _lock = new();
    private IConfiguration _configuration;
    private bool IsInitialized = false;

    private List<User> _users = new();
    private List<UserDevice> _userDevices = new();
    private List<UserAccount> _userAccounts = new();
    private List<DeviceAlertView> _deviceAlerts = new();
    private List<AnalysisResultView> _analysisResults = new();
    private List<RemoteAccessAnalysisResultView> _remoteAccessAnalysisResults = new();
    private List<UrlAnalysisResultView> _urlAnalysisResults = new();
    private List<TrackUrlAnalysisResultView> _trackUrlAnalysisResults = new();
    private List<KnownPhishingWebsite> _knownPhishingWebsites = new();
    private List<SafeDomain> _safeDomains = new();
    private List<string> _riskyDomains = new();
    private List<UserDeviceUrlSurfData> _riskyUrlSurfings = new();
    



    public ASView(
        IServiceProvider serviceProvider, ILogger<ASView> logger, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        this._configuration = configuration;
    }

    public void Start()
    {
        _logger.LogInformation("ASView starting...");
        Initialize();
    }

    private void Initialize()
    {
        if (this.IsInitialized)
        {
            _logger.LogInformation("ASView already initialized, skipping...");
            return;
        }

        _logger.LogInformation("ASView initializing - loading data into memory...");
        
        // Run synchronously in background to avoid blocking - we're in a startup context
        Task.Run(async () => await LoadDataAsync()).GetAwaiter().GetResult();
        
        this.IsInitialized = true;
        
        _logger.LogInformation($"ASView initialized: {_users.Count} users, {_userDevices.Count} devices, {_userAccounts.Count} accounts");
    }

    public void ReInitialize()
    {
        _logger.LogInformation("ASView re-initialization requested...");
        this.IsInitialized = false;
        this.Initialize();
    }

    public void Stop()
    {
        _logger.LogInformation("ASView stopping...");
    }

    public virtual async Task Handle(IDomainEvent evt)
    {
        switch (evt)
        {
            case AnalysisResultReceived analysisEvent:
                HandleAnalysisResultReceived(analysisEvent);
                break;
            case DeviceAlertReceived alertEvent:
                HandleDeviceAlertReceived(alertEvent);
                break;

            case UserAdded userAdded:
                HandleUserAdded(userAdded);
                break;
            case UserUpdated userUpdated:
                HandleUserUpdated(userUpdated);
                break;
            case UserDeleted userDeleted:
                HandleUserDeleted(userDeleted);
                break;
            case SystemConfigurationChanged sysConfigChanged:
                HandleSystemConfigurationChanged(sysConfigChanged);
                break;
        }
    }

    private void HandleDeviceAlertReceived(DeviceAlertReceived alertEvent)
    {
        // Offload the processing to a background task to avoid blocking the event handler
        Task.Run(() => ProcessDeviceAlertReceived(alertEvent));
    }
    private void ProcessDeviceAlertReceived(DeviceAlertReceived alertEvent)
    {
        try
        {
            _logger.LogInformation($"ASView handling DeviceAlertReceived: AlertType={alertEvent.Alert.AlertType}");
            var user = this.FindUserByDeviceUid(alertEvent.DeviceUid);

            DeviceAlertView? view = null;
            switch (alertEvent.Alert)
            {
                case UrlAlert urlAlert:
                    _logger.LogInformation($"Received UrlAlert for URL: {urlAlert.Url}");
                    view = new UrlAlertView(
                        new Key(alertEvent.Alert.AlertType, alertEvent.Alert.AlertId ?? "0"), 
                        alertEvent.Alert.AlertId ?? "0",
                        alertEvent.Alert.AlertType,
                        alertEvent.Alert.Priority,
                        alertEvent.Timestamp, alertEvent.Alert.Token,
                        alertEvent.Alert.DeviceInfo.DeviceUid, alertEvent.Alert.DeviceInfo.DeviceType, 
                        alertEvent.Alert.DeviceInfo.OperatingSystem, alertEvent.Alert.DeviceInfo.MACAddress, user?.Key,
                            urlAlert.Url, urlAlert.Trackers, urlAlert.IFrameDomains, urlAlert.UserAgent
                            
                            );
                    break;
                case RemoteAccessAlert ra:
                    _logger.LogInformation($"Received RemoteAccessAlert for App: {ra.RemoteAccessApp}");
                    view = new RemoteAccessAlertView(
                        new Key(alertEvent.Alert.AlertType, alertEvent.Alert.AlertId ?? "0"),
                        alertEvent.Alert.AlertId ?? "0",
                        alertEvent.Alert.AlertType,
                        alertEvent.Alert.Priority,
                        alertEvent.Timestamp, alertEvent.Alert.Token,
                        ra.DeviceInfo.DeviceUid, ra.DeviceInfo.DeviceType, ra.DeviceInfo.OperatingSystem, ra.DeviceInfo.MACAddress,
                        user?.Key,
                        ra.RemoteAccessApp, ra.RunningProcesses, ra.ConnectionUrl, ra.ConnectionStatus, ra.ConnectionsCount, ra.SessionStatus, ra.BrowserTabs
                        );
                    break;
            }

            if (view is not null)
            {
                lock (_lock) { _deviceAlerts.Add(view); }
            }

            _logger.LogInformation($"ASView added device alert: AlertType={alertEvent.Alert.AlertType}, UserKey={user?.Key}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling DeviceAlertReceived in ASView");
        }
    }

    private void HandleAnalysisResultReceived(AnalysisResultReceived analysisEvent)
    {
        try
        {
            _logger.LogInformation($"ASView handling AnalysisResultReceived: AlertType={analysisEvent.AlertType}");

            string jsonValue = string.Empty;
            string discriminator = string.Empty;

            switch (analysisEvent.AlertType)
            {
                case nameof(UrlAlert):
                    var vm1 = analysisEvent.AnalyzerResults
                        .FirstOrDefault(i => i.Value.Item1 is UrlAnalysisResult).Value?.Item1 as UrlAnalysisResult;
                    discriminator = nameof(UrlAnalysisResult);
                    jsonValue = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        AnalyzerResults = vm1,
                        Severity = analysisEvent.Severity.ToString(),
                        Message = analysisEvent.Message,
                        Details = analysisEvent.Details,
                        Timestamp = analysisEvent.AnalysisTimestamp,
                        DeviceUid = analysisEvent.DeviceUid
                    });
                    break;
                case nameof(RemoteAccessAlert):
                    var vm2 = analysisEvent.AnalyzerResults
                        .FirstOrDefault(i => i.Value.Item1 is RemoteAccessAnalysisResult).Value?.Item1 as RemoteAccessAnalysisResult;
                    discriminator = nameof(RemoteAccessAnalysisResult);
                    jsonValue = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        AnalyzerResults = vm2,
                        Severity = analysisEvent.Severity.ToString(),
                        Message = analysisEvent.Message,
                        Details = analysisEvent.Details,
                        Timestamp = analysisEvent.AnalysisTimestamp,
                        DeviceUid = analysisEvent.DeviceUid
                    });
                    break;
            }

            var container = new AnalysisResultContainer(
                 Guid.NewGuid().ToString(),
                analysisEvent.UserKeyField,
                discriminator,
                analysisEvent.Timestamp,
                jsonValue,
                false,
                null,
                false,
                analysisEvent.DeviceAlertKeyField
            );

            lock (_lock)
            {
                // Create typed views so specific data (AnalysisResult, Alert) is preserved
                switch (analysisEvent.AlertType)
                {
                    case nameof(UrlAlert):
                        var urlView = new UrlAnalysisResultView(container);
                        _urlAnalysisResults.Add(urlView);
                        _analysisResults.Add(urlView);
                        break;
                    case nameof(TrackUrlAlert):
                        var trackUrlView = new TrackUrlAnalysisResultView(container);
                        _trackUrlAnalysisResults.Add(trackUrlView);
                        _analysisResults.Add(trackUrlView);
                        break;
                    case nameof(RemoteAccessAlert):
                        var raView = new RemoteAccessAnalysisResultView(container);
                        _remoteAccessAnalysisResults.Add(raView);
                        _analysisResults.Add(raView);
                        break;
                }
            }

            _logger.LogInformation($"ASView added analysis result: Discriminator={discriminator}, UserKey={analysisEvent.UserKeyField}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling AnalysisResultReceived in ASView");
        }
    }

    private void HandleUserAdded(UserAdded evt)
    {
        lock (_lock) { _users.Add(evt.User); }
        _logger.LogInformation("ASView: User added to cache - {FirstName} {LastName} (Key: {Key})",
            evt.User.FirstName, evt.User.LastName, evt.User.KeyField);
    }

    private void HandleUserUpdated(UserUpdated evt)
    {
        lock (_lock)
        {
            var existing = _users.FindIndex(u => u.KeyField == evt.User.KeyField);
            if (existing >= 0)
            {
                _users[existing] = evt.User;
                _logger.LogInformation("ASView: User updated in cache - {FirstName} {LastName} (Key: {Key})",
                    evt.User.FirstName, evt.User.LastName, evt.User.KeyField);
            }
            else
            {
                _users.Add(evt.User);
                _logger.LogInformation("ASView: User not found in cache during update, added - {FirstName} {LastName} (Key: {Key})",
                    evt.User.FirstName, evt.User.LastName, evt.User.KeyField);
            }
        }
    }

    private void HandleUserDeleted(UserDeleted evt)
    {
        lock (_lock)
        {
            var existing = _users.FirstOrDefault(u => u.KeyField == evt.UserKeyField);
            if (existing != null)
            {
                existing.IsDeleted = true;
            }
        }
        _logger.LogInformation("ASView: User marked as deleted in cache (Key: {Key})", evt.UserKeyField);
    }

    private void HandleSystemConfigurationChanged(SystemConfigurationChanged evt)
    {
        _logger.LogInformation("ASView: System configuration changed, reloading System Configuration data...");
        this._configuration = evt.NewConfiguration;
    }
    public Type[] GetHandleableEvents()
    {
        return new[] { typeof(AnalysisResultReceived), typeof(DeviceAlertReceived), typeof(UserAdded), typeof(UserUpdated), typeof(UserDeleted), typeof(SystemConfigurationChanged) };
    }

    private async Task LoadDataAsync()
    {
        try
        {
            _logger.LogInformation("ASView LoadDataAsync starting...");
            
            // Create a scope to get scoped repositories
            using (var scope = _serviceProvider.CreateScope())
            {
                var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                var userDeviceRepository = scope.ServiceProvider.GetRequiredService<IUserDeviceRepository>();
                var userAccountRepository = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();

                var deviceAlertRepository = scope.ServiceProvider.GetRequiredService<IDeviceAlertRepository>();
                var analysisResultRepository = scope.ServiceProvider.GetRequiredService<IAnalysisResultRepository>();

                _logger.LogInformation("Fetching users from repository...");
                var users = await userRepository.GetAllAsync();
                _logger.LogInformation($"Users fetched: {users.Count()} records");
                
                _logger.LogInformation("Fetching devices from repository...");
                var devices = await userDeviceRepository.GetAllAsync();
                _logger.LogInformation($"Devices fetched: {devices.Count()} records");
                
                _logger.LogInformation("Fetching accounts from repository...");
                var accounts = await userAccountRepository.GetAllAsync();
                _logger.LogInformation($"Accounts fetched: {accounts.Count()} records");
                
                var deviceAlerts = await deviceAlertRepository.GetAllAsync();
                _logger.LogInformation($"deviceAlerts fetched: {deviceAlerts.Count()} records");

                var analysisResults = await analysisResultRepository.GetAllAsync();
                analysisResults = analysisResults.Where(i => i.JsonValue is not null && !string.IsNullOrEmpty(i.JsonValue)).OrderByDescending(i => i.Timestamp).Take(1000);
                _logger.LogInformation($"analysisResults fetched: {deviceAlerts.Count()} records");

                var knownPhishingWebsiteRepository = scope.ServiceProvider.GetRequiredService<IKnownPhishingWebsiteRepository>();
                var knownPhishingWebsites = await knownPhishingWebsiteRepository.GetAllActiveAsync();
                _logger.LogInformation($"knownPhishingWebsites fetched: {knownPhishingWebsites.Count()} records");

                var safeDomainRepository = scope.ServiceProvider.GetRequiredService<ISafeDomainRepository>();
                var safeDomains = await safeDomainRepository.GetAllActiveAsync();
                _logger.LogInformation($"safeDomains fetched: {safeDomains.Count()} records");
                
                // Use lock when updating shared state
                lock (_lock)
                {
                    _users = users.ToList();
                    _userDevices = devices.ToList();
                    _userAccounts = accounts.ToList();
                    _deviceAlerts = (List<DeviceAlertView>)(
                        deviceAlerts.Where(i => i is UrlAlertEntity).Select(i => new UrlAlertView(i as UrlAlertEntity))
                        .Union<DeviceAlertView>
                        (
                        deviceAlerts.Where(i => i is RemoteAccessAlertEntity).Select(i => new RemoteAccessAlertView(i as RemoteAccessAlertEntity))
                        )).OrderByDescending(i => i.Timestamp).ToList();

                    _remoteAccessAnalysisResults = analysisResults.Where(i => i.Discriminator == nameof(RemoteAccessAnalysisResult)).Select(i => new RemoteAccessAnalysisResultView(i)).ToList();
                    _urlAnalysisResults = analysisResults.Where(i => i.Discriminator == nameof(UrlAnalysisResult) && !i.IsDisabled).
                        Select(i => new UrlAnalysisResultView(i))
                        .OrderByDescending(i => i.Timestamp).ToList();

                    _analysisResults = _urlAnalysisResults.Cast<AnalysisResultView>()
                        .Concat(_remoteAccessAnalysisResults)
                        .OrderByDescending(i => i.Timestamp).ToList();
                    _knownPhishingWebsites = knownPhishingWebsites.ToList();
                    _safeDomains = safeDomains.ToList();
                    
                    _riskyDomains = _deviceAlerts
                        .OfType<UrlAlertView>()
                        .Where(u => !string.IsNullOrEmpty(u.Url) && this._knownPhishingWebsites.Select(i => i.Domain).Contains(u.Url))
                        .Distinct()
                        .Select(u => u.Url.ToLower())
                        .Union(
                            this._urlAnalysisResults.Where(i => (i.AnalysisResult?.Success == true && i.AnalysisResult?.risk_assessment?.risk_score >= 61))
                            .Select(r => KnownPhishingWebsite.GetDomainFromUrl((r.Alert as UrlAlert)!.Url.ToLower()))
                        )
                        .ToList();

                    var q = _deviceAlerts
                        .OfType<UrlAlertView>()
                        .Where(u => !string.IsNullOrEmpty(u.Url) && this._riskyDomains.Contains(u.Url))
                        .Distinct();

                    this._riskyUrlSurfings = q
                        .Where(u => !string.IsNullOrEmpty(u.Url) && this._riskyDomains.Contains(u.Url))
                        .Distinct()
                        .Select(u => new UserDeviceUrlSurfData(
                            u.UserKey,
                            u.Url,
                            u.DeviceUid,
                            MessagingApp.Unknown,
                            (this._analysisResults.FirstOrDefault(i => i.Alert?.AlertId == u.AlertId)?.AnalysisResult as UrlAnalysisResult)?.risk_assessment,
                            q.Where(i => i.UserKey == u.UserKey && i.Url.ToLower() == u.Url.ToLower()).Select(i => new SurfHistoryItem(u.Url, i.Timestamp)).ToList()
                            )
                        {  })
                        .ToList();
                }
            }

            _logger.LogInformation($"ASView data loaded: {_users.Count} users, {_userDevices.Count} devices, {_userAccounts.Count} accounts");
            
            // Debug: Show what was loaded
            foreach (var user in _users)
            {
                _logger.LogDebug($"  User loaded: {user.FirstName} {user.LastName} (Key: {user.Key})");
            }
            foreach (var device in _userDevices)
            {
                _logger.LogDebug($"  Device loaded: {device.DeviceUid} (UserKey: {device.UserKey})");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading data into ASView");
            _logger.LogError($"Exception details: {ex.Message}");
            _logger.LogError($"Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                _logger.LogError($"Inner exception: {ex.InnerException.Message}");
            }
        }
    }

    public List<User> GetUsers()
    {
        lock (_lock) { return _users.ToList(); }
    }
    
    public List<UserDeviceView> GetUserDevices(Key userKey)
    {
        lock (_lock) 
        { 
            return _userDevices
                .Where(i => i.UserKeyField?.ToString() == userKey.Value)
                .Select(i => new UserDeviceView(i))
                .ToList(); 
        }
    }
    
    public List<UserAccountView> GetUserAccounts(Key userKey)
    {
        lock (_lock) 
        { 
            return _userAccounts
                .Where(i => i.UserKeyField.ToString() == userKey.Value)
                .Select(i => new UserAccountView(i))
                .ToList(); 
        }
    }
    
    public UserDevice? FindUserDeviceByDeviceUid(string deviceUid)
    {
        lock (_lock)
        {
            return _userDevices.FirstOrDefault(d => d.DeviceUid == deviceUid && !d.IsDeleted);
        }
    }

    public User? FindUserByDeviceUid(string deviceUid)
    {
        lock (_lock)
        {
            var device = _userDevices.FirstOrDefault(d => d.DeviceUid == deviceUid && !d.IsDeleted);
            return _users.FirstOrDefault(i => i.KeyField == device?.UserKeyField);
        }
    }

    public User? FindUserByKey(Common.Models.Key userKey)
    {
        lock (_lock)
        {
            var keyField = Common.Models.Entity.GetDbKey(userKey);
            return _users.FirstOrDefault(u => u.KeyField == keyField && !u.IsDeleted);
        }
    }

    public User? FindUserByEmail(string email)
    {
        lock (_lock)
        {
            return _users.FirstOrDefault(u =>
                u.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && !u.IsDeleted);
        }
    }

    public User? FindUserByEmailActive(string email)
    {
        lock (_lock)
        {
            return _users.FirstOrDefault(u =>
                u.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && !u.IsDeleted && !u.IsDisabled);
        }
    }

    /// <summary>
    /// Add a device to the in-memory cache (call after persisting to DB).
    /// </summary>
    public void AddUserDevice(UserDevice device)
    {
        lock (_lock) { _userDevices.Add(device); }
        _logger.LogInformation("ASView: Added device {DeviceUid} to cache", device.DeviceUid);
    }

    internal IEnumerable<DeviceAlertView> GetActiveDeviceAlertsByUserKey(Key key, int? alertExpiryDays = 30)
    {
        lock (_lock)
        {
            int daysToSubtract = alertExpiryDays ?? 30;
            return this._deviceAlerts
                .Where(da => da.UserKey == key && da.Timestamp >= DateTime.UtcNow.AddDays(-daysToSubtract))
                .ToList();
        }
    }

    //internal IEnumerable<UserDeviceUrlSurfData> GetSuspeciousUserSurfDataByUserKey(Key key, int? alertExpiryDays = 30)
    //{
    //    int daysToSubtract = alertExpiryDays ?? 30;
    //    return this._deviceAlerts.OfType<UrlAlertView>()
    //        .Where(da => da.UserKey == key && this._da.Url && da.Timestamp >= DateTime.UtcNow.AddDays(-daysToSubtract))
    //        .Select(i => new UserDeviceUrlSurfData(key, i.DeviceUid);
    //}


    internal IEnumerable<UserDeviceUrlSurfData> GetRiskyUrlSurfingByUserKey(Key key, int? alertExpiryDays = 30)
    {
        lock (_lock)
        {
            int daysToSubtract = alertExpiryDays ?? 30;
            var res = this._riskyUrlSurfings
                .Where(da => da.UserKey.Value == key.Value 
                && (daysToSubtract == 0 || da.CreatedAt >= DateTime.UtcNow.AddDays(-daysToSubtract)));
            return res.ToList();
        }
    }

    internal IEnumerable<RemoteAccessAnalysisResultView> GetRemoteAccessAnalysisResultsByUserKey(Key key, int? alertExpiryDays = 30)
    {
        lock (_lock)
        {
            int daysToSubtract = alertExpiryDays ?? 30;
            var res = this._remoteAccessAnalysisResults
                .Where(da => da.UserKey?.Value == key.Value && da.Timestamp >= DateTime.UtcNow.AddDays(-daysToSubtract));
            return res.ToList();
        }
    }

    public List<KnownPhishingWebsite> GetKnownPhishingWebsites()
    {
        lock (_lock) { return _knownPhishingWebsites.ToList(); }
    }

    public List<SafeDomain> GetSafeDomains()
    {
        lock (_lock) { return _safeDomains.ToList(); }
    }

    public virtual bool IsSafeDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return false;
        
        lock (_lock)
        {
            var normalized = domain.ToLowerInvariant();
            return _safeDomains.Any(d => d.Domain.Equals(normalized, StringComparison.OrdinalIgnoreCase) && !d.IsDeleted);
        }
    }

    internal IEnumerable<UrlAnalysisResultView> GetUrlAnalysisResultsByUserKey(Key key, int? alertExpiryDays = 30)
    {
        lock (_lock)
        {
            int daysToSubtract = alertExpiryDays ?? 30;
            var res = this._urlAnalysisResults
                .Where(da => da.UserKey.Value == key.Value && da.Timestamp >= DateTime.UtcNow.AddDays(-daysToSubtract));
            return res.ToList();
        }
    }
    internal bool TryGetCachedUrlAnalysis(string url, int numberOfMonthsAgo, out UrlAnalysisResultView? cachedResult)
    {
        lock (_lock)
        {
            var urlLower = url.ToLower();
            var urlDomain = KnownPhishingWebsite.GetDomainFromUrl(urlLower);
            
            cachedResult = this._urlAnalysisResults
                .FirstOrDefault(da =>
                {
                    var cachedUrl = da.AnalysisResult?.Url?.ToLower();
                    if (cachedUrl == null) return false;
                    return (cachedUrl == urlLower ||
                        KnownPhishingWebsite.GetDomainFromUrl(cachedUrl) == urlDomain)
                        &&
                        (da.Timestamp >= DateTime.UtcNow.AddMonths(-numberOfMonthsAgo) || numberOfMonthsAgo <= 0);
                });
            
            return cachedResult is not null;
        }
    }
}
