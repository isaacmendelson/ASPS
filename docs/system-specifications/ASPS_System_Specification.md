# ASPS System Specification

**Version:** 1.0 | **Date:** 2026-06-27 | **Status:** Authoritative

ASPS (Anti-Scam Protection System) is a distributed real-time threat-protection platform that defends end users — particularly elderly, immigrant, and tech-anxious adults — from online scams, phishing attacks, and unauthorized remote-access sessions. The system monitors browsing activity and installed remote-access software on user devices, forwards signals to a central analysis engine, and delivers immediate protective actions to the user. Five subsystems are fully built; one (Mobile Agent) is planned with wire-protocol defined; one (Users Portal) is a design artifact only. Sources: (`docs/system-specifications/ASPS_System_Overview.md`, `CLAUDE.md`)

---

## Subsystem Status at a Glance

| # | Subsystem | Status | Primary Path | Key Citation |
|---|-----------|--------|-------------|--------------|
| 1 | Backend (Host Service) | **built** | `ASPSBackend14_J/ASPSBackend/`, `Business/`, `Common/`, `Interface/` | `ASPSBackend14_J/ASPSBackend/Program.cs` |
| 2 | Admin Portal + REST | **built** | `ASPSBackend14_J/WebApi/` | `ASPSBackend14_J/WebApi/Program.cs` |
| 3 | Desktop Agent | **built** | `apps/desktop/win/src/` | `apps/desktop/win/src/main.py` |
| 4 | Mobile Agent | **planned** | *(no directory)* | `docs/ASPS_DATA_FLOW.md §10`; SCRUM-898, SCRUM-899 |
| 5 | Browser Extension | **built** | `apps/extension/chrome/` | `apps/extension/chrome/manifest.json` |
| 6 | Users Portal | **planned** | *(no directory)* | `docs/system-specifications/מסמך ארכיטקטורה…md` (design only) |
| 7 | URL Analyzer | **built** | `Analyzers/basic-url-analyzer/` | `Analyzers/basic-url-analyzer/api.py` |

---

## System Architecture & Data Flow

The end-to-end flow moves signals from the user's device to the backend analysis engine and back:

```
[Chrome Extension]
     |  WebSocket (localhost:8080–8484)
     v
[Desktop Agent (Python)]
     |  ZMQ REQ → port 50001 (CURVE-encrypted)
     v
[ASPSBackend (.NET 8)]
     |  EF Core → MySQL 8 (port 3306)
     |  Python subprocess → URL Analyzer
     |  ZMQ PUB → port 50002 (CURVE-encrypted)
     v
[Desktop Agent receives result]
     |  WebSocket
     v
[Chrome Extension shows warning/block]

[Admin browser] ← SignalR (port 5001/5002) ← WebApi ← NetMQ REQ (port 5556) ← Backend
```

### Ports Reference

