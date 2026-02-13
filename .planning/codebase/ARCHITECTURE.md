# Architecture

**Analysis Date:** 2026-02-13

## Pattern Overview

**Overall:** Distributed Event-Driven CQRS with Multi-Process Actor Model

**Key Characteristics:**
- Separate processes for presentation (WebApi), business logic (ASPSBackend), and clients
- Inter-process communication via NetMQ (ZeroMQ) with CURVE encryption
- Event-driven analysis engine with per-user domain managers
- Real-time notification broadcast via ZMQ PUB/SUB and WebSocket
- Client-server bridge pattern (desktop app mediates between extension and backend)

## Layers

**Presentation Layer (WebApi):**
- Purpose: HTTP/REST API and admin dashboard UI
- Location: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\WebApi`
- Contains: ASP.NET Core controllers, Razor Pages, SignalR hubs, DTOs
- Depends on: CQRSClient (NetMQ), no direct database access
- Used by: Web browsers, admin users
- Communication: Sends Commands/Queries to Business layer via NetMQ REQ/REP on tcp://localhost:5556

**Business Layer (ASPSBackend):**
- Purpose: Domain logic, real-time analysis, event processing, data persistence
- Location: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Business`
- Contains: CQRS handlers, domain analyzers, messaging services, event-driven actors
- Depends on: Repositories (Interface layer), Common entities/models
- Used by: WebApi (via CQRSGateway), clients (via RealTimeAlertListener)
- Communication: Listens on tcp://*:5556 (CQRS Gateway), tcp://*:50001 (RealTimeAlertListener REP), tcp://*:5555 (NotificationPublisher PUB)

**Data Layer (Interface + EF):**
- Purpose: Database abstraction and persistence
- Location: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Interface`, `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Business\Data\EF`
- Contains: Repository interfaces, EF Core implementations, AppDbContext
- Depends on: Common entities, MySQL database
- Used by: Business layer only (WebApi has zero database access)

**Common Layer:**
- Purpose: Shared contracts, entities, models, enums
- Location: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Common`
- Contains: Entities, DTOs, domain models, messaging contracts, interfaces
- Depends on: Nothing
- Used by: All backend projects

**Desktop Client (Python):**
- Purpose: Monitor user device, bridge extension to backend
- Location: `c:\Users\pc\Desktop\asps\apps\desktop\win`
- Contains: ZMQ client, WebSocket server, remote access monitor, browser history scanner
- Depends on: Backend (ZMQ), extension (WebSocket)
- Used by: End users on Windows
- Communication: ZMQ REQ to backend tcp://localhost:50001, WebSocket server on ws://localhost:9998-9999 for extension

**Extension Client (JavaScript):**
- Purpose: Browser-side URL scanning and user warnings
- Location: `c:\Users\pc\Desktop\asps\apps\extension\chrome`
- Contains: Background service worker, content scripts, popup UI, services
- Depends on: Desktop app (WebSocket)
- Used by: Chrome browser users
- Communication: WebSocket to desktop app ws://localhost:9998-9999

## Data Flow

**URL Scan Flow (Extension → Desktop → Backend → Analysis → Notification):**

