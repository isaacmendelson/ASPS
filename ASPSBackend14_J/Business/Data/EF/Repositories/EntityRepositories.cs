using Common.Entities;
using Common.Models;
using Interface.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DeviceAlertEntity = Common.Entities.DeviceAlertEntity;
using TrackUrlAlertEntity = Common.Entities.TrackUrlAlertEntity;

namespace Business.Data.EF.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    public async Task<User?> GetByKeycloakIdAsync(string keycloakUserId)
    {
        // First try exact KeycloakUserId match
        var user = await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.KeycloakUserId == keycloakUserId && !u.IsDeleted);
        
        // If not found, try matching by FirstName (for dev login compatibility)
        if (user == null)
        {
            user = await _dbSet
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.FirstName == keycloakUserId && !u.IsDeleted);
        }
        
        return user;
    }

    public async Task<User?> GetUserWithDetailsAsync(Key key)
    {
        var keyField = Entity.GetDbKey(key);
        
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.KeyField == keyField && !u.IsDeleted)
            .FirstOrDefaultAsync();
        
        // No navigation properties - caller should fetch devices/accounts separately if needed
        return user;
    }

    public async Task<IEnumerable<User>> GetActiveUsersAsync()
    {
        try
        {
            // Get all users first, then filter in memory
            var allUsers = await _dbSet.ToListAsync();
            
            Console.WriteLine($"DEBUG: Total users in DB: {allUsers.Count}");
            
            foreach (var user in allUsers)
            {
                Console.WriteLine($"  User: {user.FirstName} {user.LastName}, Key: {user.Key.Type}|{user.Key.Value}|{user.Key.InstanceName}, IsDeleted: {user.IsDeleted}, IsDisabled: {user.IsDisabled}");
            }
            
            var activeUsers = allUsers.Where(u => !u.IsDeleted && !u.IsDisabled).ToList();
            Console.WriteLine($"DEBUG: Active users after filter: {activeUsers.Count}");
            
            return activeUsers;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in GetActiveUsersAsync: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
            return new List<User>(); // Return empty list
        }
    }
}

public class UserDeviceRepository : Repository<UserDevice>, IUserDeviceRepository
{
    public UserDeviceRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<UserDevice>> GetByUserKeyAsync(Key userKey)
    {
        var userKeyField = Entity.GetDbKey(userKey);
        return await _dbSet.Where(d => d.UserKeyField == userKeyField && !d.IsDeleted).ToListAsync();
    }

    public async Task<UserDevice?> GetByDeviceUidAsync(string deviceUid)
    {
        return await _dbSet.FirstOrDefaultAsync(d => d.DeviceUid == deviceUid && !d.IsDeleted);
    }

    public async Task<IEnumerable<UserDevice>> GetMonitoredDevicesAsync()
    {
        return await _dbSet
            .Where(d => d.MonitoringStatus == Common.Enums.DeviceMonitoringStatus.Enabled && !d.IsDeleted)
            .ToListAsync();
    }
}

public class UserAccountRepository : Repository<UserAccount>, IUserAccountRepository
{
    public UserAccountRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<UserAccount>> GetByUserKeyAsync(Key userKey)
    {
        var userKeyField = Entity.GetDbKey(userKey);
        return await _context.UserAccounts
            .Where(a => a.UserKeyField == userKeyField && !a.IsDeleted)
            .ToListAsync();
    }

    public async Task<UserAccount?> GetByUserNameAsync(string userName)
    {
        return await _context.UserAccounts
            .FirstOrDefaultAsync(a => a.UserName == userName && !a.IsDeleted);
    }
}

public class AnalysisResultRepository : Repository<AnalysisResultContainer>, IAnalysisResultRepository
{
    private readonly ILogger<AnalysisResultRepository> _logger;

    public AnalysisResultRepository(AppDbContext context, ILogger<AnalysisResultRepository> logger) : base(context) 
    {
        _logger = logger;
    }

