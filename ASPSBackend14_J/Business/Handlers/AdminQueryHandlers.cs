using Business.Queries;
using Common.Entities;
using Interface.Repositories;
using Microsoft.Extensions.Logging;

namespace Business.Handlers;

public class AdminQueryHandlers
{
    private readonly IUserRepository _userRepository;
    private readonly IUserDeviceRepository _userDeviceRepository;
    private readonly IDeviceAlertRepository _deviceAlertRepository;
    private readonly IKnownPhishingWebsiteRepository _phishingWebsiteRepository;
    private readonly ITrackedDomainRepository _trackedDomainRepository;
    private readonly IAnalysisResultRepository _analysisResultRepository;
    private readonly ILogger<AdminQueryHandlers> _logger;

    public AdminQueryHandlers(
        IUserRepository userRepository,
        IUserDeviceRepository userDeviceRepository,
        IDeviceAlertRepository deviceAlertRepository,
        IKnownPhishingWebsiteRepository phishingWebsiteRepository,
        ITrackedDomainRepository trackedDomainRepository,
        IAnalysisResultRepository analysisResultRepository,
        ILogger<AdminQueryHandlers> logger)
    {
        _userRepository = userRepository;
        _userDeviceRepository = userDeviceRepository;
        _deviceAlertRepository = deviceAlertRepository;
        _phishingWebsiteRepository = phishingWebsiteRepository;
        _trackedDomainRepository = trackedDomainRepository;
        _analysisResultRepository = analysisResultRepository;
        _logger = logger;
    }

