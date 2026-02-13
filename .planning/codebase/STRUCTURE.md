# Codebase Structure

**Analysis Date:** 2026-02-13

## Directory Layout

```
c:\Users\pc\Desktop\asps\
├── ASPSBackend14_J\          # C# .NET backend system
│   ├── ASPSBackend\          # Console app entry point
│   ├── Business\             # Business logic and analysis
│   ├── Common\               # Shared models and contracts
│   ├── Interface\            # Repository interfaces
│   └── WebApi\               # Web presentation layer
├── apps\                     # Client applications
│   ├── desktop\              # Desktop clients
│   │   ├── win\              # Windows Python app
│   │   └── macos\            # macOS app (not analyzed)
│   └── extension\            # Browser extensions
│       └── chrome\           # Chrome/Edge extension
├── basic-url-analyzer\       # Standalone URL analyzer tool
├── python_clients\           # Python client examples
└── WebApi\                   # Symlink/duplicate of ASPSBackend14_J\WebApi
```

## Directory Purposes

### ASPSBackend14_J (Backend System)

**ASPSBackend\:**
- Purpose: Main console application entry point for business layer
- Contains: `Program.cs` (DI setup, service startup), `appsettings.json`
- Key files: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\ASPSBackend\Program.cs`
- Runs: ASView, NetMQMessageProcessor, RealTimeAlertListener, CQRSGateway

**Business\:**
- Purpose: Core business logic, CQRS, analysis engine
- Contains: Commands, Queries, Handlers, Analyzers, Messaging, Views, Services
- Subdirectories:
  - `Commands\`: UserCommands.cs, AdminCommands.cs, UserDeviceCommands.cs
  - `Queries\`: UserQueries.cs, AdminQueries.cs
  - `Handlers\`: UserCommandHandlers.cs, AdminQueryHandlers.cs
  - `Messaging\`: CQRSGateway.cs, RealTimeAlertListener.cs, NetMQMessageProcessor.cs, NotificationPublisher.cs
  - `RealtimeAnalysis\`: UserDomain (UDAnalysisManager.cs, UDUrlAnalyzer.cs), Indicators, AlertPersistenceActor.cs
  - `Views\`: ASView.cs (in-memory cache)
  - `Services\`: TokenStore.cs, CurveKeyManager.cs, UserDomainManagerService.cs
  - `Data\EF\`: AppDbContext.cs, Repositories (EntityRepositories.cs)

**Common\:**
- Purpose: Shared contracts, entities, models, enums
- Contains: Domain entities, DTOs, messaging contracts, interfaces
- Subdirectories:
  - `Entities\`: User.cs, UserDevice.cs, DeviceAlertEntity.cs, AnalysisResultContainer.cs
  - `Models\`: Key.cs, Entity.cs, DeviceAlert.cs, Alerts (UrlAlert.cs, RemoteAccessAlert.cs)
  - `Enums\`: DeviceType.cs, OperatingSystemType.cs, AlertPriority.cs
  - `Messaging\`: Command.cs, Query.cs, Message.cs
  - `Interfaces\`: IDomainEventHandler.cs, IBackgroundTask.cs
  - `ViewModels\`: UrlAnalysisResultVm.cs, RemoteAccessAnalysisResultVm.cs

**Interface\:**
- Purpose: Repository interface definitions
- Contains: IUserRepository.cs, IUserDeviceRepository.cs, IAnalysisResultRepository.cs, IDeviceAlertRepository.cs
- Location: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Interface\Repositories`

