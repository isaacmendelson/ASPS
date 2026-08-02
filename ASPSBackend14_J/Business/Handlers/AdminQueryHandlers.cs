using Business.Queries;
using Common.Entities;
using Common.Enums;
using Common.Models;
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

    // =========================================================================
    // ASPS-647 — Paged Devices, Alerts, Analysis Results (Angular Admin API)
    // =========================================================================

    public virtual async Task<GetAllDevicesPagedQueryResult> HandleAsync(GetAllDevicesPagedQuery query)
    {
        try
        {
            var devices = (await _userDeviceRepository.GetAllAsync()).ToList();
            var users = (await _userRepository.GetAllAsync()).ToList();

            // Build a lookup for user display names
            var userNames = users.ToDictionary(u => u.KeyField, u => $"{u.FirstName} {u.LastName}".Trim());

            // Map to DTOs
            IEnumerable<DeviceDto> items = devices.Select(d => new DeviceDto
            {
                Key = d.Key,
                DeviceUid = d.DeviceUid,
                DeviceType = d.DeviceType,
                OperatingSystem = d.OperatingSystem,
                MonitoringStatus = d.MonitoringStatus,
                Make = d.Make,
                Model = d.Model,
                MAC = d.MAC,
                DateCreated = d.DateCreated,
                UserKey = d.UserKey,
                UserName = !string.IsNullOrEmpty(d.UserKeyField) && userNames.TryGetValue(d.UserKeyField, out var name) && !string.IsNullOrWhiteSpace(name)
                    ? name
                    : "Unregistered"
            });

            // Status filter
            if (!string.IsNullOrWhiteSpace(query.Status) &&
                Enum.TryParse<DeviceMonitoringStatus>(query.Status, ignoreCase: true, out var statusFilter))
            {
                items = items.Where(d => d.MonitoringStatus == statusFilter);
            }

            // Search filter (DeviceUid or UserName)
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                items = items.Where(d =>
                    d.DeviceUid.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    d.UserName.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            // Sort
            items = ApplyDeviceSort(items, query.SortBy, query.SortDirection);

            var list = items.ToList();
            var totalCount = list.Count;
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            var paged = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new GetAllDevicesPagedQueryResult
            {
                Success = true,
                Result = new PagedResult<DeviceDto>
                {
                    Items = paged,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paged devices");
            return new GetAllDevicesPagedQueryResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    public virtual async Task<GetDeviceAlertsPagedQueryResult> HandleAsync(GetDeviceAlertsPagedQuery query)
    {
        try
        {
            var deviceKeyField = query.DeviceKeyField;
            var allAlerts = (await _deviceAlertRepository.GetAllAsync()).ToList();
            var users = (await _userRepository.GetAllAsync()).ToList();
            var userNames = users.ToDictionary(u => u.KeyField, u => $"{u.FirstName} {u.LastName}".Trim());

            IEnumerable<AlertDto> items = allAlerts
                .Where(a => a.DeviceKeyField == deviceKeyField)
                .Select(a => MapAlertToDto(a, userNames));

            // Search
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                items = items.Where(a =>
                    a.AlertType.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    a.DeviceUid.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            // Sort
            items = ApplyAlertSort(items, query.SortBy, query.SortDirection);

            var list = items.ToList();
            var totalCount = list.Count;
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            return new GetDeviceAlertsPagedQueryResult
            {
                Success = true,
                Result = new PagedResult<AlertDto>
                {
                    Items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paged device alerts");
            return new GetDeviceAlertsPagedQueryResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    public virtual async Task<GetAllAlertsPagedQueryResult> HandleAsync(GetAllAlertsPagedQuery query)
    {
        try
        {
            var allAlerts = (await _deviceAlertRepository.GetAllAsync()).ToList();
            var users = (await _userRepository.GetAllAsync()).ToList();
            var userNames = users.ToDictionary(u => u.KeyField, u => $"{u.FirstName} {u.LastName}".Trim());

            IEnumerable<AlertDto> items = allAlerts.Select(a => MapAlertToDto(a, userNames));

            // Time-range filter
            if (query.From.HasValue)
                items = items.Where(a => a.Timestamp >= query.From.Value);
            if (query.To.HasValue)
                items = items.Where(a => a.Timestamp <= query.To.Value);

            // Severity/priority filter
            if (!string.IsNullOrWhiteSpace(query.Severity) &&
                Enum.TryParse<Priority>(query.Severity, ignoreCase: true, out var priorityFilter))
            {
                items = items.Where(a => a.Priority == priorityFilter);
            }

            // Search
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                items = items.Where(a =>
                    a.AlertType.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    a.DeviceUid.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (a.UserName != null && a.UserName.Contains(search, StringComparison.OrdinalIgnoreCase)));
            }

            // Sort
            items = ApplyAlertSort(items, query.SortBy, query.SortDirection);

            var list = items.ToList();
            var totalCount = list.Count;
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            return new GetAllAlertsPagedQueryResult
            {
                Success = true,
                Result = new PagedResult<AlertDto>
                {
                    Items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paged alerts");
            return new GetAllAlertsPagedQueryResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    public virtual async Task<GetAlertDetailQueryResult> HandleAsync(GetAlertDetailQuery query)
    {
        try
        {
            var alert = await _deviceAlertRepository.GetByKeyAsync(query.AlertKey);
            if (alert == null)
                return new GetAlertDetailQueryResult { Success = false, Message = "Alert not found" };

            var users = (await _userRepository.GetAllAsync()).ToList();
            var userNames = users.ToDictionary(u => u.KeyField, u => $"{u.FirstName} {u.LastName}".Trim());

            return new GetAlertDetailQueryResult
            {
                Success = true,
                Alert = MapAlertToDto(alert, userNames)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting alert detail");
            return new GetAlertDetailQueryResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    public virtual async Task<GetAllAnalysisResultsPagedQueryResult> HandleAsync(GetAllAnalysisResultsPagedQuery query)
    {
        try
        {
            var allResults = (await _analysisResultRepository.GetAllAsync()).ToList();

            IEnumerable<AnalysisResultDto> items = allResults.Select(MapAnalysisToDto);

            // Search
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                items = items.Where(r =>
                    r.Discriminator.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (r.Url != null && r.Url.Contains(search, StringComparison.OrdinalIgnoreCase)));
            }

            // Sort
            items = ApplyAnalysisSort(items, query.SortBy, query.SortDirection);

            var list = items.ToList();
            var totalCount = list.Count;
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            return new GetAllAnalysisResultsPagedQueryResult
            {
                Success = true,
                Result = new PagedResult<AnalysisResultDto>
                {
                    Items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paged analysis results");
            return new GetAllAnalysisResultsPagedQueryResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    public virtual async Task<GetAnalysisResultDetailQueryResult> HandleAsync(GetAnalysisResultDetailQuery query)
    {
        try
        {
            var result = await _analysisResultRepository.GetByKeyAsync(query.AnalysisKey);
            if (result == null)
                return new GetAnalysisResultDetailQueryResult { Success = false, Message = "Analysis result not found" };

            return new GetAnalysisResultDetailQueryResult
            {
                Success = true,
                AnalysisResult = MapAnalysisToDto(result)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting analysis result detail");
            return new GetAnalysisResultDetailQueryResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    // =========================================================================
    // Private mapping helpers
    // =========================================================================

    private static AlertDto MapAlertToDto(DeviceAlertEntity a, Dictionary<string, string> userNames)
    {
        string? userName = null;
        if (!string.IsNullOrEmpty(a.UserKeyField) && userNames.TryGetValue(a.UserKeyField, out var name) && !string.IsNullOrWhiteSpace(name))
            userName = name;

        return new AlertDto
        {
            Key = a.Key,
            AlertType = a.AlertType ?? string.Empty,
            Priority = a.Priority,
            Status = a.Status,
            Timestamp = a.Timestamp,
            DeviceUid = a.DeviceUid,
            DeviceType = a.DeviceType,
            OperatingSystem = a.OperatingSystem,
            DeviceKey = a.DeviceKey,
            UserKey = a.UserKey,
            UserName = userName,
            AnalysisKey = !string.IsNullOrEmpty(a.AnalysisKeyField)
                ? new Key("AnalysisResultContainer", a.AnalysisKeyField)
                : null
        };
    }

    private static AnalysisResultDto MapAnalysisToDto(AnalysisResultContainer r)
    {
        string? url = null;
        if (r is UrlAnalysisResultContainer urlResult)
            url = urlResult.Url;
        else if (r is TrackUrlAnalysisResultContainer trackResult)
            url = trackResult.Url;

        return new AnalysisResultDto
        {
            Key = r.Key,
            Discriminator = r.Discriminator,
            Timestamp = r.Timestamp,
            IsFromCache = r.IsFromCache,
            HasError = r.HasError ?? false,
            ErrorMessage = r.ErrorMessage,
            JsonValue = r.JsonValue,
            UserKey = new Key("User", r.UserKeyField),
            DeviceAlertKey = r.DeviceAlertKey,
            Url = url
        };
    }

    private static IEnumerable<DeviceDto> ApplyDeviceSort(IEnumerable<DeviceDto> items, string? sortBy, string sortDirection)
    {
        var asc = sortDirection != "desc";
        return sortBy?.ToLowerInvariant() switch
        {
            "deviceuid" => asc ? items.OrderBy(d => d.DeviceUid) : items.OrderByDescending(d => d.DeviceUid),
            "devicetype" => asc ? items.OrderBy(d => d.DeviceType) : items.OrderByDescending(d => d.DeviceType),
            "monitoringstatus" => asc ? items.OrderBy(d => d.MonitoringStatus) : items.OrderByDescending(d => d.MonitoringStatus),
            "username" => asc ? items.OrderBy(d => d.UserName) : items.OrderByDescending(d => d.UserName),
            "datecreated" => asc ? items.OrderBy(d => d.DateCreated) : items.OrderByDescending(d => d.DateCreated),
            _ => items.OrderByDescending(d => d.DateCreated)
        };
    }

    private static IEnumerable<AlertDto> ApplyAlertSort(IEnumerable<AlertDto> items, string? sortBy, string sortDirection)
    {
        var asc = sortDirection != "desc";
        return sortBy?.ToLowerInvariant() switch
        {
            "alerttype" => asc ? items.OrderBy(a => a.AlertType) : items.OrderByDescending(a => a.AlertType),
            "priority" => asc ? items.OrderBy(a => a.Priority) : items.OrderByDescending(a => a.Priority),
            "status" => asc ? items.OrderBy(a => a.Status) : items.OrderByDescending(a => a.Status),
            "timestamp" => asc ? items.OrderBy(a => a.Timestamp) : items.OrderByDescending(a => a.Timestamp),
            "deviceuid" => asc ? items.OrderBy(a => a.DeviceUid) : items.OrderByDescending(a => a.DeviceUid),
            _ => items.OrderByDescending(a => a.Timestamp)
        };
    }

    private static IEnumerable<AnalysisResultDto> ApplyAnalysisSort(IEnumerable<AnalysisResultDto> items, string? sortBy, string sortDirection)
    {
        var asc = sortDirection != "desc";
        return sortBy?.ToLowerInvariant() switch
        {
            "discriminator" => asc ? items.OrderBy(r => r.Discriminator) : items.OrderByDescending(r => r.Discriminator),
            "timestamp" => asc ? items.OrderBy(r => r.Timestamp) : items.OrderByDescending(r => r.Timestamp),
            "isfromcache" => asc ? items.OrderBy(r => r.IsFromCache) : items.OrderByDescending(r => r.IsFromCache),
            _ => items.OrderByDescending(r => r.Timestamp)
        };
    }
}
