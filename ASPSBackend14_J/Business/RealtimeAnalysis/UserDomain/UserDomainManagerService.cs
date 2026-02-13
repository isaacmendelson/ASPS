using Business.Data.EF;
using Business.Views;
using Common.Entities;
using Common.Interfaces;
using Common.Models;
using Interface.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

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
    private readonly AppDbContext _dbContext;
    private readonly List<IDomainEventHandler> _eventHandlers;
    private readonly IKnownPhishingWebsiteRepository _phishingRepo;
    private readonly ASView _aSView;

    public UserDomainManagerService(
        ILoggerFactory loggerFactory, 
        IConfiguration configuration,
        AppDbContext dbContext,
        ASView aSView,
        IEnumerable<IDomainEventHandler> eventHandlers,
        IKnownPhishingWebsiteRepository phishingRepo)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<UserDomainManagerService>();
        _configuration = configuration;
        _aSView = aSView;
        _dbContext = dbContext;
        _eventHandlers = eventHandlers.ToList();
        _phishingRepo = phishingRepo;
        
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
        var user = _dbContext.Users
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
            manager = new UDAnalysisManager(udUser, _loggerFactory, _aSView, _configuration, _eventHandlers, _phishingRepo);
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
        var device = await _dbContext.UserDevices
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
        var udUser = new UDUser(new Key("User", user.KeyField))
        {
            KeycloakUserId = user.KeycloakUserId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Address = user.Address,
            City = user.City,
            State = user.State,
            Zip = user.Zip,
            Country = user.Country,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role,
            GuardianKey = user.GuardianKey,
            Locale = user.Locale,
            Timezone = user.Timezone,
            DateCreated = user.DateCreated,
            DateModified = user.DateModified,
            DateDeleted = user.DateDeleted,
            IsDisabled = user.IsDisabled
        };

        return udUser;
    }
}
