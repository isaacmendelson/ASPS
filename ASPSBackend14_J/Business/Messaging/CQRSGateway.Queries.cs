using Business.Queries;
using Business.Handlers;
using Business.Services;
using Business.Views;
using Common.Models;
using Interface.Repositories;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Business.Messaging;

/// <summary>
/// CQRS Gateway — query dispatch and query handler methods.
/// </summary>
public partial class CQRSGateway
{
    private async Task<string> ProcessQueryAsync(string messageJson, JObject jObject)
    {
        using var scope = _serviceProvider.CreateScope();

        // Get query type from JSON
        var queryType = jObject["QueryType"]?.ToString();

        if (string.IsNullOrEmpty(queryType))
        {
            return CreateErrorResponse("QueryType field is missing or empty");
        }

        _logger.LogInformation("Handling query: {QueryType}", queryType);

        return queryType switch
        {
            "GetDashboardStatsQuery" => await HandleGetDashboardStatsQuery(scope),
            "GetUsersWithDeviceCountsQuery" => await HandleGetUsersWithDeviceCountsQuery(messageJson, scope),
            "GetAllDevicesQuery" => await HandleGetAllDevicesQuery(messageJson, scope),
            "GetRecentAlertsQuery" => await HandleGetRecentAlertsQuery(messageJson, scope),
            // ASPS-647 — Paged API endpoints (Angular Admin)
            "GetAllDevicesPagedQuery" => await HandleGetAllDevicesPagedQuery(messageJson, scope),
            "GetDeviceAlertsPagedQuery" => await HandleGetDeviceAlertsPagedQuery(messageJson, scope),
            "GetAllAlertsPagedQuery" => await HandleGetAllAlertsPagedQuery(messageJson, scope),
            "GetAlertDetailQuery" => await HandleGetAlertDetailQuery(messageJson, scope),
            "GetAllAnalysisResultsPagedQuery" => await HandleGetAllAnalysisResultsPagedQuery(messageJson, scope),
            "GetAnalysisResultDetailQuery" => await HandleGetAnalysisResultDetailQuery(messageJson, scope),
            "GetUserByKeyQuery" => await HandleGetUserByKeyQuery(messageJson, scope),
            "GetDeviceByKeyQuery" => await HandleGetDeviceByKeyQuery(messageJson, scope),
            "GetDeviceByUidQuery" => await HandleGetDeviceByUidQuery(messageJson, scope),
            "GetDevicesByUserQuery" => await HandleGetDevicesByUserQuery(messageJson, scope),
            "GetAlertsByDeviceQuery" => await HandleGetAlertsByDeviceQuery(messageJson, scope),
            "GetAlertByKeyQuery" => await HandleGetAlertByKeyQuery(messageJson, scope),
            "GetAllAnalysisResultsQuery" => await HandleGetAllAnalysisResultsQuery(messageJson, scope),
            "GetAnalysisResultByAlertKeyQuery" => await HandleGetAnalysisResultByAlertKeyQuery(messageJson, scope),
            "GetAllPhishingWebsitesQuery" => HandleGetAllPhishingWebsitesQuery(messageJson),
            "GetAllTrackedDomainsQuery" => await HandleGetAllTrackedDomainsQuery(messageJson),
            "ValidateDeviceTokenQuery" => HandleValidateDeviceTokenQuery(messageJson),
            "GetVersionQuery" => HandleGetVersionQuery(),
            // Simulation Queries
            "GetSimulationsQuery" => await HandleGetSimulationsQuery(messageJson, scope),
            "GetSimulationDetailsQuery" => await HandleGetSimulationDetailsQuery(messageJson, scope),
            "GetSimulationUsersQuery" => await HandleGetSimulationUsersQuery(messageJson, scope),
            "GetSimulationUserDevicesQuery" => await HandleGetSimulationUserDevicesQuery(messageJson, scope),
            "GetSimulationDevicesQuery" => await HandleGetSimulationDevicesQuery(messageJson, scope),
            "GetUserByKeycloakIdQuery" => await HandleGetUserByKeycloakIdQuery(messageJson, scope),
            // Website Category Queries (SCRUM-822)
            "GetAllWebsiteCategoriesQuery" => await HandleGetAllWebsiteCategoriesQuery(messageJson, scope),
            "GetWebsiteCategoryByNameQuery" => await HandleGetWebsiteCategoryByNameQuery(messageJson, scope),
            "GetParentCategoriesQuery" => await HandleGetParentCategoriesQuery(messageJson, scope),
            // Roadmap Queries
            "GetRoadmapByIdQuery" => await HandleGetRoadmapByIdQuery(messageJson, scope),
            "ListRoadmapsQuery" => await HandleListRoadmapsQuery(messageJson, scope),
            // User Queries
            "GetAllUsersQuery" => await HandleGetAllUsersQuery(messageJson, scope),
            "GetUserDetailsQuery" => await HandleGetUserDetailsQuery(messageJson, scope),
            "GetUserDevicesQuery" => await HandleGetUserDevicesQuery(messageJson, scope),
            "GetUserAccountsQuery" => await HandleGetUserAccountsQuery(messageJson, scope),
            // Bank Website Queries (ASPS-297)
            "GetAllBankWebsitesQuery" => await HandleGetAllBankWebsitesQuery(messageJson, scope),
            "GetBankWebsiteByIdQuery" => await HandleGetBankWebsiteByIdQuery(messageJson, scope),
            "CheckDomainIsBankQuery" => await HandleCheckDomainIsBankQuery(messageJson, scope),
            // Blacklisted Phone Number Queries (ASPS-282)
            "GetAllBlacklistedPhoneNumbersQuery" => await HandleGetAllBlacklistedPhoneNumbersQuery(messageJson, scope),
            "GetBlacklistedPhoneNumberByIdQuery" => await HandleGetBlacklistedPhoneNumberByIdQuery(messageJson, scope),
            "CheckPhoneNumberBlacklistedQuery" => await HandleCheckPhoneNumberBlacklistedQuery(messageJson, scope),
            // SCRUM-904 — User Risk Score
            "GetLatestUserRiskScoreQuery" => await HandleGetLatestUserRiskScoreQuery(messageJson, scope),
            // ASPS-649 — Angular Admin: Simulations + Roadmaps paged queries
            "GetAllSimulationsPagedQuery" => await HandleGetAllSimulationsPagedQuery(messageJson, scope),
            "GetSimulationByKeyFieldQuery" => await HandleGetSimulationByKeyFieldQuery(messageJson, scope),
            "GetAllRoadmapsPagedQuery" => await HandleGetAllRoadmapsPagedQuery(messageJson, scope),
            _ => CreateErrorResponse($"Unknown query type: {queryType}")
        };
    }

