using Business.Data.EF.Repositories;
using Business.DomainEvents;
using Business.RealtimeAnalysis.UserDomain;
using Common.Entities;
using Common.Interfaces;
using Common.Models;
using Common.Models.Alerts;
using Interface.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Business.Views;

public class ASView : IDomainEventHandler, IBackgroundTask
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ASView> _logger;
    
    private List<User> _users = new();
    private List<UserDevice> _userDevices = new();
    private List<UserAccount> _userAccounts = new();
    private List<DeviceAlertView> _deviceAlerts = new();
    private List<AnalysisResultView> _analysisResults = new();
    private List<RemoteAccessAnalysisResultView> _remoteAccessAnalysisResults = new();
    private List<UrlAnalysisResultView> _urlAnalysisResults = new();
    private List<KnownPhishingWebsite> _knownPhishingWebsites = new();
    private List<SafeDomain> _safeDomains = new();
    

    public ASView(
        IServiceProvider serviceProvider,
        ILogger<ASView> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public void Start()
    {
        _logger.LogInformation("ASView starting - loading data into memory...");
        
        LoadDataAsync().Wait();
        
        _logger.LogInformation($"ASView loaded: {_users.Count} users, {_userDevices.Count} devices, {_userAccounts.Count} accounts");
    }

    public void Stop()
    {
        _logger.LogInformation("ASView stopping...");
    }

    public async Task Handle(IDomainEvent evt)
    {
        switch (evt)
        {
            case AnalysisResultReceived analysisEvent:
                HandleAnalysisResultReceived(analysisEvent);
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
                        .FirstOrDefault(i => i.Value.Item1 is UrlAnalysisResultVm).Value?.Item1 as UrlAnalysisResultVm;
                    discriminator = nameof(UrlAnalysisResultVm);
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
                        .FirstOrDefault(i => i.Value.Item1 is RemoteAccessAnalysisResultVm).Value?.Item1 as RemoteAccessAnalysisResultVm;
                    discriminator = nameof(RemoteAccessAnalysisResultVm);
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

            _analysisResults.Add(new AnalysisResultView(container));

            switch (analysisEvent.AlertType)
            {
                case nameof(UrlAlert):
                    _urlAnalysisResults.Add(new UrlAnalysisResultView(container));
                    break;
                case nameof(RemoteAccessAlert):
                    _remoteAccessAnalysisResults.Add(new RemoteAccessAnalysisResultView(container));
                    break;
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
        _users.Add(evt.User);
        _logger.LogInformation("ASView: User added to cache - {FirstName} {LastName} (Key: {Key})",
            evt.User.FirstName, evt.User.LastName, evt.User.KeyField);
    }

    private void HandleUserUpdated(UserUpdated evt)
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

    private void HandleUserDeleted(UserDeleted evt)
    {
        var existing = _users.FirstOrDefault(u => u.KeyField == evt.UserKeyField);
        if (existing != null)
        {
            existing.IsDeleted = true;
            _logger.LogInformation("ASView: User marked as deleted in cache (Key: {Key})", evt.UserKeyField);
        }
    }

    public Type[] GetHandleableEvents()
    {
        return new[] { typeof(AnalysisResultReceived), typeof(UserAdded), typeof(UserUpdated), typeof(UserDeleted) };
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
                analysisResults = analysisResults.Where(i => i.JsonValue is not null && !string.IsNullOrEmpty(i.JsonValue)).OrderByDescending(i => i.Timestamp).Take(50);
                _logger.LogInformation($"analysisResults fetched: {deviceAlerts.Count()} records");

                var knownPhishingWebsiteRepository = scope.ServiceProvider.GetRequiredService<IKnownPhishingWebsiteRepository>();
                var knownPhishingWebsites = await knownPhishingWebsiteRepository.GetAllActiveAsync();
                _logger.LogInformation($"knownPhishingWebsites fetched: {knownPhishingWebsites.Count()} records");

                _users = users.ToList();
                _userDevices = devices.ToList();
                _userAccounts = accounts.ToList();

                _deviceAlerts = deviceAlerts.Select(i => new DeviceAlertView(i)).ToList();
                _analysisResults = analysisResults.Select(i => new AnalysisResultView(i)).ToList();
                _remoteAccessAnalysisResults = analysisResults.Where(i => i.Discriminator == nameof(RemoteAccessAnalysisResultVm)).Select(i => new RemoteAccessAnalysisResultView(i)).ToList();
                _urlAnalysisResults = analysisResults.Where(i => i.Discriminator == nameof(UrlAnalysisResultVm)).Select(i => new UrlAnalysisResultView(i)).ToList();
                var _urlAnalyzerResults = analysisResults.Where(i => i.Discriminator == nameof(UrlAnalysisResultVm)).Select(i => new UrlAnalysisResultView(i)).ToList();
                //var x = analysisResults.Where(i => i.Discriminator == nameof(UrlAnalysisResultVm)).Select(i => new UrlAnalyzerResultVm(i)).ToList();
                _knownPhishingWebsites = knownPhishingWebsites.ToList();

                var safeDomainRepository = scope.ServiceProvider.GetRequiredService<ISafeDomainRepository>();
                var safeDomains = await safeDomainRepository.GetAllActiveAsync();
                _logger.LogInformation($"safeDomains fetched: {safeDomains.Count()} records");
                _safeDomains = safeDomains.ToList();
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

    public List<User> GetUsers() => _users;
    public List<UserDeviceView> GetUserDevices(Key userKey) => _userDevices.Where(i => i.UserKeyField.ToString() == userKey.Value).Select(i => new UserDeviceView(i)).ToList();
    public List<UserAccountView> GetUserAccounts(Key userKey) => _userAccounts.Where(i => i.UserKeyField.ToString() == userKey.Value).Select(i => new UserAccountView(i)).ToList();
    
    public UserDevice? FindUserDeviceByDeviceUid(string deviceUid)
    {
        return _userDevices.FirstOrDefault(d => d.DeviceUid == deviceUid && !d.IsDeleted);
    }

    public User? FindUserByDeviceUid(string deviceUid)
    {
        return _users.FirstOrDefault(i => i.KeyField == _userDevices.FirstOrDefault(d => d.DeviceUid == deviceUid && !d.IsDeleted)?.UserKeyField);
    }

    public User? FindUserByKey(Common.Models.Key userKey)
    {
        var keyField = Common.Models.Entity.GetDbKey(userKey);
        return _users.FirstOrDefault(u => u.KeyField == keyField && !u.IsDeleted);
    }

    public User? FindUserByEmail(string email)
    {
        return _users.FirstOrDefault(u =>
            u.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && !u.IsDeleted);
    }

    public User? FindUserByEmailActive(string email)
    {
        return _users.FirstOrDefault(u =>
            u.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && !u.IsDeleted && !u.IsDisabled);
    }

    /// <summary>
    /// Add a device to the in-memory cache (call after persisting to DB).
    /// </summary>
    public void AddUserDevice(UserDevice device)
    {
        _userDevices.Add(device);
        _logger.LogInformation("ASView: Added device {DeviceUid} to cache", device.DeviceUid);
    }

    internal IEnumerable<DeviceAlertView> GetActiveDeviceAlertsByUserKey(Key key, int? alertExpiryDays = 30)
    {
        int daysToSubtract = alertExpiryDays ?? 30;
        return this._deviceAlerts
            .Where(da => da.UserKey == key && da.Timestamp >= DateTime.UtcNow.AddDays(-daysToSubtract));
    }

    internal IEnumerable<IAnalysisResultView> GetAnalysisResultsByUserKey(Key key, int? alertExpiryDays = 30)
    {
        int daysToSubtract = alertExpiryDays ?? 30;
        var res = this._analysisResults
            .Where(da => da.UserKey.Value == key.Value && da.Timestamp >= DateTime.UtcNow.AddDays( -daysToSubtract));
        return res;
    }

     internal IEnumerable<RemoteAccessAnalysisResultView> GetRemoteAccessAnalysisResultsByUserKey(Key key, int? alertExpiryDays = 30)
    {
        int daysToSubtract = alertExpiryDays ?? 30;
        var res = this._remoteAccessAnalysisResults
            .Where(da => da.UserKey?.Value == key.Value && da.Timestamp >= DateTime.UtcNow.AddDays(-daysToSubtract));
        return res;
    }

    public List<KnownPhishingWebsite> GetKnownPhishingWebsites() => _knownPhishingWebsites;

    public List<SafeDomain> GetSafeDomains() => _safeDomains;

    public bool IsSafeDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return false;
        var normalized = domain.ToLowerInvariant();
        return _safeDomains.Any(d => d.Domain.Equals(normalized, StringComparison.OrdinalIgnoreCase) && !d.IsDeleted);
    }

    internal IEnumerable<UrlAnalysisResultView> GetUrlAnalysisResultsByUserKey(Key key, int? alertExpiryDays = 30)
    {
        int daysToSubtract = alertExpiryDays ?? 30;
        var res = this._urlAnalysisResults
            .Where(da => da.UserKey.Value == key.Value && da.Timestamp >= DateTime.UtcNow.AddDays(-daysToSubtract));
        return res;
    }
    //Common.Entities.KnownPhishingWebsite.GetDomainFromUrl(urlAlert.Url)
    internal bool TryGetCachedUrlAnalysis(string url, int numberOfMonthsAgo, out UrlAnalysisResultView? cachedResult)
    {
        cachedResult = this._urlAnalysisResults
           .FirstOrDefault(da => (
           (da.Alert as UrlAlert)?.Url.ToLower() == url.ToLower() ||
           KnownPhishingWebsite.GetDomainFromUrl((da.Alert as UrlAlert)?.Url.ToLower()) == KnownPhishingWebsite.GetDomainFromUrl(url.ToLower())
            ) && 
            (da.Timestamp >= DateTime.UtcNow.AddMonths(-numberOfMonthsAgo) || numberOfMonthsAgo <= 0));
        if (cachedResult is not null)
        {
            return true;
        }
        return false;
    }
}