**WebApi\:**
- Purpose: HTTP presentation layer, admin dashboard
- Contains: Controllers, Pages, Hubs, Services, wwwroot (static files)
- Subdirectories:
  - `Controllers\`: AdminController.cs, UsersController.cs
  - `Services\`: CQRSClient.cs, NetMQClientService.cs
  - `Hubs\`: NotificationsHub.cs (SignalR)
  - `Pages\`: Admin dashboard Razor Pages
  - `DTOs\`: CreateUserDto.cs
  - `wwwroot\`: CSS, JS, images for admin UI

### apps (Client Applications)

**desktop\win\:**
- Purpose: Windows desktop client application (Python)
- Contains: Main app, services, handlers, monitoring, ZMQ/WebSocket clients
- Key files:
  - `src\main.py`: Application entry point
  - `src\core\container.py`: Dependency injection container
  - `src\config.py`: Configuration constants
  - `src\zmq_client.py`: ZMQ REQ client for alerts
  - `src\notification_client.py`: ZMQ SUB client for notifications
  - `src\extension_server.py`: WebSocket server for extension
  - `src\auth_manager.py`: Token authentication
  - `src\cache_manager.py`: Result caching
- Subdirectories:
  - `src\services\`: scan_service.py, monitor_service.py, protection_service.py
  - `src\handlers\`: extension_handler.py, notification_handler.py
  - `src\detection\`: log_parsers.py, geolocation.py, direction.py (remote access detection)
  - `src\ui\`: colors.py (UI constants)

**extension\chrome\:**
- Purpose: Chrome/Edge browser extension (JavaScript ES6 modules)
- Contains: Background worker, content scripts, services, UI components
- Key files:
  - `manifest.json`: Extension manifest (v3)
  - `background.js`: Service worker entry point
  - `content.js`: Content script injected into pages
  - `popup.html` + `popup.js`: Extension popup UI
- Subdirectories:
  - `services\`: ScanService.js, ConnectionService.js, ProtectionService.js, AuthService.js, CacheService.js, IconService.js
  - `messaging\`: MessageBus.js, MessageTypes.js, index.js
  - `state\`: StateManager.js (chrome.storage wrapper)
  - `warning\`: RemoteAccessWarning.js, FrictionController.js, ShadowContainer.js

**basic-url-analyzer\:**
- Purpose: Standalone URL analysis tool (not integrated)
- Contains: Python scripts for URL scanning
- Structure: Multiple subdirectories with duplicated code (needs cleanup)

**python_clients\:**
- Purpose: Example Python clients for testing backend
- Contains: `python-client-with-notifications.py` (ZMQ REQ/SUB example)

## Key File Locations

**Entry Points:**
- Backend Console: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\ASPSBackend\Program.cs`
- WebApi: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\WebApi\Program.cs`
- Desktop App: `c:\Users\pc\Desktop\asps\apps\desktop\win\src\main.py`
- Extension: `c:\Users\pc\Desktop\asps\apps\extension\chrome\background.js`

**Configuration:**
- Backend: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\ASPSBackend\appsettings.json`
- WebApi: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\WebApi\appsettings.json`
- Desktop: `c:\Users\pc\Desktop\asps\apps\desktop\win\src\config.py`
- Extension: `c:\Users\pc\Desktop\asps\apps\extension\chrome\manifest.json`

**Core Logic:**
- CQRS Gateway: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Business\Messaging\CQRSGateway.cs`
- Alert Listener: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Business\Messaging\RealTimeAlertListener.cs`
- Analysis Manager: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Business\RealtimeAnalysis\UserDomain\UDAnalysisManager.cs`
- URL Analyzer: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Business\RealtimeAnalysis\UserDomain\UDUrlAnalyzer.cs`
- Desktop Container: `c:\Users\pc\Desktop\asps\apps\desktop\win\src\core\container.py`
- Extension Scan: `c:\Users\pc\Desktop\asps\apps\extension\chrome\services\ScanService.js`

**Testing:**
- No dedicated test directories detected
- SQL test files in root: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\INSERT-TEST-DATA.sql`, `populate-test-data.sql`

## Naming Conventions

