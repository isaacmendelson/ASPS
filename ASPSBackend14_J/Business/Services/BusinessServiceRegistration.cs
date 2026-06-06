using Business.Commands;
using Business.Queries;
using Business.Handlers;
using Business.Data.EF;
using Business.Data.EF.Repositories;
using Interface.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Business.Services;

/// <summary>
/// Service to register Business layer dependencies in WebApi
/// This keeps all database configuration in Business layer
/// </summary>
public static class BusinessServiceRegistration
{
    public static IServiceCollection AddBusinessServices(
        this IServiceCollection services, 
        string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string is required. " +
                "Pass the connection string from ASPSBackend configuration.");
        }

        // Register DbContext (Business layer only)
        services.AddDbContext<AppDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

        // Register Repositories (Business layer only)
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserDeviceRepository, UserDeviceRepository>();
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IDeviceAlertRepository, DeviceAlertRepository>();
        services.AddScoped<IAnalysisResultRepository, AnalysisResultRepository>();
        services.AddScoped<IKnownPhishingWebsiteRepository, KnownPhishingWebsiteRepository>();
        services.AddScoped<ISimulationRepository, SimulationRepository>();

        // SCRUM-904 — consent service for User Risk Score data sources
        services.AddScoped<ConsentService>();
        // SCRUM-904 Step B — persisted per-user URS weight set
        services.AddScoped<IUserRiskProfileRepository, UserRiskProfileRepository>();
        // SCRUM-904 Step B — signal aggregators + URS calculator
        services.AddScoped<Business.RealtimeAnalysis.UserDomain.SignalAggregators.BehaviorAggregator>();
        services.AddScoped<Business.RealtimeAnalysis.UserDomain.SignalAggregators.LiveThreatAggregator>();
        services.AddScoped<Business.RealtimeAnalysis.UserDomain.SignalAggregators.InboundVectorAggregator>();
        services.AddScoped<Business.RealtimeAnalysis.UserDomain.SignalAggregators.CrossModalCorrelationAggregator>();
        services.AddScoped<Business.RealtimeAnalysis.UserDomain.UserRiskScoreCalculator>();
        // SCRUM-904 — URS query handlers (CQRS path for the admin UI)
        services.AddScoped<Business.Handlers.UserRiskScoreQueryHandlers>();

        // Register Background Services
        services.AddSingleton<SimulationRunner>();
        services.AddHostedService(provider => provider.GetRequiredService<SimulationRunner>());

        // Register Command and Query Handlers (These are what WebApi uses)
        services.AddScoped<UserCommandHandlers>();
        services.AddScoped<UserQueryHandlers>();
        services.AddScoped<AdminCommandHandlers>();
        services.AddScoped<AdminQueryHandlers>();
        services.AddScoped<SimulationCommandHandlers>();
        services.AddScoped<SimulationQueryHandlers>();

        Console.WriteLine("✓ Business layer services registered");
        Console.WriteLine($"✓ Database: {GetDatabaseName(connectionString)}");

        return services;
    }

    private static string GetDatabaseName(string connectionString)
    {
        try
        {
            var parts = connectionString.Split(';');
            var dbPart = parts.FirstOrDefault(p => p.Trim().StartsWith("database=", StringComparison.OrdinalIgnoreCase));
            if (dbPart != null)
            {
                return dbPart.Split('=')[1].Trim();
            }
        }
        catch { }
        return "Unknown";
    }
}
