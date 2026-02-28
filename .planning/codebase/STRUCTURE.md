# Codebase Structure

**Analysis Date:** 2026-02-16

## Directory Layout

```
/c/Jobs/ASPS/Software/
├── ASPSBackend14_J/                          # Main C# solution
│   ├── ASPSBackend.sln                       # Visual Studio solution file
│   ├── ASPSBackend/                          # Console app entry point (business process)
│   │   ├── ASPSBackend.csproj
│   │   ├── Program.cs                        # Main entry: starts ASView, listeners, CQRS Gateway
│   │   ├── appsettings.json                  # Config: NetMQ endpoints, logging levels
│   │   ├── Properties/
│   │   └── bin/, obj/
│   │
│   ├── Common/                               # Shared types layer
│   │   ├── Common.csproj
│   │   ├── Entities/                         # EF Core entities
│   │   │   ├── User.cs
│   │   │   ├── UserDevice.cs
│   │   │   ├── PersonalComputer.cs          # UserDevice subtype
│   │   │   ├── SmartPhone.cs                 # UserDevice subtype
│   │   │   ├── UserAccount.cs
│   │   │   ├── AnalysisResults.cs
│   │   │   ├── DeviceAlerts.cs
│   │   │   ├── KnownPhishingWebsite.cs
│   │   │   └── SafeDomain.cs
│   │   ├── Enums/
│   │   │   ├── Enumerations.cs               # Severity, AlertType, ConnectionStatus, etc.
│   │   │   └── WebsiteType.cs
│   │   ├── Models/
│   │   │   ├── Key.cs                        # Key(Type, Value) domain model
│   │   │   ├── Entity.cs                     # Base entity with KeyField, DateCreated
│   │   │   ├── Alerts/
│   │   │   │   ├── DeviceAlert.cs            # Base class
│   │   │   │   ├── UrlAlert.cs               # URL phishing alerts
│   │   │   │   └── RemoteAccessAlert.cs      # Remote access app alerts
│   │   │   └── DeviceInfo.cs
│   │   ├── Interfaces/
│   │   │   ├── IAnalysisResult.cs
│   │   │   ├── IDomainEvent.cs               # Event interface
│   │   │   ├── IHasTag.cs
│   │   │   └── ITag.cs
│   │   ├── Exceptions/
│   │   │   ├── DomainException.cs
│   │   │   └── ErrorMessage.cs
│   │   ├── Messaging/
│   │   │   └── CQRS.cs                       # Command, Query base classes
│   │   ├── ViewModels/                       # DTOs for transfer
│   │   │   ├── UrlAnalysisResultVm.cs
│   │   │   └── RiskAssessmentVm.cs
│   │   └── bin/, obj/
│   │
│   ├── Interface/                            # Repository interfaces
│   │   ├── Interface.csproj
│   │   ├── Repositories/
│   │   │   ├── IRepository.cs                # Generic interface
│   │   │   ├── IUserRepository.cs
│   │   │   ├── IUserDeviceRepository.cs
│   │   │   ├── IUserAccountRepository.cs
│   │   │   ├── IAnalysisResultRepository.cs
│   │   │   ├── IDeviceAlertRepository.cs
│   │   │   ├── IAlertFlagRepository.cs
│   │   │   ├── IKnownPhishingWebsiteRepository.cs
│   │   │   └── ISafeDomainRepository.cs
│   │   ├── Analysis/
│   │   └── bin/, obj/
│   │
│   ├── Business/                             # Business logic layer
│   │   ├── Business.csproj
│   │   ├── Commands/
│   │   │   ├── UserCommands.cs               # CreateUserCommand, UpdateUserCommand, DeleteUserCommand
│   │   │   ├── AdminCommands.cs
│   │   │   └── UserDeviceCommands.cs
│   │   ├── Queries/
│   │   │   ├── UserQueries.cs                # GetAllUsersQuery, GetUserByKeyQuery, etc.
│   │   │   └── AdminQueries.cs
│   │   ├── Handlers/
│   │   │   ├── CommandQueryHandlers.cs       # Abstract base class
│   │   │   ├── UserCommandHandlers.cs        # Implements command handling
│   │   │   ├── UserQueryHandlers.cs          # Implements query handling
│   │   │   ├── AdminCommandHandlers.cs
│   │   │   ├── AdminQueryHandlers.cs
│   │   │   └── UserDeviceCommandHandlers.cs
│   │   ├── DomainEvents/
│   │   │   └── DomainEvents.cs               # UserAdded, UserUpdated, UserDeleted, AnalysisResultReceived, DeviceAlertReceived, etc.
│   │   ├── Data/
│   │   │   └── EF/
│   │   │       ├── AppDbContext.cs           # EF Core DbContext, model configuration
│   │   │       └── Repositories/
│   │   │           ├── Repository.cs         # Generic base repository
│   │   │           ├── EntityRepositories.cs # Auto-generated repository classes
│   │   │           ├── KnownPhishingWebsiteRepository.cs
│   │   │           └── SafeDomainRepository.cs
│   │   ├── Messaging/
│   │   │   ├── CQRSGateway.cs                # Listens on tcp://*:5556 for WebApi commands/queries
│   │   │   ├── NetMQMessageProcessor.cs      # Processes NetMQ messages
│   │   │   ├── RealTimeAlertListener.cs      # Listens on tcp://*:50001 for device alerts
│   │   │   ├── NotificationPublisher.cs      # Sends WebSocket notifications
│   │   │   └── AnalysisResultNotification.cs # Notification models
│   │   │   └── NotificationPublisherActor.cs # Event handler for publishing
│   │   ├── RealtimeAnalysis/
│   │   │   ├── IIndicatorFactory.cs          # Factory interface
│   │   │   ├── IndicatorFactory.cs           # Creates indicator objects
│   │   │   ├── ProtectiveActionsFactory.cs   # Creates protective action objects
│   │   │   ├── Indicators/
│   │   │   │   ├── IIndicator.cs             # Interface
│   │   │   │   ├── Indicator.cs              # Base class
│   │   │   │   ├── IndicatorType.cs          # Enum: KnownPhishing, MaliciousDomain, etc.
│   │   │   │   ├── IndicatorSource.cs        # Enum: UrlAnalyzer, PhishingDB, etc.
│   │   │   │   ├── IndicatorSubject.cs       # Enum: Url, Domain, Tracker, etc.
│   │   │   │   ├── IndicatorLayer.cs         # Enum: Network, Application, etc.
│   │   │   │   ├── KnownPhishingIndicator.cs
│   │   │   │   ├── DomainBlacklistedIndicator.cs
│   │   │   │   ├── MlAnalysisIndicator.cs
│   │   │   │   ├── NoMxRecordIndicator.cs
│   │   │   │   ├── WhoisCountryIndicator.cs
│   │   │   │   ├── WhoisDomainAgeIndicator.cs
│   │   │   │   ├── RemoteAccessIndicator.cs
│   │   │   │   └── [more indicators]
│   │   │   ├── ProtectivActions/
│   │   │   │   ├── IProtectiveAction.cs      # Interface
│   │   │   │   ├── ProtectiveAction.cs       # Base class
│   │   │   │   ├── ProtectiveActionType.cs   # Enum: DisplayNotification, BlockDomain, etc.
│   │   │   │   └── [action implementations]
│   │   │   ├── Scores/
│   │   │   │   └── NumericScore.cs           # Score model
│   │   │   ├── UserDomain/
│   │   │   │   ├── UDAnalysis.cs             # Per-user analyzer, fires domain events
│   │   │   │   ├── UDUser.cs                 # User wrapper with Key
│   │   │   │   ├── UDAnalysisResult.cs       # Result data class
│   │   │   │   ├── UDUrlAnalyzer.cs          # Analyzer for UrlAlert
│   │   │   │   ├── UDRemoteAccessAnalyzer.cs # Analyzer for RemoteAccessAlert
│   │   │   │   ├── UDPhishingAnalyzer.cs     # Analyzer for phishing URLs
│   │   │   │   ├── UDUserAnalyzer.cs         # User-level analysis from device alerts
│   │   │   │   └── UserDomainManagerService.cs # Creates/manages UDAnalysisManager per user
│   │   │   ├── AlertPersistenceActor.cs      # Event handler: saves alerts to database
│   │   │   └── AnalysisPersistenceActor.cs   # Event handler: saves analysis results
│   │   ├── Services/
│   │   │   ├── TokenStore.cs                 # Manages authentication tokens
│   │   │   ├── CurveKeyManager.cs            # Manages ZMQ encryption keys
│   │   │   └── [other services]
│   │   ├── Views/
│   │   │   ├── ASView.cs                     # In-memory cache, event handler hub
│   │   │   ├── DeviceAlertView.cs            # Device alert data view
│   │   │   ├── UrlAlertView.cs               # URL-specific alert view
│   │   │   ├── AnalysisResultView.cs         # Analysis result view
│   │   │   └── [other view models]
│   │   └── bin/, obj/
│   │
│   ├── WebApi/                               # HTTP API layer
│   │   ├── WebApi.csproj
│   │   ├── Program.cs                        # Main entry: configures ASP.NET, CQRSClient, SignalR
│   │   ├── Controllers/
│   │   │   ├── UsersController.cs            # GET/POST/PUT/DELETE /api/users
│   │   │   └── UserDevicesController.cs      # GET/POST /api/userdevices
│   │   ├── DTOs/
│   │   │   ├── UserRequest.cs
│   │   │   ├── UserResponse.cs
│   │   │   └── [other DTOs]
│   │   ├── Services/
│   │   │   ├── CQRSClient.cs                 # Sends commands/queries to ASPSBackend via NetMQ
│   │   │   └── NetMQClientService.cs         # Subscribes to real-time alerts
│   │   ├── Hubs/
│   │   │   └── NotificationsHub.cs           # SignalR hub for pushing notifications to clients
│   │   ├── Pages/
│   │   │   ├── Users/
│   │   │   │   ├── Index.cshtml              # User list admin UI
│   │   │   │   └── Details.cshtml            # User detail admin UI
│   │   │   ├── Devices/
│   │   │   │   ├── Index.cshtml              # Device list admin UI
│   │   │   │   └── Details.cshtml            # Device detail admin UI
│   │   │   ├── DeviceAlerts/
│   │   │   │   └── Index.cshtml              # Alert list admin UI
│   │   │   ├── AnalysisResults/
│   │   │   │   └── Index.cshtml              # Analysis result list admin UI
│   │   │   ├── KnownPhishingWebsites/
│   │   │   ├── SystemConfigurations/
│   │   │   └── Shared/
│   │   │       ├── Layout.cshtml
│   │   │       └── _Layout.cshtml
│   │   ├── wwwroot/
│   │   │   ├── css/
│   │   │   └── js/
│   │   ├── Properties/
│   │   │   └── PublishProfiles/
│   │   └── bin/, obj/
│   │
│   ├── .vs/                                  # Visual Studio cache
│   ├── appsettings.json                      # Default config (fallback)
│   └── [project files]
│
├── Analyzers/                                # Python analyzer services
│   └── basic-url-analyzer/                   # URL analysis service (separate from C# backend)
│       ├── config/
│       ├── core/
│       ├── models/
│       ├── scrapers/
│       ├── tests/
│       ├── utils/
│       └── [Python package structure]
│
├── apps/                                     # Client applications
│   ├── desktop/
│   │   └── win/
│   │       └── src/                          # Windows desktop app
│   │           ├── core/
│   │           ├── data/
│   │           ├── detection/
│   │           ├── handlers/
│   │           ├── services/
│   │           └── ui/
│   ├── extension/
│   │   └── chrome/                           # Chrome extension
│   │       ├── icons/
│   │       ├── messaging/
│   │       ├── services/
│   │       ├── state/
│   │       ├── utils/
│   │       └── warning/
│   └── [other apps]
│
└── [other root files: README, git config, etc.]
```

