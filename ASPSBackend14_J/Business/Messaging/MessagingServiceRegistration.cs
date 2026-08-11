using Business.Commands;
using Business.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Business.Messaging;

/// <summary>
/// Centralized DI registration for the messaging subsystem.
/// Call AddMessagingServices() to register all messaging abstractions and their implementations.
/// </summary>
public static class MessagingServiceRegistration
{
    /// <summary>
    /// Registers messaging services with the DI container.
    /// Phase 1: Registers the CqrsHandlerRegistry with all 21 command handlers (ASPS-679)
    /// and all 50 query handlers (ASPS-680).
    /// Future phases will register IAlertIngress, INotificationEgress, ICqrsTransport implementations.
    /// </summary>
    public static IServiceCollection AddMessagingServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Handler registry (singleton — static dispatch map built at startup)
        var registry = new CqrsHandlerRegistry();
        RegisterCommandHandlers(registry);
        CqrsQueryRegistration.RegisterQueryHandlers(registry);

        services.AddSingleton(registry);

        return services;
    }

    /// <summary>
    /// Registers all command types with their handlers. Mirrors the switch dispatch in
    /// CQRSGateway.Commands.cs (ASPS-679) — that file is left untouched pending cutover.
    /// </summary>
    private static void RegisterCommandHandlers(CqrsHandlerRegistry registry)
    {
        // Admin Commands
        registry.RegisterCommand<CreateUserAdminCommand, AdminCommandHandlers>("CreateUserAdminCommand");

        // System Commands
        registry.RegisterCommand<ReInitializeASViewCommand, SystemCommandHandlers>("ReInitializeASViewCommand");

        // Simulation Commands
        registry.RegisterCommand<CreateSimulationCommand, SimulationCommandHandlers>("CreateSimulationCommand");
        registry.RegisterCommand<UpdateSimulationCommand, SimulationCommandHandlers>("UpdateSimulationCommand");
        registry.RegisterCommand<DeleteSimulationCommand, SimulationCommandHandlers>("DeleteSimulationCommand");
        registry.RegisterCommand<RunSimulationCommand, SimulationCommandHandlers>("RunSimulationCommand");

        // Website Category Commands (SCRUM-822)
        registry.RegisterCommand<CreateWebsiteCategoryCommand, WebsiteCategoryCommandHandlers>("CreateWebsiteCategoryCommand");
        registry.RegisterCommand<UpdateWebsiteCategoryCommand, WebsiteCategoryCommandHandlers>("UpdateWebsiteCategoryCommand");

        // Tracked Domain Commands (ASPS-371)
        registry.RegisterCommand<AddTrackedDomainCommand, TrackedDomainCommandHandlers>("AddTrackedDomainCommand");
        registry.RegisterCommand<UpdateTrackedDomainCommand, TrackedDomainCommandHandlers>("UpdateTrackedDomainCommand");
        registry.RegisterCommand<DeleteTrackedDomainCommand, TrackedDomainCommandHandlers>("DeleteTrackedDomainCommand");

        // Roadmap Commands
        registry.RegisterCommand<CreateRoadmapCommand, RoadmapCommandHandlers>("CreateRoadmapCommand");
        registry.RegisterCommand<SaveRoadmapCommand, RoadmapCommandHandlers>("SaveRoadmapCommand");
        registry.RegisterCommand<UpdateRoadmapMetadataCommand, RoadmapCommandHandlers>("UpdateRoadmapMetadataCommand");
        registry.RegisterCommand<ArchiveRoadmapCommand, RoadmapCommandHandlers>("ArchiveRoadmapCommand");

        // User Commands
        registry.RegisterCommand<CreateUserCommand, UserCommandHandlers>("CreateUserCommand");
        registry.RegisterCommand<UpdateUserCommand, UserCommandHandlers>("UpdateUserCommand");
        registry.RegisterCommand<DeleteUserCommand, UserCommandHandlers>("DeleteUserCommand");

        // User Device Commands
        registry.RegisterCommand<CreateUserDeviceCommand, UserDeviceCommandHandlers>("CreateUserDeviceCommand");
        registry.RegisterCommand<UpdateUserDeviceCommand, UserDeviceCommandHandlers>("UpdateUserDeviceCommand");
        registry.RegisterCommand<DeleteUserDeviceCommand, UserDeviceCommandHandlers>("DeleteUserDeviceCommand");
    }
}