    // =========================================================================
    // Admin Query Handlers
    // =========================================================================

    private async Task<string> HandleGetDashboardStatsQuery(IServiceScope scope)
    {
        var handler = scope.ServiceProvider.GetRequiredService<AdminQueryHandlers>();
        var result = await handler.HandleAsync(new GetDashboardStatsQuery());
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleGetUsersWithDeviceCountsQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetUsersWithDeviceCountsQuery>(messageJson) ?? new GetUsersWithDeviceCountsQuery();
        var handler = scope.ServiceProvider.GetRequiredService<AdminQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleGetAllDevicesQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetAllDevicesQuery>(messageJson) ?? new GetAllDevicesQuery();
        var handler = scope.ServiceProvider.GetRequiredService<AdminQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleGetRecentAlertsQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetRecentAlertsQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetRecentAlertsQuery format");

        var handler = scope.ServiceProvider.GetRequiredService<AdminQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleGetDeviceByKeyQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetDeviceByKeyQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetDeviceByKeyQuery format");

        var handler = scope.ServiceProvider.GetRequiredService<AdminQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleGetDeviceByUidQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetDeviceByUidQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetDeviceByUidQuery format");

        var handler = scope.ServiceProvider.GetRequiredService<AdminQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleGetDevicesByUserQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetDevicesByUserQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetDevicesByUserQuery format");

        var handler = scope.ServiceProvider.GetRequiredService<AdminQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleGetAlertsByDeviceQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetAlertsByDeviceQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetAlertsByDeviceQuery format");

        var handler = scope.ServiceProvider.GetRequiredService<AdminQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleGetAlertByKeyQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetAlertByKeyQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetAlertByKeyQuery format");

        var handler = scope.ServiceProvider.GetRequiredService<AdminQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleGetAllAnalysisResultsQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetAllAnalysisResultsQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetAllAnalysisResultsQuery format");

        var handler = scope.ServiceProvider.GetRequiredService<AdminQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleGetAnalysisResultByAlertKeyQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetAnalysisResultByAlertKeyQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetAnalysisResultByAlertKeyQuery format");

        var handler = scope.ServiceProvider.GetRequiredService<AdminQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    // =========================================================================
    // Special Query Handlers (direct service access, no handler class)
    // =========================================================================

    private string HandleGetAllPhishingWebsitesQuery(string messageJson)
    {
        try
        {
            var query = JsonConvert.DeserializeObject<GetAllPhishingWebsitesQuery>(messageJson) ?? new GetAllPhishingWebsitesQuery();
            var asView = _serviceProvider.GetRequiredService<ASView>();
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

            return JsonConvert.SerializeObject(result, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting phishing websites from ASView");
            return CreateErrorResponse($"Error: {ex.Message}");
        }
    }

    private async Task<string> HandleGetAllTrackedDomainsQuery(string messageJson)
    {
        try
        {
            var query = JsonConvert.DeserializeObject<GetAllTrackedDomainsQuery>(messageJson) ?? new GetAllTrackedDomainsQuery();

            using var scope = _serviceProvider.CreateScope();
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

            return JsonConvert.SerializeObject(result, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tracked domains");
            return CreateErrorResponse($"Error: {ex.Message}");
        }
    }

    private string HandleValidateDeviceTokenQuery(string messageJson)
    {
        try
        {
            var query = JsonConvert.DeserializeObject<ValidateDeviceTokenQuery>(messageJson);
            if (query == null) return CreateErrorResponse("Invalid ValidateDeviceTokenQuery format");

            var tokenStore = _serviceProvider.GetRequiredService<TokenStore>();
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

            return JsonConvert.SerializeObject(result, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating device token");
            return CreateErrorResponse($"Error: {ex.Message}");
        }
    }

    private string HandleGetVersionQuery()
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
            return JsonConvert.SerializeObject(result, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting version");
            return CreateErrorResponse($"Error: {ex.Message}");
        }
    }

    // =========================================================================
    // Simulation Query Handlers
    // =========================================================================

    private async Task<string> HandleGetSimulationsQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetSimulationsQuery>(messageJson) ?? new GetSimulationsQuery();
        var handler = scope.ServiceProvider.GetRequiredService<SimulationQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleGetSimulationDetailsQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetSimulationDetailsQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetSimulationDetailsQuery format");

        var handler = scope.ServiceProvider.GetRequiredService<SimulationQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleGetSimulationUsersQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetSimulationUsersQuery>(messageJson) ?? new GetSimulationUsersQuery();
        var handler = scope.ServiceProvider.GetRequiredService<SimulationQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleGetSimulationUserDevicesQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetSimulationUserDevicesQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetSimulationUserDevicesQuery format");

        var handler = scope.ServiceProvider.GetRequiredService<SimulationQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleGetSimulationDevicesQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetSimulationDevicesQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetSimulationDevicesQuery format");

        var handler = scope.ServiceProvider.GetRequiredService<SimulationQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    // =========================================================================
    // User Query Handlers
    // =========================================================================

    private async Task<string> HandleGetUserByKeyQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetUserByKeyQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetUserByKeyQuery format");

        var handler = scope.ServiceProvider.GetRequiredService<UserQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleGetUserByKeycloakIdQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetUserByKeycloakIdQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetUserByKeycloakIdQuery format");

        var handler = scope.ServiceProvider.GetRequiredService<UserQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleGetAllUsersQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetAllUsersQuery>(messageJson) ?? new GetAllUsersQuery();
        var handler = scope.ServiceProvider.GetRequiredService<UserQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleGetUserDetailsQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetUserDetailsQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetUserDetailsQuery format");
        var handler = scope.ServiceProvider.GetRequiredService<UserQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleGetUserDevicesQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetUserDevicesQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetUserDevicesQuery format");
        var handler = scope.ServiceProvider.GetRequiredService<UserQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleGetUserAccountsQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetUserAccountsQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetUserAccountsQuery format");
        var handler = scope.ServiceProvider.GetRequiredService<UserQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    // =========================================================================
    // Website Category Query Handlers (SCRUM-822)
    // =========================================================================

    private async Task<string> HandleGetAllWebsiteCategoriesQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetAllWebsiteCategoriesQuery>(messageJson) ?? new GetAllWebsiteCategoriesQuery();
        var handler = scope.ServiceProvider.GetRequiredService<WebsiteCategoryQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleGetWebsiteCategoryByNameQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetWebsiteCategoryByNameQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetWebsiteCategoryByNameQuery format");

        var handler = scope.ServiceProvider.GetRequiredService<WebsiteCategoryQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleGetParentCategoriesQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetParentCategoriesQuery>(messageJson) ?? new GetParentCategoriesQuery();
        var handler = scope.ServiceProvider.GetRequiredService<WebsiteCategoryQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    // =========================================================================
    // Roadmap Query Handlers
    // =========================================================================

    private async Task<string> HandleGetRoadmapByIdQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetRoadmapByIdQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetRoadmapByIdQuery format");
        var handler = scope.ServiceProvider.GetRequiredService<RoadmapQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleListRoadmapsQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<ListRoadmapsQuery>(messageJson) ?? new ListRoadmapsQuery();
        var handler = scope.ServiceProvider.GetRequiredService<RoadmapQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    // =========================================================================
    // Bank Website Query Handlers (ASPS-297)
    // =========================================================================

    private async Task<string> HandleGetAllBankWebsitesQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetAllBankWebsitesQuery>(messageJson) ?? new GetAllBankWebsitesQuery();
        var handler = scope.ServiceProvider.GetRequiredService<BankWebsiteQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleGetBankWebsiteByIdQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetBankWebsiteByIdQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetBankWebsiteByIdQuery format");
        var handler = scope.ServiceProvider.GetRequiredService<BankWebsiteQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleCheckDomainIsBankQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<CheckDomainIsBankQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid CheckDomainIsBankQuery format");
        var handler = scope.ServiceProvider.GetRequiredService<BankWebsiteQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    // =========================================================================
    // Blacklisted Phone Number Query Handlers (ASPS-282)
    // =========================================================================

    private async Task<string> HandleGetAllBlacklistedPhoneNumbersQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetAllBlacklistedPhoneNumbersQuery>(messageJson) ?? new GetAllBlacklistedPhoneNumbersQuery();
        var handler = scope.ServiceProvider.GetRequiredService<BlacklistedPhoneNumberQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleGetBlacklistedPhoneNumberByIdQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetBlacklistedPhoneNumberByIdQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetBlacklistedPhoneNumberByIdQuery format");
        var handler = scope.ServiceProvider.GetRequiredService<BlacklistedPhoneNumberQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleCheckPhoneNumberBlacklistedQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<CheckPhoneNumberBlacklistedQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid CheckPhoneNumberBlacklistedQuery format");
        var handler = scope.ServiceProvider.GetRequiredService<BlacklistedPhoneNumberQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    // =========================================================================
    // SCRUM-904 — User Risk Score Query Handlers
    // =========================================================================

    private async Task<string> HandleGetLatestUserRiskScoreQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetLatestUserRiskScoreQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetLatestUserRiskScoreQuery format");
        var handler = scope.ServiceProvider.GetRequiredService<UserRiskScoreQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    // =========================================================================
    // ASPS-647 — Paged API Query Handlers (Angular Admin)
    // =========================================================================

    private static readonly JsonSerializerSettings _defaultSerializerSettings = new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.None,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
    };

    private async Task<string> HandleGetAllDevicesPagedQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetAllDevicesPagedQuery>(messageJson) ?? new GetAllDevicesPagedQuery();
        var handler = scope.ServiceProvider.GetRequiredService<AdminQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, _defaultSerializerSettings);
    }