## Directory Purposes

**Common/**
- Purpose: Shared types and contracts across all projects
- Contains: Entities, enums, models, interfaces, exceptions, CQRS messages
- Key files: `Common/Entities/User.cs`, `Common/Enums/Enumerations.cs`, `Common/Models/Alerts/`
- No dependencies on other layers

**Interface/**
- Purpose: Decouple Business layer from implementation details
- Contains: Repository interfaces, analysis interfaces
- Key files: `Interface/Repositories/` (all IRepository types)
- Used by: Business layer via dependency injection

**Business/Commands/**
- Purpose: Command definitions (write operations)
- Contains: CreateUserCommand, UpdateUserCommand, DeleteUserCommand, CreateUserDeviceCommand, etc.
- Pattern: Command class extends Command (from Common.Messaging), includes fields and properties
- Location: `Business/Commands/` (separate files per domain)

**Business/Queries/**
- Purpose: Query definitions (read operations)
- Contains: GetAllUsersQuery, GetUserByKeyQuery, GetUserDevicesQuery, GetAnalysisResultsQuery, etc.
- Pattern: Query class extends Query (from Common.Messaging) with result class (extends QueryResult)
- Location: `Business/Queries/` (separate files per domain)

**Business/Handlers/**
- Purpose: Command and Query execution logic
- Contains: UserCommandHandlers, UserQueryHandlers, AdminCommandHandlers, etc.
- Pattern: HandleAsync(Command/Query) → business logic → CommandResult/QueryResult
- Key files:
  - `CommandQueryHandlers.cs`: Abstract base (command/query routing logic)
  - `UserCommandHandlers.cs`: User creation, update, deletion
  - `UserQueryHandlers.cs`: User retrieval operations
- Called by: CQRSGateway in ASPSBackend process

**Business/DomainEvents/**
- Purpose: Define events that occur in domain
- Contains: UserAdded, UserUpdated, UserDeleted, AnalysisResultReceived, DeviceAlertReceived, etc.
- Pattern: Event class implements IDomainEvent
- Fired by: Handlers after successful operations
- Handled by: Registered IDomainEventHandler implementations (ASView, AlertPersistenceActor, NotificationPublisherActor)

**Business/Data/EF/**
- Purpose: Database access via Entity Framework Core
- Contains: AppDbContext (model configuration), Repository implementations, EF configurations
- Key file: `AppDbContext.cs` (DbSet properties, OnModelCreating with Fluent API)
- Database: Pomelo.EntityFrameworkCore.MySql for MySQL 8.0.44
- Repositories: Generic Repository<T> + entity-specific implementations

**Business/RealtimeAnalysis/UserDomain/**
- Purpose: Per-user alert analysis with pluggable analyzers
- Contains:
  - `UDAnalysis.cs`: Main analyzer for a user, registers analyzer chain
  - `UDUrlAnalyzer.cs`: Analyzes UrlAlert (phishing detection)
  - `UDRemoteAccessAnalyzer.cs`: Analyzes RemoteAccessAlert (RDP/TeamViewer)
  - `UDPhishingAnalyzer.cs`: Phishing-specific rules
  - `UserDomainManagerService.cs`: Creates/caches UDAnalysisManager per user
- Flow: Device alert → RealTimeAlertListener → UDAnalysisManager.AnalyzeAsync() → analyzers → AnalysisResultReceived event
- Each user gets isolated analysis context (active/expired alerts tracked per user)

**Business/RealtimeAnalysis/Indicators/**
- Purpose: Define indicators detected during analysis
- Contains: IIndicator interface, Indicator base class, specific indicators (KnownPhishingIndicator, DomainBlacklistedIndicator, etc.)
- Pattern: IIndicator with properties (Type, Source, Subject, Layer, RiskScore)
- Created by: IndicatorFactory from analysis results

**Business/RealtimeAnalysis/ProtectivActions/**
- Purpose: Define protective actions recommended from analysis
- Contains: IProtectiveAction, ProtectiveAction base, action types (DisplayNotification, BlockDomain, etc.)
- Created by: ProtectiveActionsFactory from analysis results

**Business/Messaging/**
- Purpose: Inter-process communication infrastructure
- Contains:
  - `CQRSGateway.cs`: Listens tcp://*:5556, deserializes, routes to handlers, serializes response
  - `RealTimeAlertListener.cs`: Listens tcp://*:50001, receives device alerts, fires events
  - `NetMQMessageProcessor.cs`: General message processing
  - `NotificationPublisher.cs`: Sends real-time notifications
  - `NotificationPublisherActor.cs`: Event handler subscribing to analysis results
- All use NetMQ for socket communication

**Business/Services/**
- Purpose: Cross-cutting services
- Contains: TokenStore (authentication), CurveKeyManager (ZMQ encryption), UserDomainManagerService
- Key file: `UserDomainManagerService.cs` (manages UDAnalysisManager instances)

**Business/Views/**
- Purpose: In-memory caching and query view models
- Contains:
  - `ASView.cs`: Main in-memory cache, loads all data on startup, implements IDomainEventHandler
  - `DeviceAlertView.cs`: Alert view model
  - `UrlAlertView.cs`: URL alert specific view
  - `AnalysisResultView.cs`: Analysis result view
- Updated by: Domain events (UserAdded, UserUpdated, AnalysisResultReceived)
- Queried by: UserQueryHandlers for fast reads without database

**WebApi/Controllers/**
- Purpose: HTTP REST endpoints
- Pattern: [ApiController] with [Route("api/[controller]")] attribute
- Contains: UsersController, UserDevicesController
- Flow: HTTP request → Action method → CQRSClient.SendQueryAsync/SendCommandAsync → return response
- No direct database access - all data via CQRSClient

**WebApi/Services/**
- Purpose: Client services for inter-process communication
- Contains:
  - `CQRSClient.cs`: Sends commands/queries to CQRSGateway via NetMQ RequestSocket
  - `NetMQClientService.cs`: Subscribes to real-time alerts via NetMQ
- Used by: Controllers, SignalR hub
- Configured in: Program.cs as singletons

**WebApi/Hubs/**
- Purpose: SignalR real-time communication
- Contains: `NotificationsHub.cs`
- Used for: Pushing notifications to connected clients (analysis results, alerts)

**WebApi/Pages/**
- Purpose: Razor Pages admin UI
- Contains: Users, Devices, DeviceAlerts, AnalysisResults, KnownPhishingWebsites, SystemConfigurations pages
- Pattern: Index.cshtml (list) + Details.cshtml (detail view)
- Uses: CQRSClient or direct PageModel logic

## Key File Locations

**Entry Points:**
- `ASPSBackend14_J/ASPSBackend/Program.cs`: ASPSBackend console app startup
- `ASPSBackend14_J/WebApi/Program.cs`: WebApi ASP.NET Core startup

**Configuration:**
- `ASPSBackend14_J/ASPSBackend/appsettings.json`: ASPSBackend config (connection string, NetMQ ports)
- `ASPSBackend14_J/WebApi/appsettings.json`: WebApi config (CQRS endpoint, NetMQ business endpoint)

**Core Logic:**
- `ASPSBackend14_J/Business/RealtimeAnalysis/UserDomain/UDAnalysis.cs`: Per-user alert analysis
- `ASPSBackend14_J/Business/Views/ASView.cs`: In-memory cache and event hub
- `ASPSBackend14_J/Business/Messaging/CQRSGateway.cs`: Command/query gateway
- `ASPSBackend14_J/Business/Handlers/UserCommandHandlers.cs`: User command logic
- `ASPSBackend14_J/Business/Handlers/UserQueryHandlers.cs`: User query logic

**Data Access:**
- `ASPSBackend14_J/Business/Data/EF/AppDbContext.cs`: EF Core context and model
- `ASPSBackend14_J/Business/Data/EF/Repositories/Repository.cs`: Generic repository base
- `ASPSBackend14_J/Interface/Repositories/`: Repository interfaces

**Views and Models:**
- `ASPSBackend14_J/Common/Entities/`: EF entities (User, UserDevice, UserAccount, etc.)
- `ASPSBackend14_J/Common/Models/Alerts/`: Alert models (UrlAlert, RemoteAccessAlert)
- `ASPSBackend14_J/Common/Enums/Enumerations.cs`: Severity, AlertType, ConnectionStatus, etc.

**Testing:**
- `ASPSBackend14_J/WebApi/Pages/`: Admin UI for testing (Users, Devices, Alerts pages)

## Naming Conventions

**Files:**
- `Entity.cs`: Singular for entity classes
- `UserCommandHandlers.cs`: PascalCase, Handler/Service/Manager suffix
- `GetAllUsersQuery.cs`: Query class named with Get prefix
- `CreateUserCommand.cs`: Command class named with action verb

**Directories:**
- `Commands/`, `Queries/`, `Handlers/`: Feature-based plural naming
- `Data/EF/`, `RealtimeAnalysis/`, `Messaging/`: Feature modules organized by concern
- `Models/`, `Entities/`, `Interfaces/`: Type-based organization

**Classes:**
- `UserCommandHandlers`, `UserQueryHandlers`: Grouped by domain (User, Admin, Device)
- `IRepository`, `Repository<T>`: Generic base with I prefix for interfaces
- `AnalyzerResult`, `UDAnalysisResult`: Result classes with -Result suffix
- `UrlAlertView`, `DeviceAlertView`: View classes with -View suffix

**Namespaces:**
- `Business.Commands`, `Business.Queries`, `Business.Handlers`: Logical separation
- `Business.RealtimeAnalysis.UserDomain`: Feature-based organization
- `WebApi.Controllers`, `WebApi.Services`: Feature-based organization
- `Common.Entities`, `Common.Models`, `Common.Enums`: Type-based organization

## Where to Add New Code

**New Feature (e.g., Add device alerts page):**
- Command: `Business/Commands/DeviceAlertCommands.cs` (if write operation)
- Query: `Business/Queries/DeviceAlertQueries.cs`
- Handler: `Business/Handlers/DeviceAlertCommandHandlers.cs`, `DeviceAlertQueryHandlers.cs`
- Entity: `Common/Entities/DeviceAlert.cs` (if not exists)
- Repository: `Interface/Repositories/IDeviceAlertRepository.cs` + implementation in `Business/Data/EF/Repositories/`
- Controller: `WebApi/Controllers/DeviceAlertsController.cs`
- Page: `WebApi/Pages/DeviceAlerts/Index.cshtml` + PageModel

**New Analyzer (e.g., Add SSL certificate checker):**
- Analyzer class: `Business/RealtimeAnalysis/UserDomain/UDSslCertificateAnalyzer.cs` (implement ISpecificAnalyzer)
- Register in: `Business/RealtimeAnalysis/UserDomain/UDAnalysis.cs` (add to _analyzers list in constructor)
- Indicators: Add new indicator classes in `Business/RealtimeAnalysis/Indicators/`
- Protective Actions: Add new action classes in `Business/RealtimeAnalysis/ProtectivActions/`
- Factory updates: Update `IndicatorFactory.cs` and `ProtectiveActionsFactory.cs` to handle new result types

**New Utility/Helper:**
- Shared helper: `Common/` (no dependencies on other layers)
- Service helper: `Business/Services/` (for analyzers/handlers)
- API helper: `WebApi/Services/` (for controllers)

**New Event Type:**
- Event definition: `Business/DomainEvents/DomainEvents.cs` (add new class implementing IDomainEvent)
- Handler: `Business/RealtimeAnalysis/AlertPersistenceActor.cs` or new actor class (implement IDomainEventHandler)
- Registration: `ASPSBackend/Program.cs` (register handler as singleton via AddSingleton)

## Special Directories

**ASPSBackend14_J/.vs/**
- Purpose: Visual Studio IDE cache and project metadata
- Generated: Yes
- Committed: No (in .gitignore)

**ASPSBackend14_J/*/bin/ and */obj/**
- Purpose: Build output and intermediate files
- Generated: Yes (during dotnet build)
- Committed: No (in .gitignore)

**ASPSBackend14_J/WebApi/wwwroot/**
- Purpose: Static assets for web application
- Contains: CSS, JavaScript, images
- Committed: Yes

**Analyzers/**
- Purpose: External Python-based analysis services (separate from C# backend)
- Language: Python
- Purpose: URL analysis, content inspection, ML scoring
- Not directly integrated with C# backend (potential integration point via HTTP/message queue)

**apps/desktop/win/** and **apps/extension/chrome/**
- Purpose: Client applications (Windows desktop app, Chrome extension)
- Language: Python (desktop) and JavaScript (extension)
- Integration: Send alerts to RealTimeAlertListener endpoint
- Not part of backend architecture analysis scope

---

*Structure analysis: 2026-02-16*
