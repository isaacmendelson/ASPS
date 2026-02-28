# Architecture

**Analysis Date:** 2026-02-16

## Pattern Overview

**Overall:** Distributed CQRS (Command Query Responsibility Segregation) with Process Separation

**Key Characteristics:**
- **Two-Process Architecture:** WebApi (presentation layer) runs separately from ASPSBackend (business + data layer)
- **NetMQ Messaging:** Inter-process communication using NetMQ sockets (ZeroMQ)
- **No Direct DB Access in WebApi:** WebApi has ZERO database access - all data operations routed through CQRS Gateway
- **Event-Driven Real-Time Analysis:** Domain events trigger handlers for persistence, notifications, and protective actions
- **Per-User Analysis Isolation:** UDAnalysisManager creates isolated analysis instances for each user to manage concurrent device alerts

## Layers

**WebApi Layer (Presentation):**
- Purpose: HTTP REST API endpoints, Razor Pages admin UI, SignalR real-time notifications
- Location: `ASPSBackend14_J/WebApi/`
- Contains: Controllers, DTOs, Services (CQRSClient, NetMQClientService), Hubs, Razor Pages
- Depends on: Common (CQRS messages), NetMQ
- Used by: Client applications, admin dashboard, extension endpoints
- Key constraint: Has ZERO access to database, repositories, or business logic - communicates exclusively via NetMQ

**Business Layer (Application + Domain Logic):**
- Purpose: Command/Query handling, real-time analysis, domain event publishing
- Location: `ASPSBackend14_J/Business/`
- Contains:
  - Commands, Queries, Handlers (CommandQueryHandlers, etc.)
  - RealtimeAnalysis (UDAnalysis per-user analyzer, analyzers like UDUrlAnalyzer, UDRemoteAccessAnalyzer)
  - DomainEvents (event definitions and event handlers)
  - Views (ASView for in-memory data cache)
  - Messaging (CQRSGateway, NetMQMessageProcessor, RealTimeAlertListener)
  - Services
- Depends on: Common (entities, models, events), Interface (repository interfaces), Data layer
- Used by: WebApi (via CQRS Gateway), ASPSBackend main process
- Responsibilities: Process commands, execute queries, analyze device alerts, fire domain events

**Data Layer (EF Core + Repositories):**
- Purpose: Database operations via Entity Framework Core with Pomelo MySQL provider
- Location: `ASPSBackend14_J/Business/Data/EF/`
- Contains: AppDbContext, Repository<T> base class, entity-specific repositories
- Depends on: Common (entities), Microsoft.EntityFrameworkCore
- Used by: Business handlers and command processors
- Database: MySQL 8.0.44

**Common Layer (Shared Contracts):**
- Purpose: Shared types, interfaces, enums, domain models
- Location: `ASPSBackend14_J/Common/`
- Contains: Entities, Enums, Models, Interfaces, Exceptions, Messaging (CQRS base classes), ViewModels
- Dependencies: None (only .NET framework)
- Used by: All layers (WebApi, Business, Interface)

**Interface Layer (Repository Interfaces):**
- Purpose: Define repository and analysis interfaces to decouple business from data access
- Location: `ASPSBackend14_J/Interface/`
- Contains: Repository interfaces (IUserRepository, IUserDeviceRepository, etc.), Analysis interfaces
- Used by: Business layer as abstractions for dependency injection

## Data Flow

**Command Flow (WebApi → ASPSBackend):**

1. Client sends HTTP POST to `/api/users` (UsersController)
2. Controller creates Command object (e.g., CreateUserCommand)
3. CQRSClient serializes command to JSON with type information
4. NetMQ RequestSocket sends to tcp://localhost:5556 (CQRS Gateway endpoint)
5. CQRSGateway.ListenLoop() receives JSON frame
6. ProcessMessageAsync() deserializes and routes to handler (UserCommandHandlers)
7. Handler executes command: repository.AddAsync() → SaveChangesAsync() → fires domain event
8. Domain event triggers registered handlers (ASView, AlertPersistenceActor, NotificationPublisher)
9. CQRSGateway serializes CommandResult to JSON and sends response back
10. CQRSClient deserializes result and returns to controller
11. Controller returns HTTP response to client

