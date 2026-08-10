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
    /// Phase 0: Only registers the CqrsHandlerRegistry.
    /// Future phases will register IAlertIngress, INotificationEgress, ICqrsTransport implementations.
    /// </summary>
    public static IServiceCollection AddMessagingServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Handler registry (singleton — static dispatch map built at startup)
        services.AddSingleton<CqrsHandlerRegistry>();

        return services;
    }
}
