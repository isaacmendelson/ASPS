using Business.Handlers;
using Business.Queries;
using Business.Services;
using Business.Views;
using Common.Models;
using Interface.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Business.Messaging;

/// <summary>
/// ASPS-680 — Query handler registrations for CqrsHandlerRegistry.
/// Mirrors the dispatch behavior previously implemented as a switch statement in
/// CQRSGateway.Queries.cs (ASPS-675 Messaging Refactoring, Phase 1).
/// Kept in a separate file from MessagingServiceRegistration.cs to avoid merge
/// conflicts with the parallel ASPS-679 command registration work.
/// </summary>
internal static class CqrsQueryRegistration
{
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        TypeNameHandling = TypeNameHandling.None,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
    };

    internal static void RegisterQueryHandlers(CqrsHandlerRegistry registry)
    {
        // =====================================================================
        // Admin Query Handlers
        // =====================================================================
        registry.RegisterQueryWithDefault<GetDashboardStatsQuery, AdminQueryHandlers>("GetDashboardStatsQuery");
        registry.RegisterQueryWithDefault<GetUsersWithDeviceCountsQuery, AdminQueryHandlers>("GetUsersWithDeviceCountsQuery");
        registry.RegisterQueryWithDefault<GetAllDevicesQuery, AdminQueryHandlers>("GetAllDevicesQuery");
        registry.RegisterQuery<GetRecentAlertsQuery, AdminQueryHandlers>("GetRecentAlertsQuery");

        // ASPS-647 — Paged API endpoints (Angular Admin)
        registry.RegisterQueryWithDefault<GetAllDevicesPagedQuery, AdminQueryHandlers>("GetAllDevicesPagedQuery");
        registry.RegisterQuery<GetDeviceAlertsPagedQuery, AdminQueryHandlers>("GetDeviceAlertsPagedQuery");
        registry.RegisterQueryWithDefault<GetAllAlertsPagedQuery, AdminQueryHandlers>("GetAllAlertsPagedQuery");
        registry.RegisterQuery<GetAlertDetailQuery, AdminQueryHandlers>("GetAlertDetailQuery");
        registry.RegisterQueryWithDefault<GetAllAnalysisResultsPagedQuery, AdminQueryHandlers>("GetAllAnalysisResultsPagedQuery");
        registry.RegisterQuery<GetAnalysisResultDetailQuery, AdminQueryHandlers>("GetAnalysisResultDetailQuery");
        registry.RegisterQuery<GetDeviceByKeyQuery, AdminQueryHandlers>("GetDeviceByKeyQuery");
        registry.RegisterQuery<GetDeviceByUidQuery, AdminQueryHandlers>("GetDeviceByUidQuery");
        registry.RegisterQuery<GetDevicesByUserQuery, AdminQueryHandlers>("GetDevicesByUserQuery");
        registry.RegisterQuery<GetAlertsByDeviceQuery, AdminQueryHandlers>("GetAlertsByDeviceQuery");
        registry.RegisterQuery<GetAlertByKeyQuery, AdminQueryHandlers>("GetAlertByKeyQuery");
        registry.RegisterQuery<GetAllAnalysisResultsQuery, AdminQueryHandlers>("GetAllAnalysisResultsQuery");
        registry.RegisterQuery<GetAnalysisResultByAlertKeyQuery, AdminQueryHandlers>("GetAnalysisResultByAlertKeyQuery");

        // =====================================================================
        // Special Query Handlers (direct service access, no handler class)
        // =====================================================================
        registry.RegisterCustom("GetAllPhishingWebsitesQuery", (json, scope) =>
            Task.FromResult(HandleGetAllPhishingWebsitesQuery(json, scope)));
        registry.RegisterCustom("GetAllTrackedDomainsQuery", HandleGetAllTrackedDomainsQueryAsync);
        registry.RegisterCustom("ValidateDeviceTokenQuery", (json, scope) =>
            Task.FromResult(HandleValidateDeviceTokenQuery(json, scope)));
        registry.RegisterCustom("GetVersionQuery", (json, scope) =>
            Task.FromResult(HandleGetVersionQuery(scope)));

        // =====================================================================
        // Simulation Query Handlers
        // =====================================================================
        registry.RegisterQueryWithDefault<GetSimulationsQuery, SimulationQueryHandlers>("GetSimulationsQuery");
        registry.RegisterQuery<GetSimulationDetailsQuery, SimulationQueryHandlers>("GetSimulationDetailsQuery");
        registry.RegisterQueryWithDefault<GetSimulationUsersQuery, SimulationQueryHandlers>("GetSimulationUsersQuery");
        registry.RegisterQuery<GetSimulationUserDevicesQuery, SimulationQueryHandlers>("GetSimulationUserDevicesQuery");
        registry.RegisterQuery<GetSimulationDevicesQuery, SimulationQueryHandlers>("GetSimulationDevicesQuery");
        registry.RegisterQuery<GetUserByKeycloakIdQuery, UserQueryHandlers>("GetUserByKeycloakIdQuery");

        // =====================================================================
        // Website Category Query Handlers (SCRUM-822)
        // =====================================================================
        registry.RegisterQueryWithDefault<GetAllWebsiteCategoriesQuery, WebsiteCategoryQueryHandlers>("GetAllWebsiteCategoriesQuery");
        registry.RegisterQuery<GetWebsiteCategoryByNameQuery, WebsiteCategoryQueryHandlers>("GetWebsiteCategoryByNameQuery");
        registry.RegisterQueryWithDefault<GetParentCategoriesQuery, WebsiteCategoryQueryHandlers>("GetParentCategoriesQuery");

        // =====================================================================
        // Roadmap Query Handlers
        // =====================================================================
        registry.RegisterQuery<GetRoadmapByIdQuery, RoadmapQueryHandlers>("GetRoadmapByIdQuery");
        registry.RegisterQueryWithDefault<ListRoadmapsQuery, RoadmapQueryHandlers>("ListRoadmapsQuery");

        // =====================================================================
        // User Query Handlers
        // =====================================================================
        registry.RegisterQuery<GetUserByKeyQuery, UserQueryHandlers>("GetUserByKeyQuery");
        registry.RegisterQueryWithDefault<GetAllUsersQuery, UserQueryHandlers>("GetAllUsersQuery");
        registry.RegisterQuery<GetUserDetailsQuery, UserQueryHandlers>("GetUserDetailsQuery");
        registry.RegisterQuery<GetUserDevicesQuery, UserQueryHandlers>("GetUserDevicesQuery");
        registry.RegisterQuery<GetUserAccountsQuery, UserQueryHandlers>("GetUserAccountsQuery");

        // =====================================================================
        // Bank Website Query Handlers (ASPS-297)
        // =====================================================================
        registry.RegisterQueryWithDefault<GetAllBankWebsitesQuery, BankWebsiteQueryHandlers>("GetAllBankWebsitesQuery");
        registry.RegisterQuery<GetBankWebsiteByIdQuery, BankWebsiteQueryHandlers>("GetBankWebsiteByIdQuery");
        registry.RegisterQuery<CheckDomainIsBankQuery, BankWebsiteQueryHandlers>("CheckDomainIsBankQuery");

        // =====================================================================
        // Blacklisted Phone Number Query Handlers (ASPS-282)
        // =====================================================================
        registry.RegisterQueryWithDefault<GetAllBlacklistedPhoneNumbersQuery, BlacklistedPhoneNumberQueryHandlers>("GetAllBlacklistedPhoneNumbersQuery");
        registry.RegisterQuery<GetBlacklistedPhoneNumberByIdQuery, BlacklistedPhoneNumberQueryHandlers>("GetBlacklistedPhoneNumberByIdQuery");
        registry.RegisterQuery<CheckPhoneNumberBlacklistedQuery, BlacklistedPhoneNumberQueryHandlers>("CheckPhoneNumberBlacklistedQuery");

        // =====================================================================
        // SCRUM-904 — User Risk Score Query Handlers
        // =====================================================================
        registry.RegisterQuery<GetLatestUserRiskScoreQuery, UserRiskScoreQueryHandlers>("GetLatestUserRiskScoreQuery");

        // =====================================================================
        // ASPS-646 — Angular Admin: Dashboard + Users API
        // =====================================================================
        registry.RegisterQueryWithDefault<GetDashboardSummaryQuery, AdminQueryHandlers>("GetDashboardSummaryQuery");
        registry.RegisterQueryWithDefault<GetAllUsersPagedQuery, AdminQueryHandlers>("GetAllUsersPagedQuery");
        registry.RegisterQuery<GetUserAlertsByKeyQuery, AdminQueryHandlers>("GetUserAlertsByKeyQuery");

        // =====================================================================
        // ASPS-649 — Angular Admin: Simulations + Roadmaps paged queries
        // =====================================================================
        registry.RegisterQueryWithDefault<GetAllSimulationsPagedQuery, ASPS649QueryHandlers>("GetAllSimulationsPagedQuery");
        registry.RegisterQuery<GetSimulationByKeyFieldQuery, ASPS649QueryHandlers>("GetSimulationByKeyFieldQuery");
        registry.RegisterQueryWithDefault<GetAllRoadmapsPagedQuery, ASPS649QueryHandlers>("GetAllRoadmapsPagedQuery");
    }

    // =========================================================================
    // Custom handler implementations — mirror CQRSGateway.Queries.cs exactly,
    // but resolve services via the dispatch-time IServiceScope instead of the
    // gateway's own _serviceProvider field.
    // =========================================================================

    private static string HandleGetAllPhishingWebsitesQuery(string messageJson, IServiceScope scope)
    {
        try
        {
            var query = JsonConvert.DeserializeObject<GetAllPhishingWebsitesQuery>(messageJson) ?? new GetAllPhishingWebsitesQuery();
            var asView = scope.ServiceProvider.GetRequiredService<ASView>();
            var websites = asView.GetKnownPhishingWebsites().AsEnumerable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                websites = websites.Where(w =>
                    (w.Url != null && w.Url.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (w.Domain != null && w.Domain.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (w.Source != null && w.Source.Contains(search, StringComparison.OrdinalIgnoreCase)));
            }

            var totalCount = websites.Count();

            // Apply paging
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 500);
            var pagedWebsites = websites
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new GetAllPhishingWebsitesQueryResult
            {
                Success = true,
                PhishingWebsites = pagedWebsites,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return JsonConvert.SerializeObject(result, SerializerSettings);
        }
        catch (Exception ex)
        {
            GetLogger(scope)?.LogError(ex, "Error getting phishing websites from ASView");
            return CreateErrorJson($"Error: {ex.Message}");
        }
    }

    private static async Task<string> HandleGetAllTrackedDomainsQueryAsync(string messageJson, IServiceScope scope)
    {
        try
        {
            var query = JsonConvert.DeserializeObject<GetAllTrackedDomainsQuery>(messageJson) ?? new GetAllTrackedDomainsQuery();

            var trackedDomainRepository = scope.ServiceProvider.GetRequiredService<ITrackedDomainRepository>();

            var domains = (await trackedDomainRepository.GetAllActiveAsync()).ToList();

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

            // Apply paging
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 500);
            var pagedDomains = domains
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new GetAllTrackedDomainsQueryResult
            {
                Success = true,
                TrackedDomains = pagedDomains,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return JsonConvert.SerializeObject(result, SerializerSettings);
        }
        catch (Exception ex)
        {
            GetLogger(scope)?.LogError(ex, "Error getting tracked domains");
            return CreateErrorJson($"Error: {ex.Message}");
        }
    }

    private static string HandleValidateDeviceTokenQuery(string messageJson, IServiceScope scope)
    {
        try
        {
            var query = JsonConvert.DeserializeObject<ValidateDeviceTokenQuery>(messageJson);
            if (query == null) return CreateErrorJson("Invalid ValidateDeviceTokenQuery format");

            var tokenStore = scope.ServiceProvider.GetRequiredService<TokenStore>();
            var validationResult = tokenStore.ValidateToken(query.DeviceUid, query.TokenValue);

            var result = new ValidateDeviceTokenQueryResult
            {
                Success = true,
                IsValid = validationResult == TokenValidationResult.Valid
            };

            if (result.IsValid)
            {
                var token = tokenStore.GetToken(query.DeviceUid);
                result.UserKeyField = token?.UserKeyField;
            }

            return JsonConvert.SerializeObject(result, SerializerSettings);
        }
        catch (Exception ex)
        {
            GetLogger(scope)?.LogError(ex, "Error validating device token");
            return CreateErrorJson($"Error: {ex.Message}");
        }
    }

    private static string HandleGetVersionQuery(IServiceScope scope)
    {
        try
        {
            // Get version from entry assembly (ASPSBackend) not from this library
            var entryAssembly = System.Reflection.Assembly.GetEntryAssembly();
            var version = entryAssembly?.GetName().Version?.ToString() ?? "0.0.0.0";
            var result = new GetVersionQueryResult
            {
                Success = true,
                Version = version,
                Component = "Backend"
            };
            return JsonConvert.SerializeObject(result, SerializerSettings);
        }
        catch (Exception ex)
        {
            GetLogger(scope)?.LogError(ex, "Error getting version");
            return CreateErrorJson($"Error: {ex.Message}");
        }
    }

    private static ILogger? GetLogger(IServiceScope scope) =>
        scope.ServiceProvider.GetService<ILogger<CQRSGateway>>();

    private static string CreateErrorJson(string message)
    {
        return JsonConvert.SerializeObject(new { Success = false, Message = message });
    }
}
