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

    // Command Handlers
    private async Task<string> HandleCreateUserAdminCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<CreateUserAdminCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid CreateUserAdminCommand format");

        var handler = scope.ServiceProvider.GetRequiredService<AdminCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
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
            TypeNameHandling = TypeNameHandling.None,
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
            TypeNameHandling = TypeNameHandling.None,
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
}