    public virtual async Task<GetDashboardStatsQueryResult> HandleAsync(GetDashboardStatsQuery query)
    {
        try
        {
            var users = await _userRepository.GetAllAsync();
            var devices = await _userDeviceRepository.GetAllAsync();
            var alerts = await _deviceAlertRepository.GetAllAsync();
            var phishing = await _phishingWebsiteRepository.GetAllActiveAsync();

            var recentAlerts = alerts.Where(a => a.Timestamp >= DateTime.UtcNow.AddHours(-24)).Count();

            return new GetDashboardStatsQueryResult
            {
                Success = true,
                UsersCount = users.Count(),
                DevicesCount = devices.Count(),
                AlertsCount24h = recentAlerts,
                PhishingWebsitesCount = phishing.Count()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard stats");
            return new GetDashboardStatsQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public virtual async Task<GetAllDevicesQueryResult> HandleAsync(GetAllDevicesQuery query)
    {
        try
        {
            var devices = await _userDeviceRepository.GetAllAsync();
            var users = await _userRepository.GetAllAsync();

            var devicesWithUsers = devices.Select(d => new DeviceWithUser
            {
                Device = d,
                UserName = !string.IsNullOrEmpty(d.UserKeyField)
                    ? users.FirstOrDefault(u => u.KeyField == d.UserKeyField)?.FirstName + " " +
                      users.FirstOrDefault(u => u.KeyField == d.UserKeyField)?.LastName ?? "Unknown"
                    : "Unregistered"
            }).ToList();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                devicesWithUsers = devicesWithUsers.Where(d =>
                    (d.Device.DeviceUid != null && d.Device.DeviceUid.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (d.Device.Model != null && d.Device.Model.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (d.UserName != null && d.UserName.Contains(search, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            return new GetAllDevicesQueryResult
            {
                Success = true,
                Devices = devicesWithUsers
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all devices");
            return new GetAllDevicesQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public virtual async Task<GetAllPhishingWebsitesQueryResult> HandleAsync(GetAllPhishingWebsitesQuery query)
    {
        try
        {
            var websites = await _phishingWebsiteRepository.GetAllActiveAsync();

            return new GetAllPhishingWebsitesQueryResult
            {
                Success = true,
                PhishingWebsites = websites.ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting phishing websites");
            return new GetAllPhishingWebsitesQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public virtual async Task<GetUsersWithDeviceCountsQueryResult> HandleAsync(GetUsersWithDeviceCountsQuery query)
    {
        try
        {
            var users = await _userRepository.GetAllAsync();
            var devices = await _userDeviceRepository.GetAllAsync();

            var usersWithCounts = users.Select(u => new UserWithDeviceCount
            {
                User = u,
                DeviceCount = devices.Count(d => d.UserKeyField == u.KeyField)
            }).ToList();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                usersWithCounts = usersWithCounts.Where(u =>
                    (u.User.FirstName != null && u.User.FirstName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (u.User.LastName != null && u.User.LastName.Contains(search, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            return new GetUsersWithDeviceCountsQueryResult
            {
                Success = true,
                Users = usersWithCounts
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users with device counts");
            return new GetUsersWithDeviceCountsQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public virtual async Task<GetRecentAlertsQueryResult> HandleAsync(GetRecentAlertsQuery query)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddHours(-query.Hours);
            var allAlerts = await _deviceAlertRepository.GetAllAsync();

            var recentAlerts = allAlerts
                .Where(a => a.Timestamp >= cutoff)
                .OrderByDescending(a => a.Timestamp)
                .ToList();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                recentAlerts = recentAlerts.Where(a =>
                    (a.AlertType != null && a.AlertType.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (a.DeviceUid != null && a.DeviceUid.Contains(search, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            return new GetRecentAlertsQueryResult
            {
                Success = true,
                Alerts = recentAlerts
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent alerts");
            return new GetRecentAlertsQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public virtual async Task<GetDevicesByUserQueryResult> HandleAsync(GetDevicesByUserQuery query)
    {
        try
        {
            var devices = await _userDeviceRepository.GetByUserKeyAsync(query.UserKey);

            return new GetDevicesByUserQueryResult
            {
                Success = true,
                Devices = devices.ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting devices by user");
            return new GetDevicesByUserQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public virtual async Task<GetDeviceByKeyQueryResult> HandleAsync(GetDeviceByKeyQuery query)
    {
        try
        {
            var device = await _userDeviceRepository.GetByKeyAsync(query.DeviceKey);
            
            if (device == null)
            {
                return new GetDeviceByKeyQueryResult
                {
                    Success = false,
                    Message = "Device not found"
                };
            }

            return new GetDeviceByKeyQueryResult
            {
                Success = true,
                Device = device
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting device by key");
            return new GetDeviceByKeyQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public virtual async Task<GetDeviceByUidQueryResult> HandleAsync(GetDeviceByUidQuery query)
    {
        try
        {
            var device = await _userDeviceRepository.GetByDeviceUidAsync(query.DeviceUid);

            if (device == null)
            {
                return new GetDeviceByUidQueryResult
                {
                    Success = false,
                    Message = "Device not found"
                };
            }

            return new GetDeviceByUidQueryResult
            {
                Success = true,
                Device = device
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting device by UID");
            return new GetDeviceByUidQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public virtual async Task<GetAlertsByDeviceQueryResult> HandleAsync(GetAlertsByDeviceQuery query)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddHours(-query.Hours);
            var allAlerts = await _deviceAlertRepository.GetAllAsync();
            
            var deviceAlerts = allAlerts
                .Where(a => a.DeviceUid == query.DeviceUid && a.Timestamp >= cutoff)
                .OrderByDescending(a => a.Timestamp)
                .ToList();

            return new GetAlertsByDeviceQueryResult
            {
                Success = true,
                Alerts = deviceAlerts
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting alerts by device");
            return new GetAlertsByDeviceQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public virtual async Task<GetAlertByKeyQueryResult> HandleAsync(GetAlertByKeyQuery query)
    {
        try
        {
            var alert = await _deviceAlertRepository.GetByKeyAsync(query.AlertKey);
            
            if (alert == null)
            {
                return new GetAlertByKeyQueryResult
                {
                    Success = false,
                    Message = "Alert not found"
                };
            }

            return new GetAlertByKeyQueryResult
            {
                Success = true,
                Alert = alert
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting alert by key");
            return new GetAlertByKeyQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public virtual async Task<GetAllAnalysisResultsQueryResult> HandleAsync(GetAllAnalysisResultsQuery query)
    {
        try
        {
            _logger.LogInformation("GetAllAnalysisResultsQuery: Starting, Hours={Hours}", query.Hours);

            var cutoff = DateTime.UtcNow.AddHours(-query.Hours);
            _logger.LogInformation("GetAllAnalysisResultsQuery: Cutoff time={Cutoff}", cutoff);

            var allResults = await _analysisResultRepository.GetAllAsync();
            _logger.LogInformation("GetAllAnalysisResultsQuery: Retrieved {Count} total analysis results from repository", allResults.Count());

            var recentResults = allResults
                .Where(r => r.Timestamp >= cutoff)
                .OrderByDescending(r => r.Timestamp)
                .ToList();

            _logger.LogInformation("GetAllAnalysisResultsQuery: Filtered to {Count} results after {Hours} hours cutoff", recentResults.Count, query.Hours);

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                recentResults = recentResults.Where(r =>
                    (r.Discriminator != null && r.Discriminator.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (r.UserKeyField != null && r.UserKeyField.Contains(search, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            return new GetAllAnalysisResultsQueryResult
            {
                Success = true,
                AnalysisResults = recentResults
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting analysis results");
            return new GetAllAnalysisResultsQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public virtual async Task<GetAnalysisResultByAlertKeyQueryResult> HandleAsync(GetAnalysisResultByAlertKeyQuery query)
    {
        try
        {
            var allResults = await _analysisResultRepository.GetAllAsync();
            var match = allResults.FirstOrDefault(r => r.DeviceAlertKeyField == query.DeviceAlertKeyField);

            return new GetAnalysisResultByAlertKeyQueryResult
            {
                Success = true,
                AnalysisResult = match
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting analysis result by alert key");
            return new GetAnalysisResultByAlertKeyQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public virtual async Task<GetAllTrackedDomainsQueryResult> HandleAsync(GetAllTrackedDomainsQuery query)
    {
        try
        {
            var allDomains = await _trackedDomainRepository.GetAllActiveAsync();
            var domains = allDomains.ToList();

            // Apply category filter
            if (!string.IsNullOrWhiteSpace(query.Category))
            {
                domains = domains.Where(d => d.Category.Equals(query.Category, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim().ToLowerInvariant();
                domains = domains.Where(d =>
                    d.Domain.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    d.Category.Contains(search, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            var totalCount = domains.Count;

            // Pagination
            var pagedDomains = domains
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();

            return new GetAllTrackedDomainsQueryResult
            {
                Success = true,
                TrackedDomains = pagedDomains,
                TotalCount = totalCount,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tracked domains");
            return new GetAllTrackedDomainsQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    // =========================================================================
    // ASPS-646 — Angular Admin: Dashboard + Users API
    // =========================================================================

    /// <summary>
    /// Returns KPI counts for the Angular admin dashboard.
    /// Total users, devices, active alerts (last 24 h), and analysis results.
    /// </summary>
    public virtual async Task<GetDashboardSummaryQueryResult> HandleAsync(GetDashboardSummaryQuery query)
    {
        try
        {
            var users = await _userRepository.GetAllAsync();
            var devices = await _userDeviceRepository.GetAllAsync();
            var allAlerts = await _deviceAlertRepository.GetAllAsync();
            var allAnalysis = await _analysisResultRepository.GetAllAsync();

            var activeAlerts24h = allAlerts.Count(a => a.Timestamp >= DateTime.UtcNow.AddHours(-24));

            return new GetDashboardSummaryQueryResult
            {
                Success = true,
                TotalUsers = users.Count(),
                TotalDevices = devices.Count(),
                ActiveAlerts24h = activeAlerts24h,
                AnalysisResultsCount = allAnalysis.Count()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard summary");
            return new GetDashboardSummaryQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Server-side paged list of users with device counts, search, and sort.
    /// </summary>
    public virtual async Task<GetAllUsersPagedQueryResult> HandleAsync(GetAllUsersPagedQuery query)
    {
        try
        {
            var users = await _userRepository.GetAllAsync();
            var devices = await _userDeviceRepository.GetAllAsync();

            var usersWithCounts = users.Select(u => new UserWithDeviceCount
            {
                User = u,
                DeviceCount = devices.Count(d => d.UserKeyField == u.KeyField)
            }).AsEnumerable();

            // Apply search filter (name or email)
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                usersWithCounts = usersWithCounts.Where(u =>
                    (u.User.FirstName != null && u.User.FirstName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (u.User.LastName != null && u.User.LastName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (u.User.Email != null && u.User.Email.Contains(search, StringComparison.OrdinalIgnoreCase)));
            }

            // Apply sort
            usersWithCounts = (query.SortBy?.ToLowerInvariant(), query.SortDirection) switch
            {
                ("firstname", "desc") => usersWithCounts.OrderByDescending(u => u.User.FirstName),
                ("firstname", _)      => usersWithCounts.OrderBy(u => u.User.FirstName),
                ("lastname", "desc")  => usersWithCounts.OrderByDescending(u => u.User.LastName),
                ("lastname", _)       => usersWithCounts.OrderBy(u => u.User.LastName),
                ("email", "desc")     => usersWithCounts.OrderByDescending(u => u.User.Email),
                ("email", _)          => usersWithCounts.OrderBy(u => u.User.Email),
                ("devicecount", "desc") => usersWithCounts.OrderByDescending(u => u.DeviceCount),
                ("devicecount", _)    => usersWithCounts.OrderBy(u => u.DeviceCount),
                ("datecreated", "desc") => usersWithCounts.OrderByDescending(u => u.User.DateCreated),
                ("datecreated", _)    => usersWithCounts.OrderBy(u => u.User.DateCreated),
                _                     => usersWithCounts.OrderBy(u => u.User.LastName)
            };

            var list = usersWithCounts.ToList();
            var totalCount = list.Count;
            var pagedItems = list
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();

            return new GetAllUsersPagedQueryResult
            {
                Success = true,
                Result = new Common.Models.PagedResult<UserWithDeviceCount>
                {
                    Items = pagedItems,
                    TotalCount = totalCount,
                    Page = query.Page,
                    PageSize = query.PageSize
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paged users");
            return new GetAllUsersPagedQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Returns paged alerts for all devices belonging to a specific user.
    /// </summary>
    public virtual async Task<GetUserAlertsByKeyQueryResult> HandleAsync(GetUserAlertsByKeyQuery query)
    {
        try
        {
            // Get all devices for this user
            var devices = await _userDeviceRepository.GetAllAsync();
            var userDeviceUids = devices
                .Where(d => d.UserKeyField == query.UserKey.Value)
                .Select(d => d.DeviceUid)
                .ToHashSet();

            var allAlerts = await _deviceAlertRepository.GetAllAsync();

            var userAlerts = allAlerts
                .Where(a => a.DeviceUid != null && userDeviceUids.Contains(a.DeviceUid))
                .AsEnumerable();

            // Apply search
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                userAlerts = userAlerts.Where(a =>
                    (a.AlertType != null && a.AlertType.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (a.DeviceUid != null && a.DeviceUid.Contains(search, StringComparison.OrdinalIgnoreCase)));
            }

            // Sort by timestamp descending by default
            userAlerts = (query.SortBy?.ToLowerInvariant(), query.SortDirection) switch
            {
                ("alerttype", "asc")  => userAlerts.OrderBy(a => a.AlertType),
                ("alerttype", _)      => userAlerts.OrderByDescending(a => a.AlertType),
                ("deviceuid", "asc")  => userAlerts.OrderBy(a => a.DeviceUid),
                ("deviceuid", _)      => userAlerts.OrderByDescending(a => a.DeviceUid),
                _                     => userAlerts.OrderByDescending(a => a.Timestamp)
            };

            var list = userAlerts.ToList();
            var totalCount = list.Count;
            var pagedItems = list
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();

            return new GetUserAlertsByKeyQueryResult
            {
                Success = true,
                Result = new Common.Models.PagedResult<DeviceAlertEntity>
                {
                    Items = pagedItems,
                    TotalCount = totalCount,
                    Page = query.Page,
                    PageSize = query.PageSize
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user alerts");
            return new GetUserAlertsByKeyQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}
