using Common.Entities;
using Common.Models;
using DeviceAlertEntity = Common.Entities.DeviceAlertEntity;
using TrackUrlAlertEntity = Common.Entities.TrackUrlAlertEntity;

namespace Interface.Repositories;

public interface ISimulationRepository : IRepository<Simulation>
{
    Task<IEnumerable<Simulation>> GetByCreatorKeyAsync(Key creatorKey);
    Task<IEnumerable<Simulation>> SearchAsync(string searchText);
}

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
    Task UpdateAnalysisKeyAsync(string deviceAlertKeyField, string value);
}

public interface IAnalysisResultRepository : IRepository<AnalysisResultContainer>
{
    Task<IEnumerable<AnalysisResultContainer>> GetByUserKeyAsync(Key userKey);
    Task<AnalysisResultContainer?> GetLatestAsync(Key userKey);
}

public interface IImmediateDangerRepository : IRepository<ImmediateDanger>
{
    Task<IEnumerable<ImmediateDanger>> GetByUserKeyAsync(Key userKey);
    Task<IEnumerable<ImmediateDanger>> GetOpenByUserKeyAsync(Key userKey);
    Task<IEnumerable<ImmediateDanger>> GetLatestAsync(TimeSpan timespan);
    Task<IEnumerable<ImmediateDanger>> GetAllOpenAsync();
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

public interface ISensitiveSiteRepository
{
    Task<IEnumerable<SensitiveSite>> GetAllActiveAsync();
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

/// <summary>
/// Repository for managing blacklisted phone numbers.
/// JIRA: ASPS-282
/// </summary>
public interface IBlacklistedPhoneNumberRepository
{
    Task<BlacklistedPhoneNumber?> GetByIdAsync(int id);
    Task<IEnumerable<BlacklistedPhoneNumber>> GetAllActiveAsync();
    Task<BlacklistedPhoneNumber?> GetByPhoneNumberAsync(string phoneNumber);
    Task<bool> IsPhoneNumberBlacklistedAsync(string phoneNumber);
    Task<int> AddAsync(BlacklistedPhoneNumber phoneNumber);
    Task<int> AddRangeAsync(IEnumerable<BlacklistedPhoneNumber> phoneNumbers);
    Task UpdateAsync(BlacklistedPhoneNumber phoneNumber);
    Task DeleteAsync(int id);
    Task<int> GetCountAsync();
}

/// <summary>
/// Repository for managing bank websites.
/// JIRA: ASPS-297
/// </summary>
public interface IBankWebsiteRepository
{
    Task<BankWebsite?> GetByIdAsync(int id);
    Task<IEnumerable<BankWebsite>> GetAllActiveAsync();
    Task<BankWebsite?> GetByDomainAsync(string domain);
    Task<IEnumerable<BankWebsite>> GetByCountryAsync(string country);
    Task<bool> IsBankDomainAsync(string domain);
    Task<int> AddAsync(BankWebsite bankWebsite);
    Task<int> AddRangeAsync(IEnumerable<BankWebsite> bankWebsites);
    Task UpdateAsync(BankWebsite bankWebsite);
    Task DeleteAsync(int id);
    Task<int> GetCountAsync();
}
