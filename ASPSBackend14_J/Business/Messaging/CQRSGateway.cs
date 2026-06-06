using Business.Commands;
using Business.Queries;
using Business.Handlers;
using Business.Services;
using Business.Views;
using Common.Messaging;
using Common.Models;
using Interface.Repositories;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Business.Messaging;

/// <summary>
/// CQRS Gateway - Listens for Commands/Queries via NetMQ
/// Processes them with handlers and sends results back
/// This runs in the ASPSBackend process, NOT in WebApi
/// </summary>
public class CQRSGateway : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CQRSGateway> _logger;
    private readonly CurveKeyManager? _curveKeyManager;
    private readonly string _endpoint;
    private ResponseSocket? _socket;
    private bool _running;

    public CQRSGateway(
        IServiceProvider serviceProvider,
        ILogger<CQRSGateway> logger,
        string endpoint = "tcp://*:5556",
        CurveKeyManager? curveKeyManager = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _endpoint = endpoint;
        _curveKeyManager = curveKeyManager;
    }

    public void Start()
    {
        _running = true;
        _socket = new ResponseSocket();
        _socket.Options.Linger = TimeSpan.Zero;
        // No CURVE on internal localhost channel — encryption is on external ports 50001/50002
        _socket.Bind(_endpoint);

        _logger.LogInformation("CQRS Gateway started on {Endpoint} (internal channel, no CURVE)", _endpoint);
        _logger.LogInformation("Listening for Commands and Queries from WebApi...");

        Task.Run(() => ListenLoop());
    }

    private async Task ListenLoop()
    {
        while (_running && _socket != null)
        {
            try
            {
                // Receive message from WebApi
                var messageJson = _socket.ReceiveFrameString();
                _logger.LogInformation("Received CQRS message ({Length} chars)", messageJson.Length);

                // Process message
                var response = await ProcessMessageAsync(messageJson);

                // Send response back to WebApi
                _socket.SendFrame(response);
                _logger.LogInformation("Sent CQRS response ({Length} chars)", response.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CQRS Gateway loop");
                
                if (_socket != null)
                {
                    var errorResponse = JsonConvert.SerializeObject(new
                    {
                        Success = false,
                        Message = $"Gateway error: {ex.Message}"
                    });
                    _socket.SendFrame(errorResponse);
                }
            }
        }
    }

    private async Task<string> ProcessMessageAsync(string messageJson)
    {
        try
        {
            // Parse JSON to determine message type
            var jObject = JObject.Parse(messageJson);
            
            var messageType = jObject["MessageType"]?.ToString();
            
            if (string.IsNullOrEmpty(messageType))
            {
                return CreateErrorResponse("MessageType field is missing or empty");
            }

            _logger.LogInformation("Processing {MessageType} message", messageType);

            // Route to appropriate handler based on message type
            return messageType switch
            {
                "Query" => await ProcessQueryAsync(messageJson, jObject),
                "Command" => await ProcessCommandAsync(messageJson, jObject),
                _ => CreateErrorResponse($"Unknown message type: {messageType}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            return CreateErrorResponse($"Processing error: {ex.Message}");
        }
    }

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
            _ => CreateErrorResponse($"Unknown query type: {queryType}")
        };
    }

    private async Task<string> ProcessCommandAsync(string messageJson, JObject jObject)
    {
        using var scope = _serviceProvider.CreateScope();
        
        var commandType = jObject["CommandType"]?.ToString();

        if (string.IsNullOrEmpty(commandType))
        {
            return CreateErrorResponse("CommandType field is missing or empty");
        }

        _logger.LogInformation("Handling command: {CommandType}", commandType);

        return commandType switch
        {
            "CreateUserAdminCommand" => await HandleCreateUserAdminCommand(messageJson, scope),
            "CreateUserDeviceCommand" => await HandleCreateUserDeviceCommand(messageJson, scope),
            "DeleteUserCommand" => await HandleDeleteUserCommand(messageJson, scope),
            "ReInitializeASViewCommand" => await HandleReInitializeASViewCommand(messageJson, scope),
            // Simulation Commands
            "CreateSimulationCommand" => await HandleCreateSimulationCommand(messageJson, scope),
            "UpdateSimulationCommand" => await HandleUpdateSimulationCommand(messageJson, scope),
            "DeleteSimulationCommand" => await HandleDeleteSimulationCommand(messageJson, scope),
            "RunSimulationCommand" => await HandleRunSimulationCommand(messageJson, scope),
            // Website Category Commands (SCRUM-822)
            "CreateWebsiteCategoryCommand" => await HandleCreateWebsiteCategoryCommand(messageJson, scope),
            "UpdateWebsiteCategoryCommand" => await HandleUpdateWebsiteCategoryCommand(messageJson, scope),
            // Tracked Domain Commands (ASPS-371)
            "AddTrackedDomainCommand" => await HandleAddTrackedDomainCommand(messageJson, scope),
            "UpdateTrackedDomainCommand" => await HandleUpdateTrackedDomainCommand(messageJson, scope),
            "DeleteTrackedDomainCommand" => await HandleDeleteTrackedDomainCommand(messageJson, scope),
            // Roadmap Commands
            "CreateRoadmapCommand" => await HandleCreateRoadmapCommand(messageJson, scope),
            "SaveRoadmapCommand" => await HandleSaveRoadmapCommand(messageJson, scope),
            "UpdateRoadmapMetadataCommand" => await HandleUpdateRoadmapMetadataCommand(messageJson, scope),
            "ArchiveRoadmapCommand" => await HandleArchiveRoadmapCommand(messageJson, scope),
            // User Commands
            "CreateUserCommand" => await HandleCreateUserCommand(messageJson, scope),
            "UpdateUserCommand" => await HandleUpdateUserCommand(messageJson, scope),
            // User Device Commands
            "UpdateUserDeviceCommand" => await HandleUpdateUserDeviceCommand(messageJson, scope),
            "DeleteUserDeviceCommand" => await HandleDeleteUserDeviceCommand(messageJson, scope),
            _ => CreateErrorResponse($"Unknown command type: {commandType}")
        };
    }

    // Query Handlers
    private async Task<string> HandleGetDashboardStatsQuery(IServiceScope scope)
    {
        var handler = scope.ServiceProvider.GetRequiredService<AdminQueryHandlers>();
        var result = await handler.HandleAsync(new GetDashboardStatsQuery());
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleGetUserByKeyQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetUserByKeyQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetUserByKeyQuery format");

        var handler = scope.ServiceProvider.GetRequiredService<UserQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    // Command Handlers
    private async Task<string> HandleCreateUserAdminCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<CreateUserAdminCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid CreateUserAdminCommand format");

        var handler = scope.ServiceProvider.GetRequiredService<AdminCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleCreateUserDeviceCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<CreateUserDeviceCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid CreateUserDeviceCommand format");

        var handler = scope.ServiceProvider.GetRequiredService<UserDeviceCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleDeleteUserCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<DeleteUserCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid DeleteUserCommand format");

        var handler = scope.ServiceProvider.GetRequiredService<UserCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleReInitializeASViewCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<ReInitializeASViewCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid ReInitializeASViewCommand format");

        var handler = scope.ServiceProvider.GetRequiredService<SystemCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private string CreateErrorResponse(string message)
    {
        return JsonConvert.SerializeObject(new
        {
            Success = false,
            Message = message
        });
    }

    public void Stop()
    {
        _running = false;
        _socket?.Dispose();
        _socket = null;
        _logger.LogInformation("CQRS Gateway stopped");
    }

    public void Dispose()
    {
        Stop();
    }

    private async Task<string> HandleGetDeviceByKeyQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetDeviceByKeyQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetDeviceByKeyQuery format");

        var handler = scope.ServiceProvider.GetRequiredService<AdminQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
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
                TypeNameHandling = TypeNameHandling.Auto,
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
                TypeNameHandling = TypeNameHandling.Auto,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting version");
            return CreateErrorResponse($"Error: {ex.Message}");
        }
    }

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
                TypeNameHandling = TypeNameHandling.Auto,
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
                TypeNameHandling = TypeNameHandling.Auto,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tracked domains");
            return CreateErrorResponse($"Error: {ex.Message}");
        }
    }

    // Simulation Query Handlers
    private async Task<string> HandleGetSimulationsQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetSimulationsQuery>(messageJson) ?? new GetSimulationsQuery();
        var handler = scope.ServiceProvider.GetRequiredService<SimulationQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    // Simulation Command Handlers
    private async Task<string> HandleCreateSimulationCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<CreateSimulationCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid CreateSimulationCommand format");

        var handler = scope.ServiceProvider.GetRequiredService<SimulationCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleUpdateSimulationCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<UpdateSimulationCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid UpdateSimulationCommand format");

        var handler = scope.ServiceProvider.GetRequiredService<SimulationCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleDeleteSimulationCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<DeleteSimulationCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid DeleteSimulationCommand format");

        var handler = scope.ServiceProvider.GetRequiredService<SimulationCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleRunSimulationCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<RunSimulationCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid RunSimulationCommand format");

        var handler = scope.ServiceProvider.GetRequiredService<SimulationCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    // Website Category Query Handlers (SCRUM-822)
    private async Task<string> HandleGetAllWebsiteCategoriesQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetAllWebsiteCategoriesQuery>(messageJson) ?? new GetAllWebsiteCategoriesQuery();
        var handler = scope.ServiceProvider.GetRequiredService<WebsiteCategoryQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    // Tracked Domain Command Handlers (ASPS-371)
    private async Task<string> HandleAddTrackedDomainCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<AddTrackedDomainCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid AddTrackedDomainCommand format");

        var handler = scope.ServiceProvider.GetRequiredService<TrackedDomainCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleUpdateTrackedDomainCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<UpdateTrackedDomainCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid UpdateTrackedDomainCommand format");

        var handler = scope.ServiceProvider.GetRequiredService<TrackedDomainCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleDeleteTrackedDomainCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<DeleteTrackedDomainCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid DeleteTrackedDomainCommand format");

        var handler = scope.ServiceProvider.GetRequiredService<TrackedDomainCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    // Website Category Command Handlers (SCRUM-822)
    private async Task<string> HandleCreateWebsiteCategoryCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<CreateWebsiteCategoryCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid CreateWebsiteCategoryCommand format");

        var handler = scope.ServiceProvider.GetRequiredService<WebsiteCategoryCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleUpdateWebsiteCategoryCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<UpdateWebsiteCategoryCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid UpdateWebsiteCategoryCommand format");

        var handler = scope.ServiceProvider.GetRequiredService<WebsiteCategoryCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    // =========================================================================
    // Roadmap dispatch handlers
    // =========================================================================
    private async Task<string> HandleGetRoadmapByIdQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetRoadmapByIdQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetRoadmapByIdQuery format");
        var handler = scope.ServiceProvider.GetRequiredService<RoadmapQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleCreateRoadmapCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<CreateRoadmapCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid CreateRoadmapCommand format");
        var handler = scope.ServiceProvider.GetRequiredService<RoadmapCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleSaveRoadmapCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<SaveRoadmapCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid SaveRoadmapCommand format");
        var handler = scope.ServiceProvider.GetRequiredService<RoadmapCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleUpdateRoadmapMetadataCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<UpdateRoadmapMetadataCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid UpdateRoadmapMetadataCommand format");
        var handler = scope.ServiceProvider.GetRequiredService<RoadmapCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleArchiveRoadmapCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<ArchiveRoadmapCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid ArchiveRoadmapCommand format");
        var handler = scope.ServiceProvider.GetRequiredService<RoadmapCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    // =========================================================================
    // User Query Handlers
    // =========================================================================
    private async Task<string> HandleGetAllUsersQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetAllUsersQuery>(messageJson) ?? new GetAllUsersQuery();
        var handler = scope.ServiceProvider.GetRequiredService<UserQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    // =========================================================================
    // User Command Handlers
    // =========================================================================
    private async Task<string> HandleCreateUserCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<CreateUserCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid CreateUserCommand format");
        var handler = scope.ServiceProvider.GetRequiredService<UserCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleUpdateUserCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<UpdateUserCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid UpdateUserCommand format");
        var handler = scope.ServiceProvider.GetRequiredService<UserCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    // =========================================================================
    // User Device Command Handlers
    // =========================================================================
    private async Task<string> HandleUpdateUserDeviceCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<UpdateUserDeviceCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid UpdateUserDeviceCommand format");
        var handler = scope.ServiceProvider.GetRequiredService<UserDeviceCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private async Task<string> HandleDeleteUserDeviceCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<DeleteUserDeviceCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid DeleteUserDeviceCommand format");
        var handler = scope.ServiceProvider.GetRequiredService<UserDeviceCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    // =========================================================================
    // SCRUM-904 — User Risk Score query handlers
    // =========================================================================
    private async Task<string> HandleGetLatestUserRiskScoreQuery(string messageJson, IServiceScope scope)
    {
        var query = JsonConvert.DeserializeObject<GetLatestUserRiskScoreQuery>(messageJson);
        if (query == null) return CreateErrorResponse("Invalid GetLatestUserRiskScoreQuery format");
        var handler = scope.ServiceProvider.GetRequiredService<UserRiskScoreQueryHandlers>();
        var result = await handler.HandleAsync(query);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }
}