| Port | Direction | Protocol | Encryption | Purpose |
|------|-----------|----------|------------|---------|
| 50001 | Agent → Backend | ZMQ REQ/ROUTER | CURVE (CurveZMQ) | Device alert intake |
| 50002 | Backend → Agent | ZMQ PUB/SUB | CURVE (CurveZMQ) | Analysis result notifications |
| 5555 | WebApi → Backend | ZMQ REQ/REP | None (localhost) | Internal CQRS message processor |
| 5556 | WebApi → Backend | ZMQ REQ/REP | None (localhost) | CQRS gateway (typed commands/queries) |
| 5001 | Browser → WebApi | HTTP | None (dev) / HTTPS (prod) | Razor Pages admin UI |
| 5002 | Browser → WebApi | HTTPS | TLS | Admin UI (HTTPS) |
| 3306 | Backend → MySQL | MySQL protocol | None (security debt — no SSL) | Persistent storage |
| 8080–8484 | Extension ↔ Agent | WebSocket (ws://) | None (localhost) | Browser extension ↔ desktop agent |
| 8180 | WebApi → Keycloak | HTTP | None (dev only) | OIDC authentication (dev Keycloak instance) |

Source: (`CLAUDE.md`; `apps/desktop/win/src/config.py`; `ASPSBackend14_J/ASPSBackend/appsettings.json`)

---

## 1. Backend (Host Service)

**Status:** built — fully operational; 51 EF migrations applied; all services start in `Program.cs` (`ASPSBackend14_J/ASPSBackend/Program.cs`)

### Purpose

The ASPSBackend is the central analysis and messaging engine. It receives device alerts from field agents over encrypted ZMQ, runs multi-stage fraud analysis, persists results to MySQL, and publishes analysis outcomes back to agents and the admin portal.

### Components

| Component | Path | Responsibility |
|-----------|------|----------------|
| `Program.cs` | `ASPSBackend14_J/ASPSBackend/Program.cs` | Entry point: DI wiring, migration, service startup |
| `RealTimeAlertListener` | `Business/Messaging/RealTimeAlertListener.cs` | ZMQ RouterSocket on port 50001; receives `UrlAlert`, `RemoteAccessAlert`, `RequestToken`, `RegisterDevice`, `RefreshToken` |
| `CQRSGateway` | `Business/Messaging/` | ZMQ on port 5556; routes typed commands/queries from WebApi |
| `NetMQMessageProcessor` | `Business/Messaging/` | ZMQ on port 5555; lower-level CQRS channel |
| `ASView` | `Business/Views/ASView.cs` | Singleton in-memory read model; caches users, devices, alerts, 506K+ phishing URLs |
| `TokenStore` | `Business/Services/TokenStore.cs` | Write-through token cache (memory + MySQL `DeviceTokens` table) |
| `CurveKeyManager` | `Business/Services/CurveKeyManager.cs` | Manages ZMQ CURVE server keypair; writes public key to `curve-server-public-key.txt` |
| `UserDomainManagerService` | `Business/RealtimeAnalysis/UserDomain/` | Lazy-init per-user `UDAnalysisManager` instances |
| `UDAnalysisManager` | `Business/RealtimeAnalysis/UserDomain/UDAnalysisManager.cs` | Orchestrates per-user analysis pipeline via domain events |
| `UDAnalysis` | `Business/RealtimeAnalysis/UserDomain/UDAnalysis.cs` | Runs ordered analyzers: `UDPhishingAnalyzer`, `UDUrlAnalyzer`, `UDRemoteAccessAnalyzer`, `TrackUrlAnalyzer` |
| `UDUserAnalyzer` | `Business/RealtimeAnalysis/UserDomain/` | Cross-device correlation; detects `ImmediateDanger` (remote session + sensitive browser tab) |
| `AlertPersistenceActor` | `Business/RealtimeAnalysis/AlertPersistenceActor.cs` | Persists `DeviceAlertEntity` rows to MySQL |
| `AnalysisPersistenceActor` | `Business/RealtimeAnalysis/AnalysisPersistenceActor.cs` | Persists `AnalysisResultContainer` JSON blobs |
| `ImmediateDangerPersistanceActor` | `Business/RealtimeAnalysis/ImmediateDangerPersistanceActor.cs` | Persists `ImmediateDanger` rows |
| `NotificationPublisher` / `NotificationPublisherActor` | `Business/Messaging/NotificationPublisher.cs` | ZMQ PUB on port 50002; publishes results per `device:{uid}` topic |
| `UserRiskScoreService` | `Business/Services/UserRiskScoreService.cs` | SCRUM-904; event-driven URS orchestration (singleton; throttled per-user) |
| `SimulationRunner` | `Business/Services/SimulationRunner.cs` | Background service for scripted alert simulations |
| `AppDbContext` | `Business/Data/EF/AppDbContext.cs` | EF Core 7 + Pomelo MySQL; 51 migrations |

### Tech Stack

- .NET 8 console application (long-running `IHost`)
- EF Core 7 (`Pomelo.EntityFrameworkCore.MySql` 7.0.0) + MySQL 8.0.44
- NetMQ 4.0.1.13 (ZeroMQ bindings) with CURVE encryption
- Newtonsoft.Json 13.0.3 (`TypeNameHandling.Auto` — known security debt; see Open Items)
- NetTopologySuite 2.6.0 (spatial data support)
- DI scope validation disabled at startup (known tech debt; TODO comment in `Program.cs`)

Source: (`ASPSBackend14_J/ASPSBackend/Program.cs`; `docs/system-specifications/ASPS_System_Overview.md §3`)

### Interfaces / Contracts

**Inbound (port 50001, ZMQ ROUTER, CURVE):**

| Message Type | Rate Limit | Description |
|-------------|------------|-------------|
| `RequestToken` | 5/min/device | Auth token request |
| `RegisterDevice` | 3/min/device | New device registration with user email |
| `RefreshToken` | 5/min/device | Token renewal |
| `UrlAlert` | token-validated | URL visited by user; includes `Url`, `Trackers`, `IFrameDomains`, `TabId`, `IPAddress`, `DeviceInfo`, `Token` |
| `RemoteAccessAlert` | token-validated | Remote-access app state change; includes `RemoteAccessApp` (enum), `Direction`, `SessionStatus`, `ConnectionStatus`, `BrowserTabs[]`, forensic fields |

**Outbound (port 50002, ZMQ PUB, CURVE):**

Topics: `device:{deviceUid}`. Message types: `AnalysisResult`, `ImmediateDangerNotification`, `ImmediateDangerEndedNotification`, `SetBrowserTabsPolicyNotification`.

**Inbound CQRS (port 5556, ZMQ REQ):**

Commands: `CreateUserAdminCommand`, `CreateUserDeviceCommand`, `DeleteUserCommand`, `UpdateUserCommand`, `SaveRoadmapCommand`, `TrackedDomain*`, `WebsiteCategory*`, `Simulation*`.
Queries: `GetDashboardStatsQuery`, `GetUsersWithDeviceCountsQuery`, `GetAllDevicesQuery`, `GetRecentAlertsQuery`, `GetDeviceByUidQuery`, `ValidateDeviceTokenQuery`, `GetAllPhishingWebsitesQuery`, and 10+ others.

Source: (`docs/system-specifications/ASPS_System_Overview.md §3.3`; `ASPSBackend14_J/ASPSBackend/Program.cs`)

### Data Flow

1. `RealTimeAlertListener` receives ZMQ message on port 50001.
2. Token validated against `TokenStore`.
3. Device/user looked up in `ASView`.
4. `DeviceAlertReceived` domain event raised to all registered `IDomainEventHandler` singletons.
5. `AlertPersistenceActor` writes to `DeviceAlerts` table.
6. `ASView` updates in-memory cache.
7. `UDAnalysisManager.Handle()` dispatches to `UDAnalysis` → runs analyzer chain → raises `AnalysisResultReceived`.
8. `AnalysisPersistenceActor` writes `AnalysisResults` JSON blob.
9. `NotificationPublisherActor` publishes result to ZMQ PUB (port 50002).
10. WebApi `NotificationSubscriber` (BackgroundService) receives on SUB socket → forwards to SignalR hub → admin browser updates live.

Source: (`docs/ASPS_DATA_FLOW.md §3`; `Business/Messaging/RealTimeAlertListener.cs`)

### Security

- **CURVE encryption:** Ports 50001 and 50002 use CurveZMQ. Server keypair stored in `appsettings.Development.json` under `Security:CurveEnabled`; public key written to `curve-server-public-key.txt` for agents to read. (`ASPSBackend14_J/ASPSBackend/appsettings.Example.json`)
- **Token auth:** 64-hex-character tokens from `RandomNumberGenerator`; bound to `DeviceUid + UserKeyField`; 24h expiry (configurable), 7-day refresh window (`TokenManagement:TokenExpirationPeriod=1440`, `MaxExpiration=10080`). (`ASPSBackend14_J/ASPSBackend/appsettings.json`)
- **Rate limiting:** Sliding-window `RateLimiter` on `RegisterDevice` (3/min), `RequestToken`/`RefreshToken` (5/min). (`docs/system-specifications/ASPS_System_Overview.md §9`)
- **Known security debt:** `TypeNameHandling.Auto` in Newtonsoft.Json (deserialization risk); `ASView` collections lack fine-grained locking; MySQL port 3306 exposed in `docker-compose.yml` (no SSL); ports 5555/5556 bound to `*:` instead of `localhost`; `LoadDataAsync().Wait()` blocking call at startup. (`STATE.md §What Needs Attention`)

### Open Items

- Replace `TypeNameHandling.Auto` with explicit discriminators (security debt, no JIRA key assigned)
- Add SSL to MySQL connection string (security debt)
- Refactor singletons consuming scoped services (DI scope validation disabled — `Program.cs` TODO comment)
- `ASView` collection locking under high concurrency load
- SCRUM-904: User Consent Preferences data model + UI (status: To Do)
- SCRUM-895: Creating TrackedDomain from Analysis Result (status: In Progress)

---

## 2. Admin Portal + REST (WebApi)

**Status:** built — Razor Pages admin dashboard + REST controllers + SignalR hub operational; Keycloak OIDC wired and tested (`ASPSBackend14_J/WebApi/Program.cs`)

### Purpose

A stateless ASP.NET Core web application providing the admin dashboard (Razor Pages), a REST API for programmatic access, and a SignalR hub for real-time admin notifications. It has **zero direct database access** — all data operations travel over CQRS via NetMQ to the Backend.

### Components

| Component | Path | Responsibility |
|-----------|------|----------------|
| `Program.cs` | `WebApi/Program.cs` | Keycloak OIDC config, CQRS client wiring, SignalR, Razor Pages, Forwarded Headers |
| **Razor Pages** | `WebApi/Pages/` | Admin UI: Index, Users, Devices, DeviceAlerts, AnalysisResults, KnownPhishingWebsites, BankWebsites, BlacklistedPhoneNumbers, Roadmaps, Simulations, SystemConfigurations, TrackedDomains, WebsiteCategories |
| **REST Controllers** | `WebApi/Controllers/` | `UsersController`, `UserDevicesController`, `AlertsController`, `SimulationsApiController`, `SystemController`, `VersionController` |
| `NotificationsHub` | `WebApi/Hubs/NotificationsHub.cs` | SignalR hub at `/notificationshub`; validates device tokens via CQRS query; groups devices |
| `CQRSClient` | `WebApi/Services/` | Sends typed commands/queries to Backend port 5556 via NetMQ REQ |
| `NetMQClientService` | `WebApi/Services/` | Raw NetMQ REQ to Backend port 5555 |
| `AdminClaimsTransformer` | `WebApi/` | Maps Keycloak claims / known usernames → `Admin` role claim |
| `SimulationRunner` | (shared from Business) | Background service for scripted simulations; also registered in WebApi |

### Tech Stack

- ASP.NET Core (.NET 8), Razor Pages, MVC Controllers
- SignalR (WebSockets-based hub)
- Keycloak OIDC (`Microsoft.AspNetCore.Authentication.OpenIdConnect`); dev authority: `http://localhost:8180/realms/asps` (`WebApi/appsettings.Development.json`)
- NetMQ 4.0.1.13 for CQRS client
- Swashbuckle/Swagger at `/swagger` (dev mode)
- Kestrel binds: HTTP `0.0.0.0:5001`, HTTPS `0.0.0.0:5002`

### Interfaces / Contracts

**Inbound (HTTP/HTTPS):**

- `GET/POST /` — Razor Pages admin dashboard
- `GET/POST /Users`, `/Devices`, `/DeviceAlerts`, `/AnalysisResults`, etc.
- REST: `GET /api/users`, `GET /api/userdevices/{uid}`, `GET/POST /api/alerts`, `GET /api/version`
- SignalR: `ws[s]://host/notificationshub?deviceUid=&token=` (devices) or no params (admin)
- Swagger: `GET /swagger` (dev only)

**Outbound:**

- NetMQ REQ to `tcp://localhost:5556` (CQRS typed commands/queries)
- NetMQ REQ to `tcp://localhost:5555` (raw CQRS)
- SignalR push to connected admin browsers: `alert`, `notification` events

**Authorization:**

- All Razor Pages protected by `AdminPolicy` (requires `Admin` role) except `/Login`, `/Logout`, `/DeviceLogin`, `/AccessDenied`
- Admin role granted via Keycloak `Administrators` group, `realm_access.admin`, or hardcoded dev usernames (`asps-admin`, `isaac`, `admin`) — (`WebApi/Program.cs:133`)

### Data Flow

Admin browser → HTTP POST to Razor Page → `ICQRSClient.SendCommandAsync` → NetMQ REQ to Backend port 5556 → `CQRSGateway` routes to handler → EF Core → MySQL → result back over NetMQ → Razor renders response.

For real-time: Backend ZMQ PUB (50002) → WebApi `NotificationSubscriber` (BackgroundService, SUB socket) → `IHubContext<NotificationsHub>.Clients.All.SendAsync()` → admin browser JS updates dashboard.

Source: (`docs/ASPS_DATA_FLOW.md §8`)

### Security

- **Keycloak OIDC:** PKCE Authorization Code flow (`ResponseType = Code`); `RequireHttpsMetadata = false` in dev (security debt for production). (`WebApi/Program.cs:88`)
- **Cookie auth:** `ASPS.Auth` cookie; `HttpOnly=true`, `SameSite=Lax`, 8h expiry with sliding renewal.
- **SignalR token validation:** Device connections validated via `ValidateDeviceTokenQuery` to Backend; invalid token → `Context.Abort()`. (`WebApi/Hubs/NotificationsHub.cs`)
- **Antiforgery:** Razor Pages POST handlers use `RequestVerificationToken` (e.g., Roadmap save). (`docs/ASPS_DATA_FLOW.md §9`)
- **No DB credentials in WebApi** — stateless by design; zero database access.
- **Known security debt:** Hardcoded admin username list in `Program.cs`; `RequireHttpsMetadata = false` must be `true` in production.

### Open Items

- Replace hardcoded admin username list with proper Keycloak group claim (`Program.cs` TODO)
- SCRUM-894: New Angular-based admin client (status: To Do)
- Production HTTPS configuration for Keycloak authority
- SCRUM-906: Auto-update extension via managed deployment (status: To Do)

---

## 3. Desktop Agent

**Status:** built — Python Windows application is fully operational; handles auth, CURVE encryption, remote-access detection, URL forwarding, and extension WebSocket server (`apps/desktop/win/src/main.py`)

**Full feature documentation:** [`DESKTOP_AGENT_FEATURES.md`](DESKTOP_AGENT_FEATURES.md) — covers all 23 features, edge cases, and known gaps.

### Purpose

A Python system-tray application running on the user's Windows PC. It bridges the Chrome extension (via local WebSocket) and the Backend (via ZMQ CURVE). It monitors remote-access software, forwards URL alerts to the Backend, receives analysis results, and executes protective actions locally (toasts, overlays, remote-session termination).

### Components

| File | Path | Responsibility |
|------|------|----------------|
| `main.py` | `apps/desktop/win/src/main.py` | Entry point; startup orchestration; `AntiScamApp` class |
| `config.py` | `src/config.py` | Ports: `BACKEND_REQ_PORT=50001`, `BACKEND_SUB_PORT=50002`, `EXTENSION_PORTS=[8080,8181,8282,8383,8484]` |
| `zmq_client.py` | `src/zmq_client.py` | ZMQ REQ socket to Backend port 50001; sends `UrlAlert`, `RemoteAccessAlert`, `RequestToken`, `RegisterDevice` |
| `notification_client.py` | `src/notification_client.py` | ZMQ SUB socket on port 50002; receives `AnalysisResult`, `ImmediateDangerNotification`, `SetBrowserTabsPolicyNotification` |
| `extension_server.py` | `src/extension_server.py` | WebSocket server; scans ports 8080→8484 for first free; handles `url_check`, `track_url_alert`, `ping` messages from extension |
| `auth_manager.py` | `src/auth_manager.py` | Token lifecycle: request, refresh, expiry check; stores via `keyring` with file-based fallback |
| `hardware_id.py` | `src/hardware_id.py` | Stable device UID from motherboard/BIOS/disk serial (PowerShell); fallback to Windows `MachineGuid`; format `PC-{16hex}`; cached at `%APPDATA%\AntiScam\device_id` |
| `remote_monitor.py` | `src/remote_monitor.py` | `psutil`-based detection of AnyDesk, TeamViewer, ChromeRemoteDesktop, QuickAssist, LogMeIn, ConnectWise, RustDesk, VNC, RDP, Ammyy Admin; GeoIP2 for country lookup; adaptive polling (5s running / 30s idle / 2s DangerMode) |
| `scan_service.py` | `src/services/scan_service.py` | URL scanning logic; local cache check → auth check → `zmq_client.send_url_alert()` |
| `protection_service.py` | `src/services/` | Executes `ProtectiveAction` items from analysis results; system-tray toasts, CenteredToast overlay, remote-session termination |
| `cache_manager.py` | `src/cache_manager.py` | Local URL result cache with configurable TTL |
| `notification_manager.py` | `src/notification_manager.py` | Routes incoming Backend notifications to handlers |
| `handlers/extension_handler.py` | `src/handlers/` | Dispatches extension messages: `url_check`, `track_url_alert`, `ping` |
| `handlers/notification_handler.py` | `src/handlers/` | Processes Backend notification messages; broadcasts typed events to extension |
| `core/container.py` | `src/core/container.py` | Dependency injection container |
| `enums.py` | `src/enums.py` | Python-side enums mirroring `Common.Enums` |
| `browser_tabs_policy` | `src/` | Manages `BrowserTabsPolicyOverride` (mode + valid_until); resets on agent restart |

### Tech Stack

- Python 3.11 (Windows)
- `pyzmq` — ZeroMQ bindings with CURVE encryption
- `websockets` — async WebSocket server
- `psutil` — remote-access process detection
- `geoip2` — country lookup for remote connections
- `keyring` — OS credential store for token persistence
- Distributed as `AntiScam.exe` (PyInstaller/Inno Setup) — `apps/desktop/win/build/`, `apps/desktop/win/installer.iss`

Source: (`apps/desktop/win/src/config.py`; `apps/desktop/win/requirements.txt`; `docs/system-specifications/ASPS_System_Overview.md §6`)

### Interfaces / Contracts

**Outbound to Backend (ZMQ REQ, port 50001, CURVE):**

```json
{
  "AlertType": "UrlAlert",
  "DeviceInfo": { "DeviceUid": "PC-...", "DeviceType": 1, "OperatingSystem": 1, "MACAddress": "..." },
  "Timestamp": "2026-06-27T00:00:00Z",
  "Token": "<64-hex>",
  "Url": "https://...",
  "Trackers": [...],
  "IFrameDomains": [...],
  "IPAddress": "...",
  "TabId": "..."
}
```

`RemoteAccessAlert` includes `RemoteAccessApp` (enum int), `Direction` (string: `"incoming"|"outgoing"|"unknown"`), `ConnectionStatus`, `SessionStatus`, `BrowserTabs[]`, forensic fields. (`docs/ASPS_DATA_FLOW.md §4.3`)

**Inbound from Backend (ZMQ SUB, port 50002, CURVE):** topic filter `device:{deviceUid}`.

**WebSocket to Extension:** `ws://localhost:{first-free-in-8080-8484}`. Messages: `url_check` (in), `url_result` (out), `remote_access_alert` (out), `immediate_danger_started/ended` (out), typed analysis events.

### Data Flow

Chrome extension sends `url_check` → `extension_server.py` → `scan_service.check_url()` → `zmq_client.send_url_alert()` (ZMQ REQ port 50001) → Backend → ACK returned immediately.

Backend publishes result → `notification_client.py` SUB socket receives → `notification_manager` routes → `notification_handler.py` executes protective actions + broadcasts to extension via WebSocket.

`remote_monitor.py` polls processes on adaptive schedule → builds `RemoteAccessAlert` → `zmq_client.send_remote_access_alert()`.

Source: (`docs/ASPS_DATA_FLOW.md §3.2–3.4`)

### Security

- **CURVE:** All ZMQ connections use CURVE. Agent reads server public key from `curve-server-public-key.txt` (Z85-encoded). Ephemeral client keypair generated per connection.
- **Token auth:** `auth_manager.py` uses `keyring` OS credential store (file-based fallback) to persist token across restarts. Token validated on every alert submission.
- **Localhost filter:** Extension WebSocket server binds `127.0.0.1` only; port scan limited to `[8080,8181,8282,8383,8484]`. (`apps/desktop/win/src/config.py`)
- **DangerMode:** When `ImmediateDangerNotification` received, polling drops to 2s and debounce is bypassed to ensure rapid response. (`docs/ASPS_DATA_FLOW.md §6`)
- **Known security debt:** WebSocket to extension is plain `ws://` (no TLS); `BrowserTabsPolicy` override not persisted across restarts; `browser_history.py` module present but active use TBD.

### Open Items

- SCRUM-863: Auto-update via Velopack (status: In Review)
- SCRUM-897: Velopack self-update implementation (status: To Do)
- SCRUM-901: Code anti-reverse-engineering/obfuscation (status: To Do)
- SCRUM-902: Code-signing with Authenticode certificate (status: To Do)
- `signalr_client.py` present in `src/` — purpose/active use TBD

---

## 4. Mobile Agent

**Status:** planned — no code directory exists in the repository. The wire protocol and target data flow are fully specified in design docs. JIRA tracks Android (SCRUM-898) and iOS (SCRUM-899) agent update tasks, confirming mobile work is on the roadmap but not yet started. (`docs/ASPS_DATA_FLOW.md §10`; KE: `agents/mobile.md` — "Planned stack; the mobile project has not started yet"; SCRUM-898, SCRUM-899)

**Note:** The LIMAT `CustomerPortal` (a separate project at `c:\Jobs\LIMAT\CustomerPortal\`) is **not** an ASPS subsystem. Do not conflate.

### Purpose

Android and iOS agents that will perform the same role as the Desktop Agent but for mobile devices: URL monitoring (via Accessibility Service / Network Extension), SMS/call screening, remote-access app detection, and forwarding alerts to the Backend over the same ZMQ CURVE wire protocol.

### Components

No implementation exists. Target architecture from `docs/ASPS_DATA_FLOW.md §10`:

- **Android:** Accessibility Service (URL hooks), SMS BroadcastReceiver, CallScreeningService, App-detect (PackageInstaller observer), ZMQ-CURVE client
- **iOS:** Network Extension (URL hooks), Message Filter Extension, Call Directory Extension, periodic blacklist sync (no real-time per-call API)
- **Shared:** ZMQ REQ client (port 50001), ZMQ SUB listener (port 50002), same alert JSON schema as Desktop Agent

### Tech Stack

- Android: Kotlin (planned) (`CLAUDE.md §Stack`)
- iOS: Swift (planned) (`CLAUDE.md §Stack`)
- ZeroMQ CURVE transport (same wire protocol as Desktop Agent)
- `DeviceInfo.DeviceType = 2` (MobilePhone), `OperatingSystem = 4` (Android) or `5` (iOS)

### Interfaces / Contracts

Wire protocol identical to Desktop Agent (same JSON alert shapes). Additional planned alert types (not yet added to Backend):

| Alert Type | Platform | Backend Status |
|------------|----------|----------------|
| `SmsAlert` | Android only | Not implemented |
| `EmailAlert` | Both (OAuth) | Not implemented |
| `PhoneAlert` | Android real-time / iOS post-hoc | Lookup via `BlacklistedPhoneNumbers` (ASPS-282) |
| `AppInstallAlert` | Android only | Not implemented |

Source: (`docs/ASPS_DATA_FLOW.md §10, §M4`)

### Data Flow

Identical to Desktop Agent for `UrlAlert` and `RemoteAccessAlert`. Additional mobile flows: SMS scan → `SmsAlert` (planned); incoming call → local blacklist lookup → `PhoneAlert` (planned).

### Security

Will use the same CURVE encryption and token auth as Desktop Agent. iOS imposes `no real-time per-call blocking` — only periodic blacklist sync via `CallDirectory` extension. (`docs/ASPS_DATA_FLOW.md §M3`)

### Open Items

- SCRUM-898: Android Agent — in-app update via Play Store (To Do)
- SCRUM-899: iOS Agent — update notification (To Do)
- Full Android and iOS implementation: no JIRA ticket for initial build found; TBD when mobile project is formally green-lit
- Backend alert handlers for `SmsAlert`, `EmailAlert`, `AppInstallAlert` not yet created

---

## 5. Browser Extension

**Status:** built — Chrome MV3 extension v0.0.1.4 is fully operational; URL scanning, warning overlays, remote-access overlay, and popup UI all implemented (`apps/extension/chrome/manifest.json`)

### Purpose

A Chrome Manifest V3 extension that provides real-time browser protection: it detects URLs visited by the user, forwards them via local WebSocket to the Desktop Agent, and renders warning/block overlays based on the analysis result. It also displays an immediate-danger overlay when the Desktop Agent signals an incoming remote-access session.

### Components

| File | Responsibility |
|------|----------------|
| `manifest.json` | MV3 config; permissions: `activeTab`, `tabs`, `webNavigation`, `storage`, `notifications`, `alarms`, `cookies`; host: `<all_urls>` |
| `background.js` | Service worker (705 lines); tab lifecycle hooks (`onUpdated`, `webNavigation.onCompleted`, `onHistoryStateUpdated`); WebSocket message dispatch; protective action routing |
| `content.js` | Content script (446 lines); tracker extraction (Facebook Pixel, GA4/UA); warning/block display injection; runs at `document_end` on `<all_urls>` |
| `content.css` | Styles for injected warning elements |
| `popup.js` / `popup.html` | Popup UI (776 lines); connection status, risk score circle, domain, feedback (thumbs up/down → Google Sheets) |
| `services/ConnectionService.js` | WebSocket management (476 lines); port scan 8080→8484; exponential backoff (1s→30s max); heartbeat every 10s; keepalive every 20s; message queue during disconnect |
| `services/ScanService.js` | URL scan orchestration (242 lines); local cache (1h TTL, 1000 entries max); sends `url_check` message |
| `services/ProtectionService.js` | Warning execution (141 lines); maps risk score to overlay type |
| `services/CacheService.js` | URL result cache (1h TTL, 1000 max entries) |
| `warning/RemoteAccessWarning.js` | Shadow DOM (closed) remote-access overlay; MutationObserver anti-tampering |
| `warning/FrictionController.js` | 7-second countdown + checkbox required before dismissal |
| `state/StateManager.js` | Reactive state with Chrome storage persistence |

### Tech Stack

- Vanilla JavaScript (no framework), Chrome Manifest V3
- Chrome Extension APIs: `tabs`, `webNavigation`, `storage`, `notifications`, `alarms`, `cookies`
- WebSocket (browser native) for Desktop Agent communication
- Shadow DOM (closed) for tamper-resistant overlays

Source: (`apps/extension/chrome/manifest.json`; `docs/system-specifications/ASPS_System_Overview.md §7`)

### Interfaces / Contracts

**Outbound (WebSocket to Desktop Agent):**

```json
{ "type": "url_check", "url": "https://...", "trackers": [...], "iframes": [...], "ipAddress": "...", "tabId": "123" }
```

**Inbound (WebSocket from Desktop Agent):**

```json
{ "score": 35, "riskType": ["phishing"], "protectiveAction": 4 }
```

Also receives: `remote_access_alert`, `immediate_danger_started`, `immediate_danger_ended`.

**Warning Levels:**

| Level | Type | Score Range |
|-------|------|-------------|
| 1 | Notify | 61–90 |
| 2 | Banner | 61–90 |
| 3 | Modal | 31–60 |
| 4 | Block | 0–30 |

Source: (`docs/system-specifications/ASPS_System_Overview.md §7`)

### Data Flow

1. `chrome.tabs.onUpdated` / `webNavigation.onCompleted` fires.
2. `ScanService.scan()` skips localhost URLs, checks local cache.
3. Cache miss → collects trackers/iframes via `content.js` → sends `url_check` via WebSocket.
4. Desktop Agent forwards to Backend and returns ACK.
5. Backend publishes result → Agent receives → broadcasts back via WebSocket.
6. `ScanService.handleResult()` → `ProtectionService` renders appropriate overlay.

Source: (`docs/ASPS_DATA_FLOW.md §3.1`)

### Security

- **Anti-tampering:** Remote-access warning rendered in closed Shadow DOM; `MutationObserver` re-injects if host element removed.
- **Friction:** 7-second countdown + checkbox required to dismiss remote-access overlay.
- **Localhost-only WebSocket:** `ConnectionService` only connects to `127.0.0.1:{port}`.
- **Known security debt:** WebSocket is plain `ws://` (no TLS). Extension popup sends feedback to Google Sheets — unclear if any PII is transmitted (`popup.js` feedback logic TBD).
- SCRUM-905 and SCRUM-906 track extension update/publishing pipeline (both To Do).

### Open Items

- SCRUM-906: Auto-update extension to latest version (To Do)
- SCRUM-905: Extension publishing prerequisite tasks (To Do)
- SCRUM-912: Chrome Web Store (CWS) publishing prerequisites (To Do)
- Feedback mechanism (Google Sheets) — PII risk not yet assessed
- Manifest version is `0.0.1.4`; release signing flow not yet established

---

## 6. Users Portal

**Status:** planned (design only) — no source code directory exists in the repository. The concept is described in design documents (`docs/system-specifications/מסמך ארכיטקטורה: שכבת המשתמש…md`) under the name "User Layer", but no web portal has been built. The Admin Portal (`WebApi/`) serves administrators only and is explicitly not a users portal. No JIRA ticket was found for a built users portal in either SCRUM or SPS projects.

**Important:** The `CustomerPortal` under `c:\Jobs\LIMAT\CustomerPortal\` belongs to the **separate LIMAT project** and is NOT an ASPS subsystem.

### Purpose

A planned end-user-facing web portal where protected users (not admins) could view their own risk scores, alert history, account settings, and protection status. This is distinct from the admin dashboard (`WebApi/`).

### Components

No implementation exists. Design artifacts:

- `docs/system-specifications/מסמך ארכיטקטורה: שכבת המשתמש (User Layer) במערכת ASPS – גרסה מורחבת.md` — Hebrew architecture doc describing the User Layer: `UDUser` entity, cross-device correlation, `UserRiskScore` axes (`vulnerability_score`, `exposure_score`), dimensions, `FinalRiskScore`.
- `docs/SCRUM-904-user-risk-score-design.md` — Design doc for the User Risk Score algorithm feeding the planned portal.
- `Business/Services/UserRiskScoreService.cs` — Backend service that computes and stores URS (SCRUM-904, built as part of the backend, not a portal).

### Tech Stack

TBD — no decision made. Likely ASP.NET Core Razor Pages or Angular (SCRUM-894 proposes Angular for admin; same choice may apply).

### Interfaces / Contracts

TBD. Will need to access backend data via CQRS queries (same pattern as `WebApi`). Will need Keycloak OIDC for end-user authentication (separate realm or client from the admin one).

### Data Flow

TBD. Expected pattern: user browser → portal → CQRS queries to Backend → user's own alert/risk data.

### Security

TBD. Requires user-scoped Keycloak client; users must only see their own data (separate from admin `AdminPolicy`).

### Open Items

- No JIRA ticket found for building the users portal (TBD)
- SCRUM-913: User Consent Preferences data model + UI (To Do) — may be a first step
- User Layer architecture document exists (`docs/system-specifications/מסמך ארכיטקטורה…md`) but is design-only
- Decision needed: standalone app vs. user-facing pages added to `WebApi/`

---

## 7. URL Analyzer

**Status:** built — Python FastAPI service is fully implemented with ML classifier, WHOIS, rules engine, content scraping, and reputation checks (`Analyzers/basic-url-analyzer/api.py`)

### Purpose

A standalone Python microservice that performs deep multi-stage analysis of URLs for scam/phishing detection. Invoked by the Backend as a Python subprocess (not over HTTP), with a 30-second timeout. Returns a structured JSON risk assessment consumed by `UDUrlAnalyzer`.

### Components

| File | Path | Responsibility |
|------|------|----------------|
| `api.py` | `Analyzers/basic-url-analyzer/api.py` | FastAPI app; endpoints: `POST /analyze`, `GET /analyze?url=`, `GET /health`, `GET /`; also supports subprocess CLI invocation |
| `core/analyzer.py` | `core/analyzer.py` | `ScamAnalyzer` — singleton orchestrating the full analysis pipeline |
| `core/whois_checker.py` | `core/` | WHOIS domain age, registrar, country, privacy protection |
| `core/content_extractor.py` | `core/` | Playwright-based HTML extraction: title, headings, forms, CTAs, links |
| `core/rules_engine.py` | `core/` | 30+ regex patterns for financial scam language, pressure tactics, fraud indicators |
| `core/ml_classifier.py` | `core/` | scikit-learn `LogisticRegression` + TF-IDF vectorization; scam probability |
| `core/reputation_checker.py` | `core/` | DuckDuckGo search for domain reputation against known-reputable sources |
| `core/category_classifier.py` | `core/` | Category classification: crypto, investment, ecommerce, romance, banking, etc. |
| `core/purpose_classifier.py` | `core/` | Purpose classification: investment scam, phishing, romance, etc. |
| `core/url_inspector.py` | `core/` | URL structure inspection |
| `core/llm_explainer.py` | `core/` | LLM-based explanation generation (TBD/optional) |
| `analyze.py` | Root | CLI entrypoint; used by Backend subprocess invocation |
| `models/` | `models/` | Trained ML model files |
| `scripts/` | `scripts/` | Training and evaluation scripts |

### Tech Stack

- Python 3.x
- FastAPI + uvicorn (API server; port configurable, default 8000 per `api.py` comment)
- `playwright` — browser automation for content extraction
- `python-whois` — WHOIS lookups
- `scikit-learn`, `numpy` — ML classifier
- `beautifulsoup4`, `lxml` — HTML parsing
- `duckduckgo_search` — web reputation checks
- `langdetect` — language detection (skips ML for non-English)
- `pyproject.toml` used for dependencies

Source: (`Analyzers/basic-url-analyzer/api.py`; `docs/system-specifications/ASPS_System_Overview.md §4`)

### Interfaces / Contracts

**As subprocess (primary invocation by Backend):**

```
python analyze.py "<url>" --json
```

Stdout: JSON risk assessment object. 30-second timeout enforced by Backend (`Business/RealtimeAnalysis/UserDomain/UDAnalysis.cs`; `docs/ASPS_DATA_FLOW.md §3.3 Step 10`).

**As FastAPI service (standalone/testing):**

```
POST /analyze   Body: { "url": "https://..." }
GET  /analyze?url=https://...
GET  /health
GET  /
```

**Output schema:**

```json
{
  "url": "...",
  "risk_score": 0,
  "risk_level": "HIGH",
  "is_scam": true,
  "confidence": 0.92,
  "domain_age_days": 2,
  "detected_language": "en",
  "category": "banking",
  "red_flags": ["urgent language", "new domain", "phishing template"],
  "error": null
}
```

`risk_score`: 0–100 where **0 = dangerous, 100 = safe** (inverted scale). (`docs/system-specifications/ASPS_System_Overview.md §4`)

### Data Flow

Backend `UDUrlAnalyzer` builds subprocess `ProcessStartInfo` → spawns Python → `analyze.py` runs full pipeline → writes JSON to stdout → Backend parses and stores as `AnalysisResults.JsonValue` blob → raises `AnalysisResultReceived` event.

FastAPI mode: used for standalone testing and can be run independently via `uvicorn api:app`.

Source: (`docs/ASPS_DATA_FLOW.md §3.3 Step 10`)

### Security

- **No authentication** on FastAPI endpoints — intended for internal/localhost use only; not exposed externally in normal operation.
- CORS configured as `allow_origins=["*"]` in `api.py` — acceptable only if not exposed publicly.
- Subprocess invocation avoids network exposure; backend controls the Python executable path via `Python:ExecutablePath` in `appsettings.json`.
- `llm_explainer.py` present — any LLM API keys or external calls would introduce a data-exfiltration vector; TBD whether it's active.

### Open Items

- `core/llm_explainer.py` present — confirm whether active and if it calls external APIs (TBD)
- CORS `allow_origins=["*"]` — needs restriction if the FastAPI server is ever exposed
- No authentication on `/analyze` endpoint; must not be exposed externally
- `CATEGORY_UPGRADE_PLAN.md` and `ML_TRAINING_GUIDE.md` in root indicate planned classifier improvements

---

## Open Questions / TBD Appendix

| # | TBD | Where to Resolve |
|---|-----|-----------------|
| 1 | `TypeNameHandling.Auto` replacement strategy | Code review `Business/Messaging/`; JIRA ticket needed |
| 2 | `ASView` locking refactor for high concurrency | `Business/Views/ASView.cs`; `STATE.md` |
| 3 | MySQL SSL enforcement in connection string | `appsettings.json`; DevOps |
| 4 | Ports 5555/5556 bound to `*:` instead of `localhost` | `appsettings.json`; security review |
| 5 | `signalr_client.py` in Desktop Agent — purpose and active status | `apps/desktop/win/src/signalr_client.py` |
| 6 | `browser_history.py` in Desktop Agent — active use or dead code | `apps/desktop/win/src/browser_history.py` |
| 7 | `core/llm_explainer.py` in URL Analyzer — active + external API calls? | `Analyzers/basic-url-analyzer/core/llm_explainer.py` |
| 8 | Users Portal: standalone app vs. user-facing pages in `WebApi/` | Architecture decision; CEO/CTO |
| 9 | Users Portal JIRA ticket — none found | Search SPS project for related issues; create ticket |
| 10 | Mobile Agent initial-build JIRA ticket — none found | Mobile project green-lit by CEO; ticket needed |
| 11 | Extension popup feedback → Google Sheets — PII assessment | `apps/extension/chrome/popup.js` review |
| 12 | Admin username hardcoding in `WebApi/Program.cs:133` — timeline to remove | `WebApi/Program.cs`; Keycloak group configuration |
| 13 | `RequireHttpsMetadata = false` in Keycloak OIDC config — production readiness | `WebApi/Program.cs:88`; cert provisioning |
| 14 | Docker MySQL port 3306 exposed without SSL | `docker-compose.yml`; DevOps |

---

*Last updated: 2026-06-27. Sources: repo code under `C:\Jobs\ASPS\GitHub\Software\`, `docs/ASPS_DATA_FLOW.md`, `docs/system-specifications/ASPS_System_Overview.md`, Knowledge Engine (KE), JIRA project SCRUM (aspsjira.atlassian.net), `CLAUDE.md`.*
