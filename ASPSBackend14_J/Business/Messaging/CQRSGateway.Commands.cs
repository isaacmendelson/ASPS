using Business.Commands;
using Business.Handlers;
using Newtonsoft.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Business.Messaging;

/// <summary>
/// CQRS Gateway — command dispatch and command handler methods.
/// </summary>
public partial class CQRSGateway
{
    private async Task<string> ProcessCommandAsync(string messageJson, Newtonsoft.Json.Linq.JObject jObject, string clientId)
    {
        using var scope = _serviceProvider.CreateScope();

        var commandType = jObject["CommandType"]?.ToString();

        if (string.IsNullOrEmpty(commandType))
        {
            return CreateErrorResponse("CommandType field is missing or empty");
        }
        if (_channelSecurity?.IsCommandAuthorized(commandType) != true)
        {
            _logger.LogWarning("Client {ClientId} is not authorized for command {CommandType}", clientId, commandType);
            return CreateErrorResponse($"Command is not authorized: {commandType}");
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

    // =========================================================================
    // Admin Command Handlers
    // =========================================================================

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

    private async Task<string> HandleReInitializeASViewCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<ReInitializeASViewCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid ReInitializeASViewCommand format");

        var handler = scope.ServiceProvider.GetRequiredService<SystemCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    // =========================================================================
    // Simulation Command Handlers
    // =========================================================================

    private async Task<string> HandleCreateSimulationCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<CreateSimulationCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid CreateSimulationCommand format");

        var handler = scope.ServiceProvider.GetRequiredService<SimulationCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
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
            TypeNameHandling = TypeNameHandling.None,
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
            TypeNameHandling = TypeNameHandling.None,
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
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    // =========================================================================
    // Website Category Command Handlers (SCRUM-822)
    // =========================================================================

    private async Task<string> HandleCreateWebsiteCategoryCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<CreateWebsiteCategoryCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid CreateWebsiteCategoryCommand format");

        var handler = scope.ServiceProvider.GetRequiredService<WebsiteCategoryCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
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
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    // =========================================================================
    // Tracked Domain Command Handlers (ASPS-371)
    // =========================================================================

    private async Task<string> HandleAddTrackedDomainCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<AddTrackedDomainCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid AddTrackedDomainCommand format");

        var handler = scope.ServiceProvider.GetRequiredService<TrackedDomainCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
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
            TypeNameHandling = TypeNameHandling.None,
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
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    // =========================================================================
    // Roadmap Command Handlers
    // =========================================================================

    private async Task<string> HandleCreateRoadmapCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<CreateRoadmapCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid CreateRoadmapCommand format");
        var handler = scope.ServiceProvider.GetRequiredService<RoadmapCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
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
            TypeNameHandling = TypeNameHandling.None,
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
            TypeNameHandling = TypeNameHandling.None,
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
            TypeNameHandling = TypeNameHandling.None,
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
            TypeNameHandling = TypeNameHandling.None,
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

    // =========================================================================
    // User Device Command Handlers
    // =========================================================================

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

    private async Task<string> HandleUpdateUserDeviceCommand(string messageJson, IServiceScope scope)
    {
        var command = JsonConvert.DeserializeObject<UpdateUserDeviceCommand>(messageJson);
        if (command == null) return CreateErrorResponse("Invalid UpdateUserDeviceCommand format");
        var handler = scope.ServiceProvider.GetRequiredService<UserDeviceCommandHandlers>();
        var result = await handler.HandleAsync(command);
        return JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
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
            TypeNameHandling = TypeNameHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }
}
