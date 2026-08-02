using Common.Entities;
using Common.Enums;
using Common.Messaging;
using Common.Models;

namespace Business.Queries;

// Dashboard Stats
public class GetDashboardStatsQuery : Query 
{
    public GetDashboardStatsQuery()
    {
        QueryType = nameof(GetDashboardStatsQuery);
    }
    
    public string QueryType { get; set; }
}

public class GetDashboardStatsQueryResult : QueryResult
{
    public int UsersCount { get; set; }
    public int DevicesCount { get; set; }
    public int AlertsCount24h { get; set; }
    public int PhishingWebsitesCount { get; set; }
}

// All Devices
public class GetAllDevicesQuery : Query
{
    public GetAllDevicesQuery()
    {
        QueryType = nameof(GetAllDevicesQuery);
    }

    public string QueryType { get; set; }
    public string? Search { get; set; }
}

public class GetAllDevicesQueryResult : QueryResult
{
    public List<DeviceWithUser> Devices { get; set; } = new();
}

// Devices by User
public class GetDevicesByUserQuery : Query
{
    public GetDevicesByUserQuery()
    {
        QueryType = nameof(GetDevicesByUserQuery);
    }
    
    public string QueryType { get; set; }
    public Key UserKey { get; set; } = new Key();
}

public class GetDevicesByUserQueryResult : QueryResult
{
    public List<UserDevice> Devices { get; set; } = new();
}

// Recent Alerts
public class GetRecentAlertsQuery : Query
{
    public GetRecentAlertsQuery()
    {
        QueryType = nameof(GetRecentAlertsQuery);
    }

    public string QueryType { get; set; }
    public int Hours { get; set; } = 24;
    public string? Search { get; set; }
}

public class GetRecentAlertsQueryResult : QueryResult
{
    public List<DeviceAlertEntity> Alerts { get; set; } = new();
}

// Alerts by Device
public class GetAlertsByDeviceQuery : Query
{
    public GetAlertsByDeviceQuery()
    {
        QueryType = nameof(GetAlertsByDeviceQuery);
    }
    
    public string QueryType { get; set; }
    public string DeviceUid { get; set; } = string.Empty;
    public int Hours { get; set; } = 24;
}

public class GetAlertsByDeviceQueryResult : QueryResult
{
    public List<DeviceAlertEntity> Alerts { get; set; } = new();
}

// All Phishing Websites
public class GetAllPhishingWebsitesQuery : Query
{
    public GetAllPhishingWebsitesQuery()
    {
        QueryType = nameof(GetAllPhishingWebsitesQuery);
    }