1. User navigates to URL in browser
2. Extension content script detects URL, extracts trackers/iframes
3. Extension background.js sends `url_check` message to desktop app via WebSocket
4. Desktop app (`extension_handler.py`) receives request, checks cache
5. Desktop app sends UrlAlert to backend via ZMQ REQ/REP (tcp://localhost:50001)
6. Backend RealTimeAlertListener receives alert, validates token, routes to UDAnalysisManager
7. UDAnalysisManager triggers UDUrlAnalyzer, which runs indicators (KnownPhishing, MLAnalysis, DomainBlacklist, etc.)
8. Analysis completes, raises AnalysisResultReceived event
9. NotificationPublisherActor publishes result to ZMQ PUB socket (tcp://*:5555)
10. Desktop app notification_client.py receives notification via ZMQ SUB
11. Desktop app broadcasts result to extension via WebSocket
12. Extension updates icon color and executes protective action

**CQRS Admin Flow (WebApi → Gateway → Handlers → Database):**

1. Admin browser requests data (e.g., dashboard stats)
2. WebApi controller creates Query object (e.g., GetDashboardStatsQuery)
3. WebApi CQRSClient sends query to ASPSBackend via NetMQ REQ/REP (tcp://localhost:5556)
4. ASPSBackend CQRSGateway receives message, routes to AdminQueryHandlers
5. Handler executes query against repositories
6. Query result returned as JSON via NetMQ
7. WebApi deserializes result, returns to browser

**Remote Access Detection Flow (Desktop Monitor → Backend → Extension Warning):**

1. Desktop app remote_monitor.py scans for remote access tools (AnyDesk, TeamViewer, etc.)
2. Detects incoming connection via log parsing and geolocation
3. Sends RemoteAccessAlert to backend via ZMQ REQ/REP
4. Backend analyzes threat, stores alert
5. Desktop app broadcasts warning to extension via WebSocket
6. Extension content script injects full-page warning overlay on all tabs

**State Management:**
- Backend: ASView holds in-memory read cache of users, devices, alerts (refreshed from DB on startup)
- Desktop: Cache for URL scan results (TTL-based), auth token storage
- Extension: StateManager holds connection status, scan results, warning state in chrome.storage.local

## Key Abstractions

**CQRS Message Contracts (Common/Messaging):**
- Purpose: Define commands and queries for WebApi ↔ Business communication
- Examples: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Common\Messaging\Command.cs`, `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Common\Messaging\Query.cs`
- Pattern: Base classes with MessageType discriminator, JSON serialization with TypeNameHandling.Auto

**Domain Events (Business/DomainEvents):**
- Purpose: Decouple analysis from persistence and notifications
- Examples: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Business\DomainEvents\DomainEvents.cs` (DeviceAlertReceived, AnalysisResultReceived)
- Pattern: Observer pattern with IDomainEventHandler interface, multiple handlers per event

**Analyzers (Business/RealtimeAnalysis):**
- Purpose: Modular risk assessment for different alert types
- Examples: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Business\RealtimeAnalysis\UserDomain\UDUrlAnalyzer.cs`, `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Business\RealtimeAnalysis\UserDomain\UDRemoteAccessAnalyzer.cs`
- Pattern: ISpecificAnalyzer interface, indicator-based scoring, returns AnalysisResult with ProtectiveAction enum

**User Domain Manager (Business/RealtimeAnalysis/UserDomain):**
- Purpose: Per-user analysis instance with scoped alert history
- Examples: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\Business\RealtimeAnalysis\UserDomain\UDAnalysisManager.cs`
- Pattern: One manager per active user, holds UDAnalysis instance, manages event handlers

**Services (Desktop/Extension):**
- Purpose: Modular business logic in client apps
- Desktop examples: `c:\Users\pc\Desktop\asps\apps\desktop\win\src\services\scan_service.py`, `c:\Users\pc\Desktop\asps\apps\desktop\win\src\services\monitor_service.py`
- Extension examples: `c:\Users\pc\Desktop\asps\apps\extension\chrome\services\ScanService.js`, `c:\Users\pc\Desktop\asps\apps\extension\chrome\services\ConnectionService.js`
- Pattern: Dependency injection via Container (desktop), singleton exports (extension)

## Entry Points

**ASPSBackend Console App:**
- Location: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\ASPSBackend\Program.cs`
- Triggers: Manual execution
- Responsibilities: Host builder, DI registration, starts ASView, NetMQMessageProcessor, RealTimeAlertListener, CQRSGateway, initializes UDAnalysisManagers for active users
- Binds: tcp://*:5555 (PUB notifications), tcp://*:50001 (REP alerts), tcp://*:5556 (REP CQRS)

**WebApi ASP.NET App:**
- Location: `c:\Users\pc\Desktop\asps\ASPSBackend14_J\WebApi\Program.cs`
- Triggers: Manual execution
- Responsibilities: Configures controllers, Razor Pages, SignalR hub, CQRSClient singleton, serves HTTP on http://localhost:5001 and https://localhost:7001
- Maps: /notificationshub (SignalR), /api/* (controllers), Razor Pages for admin dashboard

**Desktop App Main:**
- Location: `c:\Users\pc\Desktop\asps\apps\desktop\win\src\main.py`
- Triggers: User launches app
- Responsibilities: Creates Container (DI), starts extension_server (WebSocket), authenticates with backend (ZMQ), starts notification_client (SUB), starts monitor_service (remote access + browser), runs tray_icon (blocking UI)

**Extension Background Service Worker:**
- Location: `c:\Users\pc\Desktop\asps\apps\extension\chrome\background.js`
- Triggers: Chrome startup, extension install/reload
- Responsibilities: Initializes stateManager, messageBus, connectionService, sets up tab listeners (onUpdated, onActivated), alarm listeners (reconnect, keepalive), connects to desktop app via WebSocket

**Extension Content Script:**
- Location: `c:\Users\pc\Desktop\asps\apps\extension\chrome\content.js`
- Triggers: Injected into every page
- Responsibilities: Scans page for trackers/iframes, listens for warning messages from background, injects RemoteAccessWarning component

## Error Handling

**Strategy:** Defensive error handling with graceful degradation

**Patterns:**
- Backend: Try-catch with ILogger.LogError, returns error responses via NetMQ (Success=false, Message=error)
- Desktop: Print to console + logger, returns error dicts to extension (type='url_result', error=true)
- Extension: Console.error logging, sets state error fields, shows user-friendly messages in popup
- Token validation: RealTimeAlertListener checks token before processing alerts, returns InvalidToken/TokenExpired status
- Timeout handling: All NetMQ sockets have timeouts (10s for CQRS, 5s for alerts), desktop ZMQ client has 5s timeout
- Connection resilience: Extension ConnectionService auto-reconnects on WebSocket disconnect, desktop notification_client reconnects to ZMQ SUB

## Cross-Cutting Concerns

**Logging:**
- Backend: Microsoft.Extensions.Logging with Console provider, LogLevel.Information minimum
- Desktop: Python logging module, formatted with timestamps
- Extension: console.log/error, prefixed with service name (e.g., "[ScanService]")

**Validation:**
- Backend: Token validation in RealTimeAlertListener (TokenStore.ValidateToken)
- Desktop: Auth token refresh in auth_manager.py (check expiration, call RefreshToken)
- Extension: URL format validation (startsWith('http')), message type validation

**Authentication:**
- Desktop → Backend: Token-based (RequestToken message returns JWT-like token with expiration)
- Token storage: Desktop stores in auth_manager.py memory, sent with every alert
- Token lifecycle: 24-hour expiration, auto-refresh on TokenExpired response

**Security:**
- CURVE encryption: Optional CurveZMQ on NetMQ sockets (server public key shared in RequestToken response)
- HTTPS: WebApi uses HTTPS in production (UseHttpsRedirection)
- Input sanitization: URL encoding, JSON schema validation

**Caching:**
- Desktop: URL scan results cached with TTL (1 hour default) in cache_manager.py
- Extension: URL results cached in CacheService (chrome.storage.local)
- Backend: ASView in-memory cache of users/devices (loaded on startup, updated via domain events)

## Inter-Project Communication

**WebApi ↔ ASPSBackend:**
- Protocol: NetMQ REQ/REP
- Port: tcp://localhost:5556
- Messages: CQRS Commands/Queries (JSON with TypeNameHandling.Auto)
- Example: WebApi sends GetDashboardStatsQuery, receives GetDashboardStatsQueryResult

**Desktop App ↔ ASPSBackend:**
- Protocol: NetMQ REQ/REP (alerts), ZMQ SUB (notifications)
- Ports: tcp://localhost:50001 (REQ alerts), tcp://localhost:5555 (SUB notifications)
- Messages: UrlAlert, RemoteAccessAlert (request), AnalysisResultNotification (broadcast)
- Security: CURVE encryption optional

**Extension ↔ Desktop App:**
- Protocol: WebSocket
- Port: ws://localhost:9998-9999 (tries multiple ports)
- Messages: url_check, remote_access_alert, url_result
- Format: JSON with 'type' field discriminator

**All Components ↔ MySQL Database:**
- Only ASPSBackend has database access via Entity Framework Core
- Connection string: MySQL 8.0.44
- Pattern: Repository pattern with scoped DbContext

---

*Architecture analysis: 2026-02-13*