**Real-Time Alert Flow (Device → ASPSBackend):**

1. Device sends alert via RealTimeAlertListener (tcp://*:50001)
2. RealTimeAlertListener deserializes alert (UrlAlert or RemoteAccessAlert)
3. Fires DeviceAlertReceived domain event
4. ASView.HandleDeviceAlertReceived() processes alert into memory views
5. UserDomainManagerService.GetOrCreateManagerForUser() retrieves UDAnalysisManager for user
6. UDAnalysisManager.AnalyzeAsync() runs pluggable analyzers (UDUrlAnalyzer, UDRemoteAccessAnalyzer)
7. Each analyzer returns AnalyzerResult with severity and indicators
8. UDAnalysis fires AnalysisResultReceived event
9. Event handlers (AlertPersistenceActor, AnalysisPersistenceActor, NotificationPublisher) process result
10. AlertPersistenceActor saves to database via repository
11. NotificationPublisher sends WebSocket message via SignalR NotificationsHub

**Query Flow (WebApi → ASPSBackend):**

1. Client sends HTTP GET to `/api/users` (UsersController)
2. Controller creates Query object (GetAllUsersQuery)
3. CQRSClient sends query via NetMQ RequestSocket to CQRSGateway
4. CQRSGateway deserializes and routes to handler (UserQueryHandlers)
5. Handler retrieves data from ASView (in-memory cache) or repository query
6. Serializes QueryResult and sends back
7. Controller deserializes and returns HTTP response with data

**State Management:**

- **In-Memory Cache (ASView):** Loads all users, devices, accounts, analysis results into memory at startup
  - Updated by domain events (UserAdded, UserUpdated, UserDeleted, AnalysisResultReceived)
  - Serves read-heavy query operations without database hits
  - Location: `Business/Views/ASView.cs`

- **Per-User Analysis State (UDAnalysis):**
  - Maintains ActiveDeviceAlerts and ExpiredDeviceAlerts lists for each user
  - UserDomainManagerService creates/manages UDAnalysisManager per active user
  - Location: `Business/RealtimeAnalysis/UserDomain/UDAnalysis.cs`

- **Database (MySQL):** Persistent storage for all entities (Users, UserDevices, DeviceAlerts, AnalysisResults, etc.)

## Key Abstractions

**CQRS Pattern:**
- Purpose: Separate read (Query) and write (Command) operations into distinct request types
- Examples: `Business/Commands/UserCommands.cs`, `Business/Queries/UserQueries.cs`
- Pattern: Command/Query classes inherit from base classes, handlers implement HandleAsync() methods
- Serialization: JSON with TypeNameHandling.Auto for polymorphism

**Repository Pattern:**
- Purpose: Abstract database access behind repository interfaces
- Examples: `Interface/Repositories/` (interfaces), `Business/Data/EF/Repositories/` (implementations)
- Pattern: Generic Repository<T> base class with entity-specific repositories extending it
- Key methods: GetByKeyAsync(), GetAllAsync(), AddAsync(), UpdateAsync(), DeleteAsync()

**Domain Events:**
- Purpose: Publish domain-level events to trigger side effects (persistence, notifications, analysis)
- Examples: `Business/DomainEvents/DomainEvents.cs` (UserAdded, AnalysisResultReceived, DeviceAlertReceived)
- Pattern: Events implement IDomainEvent, handlers implement IDomainEventHandler with Handle() method
- Handlers registered in ASPSBackend Program.cs as singletons

**Analyzer Pattern:**
- Purpose: Pluggable analyzers that can process different alert types
- Examples: `Business/RealtimeAnalysis/UserDomain/UDAnalysis.cs` (UDUrlAnalyzer, UDRemoteAccessAnalyzer, UDPhishingAnalyzer)
- Pattern: Implement ISpecificAnalyzer interface with CanAnalyze() and AnalyzeAsync() methods
- Result: AnalyzerResult containing severity, indicators, protective actions, and details

**Indicator Factory Pattern:**
- Purpose: Create indicator objects from analysis results
- Location: `Business/RealtimeAnalysis/IndicatorFactory.cs`
- Pattern: Factory method CreateIndicators() returns IIndicator[] based on analysis result type

## Entry Points

**WebApi Main Entry:**
- Location: `ASPSBackend14_J/WebApi/Program.cs`
- Triggers: dotnet run from WebApi project directory
- Responsibilities:
  - Configure ASP.NET Core (controllers, Razor Pages, SignalR)
  - Create CQRSClient singleton connected to tcp://localhost:5556
  - Create NetMQClientService singleton for real-time alert subscriptions
  - Map controllers, hub endpoints, and pages
  - Output: HTTP/HTTPS server on 5001/7001, Swagger on /swagger

**ASPSBackend Main Entry:**
- Location: `ASPSBackend14_J/ASPSBackend/Program.cs`
- Triggers: dotnet run from ASPSBackend project directory
- Responsibilities:
  - Configure dependency injection (DbContext, repositories, handlers, services)
  - Start ASView (load database into memory cache)
  - Start NetMQMessageProcessor (legacy messaging)
  - Start RealTimeAlertListener (tcp://*:50001 for device alerts)
  - Initialize UDAnalysisManagers for active users
  - Start CQRSGateway (tcp://*:5556 for WebApi commands/queries)
  - Output: Console messages, ready for WebApi connections

**WebApi Controllers:**
- `UsersController.cs`: Routes `/api/users` endpoints to GetAllUsersQuery, GetUserByKeyQuery, CreateUserCommand, UpdateUserCommand, DeleteUserCommand
- `UserDevicesController.cs`: Routes `/api/userdevices` endpoints

## Error Handling

**Strategy:** Try-catch with logging in handlers, timeout-based failures in CQRS communication

**Patterns:**

**Command Execution (CommandHandlers):**
```csharp
// In UserCommandHandlers.HandleAsync(CreateUserCommand):
try
{
    var user = new User { KeyField = Guid.NewGuid().ToString(), ... };
    var created = await _userRepository.AddAsync(user);
    await _asView.Handle(new UserAdded(created));
    return new CreateUserCommandResult { Success = true, Message = "User created successfully", UserKey = created.Key };
}
catch (Exception ex)
{
    return new CreateUserCommandResult { Success = false, Message = $"Error creating user: {ex.Message}" };
}
```

**NetMQ Communication (CQRSClient):**
```csharp
// In WebApi.Services.CQRSClient.SendQueryAsync():
try
{
    var sent = socket.TrySendFrame(_timeout, queryJson);
    if (!sent) throw new TimeoutException($"Failed to send query after {_timeout.TotalSeconds}s");

    var received = socket.TryReceiveFrameString(_timeout, out responseJson);
    if (!received) throw new TimeoutException($"No response from {_endpoint} after {_timeout.TotalSeconds}s");
}
catch (TimeoutException ex)
{
    _logger.LogError("Timeout: Is ASPSBackend running?", ex);
    throw;
}
```

**Event Handler Errors (Domain Event Publishing):**
```csharp
// In UDAnalysis.FireAnalysisResultEvent():
foreach (var handler in _eventHandlers)
{
    try
    {
        handler.Handle(analysisEvent);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Error in event handler {handler.GetType().Name}");
        // Continue processing other handlers despite individual failures
    }
}
```

## Cross-Cutting Concerns

**Logging:**
- Framework: Microsoft.Extensions.Logging
- Location: Configured in Program.cs, injected via ILogger<T>
- Approach: Console output in development, structured logging in production
- Key usage: Handler execution, CQRS message processing, alert analysis

**Validation:**
- Location: Implicit in command/query handler logic
- Approach: Check for null/missing data before processing, return failed CommandResult/QueryResult
- Example: "if (user == null) return new UpdateUserCommandResult { Success = false, Message = "User not found" }"

**Authentication:**
- Framework: Keycloak (external identity provider)
- Integration: User.KeycloakUserId field maps authenticated users
- Current scope: Not enforced in API endpoints (placeholder for future implementation)

---

*Architecture analysis: 2026-02-16*
