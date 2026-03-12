using Business.Data.EF;
using Business.Data.EF.Repositories;
using Business.Handlers;
using Business.Messaging;
using Business.RealtimeAnalysis;
using Business.RealtimeAnalysis.UserDomain;
using Business.Services;
using Business.Views;
using Common.Interfaces;
using Interface.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ASPSBackend;

class Program
{
    static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();

        Console.WriteLine("========================================");
        Console.WriteLine("ASPSBackend System2 Starting...");
        Console.WriteLine("========================================");

        // Load persisted tokens from database
        var tokenStore = host.Services.GetRequiredService<TokenStore>();
        await tokenStore.LoadFromDatabaseAsync();

        // Start ASView
        var asView = host.Services.GetRequiredService<ASView>();
        asView.Start();

        // Start NetMQ CQRS Message Processor
        var messageProcessor = host.Services.GetRequiredService<NetMQMessageProcessor>();
        messageProcessor.Start();

        // Start Real-Time Alert Listener
        var alertListener = host.Services.GetRequiredService<RealTimeAlertListener>();
        
        // Initialize UDAnalysisManagers for active users
        await InitializeAnalysisManagersAsync(host.Services, alertListener);
        
        alertListener.Start();

        // Start CQRS Gateway (for WebApi Commands/Queries)
        var cqrsGateway = host.Services.GetRequiredService<CQRSGateway>();
        cqrsGateway.Start();

        // Get configuration to display mode
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        var listenerMode = configuration.GetValue<string>("NetMQ:RealTimeListenerMode", "Router");
        var listenerPort = configuration.GetValue<int>("NetMQ:RealTimeListenerPort", 50001);

        Console.WriteLine("========================================");
        Console.WriteLine("✓ ASView started");
        Console.WriteLine("✓ TokenStore started");
        Console.WriteLine("✓ NetMQ CQRS processor started (tcp://*:5555)");
        Console.WriteLine($"✓ Real-time alert listener started (tcp://*:{listenerPort}, Mode: {listenerMode})");
        Console.WriteLine("✓ CQRS Gateway started (tcp://*:5556) - Listening for WebApi Commands/Queries");
        Console.WriteLine("✓ UDAnalysisManagers initialized");
        Console.WriteLine("========================================");
        Console.WriteLine("ASPSBackend is running. Press Ctrl+C to exit.");
        Console.WriteLine("========================================");