**Files (C# Backend):**
- PascalCase.cs: `Program.cs`, `ASView.cs`, `CQRSGateway.cs`
- Suffix patterns: `*Commands.cs`, `*Queries.cs`, `*Handlers.cs`, `*Repository.cs`, `*Actor.cs`
- Config: `appsettings.json`, `*.csproj`

**Files (Python Desktop):**
- snake_case.py: `main.py`, `zmq_client.py`, `auth_manager.py`
- Suffix patterns: `*_service.py`, `*_handler.py`, `*_manager.py`, `*_client.py`
- Config: `config.py`, `requirements.txt`

**Files (JavaScript Extension):**
- PascalCase.js for classes: `ScanService.js`, `ConnectionService.js`, `StateManager.js`
- camelCase.js for scripts: `background.js`, `content.js`, `popup.js`
- kebab-case for CSS: `content.css`, `warning-styles.js` (CSS-in-JS)

**Directories:**
- PascalCase (C#): `ASPSBackend`, `Business`, `Common`, `Interface`, `WebApi`
- lowercase (Python): `src`, `services`, `handlers`, `detection`, `ui`
- lowercase (JS): `services`, `messaging`, `state`, `warning`

**Classes:**
- C#: PascalCase (e.g., `CQRSGateway`, `UDAnalysisManager`, `ASView`)
- Python: PascalCase (e.g., `ZMQClient`, `ScanService`, `Container`)
- JavaScript: PascalCase for classes (e.g., `ScanService`), SCREAMING_SNAKE_CASE for constants (e.g., `MSG`, `PROTECTIVE_ACTION`)

**Methods/Functions:**
- C#: PascalCase (e.g., `SendQueryAsync`, `HandleAsync`)
- Python: snake_case (e.g., `send_url_alert`, `check_url`)
- JavaScript: camelCase (e.g., `handleResult`, `scanCurrentTab`)

**Variables:**
- C#: camelCase for locals/fields (e.g., `_logger`, `messageJson`), PascalCase for properties
- Python: snake_case (e.g., `device_id`, `cache_manager`)
- JavaScript: camelCase (e.g., `connectionService`, `stateManager`)

## Where to Add New Code

**New CQRS Command/Query:**
- Define: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Business\Commands\` or `Queries\`
- Handler: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Business\Handlers\`
- Register in: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Business\Messaging\CQRSGateway.cs` (ProcessQueryAsync/ProcessCommandAsync switch)

**New Domain Event:**
- Define: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Business\DomainEvents\DomainEvents.cs`
- Handler: Create new *Actor.cs in `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Business\RealtimeAnalysis\` or `Business\Messaging\`
- Register: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\ASPSBackend\Program.cs` (services.AddSingleton<IDomainEventHandler>)

**New Analyzer:**
- Implementation: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Business\RealtimeAnalysis\UserDomain\UDXxxAnalyzer.cs` (implement ISpecificAnalyzer)
- Indicators: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Business\RealtimeAnalysis\Indicators\`
- Register: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Business\RealtimeAnalysis\UserDomain\UDAnalysisManager.cs` constructor (_analyzers list)

**New Desktop Service:**
- Implementation: `c:\Users\pc\Desktop\asps\apps\desktop\win\src\services\*_service.py`
- Register: `c:\Users\pc\Desktop\asps\apps\desktop\win\src\core\container.py` (add to Container class)

**New Extension Service:**
- Implementation: `c:\Users\pc\Desktop\asps\apps\extension\chrome\services\XxxService.js` (singleton export)
- Import: `c:\Users\pc\Desktop\asps\apps\extension\chrome\background.js`

**New WebApi Controller:**
- Implementation: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\WebApi\Controllers\XxxController.cs`
- No registration needed (auto-discovered)

**New Entity:**
- Define: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Common\Entities\` (inherit from Entity)
- DbContext: Add DbSet to `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Business\Data\EF\AppDbContext.cs`
- Repository: Interface in `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Interface\Repositories\`, implementation in `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Business\Data\EF\Repositories\`

**New Alert Type:**
- Model: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Common\Models\Alerts\` (inherit from DeviceAlert)
- Entity: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Common\Entities\` (inherit from DeviceAlertEntity)
- Handler: Update `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Business\Messaging\RealTimeAlertListener.cs` (ProcessAlertAsync switch)
- Desktop: Add send method to `c:\Users\pc\Desktop\asps\apps\desktop\win\src\zmq_client.py`

## Special Directories

**bin\ and obj\:**
- Purpose: .NET build artifacts
- Generated: Yes (MSBuild)
- Committed: No (.gitignore)

**node_modules\:**
- Purpose: Not used (no Node.js in this project)
- Generated: N/A
- Committed: N/A

**.venv\ and venv\:**
- Purpose: Python virtual environments (desktop app)
- Generated: Yes (python -m venv)
- Committed: No
- Location: `c:\Users\pc\Desktop\asps\apps\desktop\win\.venv`

**.vs\:**
- Purpose: Visual Studio settings and cache
- Generated: Yes (Visual Studio)
- Committed: No
- Location: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\.vs`

**wwwroot\:**
- Purpose: Static web assets (CSS, JS, images) for WebApi
- Generated: No (manually created)
- Committed: Yes
- Location: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\WebApi\wwwroot`

**icons\:**
- Purpose: Extension icons (16px, 48px, 128px)
- Generated: No
- Committed: Yes
- Location: `c:\Users\pc\Desktop\asps\apps\extension\chrome\icons`

**.planning\:**
- Purpose: GSD planning and codebase documentation
- Generated: Yes (by GSD commands)
- Committed: Should be committed
- Location: `c:\Users\pc\Desktop\asps\.planning`

## Project Files

**C# Projects (.csproj):**
- `c:\Users\pc\Desktop\asps\ASPSBackend14_J\ASPSBackend\ASPSBackend.csproj` - References: Business, Common
- `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Business\Business.csproj` - References: Common, Interface
- `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Common\Common.csproj` - No references
- `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Interface\Interface.csproj` - References: Common
- `c:\Users\pc\Desktop\asps\ASPSBackend14_J\WebApi\WebApi.csproj` - References: Common only (no Business!)

**Solution File:**
- `c:\Users\pc\Desktop\asps\ASPSBackend14_J\ASPSBackend.sln` - Contains all 5 projects

**Python Project:**
- `c:\Users\pc\Desktop\asps\apps\desktop\win\requirements.txt` - Dependencies
- No setup.py or pyproject.toml (simple script-based app)

**Extension Manifest:**
- `c:\Users\pc\Desktop\asps\apps\extension\chrome\manifest.json` - Chrome extension v3

## Dependency Flow

**Backend Project Dependencies:**
```
ASPSBackend (Console)
  ├─→ Business
  │    ├─→ Common
  │    └─→ Interface
  │         └─→ Common
  └─→ Common

WebApi (ASP.NET)
  └─→ Common (NO Business reference!)
```

**Client Dependencies:**
```
Desktop App (Python)
  ├─→ Backend (via ZMQ tcp://localhost:50001)
  └─→ Extension (via WebSocket ws://localhost:9998-9999)

Extension (JavaScript)
  └─→ Desktop App (via WebSocket)
```

**Communication Topology:**
```
Browser (Admin)
  ↓ HTTP
WebApi
  ↓ NetMQ REQ/REP (tcp://localhost:5556)
ASPSBackend (CQRSGateway)
  ↓ Repositories
Database (MySQL)

Browser (Extension)
  ↓ WebSocket (ws://localhost:9998-9999)
Desktop App
  ↓ ZMQ REQ/REP (tcp://localhost:50001)
ASPSBackend (RealTimeAlertListener)
  ↓ UDAnalysisManager → Analyzers
  ↓ NotificationPublisher (tcp://*:5555)
Desktop App (notification_client)
  ↓ WebSocket
Extension
```

---

*Structure analysis: 2026-02-13*