    public string QueryType { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? Search { get; set; }
}

public class GetAllPhishingWebsitesQueryResult : QueryResult
{
    public List<KnownPhishingWebsite> PhishingWebsites { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

// Users with Device Counts
public class GetUsersWithDeviceCountsQuery : Query
{
    public GetUsersWithDeviceCountsQuery()
    {
        QueryType = nameof(GetUsersWithDeviceCountsQuery);
    }

    public string QueryType { get; set; }
    public string? Search { get; set; }
}

public class GetUsersWithDeviceCountsQueryResult : QueryResult
{
    public List<UserWithDeviceCount> Users { get; set; } = new();
}

// Device by Key
public class GetDeviceByKeyQuery : Query
{
    public GetDeviceByKeyQuery()
    {
        QueryType = nameof(GetDeviceByKeyQuery);
    }
    
    public string QueryType { get; set; }
    public Key DeviceKey { get; set; } = new Key();
}

public class GetDeviceByKeyQueryResult : QueryResult
{
    public UserDevice? Device { get; set; }
}

// Device by UID
public class GetDeviceByUidQuery : Query
{
    public GetDeviceByUidQuery()
    {
        QueryType = nameof(GetDeviceByUidQuery);
    }

    public string QueryType { get; set; }
    public string DeviceUid { get; set; } = string.Empty;
}

public class GetDeviceByUidQueryResult : QueryResult
{
    public UserDevice? Device { get; set; }
}

// Alert by Key
public class GetAlertByKeyQuery : Query
{
    public GetAlertByKeyQuery()
    {
        QueryType = nameof(GetAlertByKeyQuery);
    }
    
    public string QueryType { get; set; }
    public Key AlertKey { get; set; } = new Key();
}

public class GetAlertByKeyQueryResult : QueryResult
{
    public DeviceAlertEntity? Alert { get; set; }
}

// Helper classes
public class DeviceWithUser
{
    public UserDevice Device { get; set; } = null!;
    public string UserName { get; set; } = string.Empty;
}

public class UserWithDeviceCount
{
    public User User { get; set; } = null!;
    public int DeviceCount { get; set; }
}

// Get all Analysis Results
public class GetAllAnalysisResultsQuery : Query
{
    public GetAllAnalysisResultsQuery()
    {
        QueryType = nameof(GetAllAnalysisResultsQuery);
    }

    public string QueryType { get; set; }
    public int Hours { get; set; } = 24;
    public string? Search { get; set; }
}

public class GetAllAnalysisResultsQueryResult : QueryResult
{
    public List<AnalysisResultContainer> AnalysisResults { get; set; } = new();
}

// Analysis Result by DeviceAlert Key
public class GetAnalysisResultByAlertKeyQuery : Query
{
    public GetAnalysisResultByAlertKeyQuery()
    {
        QueryType = nameof(GetAnalysisResultByAlertKeyQuery);
    }

    public string QueryType { get; set; }
    public string DeviceAlertKeyField { get; set; } = string.Empty;
}

public class GetAnalysisResultByAlertKeyQueryResult : QueryResult
{
    public AnalysisResultContainer? AnalysisResult { get; set; }
}

// Get all Tracked Domains
public class GetAllTrackedDomainsQuery : Query
{
    public GetAllTrackedDomainsQuery()
    {
        QueryType = nameof(GetAllTrackedDomainsQuery);
    }

    public string QueryType { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? Search { get; set; }
    public string? Category { get; set; }
}

public class GetAllTrackedDomainsQueryResult : QueryResult
{
    public List<TrackedDomain> TrackedDomains { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class GetVersionQuery : Query 
{
    public GetVersionQuery()
    {
        QueryType = nameof(GetVersionQuery);
    }

    public string QueryType { get; set; }
}

public class GetVersionQueryResult : QueryResult
{
    public string Version { get; set; } = "N/A";
    public string Component { get; set; } = "Backend";
}

// =========================================================================
// ASPS-646 — Angular Admin: Dashboard + Users API
// =========================================================================

/// <summary>
/// GET /api/dashboard/summary — returns KPI counts for the Angular admin dashboard.
/// Returns: total users, total devices, active alerts (last 24 h), analysis results count.
/// </summary>
public class GetDashboardSummaryQuery : Query
{
    public GetDashboardSummaryQuery() { QueryType = nameof(GetDashboardSummaryQuery); }
    public string QueryType { get; set; }
}

public class GetDashboardSummaryQueryResult : QueryResult
{
    public int TotalUsers { get; set; }
    public int TotalDevices { get; set; }
    public int ActiveAlerts24h { get; set; }
    public int AnalysisResultsCount { get; set; }
}

/// <summary>
/// GET /api/users — server-side paged list with search and sort;
/// includes device count per user.
/// </summary>
public class GetAllUsersPagedQuery : PagedQuery
{
    public GetAllUsersPagedQuery() { QueryType = nameof(GetAllUsersPagedQuery); }
    public string QueryType { get; set; }
}

public class GetAllUsersPagedQueryResult : QueryResult
{
    public PagedResult<UserWithDeviceCount> Result { get; set; } = new();
}

/// <summary>
/// GET /api/users/{key}/alerts — paged alerts for a specific user (via their devices).
/// </summary>
public class GetUserAlertsByKeyQuery : PagedQuery
{
    public GetUserAlertsByKeyQuery() { QueryType = nameof(GetUserAlertsByKeyQuery); }
    public string QueryType { get; set; }
    public Key UserKey { get; set; } = new Key();
}

public class GetUserAlertsByKeyQueryResult : QueryResult
{
    public PagedResult<DeviceAlertEntity> Result { get; set; } = new();
}

// =========================================================================
// ASPS-647 — Paged Devices, Alerts, Analysis Results (Angular Admin API)
// =========================================================================

/// <summary>Server-side paged device list with search and status filter.</summary>
public class GetAllDevicesPagedQuery : PagedQuery
{
    /// <summary>Optional filter: only return devices with this MonitoringStatus string (e.g. "Enabled").</summary>
    public string? Status { get; set; }
}

public class GetAllDevicesPagedQueryResult : QueryResult
{
    public PagedResult<DeviceDto> Result { get; set; } = new();
}

/// <summary>Server-side paged alerts for a single device (by DeviceKeyField).</summary>
public class GetDeviceAlertsPagedQuery : PagedQuery
{
    public string DeviceKeyField { get; set; } = string.Empty;
}

public class GetDeviceAlertsPagedQueryResult : QueryResult
{
    public PagedResult<AlertDto> Result { get; set; } = new();
}

/// <summary>Server-side paged alert list with time-range and severity filters.</summary>
public class GetAllAlertsPagedQuery : PagedQuery
{
    /// <summary>ISO 8601 lower bound (inclusive). Null = no lower bound.</summary>
    public DateTime? From { get; set; }

    /// <summary>ISO 8601 upper bound (inclusive). Null = no upper bound.</summary>
    public DateTime? To { get; set; }

    /// <summary>Filter by severity/priority name (e.g. "High"). Null = all.</summary>
    public string? Severity { get; set; }
}

public class GetAllAlertsPagedQueryResult : QueryResult
{
    public PagedResult<AlertDto> Result { get; set; } = new();
}

/// <summary>Single alert with full detail including related device and user info.</summary>
public class GetAlertDetailQuery : Query
{
    public Key AlertKey { get; set; } = new Key();
}

public class GetAlertDetailQueryResult : QueryResult
{
    public AlertDto? Alert { get; set; }
}

/// <summary>Server-side paged analysis result list with search and sort.</summary>
public class GetAllAnalysisResultsPagedQuery : PagedQuery
{
    /// <summary>ISO 8601 lower bound (inclusive). Null = no lower bound.</summary>
    public DateTime? From { get; set; }

    /// <summary>ISO 8601 upper bound (inclusive). Null = no upper bound.</summary>
    public DateTime? To { get; set; }
}

public class GetAllAnalysisResultsPagedQueryResult : QueryResult
{
    public PagedResult<AnalysisResultDto> Result { get; set; } = new();
}

/// <summary>Single analysis result detail by key.</summary>
public class GetAnalysisResultDetailQuery : Query
{
    public Key AnalysisKey { get; set; } = new Key();
}

public class GetAnalysisResultDetailQueryResult : QueryResult
{
    public AnalysisResultDto? AnalysisResult { get; set; }
}

// =========================================================================
// ASPS-649 — Angular Admin: Simulations + Roadmaps + System API
// =========================================================================

/// <summary>Server-side paged list of simulations with optional search.</summary>
public class GetAllSimulationsPagedQuery : PagedQuery { }

public class SimulationDto
{
    public Key Key { get; set; } = new Key();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CreatorKeyField { get; set; } = string.Empty;
    public List<Common.Models.SimulationStep> Steps { get; set; } = new();
    public bool IsDeleted { get; set; }
    public bool IsDisabled { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
}

public class GetAllSimulationsPagedQueryResult : QueryResult
{
    public PagedResult<SimulationDto> Result { get; set; } = new();
}

/// <summary>Single simulation by KeyField, with deserialized steps.</summary>
public class GetSimulationByKeyFieldQuery : Query
{
    public string KeyField { get; set; } = string.Empty;
}

public class GetSimulationByKeyFieldQueryResult : QueryResult
{
    public SimulationDto? Simulation { get; set; }
}

/// <summary>Server-side paged list of roadmaps with optional includeArchived flag.</summary>
public class GetAllRoadmapsPagedQuery : PagedQuery
{
    public bool IncludeArchived { get; set; } = false;
}

public class RoadmapDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Version { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? LastUpdatedBy { get; set; }
    public bool IsArchived { get; set; }
}

public class GetAllRoadmapsPagedQueryResult : QueryResult
{
    public PagedResult<RoadmapDto> Result { get; set; } = new();
}
