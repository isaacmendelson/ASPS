using Common.Entities;
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