        await host.RunAsync();
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseDefaultServiceProvider((context, options) =>
            {
                // DI scope validation is disabled — singleton/scoped mismatches
                // exist in this codebase and are handled manually via IServiceScopeFactory.
                // TODO: refactor singletons that consume scoped services.
                options.ValidateScopes = false;
                options.ValidateOnBuild = false;
            })
            .ConfigureAppConfiguration((ctx, config) =>
            {
                // Always load appsettings.Development.json if it exists (local dev overrides).
                // This supplements the default env-based loading in case DOTNET_ENVIRONMENT
                // is not set (e.g., Visual Studio multi-startup projects).
                config.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
            })
            .ConfigureServices((hostContext, services) =>
            {
                var configuration = hostContext.Configuration;

                // Add DbContext with MySQL
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrWhiteSpace(connectionString))
                    throw new InvalidOperationException(
                        "ConnectionStrings:DefaultConnection is empty. " +
                        "Create appsettings.Development.json with your DB connection string, " +
                        "or set ASPNETCORE_ENVIRONMENT=Development so the file is loaded. " +
                        "See appsettings.Example.json for the required format.");

                var serverVersion = new MySqlServerVersion(new Version(8, 0, 44));
                var isDevelopment = hostContext.HostingEnvironment.IsDevelopment();
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseMySql(connectionString, serverVersion);
                    if (isDevelopment)
                    {
                        // EnableSensitiveDataLogging only in development — never in production
                        options.LogTo(Console.WriteLine, LogLevel.Information)
                               .EnableSensitiveDataLogging()
                               .EnableDetailedErrors();
                    }
                });

                // Add Repositories
                services.AddScoped<IUserRepository, UserRepository>();
                services.AddScoped<IUserDeviceRepository, UserDeviceRepository>();
                services.AddScoped<IUserAccountRepository, UserAccountRepository>();
                services.AddScoped<IAnalysisResultRepository, AnalysisResultRepository>();
                services.AddScoped<IDeviceAlertRepository, DeviceAlertRepository>();
                services.AddScoped<ITrackUrlAlertRepository, TrackUrlAlertRepository>();
                services.AddScoped<IAlertFlagRepository, AlertFlagRepository>();
                services.AddScoped<IKnownPhishingWebsiteRepository, KnownPhishingWebsiteRepository>();
                services.AddScoped<ISafeDomainRepository, SafeDomainRepository>();

                // Add Handlers
                services.AddScoped<UserCommandHandlers>();
                services.AddScoped<UserQueryHandlers>();
                services.AddScoped<AdminCommandHandlers>();
                services.AddScoped<AdminQueryHandlers>();

                // Add CQRS Gateway (listens for Commands/Queries from WebApi)
                services.AddSingleton<CQRSGateway>();
                services.AddScoped<UserDeviceCommandHandlers>();

                // Add Views
                services.AddSingleton<ASView>();
                services.AddSingleton<IDomainEventHandler>(sp => sp.GetRequiredService<ASView>());

                // Add Token Store and CurveZMQ Key Manager
                services.AddSingleton<TokenStore>();
                services.AddSingleton<CurveKeyManager>();

                // Add Notification Publisher
                services.AddSingleton<Business.Messaging.NotificationPublisher>();

                // Add Event Handlers for Analysis Results
                services.AddSingleton<IDomainEventHandler, AlertPersistenceActor>();
                services.AddSingleton<IDomainEventHandler, AnalysisPersistenceActor>();
                services.AddSingleton<IDomainEventHandler, NotificationPublisherActor>();

                // Add UserDomainManagerService (manages per-user analysis instances)
                services.AddSingleton<UserDomainManagerService>();

                // Add Messaging
                services.AddSingleton<NetMQMessageProcessor>();
                services.AddSingleton<RealTimeAlertListener>(sp =>
                {
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    var asView = sp.GetRequiredService<ASView>();
                    var userDomainService = sp.GetRequiredService<UserDomainManagerService>();
                    var tokenStore = sp.GetRequiredService<TokenStore>();
                    var curveKeyManager = sp.GetRequiredService<CurveKeyManager>();
                    var configuration = sp.GetRequiredService<IConfiguration>();
                    var port = configuration.GetValue<int>("NetMQ:RealTimeListenerPort", 50001);

                    // Read socket mode from configuration (default to Router for concurrent two-way communication)
                    var modeString = configuration.GetValue<string>("NetMQ:RealTimeListenerMode", "Router");
                    var mode = Enum.TryParse<SocketMode>(modeString, true, out var parsedMode)
                        ? parsedMode
                        : SocketMode.Router;

                    return new RealTimeAlertListener(loggerFactory, sp, asView, userDomainService, tokenStore, curveKeyManager, port, mode);
                });

                // Add Logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            });

    static async Task InitializeAnalysisManagersAsync(IServiceProvider services, RealTimeAlertListener alertListener)
    {
        using var scope = services.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var userDomainService = services.GetRequiredService<UserDomainManagerService>();

        try
        {
            var activeUsers = await userRepository.GetActiveUsersAsync();

            if (!activeUsers.Any())
            {
                Console.WriteLine("  → No active users found. UDAnalysisManagers will be created when users connect.");
                return;
            }

            foreach (var user in activeUsers)
            {
                var userKey = new Common.Models.Key("User", user.KeyField);
                var manager = userDomainService.GetOrCreateManagerForUser(userKey);
                
                Console.WriteLine($"  → UDAnalysisManager initialized for user: {user.FirstName} {user.LastName}");
            }
            
            Console.WriteLine($"  → Total managers initialized: {userDomainService.GetActiveManagerCount()}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Warning: Could not initialize analysis managers: {ex.Message}");
            Console.WriteLine($"  → System will continue. Managers will be created on-demand.");
        }
    }
}