    private async Task<string> HandleGetDeviceAlertsPagedQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetDeviceAlertsPagedQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetDeviceAlertsPagedQuery format");
        var handler = scope.ServiceProvider.GetRequiredService<AdminQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, _defaultSerializerSettings);
    }

    private async Task<string> HandleGetAllAlertsPagedQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetAllAlertsPagedQuery>(messageJson) ?? new GetAllAlertsPagedQuery();
        var handler = scope.ServiceProvider.GetRequiredService<AdminQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, _defaultSerializerSettings);
    }

    private async Task<string> HandleGetAlertDetailQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetAlertDetailQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetAlertDetailQuery format");
        var handler = scope.ServiceProvider.GetRequiredService<AdminQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, _defaultSerializerSettings);
    }

    private async Task<string> HandleGetAllAnalysisResultsPagedQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetAllAnalysisResultsPagedQuery>(messageJson) ?? new GetAllAnalysisResultsPagedQuery();
        var handler = scope.ServiceProvider.GetRequiredService<AdminQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, _defaultSerializerSettings);
    }

    private async Task<string> HandleGetAnalysisResultDetailQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetAnalysisResultDetailQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetAnalysisResultDetailQuery format");
        var handler = scope.ServiceProvider.GetRequiredService<AdminQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, _defaultSerializerSettings);
    }

    // =========================================================================
    // ASPS-649 — Simulations + Roadmaps paged query handlers
    // =========================================================================

    private async Task<string> HandleGetAllSimulationsPagedQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetAllSimulationsPagedQuery>(messageJson) ?? new GetAllSimulationsPagedQuery();
        var handler = scope.ServiceProvider.GetRequiredService<ASPS649QueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, _defaultSerializerSettings);
    }

    private async Task<string> HandleGetSimulationByKeyFieldQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetSimulationByKeyFieldQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetSimulationByKeyFieldQuery format");
        var handler = scope.ServiceProvider.GetRequiredService<ASPS649QueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, _defaultSerializerSettings);
    }

    private async Task<string> HandleGetAllRoadmapsPagedQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetAllRoadmapsPagedQuery>(messageJson) ?? new GetAllRoadmapsPagedQuery();
        var handler = scope.ServiceProvider.GetRequiredService<ASPS649QueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, _defaultSerializerSettings);
    }
}