    public override async Task<IEnumerable<AnalysisResultContainer>> GetAllAsync()
    {
        try
        {
            var knownDiscriminators = new[] { "AnalysisResultContainer", "UrlAnalysisResult", "TrackUrlAnalysisResult", "RemoteAccessAnalysisResult" };
            var results = await _dbSet
                .AsNoTracking()
                .Where(a => !a.IsDeleted && knownDiscriminators.Contains(a.Discriminator))
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} analysis results from database", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving analysis results - returning empty list");
            // Return empty list instead of throwing - gracefully handle errors
            return new List<AnalysisResultContainer>();
        }
    }

    public async Task<IEnumerable<AnalysisResultContainer>> GetByUserKeyAsync(Key userKey)
    {
        try
        {
            var userKeyField = Entity.GetDbKey(userKey);
            var results = await _dbSet
                .AsNoTracking()
                .Where(a => a.UserKeyField == userKeyField && !a.IsDeleted)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving analysis results for user {UserKey}", userKey);
            return new List<AnalysisResultContainer>();
        }
    }

    public async Task<AnalysisResultContainer?> GetLatestAsync(Key userKey)
    {
        try
        {
            var userKeyField = Entity.GetDbKey(userKey);
            return await _dbSet
                .AsNoTracking()
                .Where(a => a.UserKeyField == userKeyField && !a.IsDeleted)
                .OrderByDescending(a => a.Timestamp)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving latest analysis result for user {UserKey}", userKey);
            return null;
        }
    }
}

public class DeviceAlertRepository : Repository<DeviceAlertEntity>, IDeviceAlertRepository
{
    public DeviceAlertRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<DeviceAlertEntity>> GetAlertsByDeviceUidAsync(string deviceUid)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(a => a.DeviceUid == deviceUid && !a.IsDeleted)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();
    }

    public async Task<IEnumerable<DeviceAlertEntity>> GetAlertsByUserKeyAsync(Key? userKey)
    {
        if (userKey == null)
            return Enumerable.Empty<DeviceAlertEntity>();

        var userKeyField = Entity.GetDbKey(userKey);
        
        return await _dbSet
            .AsNoTracking()
            .Where(a => a.UserKeyField == userKeyField && !a.IsDeleted)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();
    }

    public async Task<IEnumerable<DeviceAlertEntity>> GetRecentAlertsAsync(TimeSpan timeSpan)
    {
        var cutoff = DateTime.UtcNow - timeSpan;
        return await _dbSet
            .AsNoTracking()
            .Where(a => a.Timestamp >= cutoff && !a.IsDeleted)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();
    }

    public async Task UpdateAnalysisKeyAsync(string deviceAlertKeyField, string value)
    {
        var alert = await _dbSet.FindAsync(deviceAlertKeyField);
        if (alert != null)
        {
            alert.AnalysisKeyField = value;
            await _context.SaveChangesAsync();
        }
    }
}

public class TrackUrlAlertRepository : Repository<TrackUrlAlertEntity>, ITrackUrlAlertRepository
{
    public TrackUrlAlertRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<TrackUrlAlertEntity>> GetAlertsByUrlAsync(string url)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(a => a.Url == url && !a.IsDeleted)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrackUrlAlertEntity>> GetAlertsByUserKeyAsync(Key? userKey)
    {
        if (userKey == null)
            return Enumerable.Empty<TrackUrlAlertEntity>();

        var userKeyField = Entity.GetDbKey(userKey);
        
        return await _dbSet
            .AsNoTracking()
            .Where(a => a.UserKeyField == userKeyField && !a.IsDeleted)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrackUrlAlertEntity>> GetRecentAlertsAsync(TimeSpan timeSpan)
    {
        var cutoff = DateTime.UtcNow - timeSpan;
        return await _dbSet
            .AsNoTracking()
            .Where(a => a.Timestamp >= cutoff && !a.IsDeleted)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();
    }
}

public class AlertFlagRepository : IAlertFlagRepository
{
    private readonly AppDbContext _context;

    public AlertFlagRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AlertFlag> AddAsync(AlertFlag flag)
    {
        flag.Created = DateTime.UtcNow;
        flag.Modified = DateTime.UtcNow;
        await _context.AlertFlags.AddAsync(flag);
        await _context.SaveChangesAsync();
        return flag;
    }

    public async Task UpdateAsync(AlertFlag flag)
    {
        flag.Modified = DateTime.UtcNow;
        _context.AlertFlags.Update(flag);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<AlertFlag>> GetOpenFlagsByUserAsync(int userKey)
    {
        return await _context.AlertFlags
            .Where(f => f.UserKey == userKey && f.Status == Common.Enums.AlertFlagStatus.Open && !f.IsDeleted)
            .ToListAsync();
    }

    public async Task CloseFlag(int flagKey)
    {
        var flag = await _context.AlertFlags.FindAsync(flagKey);
        if (flag != null)
        {
            flag.Status = Common.Enums.AlertFlagStatus.Closed;
            flag.Modified = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
