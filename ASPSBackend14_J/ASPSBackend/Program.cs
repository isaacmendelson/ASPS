using Business.Data.EF;
using Business.Data.EF.Repositories;
using Business.DomainEvents;
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

public class Program
{
    static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();

        var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0.1";
        Console.WriteLine("========================================");
        Console.WriteLine($"[INFO] ASPSBackend v{version} starting...");
        Console.WriteLine("========================================");

        // Auto-migrate database in development
        var env = host.Services.GetRequiredService<IHostEnvironment>();
        if (env.IsDevelopment())
        {
            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
            Console.WriteLine("✓ Database migrations applied");
        }

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

        // ASPS-620: Inject reconnect snapshot service
        var reconnectSnapshotService = host.Services.GetRequiredService<Business.Messaging.ReconnectSnapshotService>();
        alertListener.SetReconnectSnapshotService(reconnectSnapshotService);

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

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseDefaultServiceProvider((context, options) =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            })
            //.ConfigureAppConfiguration((ctx, config) =>
            //{
            //    // Always load appsettings.Development.json if it exists (local dev overrides).
            //    // This supplements the default env-based loading in case DOTNET_ENVIRONMENT
            //    // is not set (e.g., Visual Studio multi-startup projects).
            //    config.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
            //})
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

                var csb = new MySqlConnector.MySqlConnectionStringBuilder(connectionString);

                Console.WriteLine(
                    $"[CONFIG] DB server={csb.Server}, " +
                    $"database={csb.Database}, user={csb.UserID}");

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
                services.AddScoped<ISensitiveSiteRepository, SensitiveSiteRepository>();
                services.AddScoped<ITrackedDomainRepository, TrackedDomainRepository>();
                services.AddScoped<ISimulationRepository, SimulationRepository>();
                services.AddScoped<IBlacklistedPhoneNumberRepository, BlacklistedPhoneNumberRepository>(); // ASPS-282
                services.AddScoped<IBankWebsiteRepository, BankWebsiteRepository>(); // ASPS-297
                services.AddScoped<IWebsiteCategoryRepository, WebsiteCategoryRepository>(); // SCRUM-820
                services.AddScoped<IRoadmapRepository, RoadmapRepository>();
                services.AddScoped<IImmediateDangerRepository, ImmediateDangerRepository>();
                services.AddScoped<INotificationOutboxRepository, NotificationOutboxRepository>(); // ASPS-620

                // Add Handlers
                services.AddScoped<UserCommandHandlers>();
                services.AddScoped<UserQueryHandlers>();
                services.AddScoped<AdminCommandHandlers>();
                services.AddScoped<AdminQueryHandlers>();
                services.AddScoped<SystemCommandHandlers>();
                services.AddScoped<SimulationQueryHandlers>();
                services.AddScoped<SimulationCommandHandlers>();
                services.AddScoped<BlacklistedPhoneNumberQueryHandlers>(); // ASPS-282
                services.AddScoped<BankWebsiteQueryHandlers>(); // ASPS-297
                services.AddScoped<WebsiteCategoryQueryHandlers>(); // SCRUM-822
                services.AddScoped<WebsiteCategoryCommandHandlers>(); // SCRUM-822
                services.AddScoped<RoadmapQueryHandlers>();
                services.AddScoped<RoadmapCommandHandlers>();
                services.AddScoped<TrackedDomainCommandHandlers>(); // ASPS-371

                services.AddScoped<UserDeviceCommandHandlers>();

                // Add Views
                services.AddSingleton<ASView>();
                services.AddSingleton<IDomainEventHandler>(sp => sp.GetRequiredService<ASView>());

                // Add Token Store and CurveZMQ Key Manager
                services.AddSingleton<TokenStore>();
                services.AddSingleton<CurveKeyManager>();
                services.AddSingleton(sp =>
                {
                    var configuration = sp.GetRequiredService<IConfiguration>();
                    var allowedCommands = configuration.GetSection("CQRS:AllowedCommands").Get<string[]>()
                        ?? CqrsChannelSecurityOptions.GatewayCommandTypes;
                    return new CqrsChannelSecurity(new CqrsChannelSecurityOptions
                    {
                        SharedSecret = configuration["CQRS:SharedSecret"] ?? string.Empty,
                        AllowedClientIds = new HashSet<string>(
                            configuration.GetSection("CQRS:AllowedClientIds").Get<string[]>()
                                ?? new[] { "asps-webapi" },
                            StringComparer.Ordinal),
                        AllowedCommands = new HashSet<string>(allowedCommands, StringComparer.Ordinal)
                    });
                });
                services.AddSingleton(sp =>
                {
                    var configuration = sp.GetRequiredService<IConfiguration>();
                    return new CQRSGateway(
                        sp,
                        sp.GetRequiredService<ILogger<CQRSGateway>>(),
                        configuration["CQRS:BindEndpoint"] ?? "tcp://127.0.0.1:5556",
                        sp.GetRequiredService<CurveKeyManager>(),
                        sp.GetRequiredService<CqrsChannelSecurity>());
                });

                // Add Notification Publisher
                services.AddSingleton<Business.Messaging.NotificationPublisher>();

                // ASPS-620: Outbox-aware publisher + reconnect snapshot service
                services.AddSingleton<Business.Messaging.OutboxNotificationPublisher>();
                services.AddSingleton<Business.Messaging.ReconnectSnapshotService>();

                // Add Event Handlers for Analysis Results
                services.AddSingleton<IDomainEventHandler, AlertPersistenceActor>();
                services.AddSingleton<IDomainEventHandler, AnalysisPersistenceActor>();
                services.AddSingleton<IDomainEventHandler, ImmediateDangerPersistanceActor>();
                services.AddSingleton<IDomainEventHandler, NotificationPublisherActor>();

                // SCRUM-904 — User Risk Score orchestration. Singleton so the
                // per-user throttle state survives across alerts; resolved as
                // an IDomainEventHandler so it receives DeviceAlertReceived
                // events alongside the other actors.
                services.AddSingleton<Business.Services.UserRiskScoreService>();
                services.AddSingleton<IDomainEventHandler>(sp =>
                    sp.GetRequiredService<Business.Services.UserRiskScoreService>());

                services.AddScoped<IDomainEventsContext>(_ =>
                    new DomainEventsContext(new Common.Models.Key("Tenant", "system")));

                // Add UserDomainManagerService (manages per-user analysis instances)
                services.AddSingleton(sp => new UserDomainManagerService(
                    sp.GetRequiredService<ILoggerFactory>(),
                    sp.GetRequiredService<IConfiguration>(),
                    sp.GetRequiredService<ASView>(),
                    sp.GetServices<IDomainEventHandler>(),
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    sp.GetRequiredService<Business.Messaging.OutboxNotificationPublisher>()));

                // Add SimulationRunner (background service for simulations)
                // Register as Singleton first so it can be injected into handlers
                services.AddSingleton<SimulationRunner>();
                services.AddHostedService(provider => provider.GetRequiredService<SimulationRunner>());

                // ASPS-620 Major-3: Periodic outbox prune (hourly, 7-day retention)
                services.AddHostedService<Business.Services.OutboxPruningService>();

                // Add Messaging
                services.AddSingleton(sp => new NetMQMessageProcessor(
                    sp.GetRequiredService<ILogger<NetMQMessageProcessor>>(),
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    configuration["NetMQ:BusinessEndpoint"] ?? "tcp://*:5555",
                    sp.GetService<CurveKeyManager>()));
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

                    return new RealTimeAlertListener(
                        loggerFactory, sp, asView, userDomainService, tokenStore,
                        curveKeyManager, port, mode, configuration);
                });


                foreach (var descriptor in services
                   .Where(d => d.ServiceType == typeof(NetMQMessageProcessor)))
                    {
                        Console.WriteLine("========================================");
                        Console.WriteLine("NetMQMessageProcessor DI registration:");
                        Console.WriteLine($"Lifetime: {descriptor.Lifetime}");
                        Console.WriteLine($"ImplementationType: {descriptor.ImplementationType}");
                        Console.WriteLine($"ImplementationFactory: {descriptor.ImplementationFactory != null}");
                        Console.WriteLine($"ImplementationInstance: {descriptor.ImplementationInstance != null}");
                        Console.WriteLine("========================================");
                    }
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
