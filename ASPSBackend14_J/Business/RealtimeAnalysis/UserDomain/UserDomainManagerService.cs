using Business.Data.EF;
using Business.DomainEvents;
using Business.Messaging;
using Business.Views;
using Common.Entities;
using Common.Interfaces;
using Common.Models;
using Common.Models.Alerts;
using Interface.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Business.RealtimeAnalysis.ProtectivActions;
namespace Business.RealtimeAnalysis.UserDomain;

/// <summary>
/// Manages UDAnalysisManager instances - one per user.
/// Responsible for creating, maintaining, and routing alerts to user-specific analysis managers.
/// </summary>
public class UserDomainManagerService
{
    private readonly ConcurrentDictionary<string, UDAnalysisManager> _userManagers = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<UserDomainManagerService> _logger;
    private readonly IConfiguration _configuration;
    private readonly List<IDomainEventHandler> _eventHandlers;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IWebsiteCategoryRepository _websiteCategoryRepo;
    private readonly ASView _aSView;
    private readonly OutboxNotificationPublisher? _notificationPublisher;

    public UserDomainManagerService(
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        ASView aSView,
        IEnumerable<IDomainEventHandler> eventHandlers,
        IServiceScopeFactory serviceScopeFactory,
        OutboxNotificationPublisher? notificationPublisher = null)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<UserDomainManagerService>();
        _configuration = configuration;
        _aSView = aSView;
        _eventHandlers = eventHandlers.ToList();
        _serviceScopeFactory = serviceScopeFactory;
        _notificationPublisher = notificationPublisher;
        _logger.LogInformation($"UserDomainManagerService initialized with {_eventHandlers.Count} event handlers");
    }

    /// <summary>
    /// Get or create a UDAnalysisManager for a specific user
    /// </summary>
    public UDAnalysisManager GetOrCreateManagerForUser(Key userKey)
    {
        var userKeyStr = userKey.Value;
        
        if (_userManagers.TryGetValue(userKeyStr, out var existingManager))
        {
            return existingManager;
        }

        // Load user from database
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = dbContext.Users
            .AsNoTracking()
            .FirstOrDefault(u => u.KeyField == userKeyStr);

        if (user == null)
        {
            _logger.LogError($"User not found: {userKeyStr}");
            throw new InvalidOperationException($"User not found: {userKeyStr}");
        }

        // Create UDUser from User entity
        var udUser = CreateUDUserFromEntity(user);

        // Create manager for this user
        var managerLogger = _loggerFactory.CreateLogger<UDAnalysisManager>();
        
        UDAnalysisManager manager;
        if (_userManagers.ContainsKey(userKeyStr))
        {
            // Another thread created the manager in the meantime
            return _userManagers[userKeyStr];
        }
        else
        {
            var alertExpiryDays = _configuration.GetValue<int>("Analysis:DeviceAlertExpiryDays", 30);
            var alertDeletionDays = _configuration.GetValue<int>("Analysis:DeviceAlertDeletionDays", 30);
            //var userAnalyzer = new UDUserAnalyzer(udUser, _aSView, alertExpiryDays, alertDeletionDays, _loggerFactory);
            manager = new UDAnalysisManager(udUser, new UDUserAnalyzer(udUser, _aSView, alertExpiryDays, alertDeletionDays, _loggerFactory, new ProtectiveActionsFactory(), _configuration, _eventHandlers), _loggerFactory, _aSView, _configuration, _eventHandlers, _serviceScopeFactory, _notificationPublisher);
        }

            // Add to dictionary
        _userManagers[userKeyStr] = manager;
        
        // Start the manager
        manager.Start();
        
        _logger.LogInformation($"Created and started UDAnalysisManager for user: {userKeyStr}");
        
        return manager;
    }

    /// <summary>
    /// Get manager for a user by device UID
    /// </summary>
    public async Task<UDAnalysisManager?> GetManagerForDeviceAsync(string deviceUid)
    {
        // Find the device and get the user
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await dbContext.UserDevices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DeviceUid == deviceUid);

        if (device == null)
        {
            _logger.LogWarning($"Device not found: {deviceUid}");
            return null;
        }

        var userKey = new Key("User", device.UserKeyField);
        return GetOrCreateManagerForUser(userKey);
    }

    /// <summary>
    /// Stop and remove a user's manager
    /// </summary>
    public void RemoveManagerForUser(Key userKey)
    {
        var userKeyStr = userKey.Value;
        
        if (_userManagers.TryRemove(userKeyStr, out var manager))
        {
            manager.Stop();
            _logger.LogInformation($"Removed UDAnalysisManager for user: {userKeyStr}");
        }
    }

    /// <summary>
    /// Get count of active managers
    /// </summary>
    public int GetActiveManagerCount()
    {
        return _userManagers.Count;
    }

    /// <summary>
    /// Stop all managers (for shutdown)
    /// </summary>
    public void StopAll()
    {
        foreach (var manager in _userManagers.Values)
        {
            manager.Stop();
        }
        _userManagers.Clear();
        _logger.LogInformation("Stopped all UDAnalysisManagers");
    }

    /// <summary>
    /// Create UDUser from User entity
    /// </summary>
    private UDUser CreateUDUserFromEntity(Common.Entities.User user)
    {
        Key key = user.Key ?? new Key("User", user.KeyField);

        var devices = this._aSView.GetUserDevices(key);
        var deviceAlerts = this._aSView.GetActiveDeviceAlertsByUserKey(key);

        var udUser = new UDUser(
            key,

            new UserInfo(
            key,
            user.KeycloakUserId,
            user.FirstName,
            user.LastName,
            user.Address,
            user.City,
            user.State,
            user.Zip,
            user.Country,
            user.PhoneNumber,
            user.Role,
            user.IsDisabled,
            user.DateCreated,
            user.GuardianKey,
            user.Locale,
            user.Timezone
            ),

            new RiskAssessment(0, "Unknown", false, 0),
            devices, 
            deviceAlerts,
            null,
            false
        );
        
        return udUser;
    }
}
