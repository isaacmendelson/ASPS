using Common.Entities;
using Common.Models;
using DeviceAlertEntity = Common.Entities.DeviceAlertEntity;
using TrackUrlAlertEntity = Common.Entities.TrackUrlAlertEntity;

namespace Interface.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByKeycloakIdAsync(string keycloakUserId);
    Task<User?> GetUserWithDetailsAsync(Key key);
    Task<IEnumerable<User>> GetActiveUsersAsync();
}

public interface IUserDeviceRepository : IRepository<UserDevice>
{
    Task<IEnumerable<UserDevice>> GetByUserKeyAsync(Key userKey);
    Task<UserDevice?> GetByDeviceUidAsync(string deviceUid);
    Task<IEnumerable<UserDevice>> GetMonitoredDevicesAsync();
}

public interface IUserAccountRepository : IRepository<UserAccount>
{
    Task<IEnumerable<UserAccount>> GetByUserKeyAsync(Key userKey);
    Task<UserAccount?> GetByUserNameAsync(string userName);
}

public interface IDeviceAlertRepository : IRepository<DeviceAlertEntity>
{
    Task<IEnumerable<DeviceAlertEntity>> GetAlertsByDeviceUidAsync(string deviceUid);
    Task<IEnumerable<DeviceAlertEntity>> GetAlertsByUserKeyAsync(Key? userKey);
    Task<IEnumerable<DeviceAlertEntity>> GetRecentAlertsAsync(TimeSpan timeSpan);
}

public interface IAnalysisResultRepository : IRepository<AnalysisResultContainer>
{
    Task<IEnumerable<AnalysisResultContainer>> GetByUserKeyAsync(Key userKey);
    Task<AnalysisResultContainer?> GetLatestAsync(Key userKey);
}

public interface IAlertFlagRepository
{
    Task<AlertFlag> AddAsync(AlertFlag flag);
    Task UpdateAsync(AlertFlag flag);
    Task<IEnumerable<AlertFlag>> GetOpenFlagsByUserAsync(int userKey);
    Task CloseFlag(int flagKey);
}

public interface ISafeDomainRepository
{
    Task<IEnumerable<SafeDomain>> GetAllActiveAsync();
    Task<bool> IsSafeDomainAsync(string domain);
}

public interface IKnownPhishingWebsiteRepository
{
    Task<KnownPhishingWebsite?> GetByIdAsync(int id);
    Task<IEnumerable<KnownPhishingWebsite>> GetAllActiveAsync();
    Task<KnownPhishingWebsite?> GetByUrlAsync(string url);
    Task<IEnumerable<KnownPhishingWebsite>> GetByDomainAsync(string domain);
    Task<bool> IsPhishingUrlAsync(string url);
    Task<bool> IsPhishingDomainAsync(string domain);
    Task<int> AddAsync(KnownPhishingWebsite website);
    Task<int> AddRangeAsync(IEnumerable<KnownPhishingWebsite> websites);
    Task UpdateAsync(KnownPhishingWebsite website);
    Task DeleteAsync(int id);
    Task<int> GetCountAsync();
}

public interface ITrackUrlAlertRepository : IRepository<TrackUrlAlertEntity>
{
    Task<IEnumerable<TrackUrlAlertEntity>> GetAlertsByUrlAsync(string url);
    Task<IEnumerable<TrackUrlAlertEntity>> GetAlertsByUserKeyAsync(Key? userKey);
    Task<IEnumerable<TrackUrlAlertEntity>> GetRecentAlertsAsync(TimeSpan timeSpan);
}

public interface ITrackedDomainRepository
{
    Task<TrackedDomain?> GetByIdAsync(int id);
    Task<IEnumerable<TrackedDomain>> GetAllActiveAsync();
    Task<TrackedDomain?> GetByDomainAsync(string domain);
    Task<IEnumerable<TrackedDomain>> GetByCategoryAsync(string category);
    Task<bool> IsTrackedDomainAsync(string domain);
    Task<int> AddAsync(TrackedDomain trackedDomain);
    Task<int> AddRangeAsync(IEnumerable<TrackedDomain> trackedDomains);
    Task UpdateAsync(TrackedDomain trackedDomain);
    Task DeleteAsync(int id);
    Task<int> GetCountAsync();
}
