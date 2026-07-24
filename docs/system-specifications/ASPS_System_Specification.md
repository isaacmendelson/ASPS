# ASPS System Specification

**Version:** 2.0 | **Audit date:** 2026-07-22 | **Status:** Authoritative as-built specification and implementation-gap audit

ASPS (Anti-Scam Protection System) is a distributed threat-protection platform intended to defend end users — particularly elderly, immigrant, and tech-anxious adults — from online scams, phishing attacks, and unauthorized remote-access sessions. The repository contains substantial Backend, WebApi, Windows Agent, Chrome Extension, and URL Analyzer implementations. No inspected subsystem is fully compliant with all requirements in `ASPS_Unified_System_Requirements_2026-07-15.md`, and the checked-in Docker topology is not deployable without correction. This document distinguishes **required behavior**, **production-reachable behavior**, **partial or defective behavior**, and **planned behavior**.

**Audit scope.** Requirements and design files under `docs/` and `docs/system-specifications/` were compared with source under `ASPSBackend14_J/`, `apps/`, `Analyzers/`, the root Dockerfiles and `docker-compose.yml`. Repository code and runtime wiring are the implementation evidence; design documents, ticket states and database migration files alone are not. External infrastructure and an already-running production database were outside the inspected snapshot.

---

## Subsystem Status at a Glance

| # | Subsystem | Status | Primary Path | Key Citation |
|---|-----------|--------|-------------|--------------|
| 1 | Backend (Host Service) | **Partial** — core intake, analysis, persistence and publish paths exist; several required pipelines are absent or dormant | `ASPSBackend14_J/ASPSBackend/`, `Business/`, `Common/`, `Interface/` | `ASPSBackend14_J/ASPSBackend/Program.cs` |
| 2 | Admin Portal + REST | **Partial** — Razor/CQRS and a SignalR hub endpoint exist; the Backend-notification bridge is absent and authorization/TLS/version gaps remain | `ASPSBackend14_J/WebApi/` | `ASPSBackend14_J/WebApi/Program.cs` |
| 3 | Desktop Agent | **Partial** — core bridge and monitoring exist; notification reliability, local authentication, update and several actions are missing or defective | `apps/desktop/win/src/` | `apps/desktop/win/src/main.py` |
| 4 | Mobile Agent | **Not implemented** — design only | *(no directory)* | `docs/ASPS_DATA_FLOW.md §10` |
| 5 | Browser Extension | **Partial** — telemetry and connection paths exist; warning/block and remote-overlay paths have runtime defects | `apps/extension/chrome/` | `apps/extension/chrome/manifest.json` |
| 6 | Users Portal | **Not implemented** — design only | *(no directory)* | `docs/system-specifications/מסמך ארכיטקטורה_ שכבת המשתמש (User Layer) במערכת ASPS – גרסה מורחבת.md` |
| 7 | URL Analyzer | **Partial** — internal pipeline exists; Backend contract, timeout, SSRF isolation and deployment are incomplete | `Analyzers/basic-url-analyzer/` | `Analyzers/basic-url-analyzer/analyze.py` |
| 8 | Container deployment | **Not deployable as checked in** — Backend image path, schema migration, TLS and secret handling are unresolved | `Dockerfile.backend`, `Dockerfile.webapi`, `docker-compose.yml` | `Dockerfile.backend:72` |

Status definitions used throughout this document:

- **Implemented** — the complete required behavior is production-reachable and the inspected contracts align.
- **Partial** — meaningful implementation exists, but one or more required paths, contracts, persistence guarantees, clients, or operational controls are absent or defective.
- **Not implemented** — no production-reachable implementation of the requirement was found. Design documents, stubs, entities, and dormant code do not count as implementation.

---

## Verified System Architecture & Data Flow

The principal runtime path is shown below. An intake acknowledgement on port 50001 is not the final analysis result; the final result is published asynchronously on port 50002. The last warning/block step is intended architecture but is currently defective because the Extension background and content-script message contracts differ.

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
[Chrome Extension warning/block — intended; current message routing is defective]

[Admin browser] ← HTTP/Razor/CQRS (port 5001/5002) ← WebApi ← NetMQ REQ (port 5556) ← Backend
[SignalR hub endpoint exists, but no Backend PUB subscriber/forwarder is registered]
```

### Ports Reference

| Port | Direction | Protocol | Encryption | Purpose |
|------|-----------|----------|------------|---------|
| 50001 | Agent → Backend | ZMQ REQ/ROUTER | CURVE (CurveZMQ) | Device alert intake |
| 50002 | Backend → Agent | ZMQ PUB/SUB | CURVE (CurveZMQ) | Analysis result notifications |
| 5555 | WebApi → Backend | ZMQ REQ/REP | None; bound to `*` and host-published by compose | Legacy/raw CQRS message processor |
| 5556 | WebApi → Backend | ZMQ REQ/REP | None; bound to `*` and host-published by compose | Typed CQRS gateway |
| 5001 | Browser → WebApi | HTTP | None | Razor Pages admin UI; the only WebApi port published by compose |
| 5002 | Browser → WebApi | HTTPS | Configured but no compose exposure/certificate provisioning | Intended HTTPS admin endpoint |
| 3306 | Backend → MySQL | MySQL protocol | None (security debt — no SSL) | Persistent storage |
| 8080–8484 | Extension ↔ Agent | WebSocket (`ws://localhost`) | None; no client authentication or Origin allow-list | Browser extension ↔ desktop agent; first free port in `[8080,8181,8282,8383,8484]` |
| 8180 | WebApi → Keycloak | HTTP | None (dev only) | OIDC authentication (dev Keycloak instance) |

Source: (`CLAUDE.md`; `apps/desktop/win/src/config.py`; `ASPSBackend14_J/ASPSBackend/appsettings.json`)

---

## 1. Backend (Host Service)

**As-built status: Partial.** Core intake, token validation, event routing, analysis, persistence, CQRS and notification-publish services are wired in `ASPSBackend14_J/ASPSBackend/Program.cs`. Phone/message/archive pipelines, reliable notification delivery and several consolidated User Layer requirements are absent. Migration files exist, but this audit does not assume that they are applied to any target database.

### Purpose

The ASPSBackend is the central analysis and messaging engine. It receives device alerts from field agents over encrypted ZMQ, runs multi-stage fraud analysis, persists results to MySQL, and publishes analysis outcomes back to agents and the admin portal.

### Components

| Component | Path | Responsibility |
|-----------|------|----------------|
| `ASPSBackend/Program.cs` | `ASPSBackend14_J/ASPSBackend/Program.cs` | Entry point: DI wiring, migration, service startup |
| `RealTimeAlertListener` | `Business/Messaging/RealTimeAlertListener.cs` | ZMQ RouterSocket on port 50001; receives `UrlAlert`, `TrackUrlAlert`, `RemoteAccessAlert`, token and registration messages |
| `CQRSGateway` | `Business/Messaging/` | ZMQ on port 5556; routes typed commands/queries from WebApi |
| `NetMQMessageProcessor` | `Business/Messaging/` | ZMQ on port 5555; lower-level CQRS channel |
| `ASView` | `Business/Views/ASView.cs` | Singleton in-memory read model; caches users, devices, alerts, 506K+ phishing URLs |
| `TokenStore` | `Business/Services/TokenStore.cs` | Write-through token cache (memory + MySQL `DeviceTokens` table) |
| `CurveKeyManager` | `Business/Services/CurveKeyManager.cs` | Manages the ZMQ CURVE server keypair and writes a runtime-generated public-key text file |
| `UserDomainManagerService` | `Business/RealtimeAnalysis/UserDomain/` | Lazy-init per-user `UDAnalysisManager` instances |
| `UDAnalysisManager` | `Business/RealtimeAnalysis/UserDomain/UDAnalysisManager.cs` | Orchestrates per-user analysis pipeline via domain events |
| `UDAnalysis` | `Business/RealtimeAnalysis/UserDomain/UDAnalysis.cs` | Runs the phishing, URL, remote-access and tracked-URL analyzer chain |
| `UDUserAnalyzer` | `Business/RealtimeAnalysis/UserDomain/` | Cross-device correlation; detects `ImmediateDanger` (remote session + sensitive browser tab) |
| `AlertPersistenceActor` | `Business/RealtimeAnalysis/AlertPersistenceActor.cs` | Persists `DeviceAlertEntity` rows to MySQL |
| `AnalysisPersistenceActor` | `Business/RealtimeAnalysis/AnalysisPersistenceActor.cs` | Persists `AnalysisResultContainer` JSON blobs |
| `ImmediateDangerPersistanceActor` | `Business/RealtimeAnalysis/ImmediateDangerPersistanceActor.cs` | Persists `ImmediateDanger` rows |
| `NotificationPublisher` / `NotificationPublisherActor` | `Business/Messaging/NotificationPublisher.cs` | ZMQ PUB on port 50002; publishes device and user topics. Agent clients currently subscribe only to device topics. |
| `UserRiskScoreService` | `Business/Services/UserRiskScoreService.cs` | Event-driven risk-score orchestration code; the Backend host does not register the complete repository/calculator dependency chain. |
| `SimulationRunner` | `Business/Services/SimulationRunner.cs` | Hosted scripted-alert runner; currently inserts raw records rather than invoking the normal analysis/event pipeline |
| `AppDbContext` | `Business/Data/EF/AppDbContext.cs` | EF Core 7 + Pomelo MySQL; 27 migration classes are checked in, but application to a target DB is deployment-dependent |

### Tech Stack

- .NET 8 console application (long-running `IHost`)
- EF Core 7 (`Pomelo.EntityFrameworkCore.MySql` 7.0.0) + MySQL 8.0.44
- NetMQ 4.0.1.13 (ZeroMQ bindings) with CURVE encryption
- Newtonsoft.Json 13.0.3 (`TypeNameHandling.Auto` — known security debt; see Open Items)
- NetTopologySuite 2.6.0 (spatial data support)
- DI scope validation disabled at startup (known tech debt; TODO comment in `ASPSBackend14_J/ASPSBackend/Program.cs`)

Source: (`ASPSBackend14_J/ASPSBackend/Program.cs`; `docs/system-specifications/ASPS_System_Overview.md §3`)

### Interfaces / Contracts

**Inbound (port 50001, ZMQ ROUTER, CURVE):**

| Message Type | Rate Limit | Description |
|-------------|------------|-------------|
| `RequestToken` | 5/min/device | Auth token request |
| `RegisterDevice` | 3/min/device | New device registration with user email |
| `RefreshToken` | 5/min/device | Token renewal |
| `UrlAlert` | token-validated | URL visited by user; includes `Url`, `Trackers`, `IFrameDomains`, `TabId`, `IPAddress`, `DeviceInfo`, `Token` |
| `TrackUrlAlert` | token-validated | Tracked-domain navigation/form context with source URL, duration, tab, timezone and optional scam correlation key |
| `RemoteAccessAlert` | token-validated | Remote-access app state change; includes `RemoteAccessApp` (enum), `Direction`, `SessionStatus`, `ConnectionStatus`, `BrowserTabs[]`, forensic fields |

**Outbound (port 50002, ZMQ PUB, CURVE):**

Topics: `device:{deviceUid}` and `user:{userKey}`. Message types include analysis results, ImmediateDanger lifecycle, tracked-domain distribution and browser-tabs policy. The Desktop Agent only subscribes to `device:{deviceUid}`, so the existence of user-topic publishing does not provide general multi-device delivery by itself.

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
10. No WebApi subscriber/forwarder for Backend PUB notifications is registered. Results reach subscribed device clients, but the intended live Backend→SignalR→Admin bridge is absent.

Source: (`docs/ASPS_DATA_FLOW.md §3`; `Business/Messaging/RealTimeAlertListener.cs`)

### Security

- **CURVE encryption:** Ports 50001 and 50002 use CurveZMQ when `Security:CurveEnabled` is enabled in `ASPSBackend14_J/ASPSBackend/appsettings.json`. `CurveKeyManager` writes a runtime-generated public-key text file; the repository does not provide a complete secure distribution/rotation flow. (`ASPSBackend14_J/ASPSBackend/appsettings.Example.json`)
- **Token auth:** 64-hex-character tokens from `RandomNumberGenerator`; bound to `DeviceUid + UserKeyField`; 24h expiry (configurable), 7-day refresh window (`TokenManagement:TokenExpirationPeriod=1440`, `MaxExpiration=10080`). (`ASPSBackend14_J/ASPSBackend/appsettings.json`)
- **Rate limiting:** Sliding-window `RateLimiter` on `RegisterDevice` (3/min), `RequestToken`/`RefreshToken` (5/min). (`docs/system-specifications/ASPS_System_Overview.md §9`)
- **Known security debt:** `TypeNameHandling.Auto` in Newtonsoft.Json (deserialization risk); `ASView` collections lack fine-grained locking; MySQL port 3306 exposed in `docker-compose.yml` (no SSL); ports 5555/5556 bound to `*:` instead of `localhost`; `LoadDataAsync().Wait()` blocking call at startup. (`STATE.md §What Needs Attention`)

### Open Items

- Replace `TypeNameHandling.Auto` with explicit discriminators (security debt, no JIRA key assigned)
- Add SSL to MySQL connection string (security debt)
- Refactor singletons consuming scoped services (DI scope validation disabled — `ASPSBackend14_J/ASPSBackend/Program.cs` TODO comment)
- `ASView` collection locking under high concurrency load
- SCRUM-904: User Consent Preferences data model + UI (status: To Do)
- SCRUM-895: Creating TrackedDomain from Analysis Result (status: In Progress)

---

## 2. Admin Portal + REST (WebApi)

**As-built status: Partial.** Razor Pages, REST controllers, CQRS clients, a SignalR hub endpoint and Keycloak wiring exist. No hosted subscriber forwards Backend PUB notifications into the hub. The checked-in container topology exposes HTTP only, several controller/debug/authentication paths need authorization hardening, and consolidated version, notification-delivery and retention administration is not implemented.

### Purpose

A stateless ASP.NET Core web application providing the admin dashboard (Razor Pages), a REST API and a SignalR hub endpoint. It has **zero direct database access** — data operations travel over CQRS via NetMQ to the Backend. The intended Backend-notification-to-SignalR forwarder is not implemented/registered.

### Components

| Component | Path | Responsibility |
|-----------|------|----------------|
| `WebApi/Program.cs` | `ASPSBackend14_J/WebApi/Program.cs` | Keycloak OIDC config, CQRS client wiring, SignalR, Razor Pages, Forwarded Headers |
| **Razor Pages** | `WebApi/Pages/` | Admin UI: Index, Users, Devices, DeviceAlerts, AnalysisResults, KnownPhishingWebsites, BankWebsites, BlacklistedPhoneNumbers, Roadmaps, Simulations, SystemConfigurations, TrackedDomains, WebsiteCategories |
| **REST Controllers** | `WebApi/Controllers/` | `UsersController`, `UserDevicesController`, `AlertsController`, `SimulationsApiController`, `SystemController`, `VersionController` |
| `NotificationsHub` | `WebApi/Hubs/NotificationsHub.cs` | SignalR hub at `/notificationshub`; validates supplied device credentials and groups devices, but accepts no-credential connections as “admin” without an authorization requirement |
| `CQRSClient` | `WebApi/Services/` | Sends typed commands/queries to Backend port 5556 via NetMQ REQ |
| `NetMQClientService` | `WebApi/Services/` | Raw NetMQ REQ to Backend port 5555 |
| `AdminClaimsTransformer` | `WebApi/` | Maps Keycloak claims / known usernames → `Admin` role claim |
| `SimulationRunner` | (shared from Business) | Background service for scripted simulations; also registered in WebApi |

### Tech Stack

- ASP.NET Core (.NET 8), Razor Pages, MVC Controllers
- SignalR (WebSockets-based hub)
- Keycloak OIDC (`Microsoft.AspNetCore.Authentication.OpenIdConnect`) when a `Keycloak` section is present. The tracked Docker configuration targets the configured HTTPS authority; without a Keycloak client ID, WebApi falls back to cookie-only development authentication (`ASPSBackend14_J/WebApi/Program.cs:60`).
- NetMQ 4.0.1.13 for CQRS client
- Swashbuckle/Swagger at `/swagger` (dev mode)
- Kestrel binds: HTTP `0.0.0.0:5001`, HTTPS `0.0.0.0:5002`

### Interfaces / Contracts

**Inbound (HTTP/HTTPS):**

- `GET/POST /` — Razor Pages admin dashboard
- `GET/POST /Users`, `/Devices`, `/DeviceAlerts`, `/AnalysisResults`, etc.
- REST: `GET /api/users`, `GET /api/userdevices/{uid}`, `GET/POST /api/alerts`, `GET /api/version`
- SignalR: `ws[s]://host/notificationshub?deviceUid=&token=` (devices); a connection with no credentials is currently accepted and treated as “admin” without verifying an admin identity
- Swagger: `GET /swagger` (dev only)

**Outbound:**

- NetMQ REQ to `tcp://localhost:5556` (CQRS typed commands/queries)
- NetMQ REQ to `tcp://localhost:5555` (raw CQRS)
- SignalR grouping/subscription methods exist, but no registered service publishes Backend results into the hub

**Authorization:**

- Razor Pages are protected by `AdminPolicy` except `/Login`, `/Logout`, `/DeviceLogin`, `/AccessDenied` and `/DebugClaims`. The SignalR hub is mapped without `RequireAuthorization`.
- Admin role granted via Keycloak `Administrators` group, `realm_access.admin`, or hardcoded dev usernames (`asps-admin`, `isaac`, `admin`) — (`WebApi/Program.cs:133`)

### Data Flow

Admin browser → HTTP POST to Razor Page → `ICQRSClient.SendCommandAsync` → NetMQ REQ to Backend port 5556 → `CQRSGateway` routes to handler → EF Core → MySQL → result back over NetMQ → Razor renders response.

The intended real-time path is Backend ZMQ PUB → WebApi subscriber → SignalR → Admin browser. The current WebApi registers `AddSignalR()` and maps the hub, but registers no PUB subscriber or `IHubContext` forwarder, so that path is not operational.

Source: (`docs/ASPS_DATA_FLOW.md §8`)

### Security

- **Keycloak OIDC:** Authorization Code flow (`ResponseType = Code`); `RequireHttpsMetadata` is forced to `false` whenever OIDC is enabled, including non-development environments (`ASPSBackend14_J/WebApi/Program.cs:85`).
- **Cookie auth:** `ASPS.Auth` cookie; `HttpOnly=true`, `SameSite=Lax`, 8h expiry with sliding renewal.
- **SignalR authorization:** Supplied device credentials are validated via `ValidateDeviceTokenQuery`, but missing credentials are accepted as an admin connection; the mapped hub has no authorization requirement (`ASPSBackend14_J/WebApi/Hubs/NotificationsHub.cs:39`; `ASPSBackend14_J/WebApi/Program.cs:268`).
- **Antiforgery:** Razor Pages POST handlers use `RequestVerificationToken` (e.g., Roadmap save). (`docs/ASPS_DATA_FLOW.md §9`)
- **No DB credentials in WebApi** — stateless by design; zero database access.
- **Known security debt:** Hardcoded admin username list; anonymous `/DebugClaims`; authenticated-claim logging; unrestricted forwarded-proxy trust; `RequireHttpsMetadata` forced to `false`; and inconsistent HTTP-only Docker exposure with secure OIDC cookies/HTTPS redirection. Several API controllers do not carry an explicit authorization requirement.

### Open Items

- Replace hardcoded admin username list with proper Keycloak group claim (`ASPSBackend14_J/WebApi/Program.cs` TODO)
- SCRUM-894: New Angular-based admin client (status: To Do)
- Production HTTPS configuration for Keycloak authority
- SCRUM-906: Auto-update extension via managed deployment (status: To Do)

---

## 3. Desktop Agent

**As-built status: Partial.** The Windows agent provides the active Backend↔Desktop↔Extension bridge, device authentication, URL and remote-access forwarding, browser-history polling, notification consumption and local UI. Browser-history output is currently suppressed by a seen-state defect; deep remote-session log monitoring is not started by the normal application path; notification ACK/replay/reconnect, authenticated local transport and automatic self-update are not implemented.

**Full feature documentation:** [`DESKTOP_AGENT_FEATURES.md`](DESKTOP_AGENT_FEATURES.md) — covers all 23 features, edge cases, and known gaps.

### Purpose

A Python system-tray application running on the user's Windows PC. It bridges the Chrome Extension (via local WebSocket) and the Backend (via ZMQ CURVE when provisioned), monitors remote-access software, forwards alerts, receives asynchronous results and displays local notifications. Remote-session termination helper code exists but is not dispatched by the normal protective-action path.

### Components

| File | Path | Responsibility |
|------|------|----------------|
| `main.py` | `apps/desktop/win/src/main.py` | Entry point; startup orchestration; `AntiScamApp` class |
| Desktop configuration | `apps/desktop/win/src/config.py` | Ports: `BACKEND_REQ_PORT=50001`, `BACKEND_SUB_PORT=50002`, `EXTENSION_PORTS=[8080,8181,8282,8383,8484]` |
| `zmq_client.py` | `src/zmq_client.py` | ZMQ REQ socket to Backend port 50001; sends URL, tracked-URL and remote-access alerts plus token requests. No Agent-side `RegisterDevice` operation was found. |
| `notification_client.py` | `src/notification_client.py` | ZMQ SUB socket on port 50002; consumes device-topic analysis, ImmediateDanger lifecycle, tracked-domain and browser-tab-policy messages. No ACK/replay/persistent queue exists. |
| `extension_server.py` | `src/extension_server.py` | Unauthenticated local WebSocket server; scans the configured port list and coordinates browser-tab requests/responses. |
| `auth_manager.py` | `src/auth_manager.py` | Token lifecycle: request, refresh, expiry check; stores via `keyring` with file-based fallback |
| `hardware_id.py` | `src/hardware_id.py` | Stable device UID from motherboard/BIOS/disk serial (PowerShell); fallback to Windows `MachineGuid`; format `PC-{16hex}`; cached at `%APPDATA%\AntiScam\device_id` |
| `remote_monitor.py` | `src/remote_monitor.py` | Process/network detection for configured remote-access tools, GeoIP enrichment and adaptive polling. Tool-ID mappings are not consistent across Backend, Agent and Extension. |
| `scan_service.py` | `src/services/scan_service.py` | URL scanning logic; local cache check → auth check → `zmq_client.send_url_alert()` |
| `protection_service.py` | `src/services/` | Intended protective-action dispatcher. The production wire contract is currently broken: Backend serializes `SubjectKey`, while this service reads `Subject`, defaults it to `0` and therefore enters none of its device/user/protector dispatch branches. The branch implementations also leave sound, email and quarantine as TODO and URL blocking as cache-only. |
| Desktop cache manager | `apps/desktop/win/src/cache_manager.py` | Local URL result cache with configurable TTL |
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

**WebSocket to Extension:** `ws://localhost:{first-free-in-8080-8484}`. This is an unauthenticated local application protocol, not a trusted security boundary.

| Message | Direction | Status | Current gap |
|---|---|---|---|
| `ping` / `pong`; heartbeat messages | Both | Implemented | Liveness only; no authenticated handshake |
| `url_check` / `url_result` | Both | Partial | No correlation ID; result may be applied to the tab active when it arrives |
| `track_url_alert` | Extension → Agent | Partial | `ReportType` and collected form metadata are not forwarded |
| `get_browser_tabs` / `browser_tabs_response` | Both | Partial | Response omits `tabId`; late-connect callback waits before the receive loop and can time out |
| `tab_closed_alert`; `tab_changed_alert` | Extension → Agent | Partial | Implemented behind volatile Extension state |
| `remote_access_alert` | Agent → Extension | Partial | Overlay module is not runtime-reachable with the current manifest |
| `immediate_danger_started/ended` | Agent → Extension | Partial | Toggles Extension state; does not itself display an overlay |
| `tracked_domains:set` | Agent → Extension | Partial | Raw list is persisted, but the navigation map is not restored after service-worker restart |
| `remote:close_session`; dismiss/continue events | Extension → Agent | Not implemented | Extension emits messages for which the Agent has no handler |
| `SetExtensionConfiguration` | Backend → Agent → Extension | Not implemented | No versioned handler, storage or validation chain |
| Notification ACK | Extension → Agent | Not implemented | No delivery identifier or retry protocol |

### Data Flow

Chrome extension sends `url_check` → `extension_server.py` → `scan_service.check_url()` → `zmq_client.send_url_alert()` (ZMQ REQ port 50001) → Backend → ACK returned immediately.

Backend publishes result → `notification_client.py` SUB socket receives → `notification_manager` routes → `notification_handler.py` attempts protective-action dispatch and broadcasts to the Extension via WebSocket. Dispatch currently becomes a no-op because Backend emits `SubjectKey` (`Common/Models/ProtectiveAction.cs:29`) while the Agent reads `Subject` and defaults it to `0` (`services/protection_service.py:48`).

`remote_monitor.py` polls processes on adaptive schedule → builds `RemoteAccessAlert` → `zmq_client.send_remote_access_alert()`.

Source: (`docs/ASPS_DATA_FLOW.md §3.2–3.4`)

### Security

- **CURVE:** External ZMQ connections can use CURVE when a server public key is already provisioned. The documented first-request bootstrap is circular when the server already requires CURVE, and reconnect does not reliably rebuild both REQ and SUB clients with a newly obtained key.
- **Token auth:** `auth_manager.py` uses `keyring` OS credential store (file-based fallback) to persist token across restarts. Token validated on every alert submission.
- **Local transport:** Extension WebSocket server binds `localhost`; port scan is limited to `[8080,8181,8282,8383,8484]`, but there is no Origin allow-list, client authentication, pairing or signed handshake.
- **DangerMode:** When `ImmediateDangerNotification` received, polling drops to 2s and debounce is bypassed to ensure rapid response. (`docs/ASPS_DATA_FLOW.md §6`)
- **Known security debt:** WebSocket is unauthenticated plain `ws://`; `BrowserTabsPolicy` is in-memory; notification delivery is volatile; cached URLs/events are plaintext. `browser_history.py` is production-reachable, but a seen-state defect currently suppresses every newly collected entry before alert submission.

### Open Items

- SCRUM-863: Auto-update via Velopack (status: In Review)
- SCRUM-897: Velopack self-update implementation (status: To Do)
- SCRUM-901: Code anti-reverse-engineering/obfuscation (status: To Do)
- SCRUM-902: Code-signing with Authenticode certificate (status: To Do)
- `signalr_client.py`, `tcp_client.py`, `google_auth.py` and `remote_monitor_backup.py` are inactive alternate/legacy paths and are not evidence of production capabilities.

---

## 4. Mobile Agent

**Status: Not implemented.** No Android or iOS source directory exists in the repository. `docs/ASPS_DATA_FLOW.md §10` contains target behavior and illustrative messages; design text is not an implemented or verified wire protocol.

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

**As-built status: Partial; it must not be treated as fully operational.** The Chrome MV3 extension v0.0.1.4 collects URL, tracker, iframe, form, tab and login-state telemetry; maintains local cache/queue state; shows popup status; and consumes tracked-domain instructions. URL warning/block message names do not match the content-script contract, and the remote overlay is dynamically imported without a corresponding `web_accessible_resources` declaration.

### Purpose

A Chrome Manifest V3 extension that collects browser telemetry and forwards it over the local WebSocket bridge. Warning, block and remote-access UI code exists, but the inspected runtime does not deliver these paths reliably. `immediate_danger_started/ended` currently controls tab-reporting state and does not itself display an ImmediateDanger overlay.

### Components

| File | Responsibility |
|------|----------------|
| `manifest.json` | MV3 config; permissions: `activeTab`, `tabs`, `webNavigation`, `storage`, `notifications`, `alarms`, `cookies`; host: `<all_urls>` |
| `background.js` | Service worker; tab/navigation lifecycle hooks, local WebSocket dispatch, tracked-domain navigation state and protection routing |
| `content.js` | Content script; tracker/iframe/form/login telemetry and warning/block injection on `<all_urls>`. It contains runtime implementations duplicated by inactive service modules. |
| `apps/extension/chrome/content.css` | Styles for injected warning elements |
| `popup.js` / `popup.html` | Popup connection/risk status and feedback submission to a fixed Google Apps Script endpoint |
| `services/ConnectionService.js` | Port scan, heartbeat/keepalive and best-effort session-storage queue. Chrome alarms make later reconnect attempts at least 30 seconds; configured max attempts are not enforced. |
| `services/ScanService.js` | URL scan orchestration and cache lookup; sends `url_check` and resolves asynchronous results without a request correlation identifier |
| `services/ProtectionService.js` | Executes the action code supplied by the Agent. Active runtime does not derive policy from the score; `determineAction()` is unused. |
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

**Agent action codes as consumed by the Extension:**

| Action code | Intended Extension behavior | Runtime status |
|---|---|---|
| 0 | None | Implemented |
| 1 | Browser notification | Implemented |
| 2 | Banner | Defective: background/content message names differ |
| 3 | Modal | Defective: background/content message names differ |
| 4 | Block page | Defective: background/content message names differ |

Score-to-action selection is performed upstream. The legacy score ranges in earlier documentation are not authoritative Extension policy.

### Data Flow

1. `chrome.tabs.onUpdated` / `webNavigation.onCompleted` fires.
2. `ScanService.scan()` skips localhost URLs, checks local cache.
3. Cache miss → collects trackers/iframes via `content.js` → sends `url_check` via WebSocket.
4. Desktop Agent forwards to Backend and returns ACK.
5. Backend publishes result → Agent receives → broadcasts back via WebSocket.
6. `ScanService.handleResult()` invokes `ProtectionService` for the browser tab active when the result arrives; the current protocol does not preserve the originating tab reliably.

### Tracked-domain behavior

The Extension accepts `tracked_domains:set`, persists the raw list, and updates content scripts. Background navigation tracking uses a separate in-memory root-domain map with a fixed 24-hour TTL. That map is not restored during service-worker initialization. `ReportType` is ignored by navigation tracking, `TrackMode.Click` observes navigation rather than DOM click events, and form classification metadata is discarded by the Agent's current forwarding contract. The expiry cleanup also references an undefined variable in its delete branch.

Source: (`docs/ASPS_DATA_FLOW.md §3.1`)

### Security

- **Anti-tampering code:** Closed Shadow DOM, reinjection defense and friction controls exist, but the remote-warning module is not reachable through the current manifest resource declaration.
- **Local WebSocket:** The Extension connects to `localhost` ports without authenticating the process that owns the port.
- **Permissions and consent:** The Extension operates on `<all_urls>` with `tabs` and `cookies` permissions and has no Extension-side consent gate.
- **External feedback:** Feedback transmits the full page URL, score, risk type, timestamp and browser User-Agent to a fixed Google Apps Script endpoint, and retains a local history. This is a defined data flow, not a TBD.
- **Injection surface:** warning markup uses `innerHTML` with values originating from the unauthenticated local channel.
- SCRUM-905 and SCRUM-906 track extension update/publishing pipeline (both To Do).

### Open Items

- SCRUM-906: Auto-update extension to latest version (To Do)
- SCRUM-905: Extension publishing prerequisite tasks (To Do)
- SCRUM-912: Chrome Web Store (CWS) publishing prerequisites (To Do)
- Feedback mechanism — complete a privacy/legal assessment for the known full-URL and User-Agent payload
- Manifest version is `0.0.1.4`; release signing flow not yet established

---

## 6. Users Portal

**Status: Not implemented (design only).** No user-facing portal source directory exists in the repository. The concept is described by the User Layer design document, but `WebApi/` is an administrator portal and is not an end-user portal.

**Important:** The `CustomerPortal` under `c:\Jobs\LIMAT\CustomerPortal\` belongs to the **separate LIMAT project** and is NOT an ASPS subsystem.

### Purpose

A planned end-user-facing web portal where protected users (not admins) could view their own risk scores, alert history, account settings, and protection status. This is distinct from the admin dashboard (`WebApi/`).

### Components

No implementation exists. Design artifacts:

- `docs/system-specifications/מסמך ארכיטקטורה_ שכבת המשתמש (User Layer) במערכת ASPS – גרסה מורחבת.md` — Hebrew architecture doc describing the User Layer: `UDUser` entity, cross-device correlation, `UserRiskScore` axes (`vulnerability_score`, `exposure_score`), dimensions, `FinalRiskScore`.
- `docs/SCRUM-904-user-risk-score-design.md` — Design doc for the User Risk Score algorithm feeding the planned portal.
- `Business/Services/UserRiskScoreService.cs` and related entities/calculators — Backend risk-scoring implementation; the complete service registration/input chain is partial and this is not a portal.

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
- The User Layer architecture document listed above is design-only.
- Decision needed: standalone app vs. user-facing pages added to `WebApi/`

---

## 7. URL Analyzer

**As-built status: Partial.** The Python CLI pipeline implements URL inspection, rules, ML, WHOIS, content, category, purpose and reputation analysis. The Backend invokes the CLI, not FastAPI. Success output is lossy when mapped to C# and error output is contract-incompatible; cloaking is absent; the scraper timeout exceeds the Backend limit; and URL validation does not prevent SSRF to private or link-local targets.

### Purpose

A Python analysis package that performs multi-stage URL analysis. The production Backend invokes `analyze.py` as a subprocess and imposes a 30-second timeout. The scraper itself can enter a 60-second fallback/retry path, so the current limits are incompatible and terminating only the parent Python process can leave Chromium children behind.

### Components

| File | Path | Responsibility |
|------|------|----------------|
| `api.py` | `Analyzers/basic-url-analyzer/api.py` | Dormant alternate FastAPI runtime; not started by compose and not invoked by the Backend |
| `core/analyzer.py` | `core/analyzer.py` | `ScamAnalyzer` — singleton orchestrating the full analysis pipeline |
| `core/whois_checker.py` | `core/` | WHOIS domain age, registrar, country, privacy protection |
| `core/content_extractor.py` | `core/` | Playwright-based HTML extraction: title, headings, forms, CTAs, links |
| `core/rules_engine.py` | `core/` | 30+ regex patterns for financial scam language, pressure tactics, fraud indicators |
| `core/ml_classifier.py` | `core/` | scikit-learn `LogisticRegression` + TF-IDF vectorization; scam probability |
| `core/reputation_checker.py` | `core/` | DuckDuckGo search for domain reputation against known-reputable sources |
| `core/category_classifier.py` | `core/` | Category classification: crypto, investment, ecommerce, romance, banking, etc. |
| `core/purpose_classifier.py` | `core/` | Purpose classification: investment scam, phishing, romance, etc. |
| `core/url_inspector.py` | `core/` | URL structure inspection |
| `core/llm_explainer.py` | `core/` | Dormant LLM explanation prototype; not connected to the production pipeline |
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

Stdout: nested JSON risk assessment. The 30-second timeout is enforced in `UDUrlAnalyzer.cs`.

**As FastAPI service (alternate/dormant):**

```
POST /analyze   Body: { "url": "https://..." }
GET  /analyze?url=https://...
GET  /health
GET  /
```

**Output contract as built:** the result is nested under objects including `risk_assessment`, `purpose`, `whois`, `content_analysis`, `ml_analysis`, `reputation`, `website_category` and `scraping_status`. It also emits `scam_type`, `content_status`, `url_inspection`, `red_flags`, `warnings` and `missing_data`.

`risk_assessment.risk_score` uses a **0–100 scale in which larger values mean greater danger**. The implementation also preserves a valid rules score of 0, while error responses use 0 as well; therefore 0 is ambiguous between “lowest observed risk” and “error/no result.” Earlier documents that reverse the direction and describe 100 as safe are incorrect.

The Python↔C# contract is only partially compatible:

- Python error output uses `error` as a string while C# expects a structured `ErrorMessage`; Python also omits an explicit `success` field, whose C# default is `true`.
- `purpose.category`, nested reputation snake-case fields and `from_cache` do not map cleanly to their C# properties.
- `scam_type`, `content_status` and `url_inspection` have no receiving C# fields and are discarded.
- Category persistence reaches a logging-only TODO, and reputation's computed score adjustment is not applied to the final risk score.

### Data Flow

Backend `UDUrlAnalyzer` builds subprocess `ProcessStartInfo` → spawns Python → `analyze.py` runs full pipeline → writes JSON to stdout → Backend parses and stores as `AnalysisResults.JsonValue` blob → raises `AnalysisResultReceived` event.

FastAPI mode can be started manually via `uvicorn api:app`, but it is not part of the canonical deployment. Its `/health` endpoint reports healthy without checking the model, browser or network dependencies.

Source: (`docs/ASPS_DATA_FLOW.md §3.4 Step 10`)

### Security

- **SSRF:** URL validation checks scheme/syntax but does not block loopback, RFC1918, link-local/metadata destinations, DNS rebinding or redirects to private targets.
- **Hostile-content isolation:** Chromium is started with `--no-sandbox`, disabled web security/site isolation, ignored TLS errors and bypassed CSP.
- **Privacy:** Full URLs can enter logs/plaintext cache; domains are sent to WHOIS and DuckDuckGo without Analyzer-specific consent, redaction or retention enforcement.
- **Alternate API:** FastAPI has no authentication and allows all CORS origins. It must remain unexposed until hardened.
- **Process cleanup:** timeout handling kills the Python parent, not the process tree.

### Open Items

- Implement private-address/DNS/redirect SSRF controls and restore Chromium sandbox boundaries.
- Align the Python JSON schema and error model with C#, including contract tests.
- Align internal/external timeouts and terminate the full process tree.
- `--explain` is currently broken and `llm_explainer.py` is dormant; either integrate and secure it or remove the exposed CLI option.
- CORS `allow_origins=["*"]` — needs restriction if the FastAPI server is ever exposed
- No authentication on `/analyze` endpoint; must not be exposed externally
- `CATEGORY_UPGRADE_PLAN.md` and `ML_TRAINING_GUIDE.md` in root indicate planned classifier improvements

---

## 8. Deployment and Operations — As Built

**Status: Not deployable as checked in.** The repository contains Dockerfiles and a three-service compose file, but a clean deployment cannot be reconstructed safely or consistently from them.

| Concern | Status | As-built evidence and gap |
|---|---|---|
| Backend image build | **Not implemented** | `Dockerfile.backend:72` copies `basic-url-analyzer/basic-url-analyzer/basic-url-analyzer/`, which does not exist; the actual source is `Analyzers/basic-url-analyzer/`. |
| Fresh database schema | **Not implemented** | Compose initializes a January 2026 SQL dump, while later EF migrations are applied only when the Backend environment is `Development`; the container environment is `Docker`. |
| WebApi TLS/OIDC | **Not implemented** | Compose publishes HTTP `80:5001` only. Kestrel/HTTPS redirect and secure OIDC cookies exist, but no 5002 exposure or certificate provisioning exists. `RequireHttpsMetadata` is forced off in code. |
| Secrets | **Not implemented** | MySQL root credentials/connection string and a Keycloak client secret are checked into runtime configuration. `.env.example` is not consumed by compose. |
| Network exposure | **Not implemented** | MySQL and Backend ports 5555/5556/50001/50002 are host-published. Ports 5555/5556 bind to `*` without CURVE or application authentication. |
| Health/readiness | **Not implemented** | Only MySQL has a compose healthcheck. Backend and WebApi have no readiness checks; dormant FastAPI `/health` does not validate dependencies. |
| Schema transport and privilege | **Not implemented** | MySQL uses the root account, exposes 3306 and sets `SslMode=None`. Containers run with the default root user. |
| Reproducibility | **Not implemented** | Floating image tags, lower-bound-only Python dependencies and a stale SQL initializer prevent deterministic reconstruction. |

### Component Versions — As Built

| Component | Source of truth | Inspected value | Reporting status |
|---|---|---:|---|
| Backend / WebApi projects | `.csproj` | `0.0.0.3` | Backend/WebApi/admin expose some build information; no system-wide inventory |
| Repository NBGV | `version.json` | `1.0-beta` | Inconsistent with project versions |
| URL Analyzer | `pyproject.toml` | `1.0.0` | Not aggregated in Admin UI |
| Desktop Agent | `apps/desktop/win/src/version.py` | `0.1.1.1` | Not reliably included by alert builders or aggregated in Admin UI |
| Desktop installer | `apps/desktop/win/installer.iss` | `0.0.0.3` | Does not match Agent runtime version |
| Chrome Extension | `manifest.json` | `0.0.1.4` | Displayed locally only |
| Extension state config | `StateManager.js` | `0.1.0` | Stale and not a release/version protocol |

No production `LatestVersion_Agent_Win` comparison, `VersionUpdateRequired`, package-download validation, restart/resend flow or managed Extension configuration/update chain was found.

---

## 9. Requirements-to-Code Implementation Audit

Audit baseline: `ASPS_Unified_System_Requirements_2026-07-15.md`. A requirement is **Implemented** only when its full end-to-end behavior is production-reachable across every required component. A class, entity, test, UI page, TODO, dormant service or partial protocol does not by itself satisfy a requirement.

### 9.1 Functional Requirements

**Result: 0 Implemented, 13 Partial, 9 Not implemented.**

| ID | Requirement | Status | Implementation evidence and remaining gap |
|---|---|---|---|
| FR-001 | Device Alert Intake | **Partial** | `RealTimeAlertListener` validates tokens and raises events; `AlertPersistenceActor` persists alerts. Required `ReceivedAt`/timezone contract and reliable client delivery are incomplete (`RealTimeAlertListener.cs:486`; `AlertPersistenceActor.cs:21`). |
| FR-002 | URL Alert Analysis | **Partial** | Phishing/rules/ML/WHOIS/content/category/reputation code exists. Reputation does not affect the score, `scam_type` is lost at the C# boundary, category persistence is TODO and cloaking is absent (`core/analyzer.py:121`; `UDUrlAnalyzer.cs:596`). |
| FR-003 | Track URL Alert Analysis | **Partial** | Extension→Agent→Backend navigation/source/duration path exists. Click/form semantics are incomplete, `ReportType` is dropped, and duration units differ between Agent and Backend (`background.js:1069`; `UDTrackUrlAnalyzer.cs:194`). |
| FR-004 | Remote Access Analysis | **Partial** | Process/network detection, Backend analysis and sensitive-tab correlation exist. Agent log watchers/deep forensics are not started by the normal app path and late-connect tab recovery is defective (`remote_monitor.py:1129`; `UDRemoteAccessAnalyzer.cs:16`). |
| FR-005 | Phone Alert Analysis | **Not implemented** | Phone blacklist storage/admin code exists, but no `PhoneAlert` intake, producer or blacklist/fake/VOIP/geographic analyzer exists (`BlacklistedPhoneNumber.cs`; `RealTimeAlertListener.cs:486`). |
| FR-006 | User-Level Correlation | **Partial** | Per-user managers and URL/remote aggregators exist. Phone, message-link, intelligence and durable scam-journey sources required by the specification are absent (`UserDomainManagerService.cs:97`; `UDUserAnalyzer.cs:23`). |
| FR-007 | Scam Journey Tracking | **Not implemented** | Only string correlation keys exist; no durable `ScamInProgress`, ordered progress items, event or service exists (`TrackUrlAlertEntity.cs:28`). |
| FR-008 | User Risk Scoring | **Partial** | Profile persistence, 0–100 calculation, weights, decay and history code exist. Required input sources and host registrations are incomplete, so the score is not complete end to end (`UserRiskProfile.cs:24`; `UserRiskScoreCalculator.cs:53`). |
| FR-009 | Immediate Danger | **Partial** | Backend detects active remote access whose direction is Incoming **or Unknown**, correlates it with logged-in sensitive tabs and emits start/end events. Treating Unknown as danger can create false positives; volatile notifications and defective client tab recovery can miss or strand Danger state (`UDUserAnalyzer.cs:690`; `UDUserAnalyzer.cs:725`; `monitor_service.py:92`). |
| FR-010 | Protective Actions | **Partial** | Backend computes action flags, but not from every required policy/user/scam input and unconditionally appends three literal `test message` actions to URL results. End-to-end Agent dispatch is currently a no-op because Backend emits `SubjectKey` while Agent reads missing `Subject`; branch implementations are mostly TODO/cache-only, and Extension banner/modal/block routing is also defective (`ProtectiveActionsFactory.cs:20`; `ProtectiveActionsFactory.cs:94`; `ProtectiveAction.cs:29`; `protection_service.py:48`). |
| FR-011 | Multi-Device Notifications | **Partial** | Publisher supports device/user concepts and tracked-domain flow fans out explicitly. Agent subscribes only to its device topic, and general required notifications are not consistently sent to every user device (`NotificationPublisher.cs:100`; `notification_client.py:96`). |
| FR-012 | Notification Reliability | **Not implemented** | No delivery entity, ACK, retry, replay or restart recovery exists. ZMQ PUB/SUB and the local Extension queue can lose messages (`NotificationPublisher.cs:58`; `MessageQueueService.js:65`). |
| FR-013 | Tracked Domains Distribution | **Partial** | Entity/repository/admin commands, notification fan-out and Extension storage exist. Delete/snapshot, ACK/replay, version and restart restoration are incomplete (`NotificationPublisher.cs:129`; `background.js:305`). |
| FR-014 | Risky URL Discovery | **Not implemented** | No production `RiskyUrlFound`, `FraudUrlTracker`, `RiskyUrl` or `RiskyUrlPage` pipeline exists (`UDUrlAnalyzer.cs`; `AppDbContext.cs`). |
| FR-015 | Risky Domain Discovery | **Not implemented** | `TrackedDomain` is present, but no `RiskyDomain`/`RiskyDomainPage`, crawler or additional-page analysis pipeline exists (`TrackedDomain.cs`; `AppDbContext.cs`). |
| FR-016 | Message Link Scanning | **Not implemented** | No email/SMS/WhatsApp connector or intake alert type exists. Browser History is not equivalent and currently drops its collected entries (`RealTimeAlertListener.cs:486`; `browser_history.py:288`). |
| FR-017 | Intelligence Targeting Alert | **Not implemented** | Event/helper scaffolding exists, but there is no production caller or external intelligence ingestion pipeline; under this audit's production-reachability rule, dormant scaffolding is not implementation (`UserIsTargetedAlertHandler.cs:22`; `DomainEvents.cs:368`). |
| FR-018 | Extension Runtime Configuration | **Partial** | Browser-tabs policy notification and local defaults exist. No versioned `ExtensionConfiguration` or complete Backend→Agent→Extension configuration chain exists (`NotificationPublisher.cs:329`; `notification_handler.py:380`). |
| FR-019 | Component Versioning | **Partial** | Components contain version values and some server versions reach Admin. Client versions are not centrally reported, and Agent/installer/Extension versions drift (`ASPSBackend14_J/WebApi/Pages/Index.cshtml.cs:25`; `version.py:5`; `installer.iss:5`). |
| FR-020 | Desktop Agent Auto Update | **Not implemented** | Build/checksum scripts exist, but no required/request messages, package validation/download, enforced update, restart or rollback flow exists (`build_release.py:209`). |
| FR-021 | Admin Simulation | **Partial** | CRUD UI, persisted JSON steps and hosted runner exist. Runner inserts raw alerts directly into storage rather than the event/analysis/notification pipeline and does not persist run status (`SimulationRunner.cs:144,247`). |
| FR-022 | Archive and Retention | **Not implemented** | No archive tables or service moves expired alerts/results according to policy. Existing expiry filters and Roadmap archive are not equivalent (`AppDbContext.cs`). |

### 9.2 Required Data Objects

| Required object/change | Status | Implementation evidence and remaining gap |
|---|---|---|
| `DeviceAlert.Timezone`, `ReceivedAt` | **Partial** | Alert messages inherit `Timestamp`; receive time exists on `DeviceAlertReceived`, and timezone is nested in `DeviceInfo`, not persisted as the required alert fields (`DeviceMessage.cs:5`; `DomainEvents.cs:82`). |
| `UrlAlert.Timestamp`, `TabId`, `Timezone` | **Partial** | Inherited timestamp and direct `TabId` exist; direct timezone does not (`UrlAlert.cs:3`; `DeviceMessage.cs:5`). |
| `TrackUrlAlert` | **Implemented** | Required URL/source/duration/scam-key/user-agent/tab/timezone fields exist and are consumed by the production analyzer (`TrackUrlAlert.cs:6`; `UDTrackUrlAnalyzer.cs:90`). |
| `TrackUrlAlertEntity` | **Implemented** | Entity, EF mapping and persistence actor exist (`TrackUrlAlertEntity.cs:8`; `AppDbContext.cs:302`; `AlertPersistenceActor.cs:142`). |
| `User` targeting/scam/locale/timezone fields | **Partial** | Locale/timezone are persisted. `IsScammed` is absent and `IsTargeted` exists only on in-memory `UDUser` (`User.cs:23`; `UDUser.cs:53`). |
| Extended `UDUser` state | **Partial** | Risk, cross-platform lock, URL and remote state exist; durable scam journeys, tracked-domain collection, phone history and behavior stats are incomplete (`UDUser.cs:47`; `UDUserAnalyzer.cs:47`). |
| `UserRiskProfile` | **Partial** | Entity/repository/calculation code exists, but the host does not register the complete service chain used by the calculator (`UserRiskProfile.cs`; `UserRiskProfileRepository.cs`; `ASPSBackend/Program.cs`). |
| `ScamInProgress` | **Not implemented** | No entity/model or persistence; only correlation-key strings exist (`TrackUrlAlert.cs:31`; `ImmediateDanger.cs:93`). |
| `ScamProgressItem` | **Not implemented** | No production type or persistence mapping was found. |
| `TrackedDomain` | **Implemented** | Persisted entity/repository/admin commands and notification flow exist (`TrackedDomain.cs`; `TrackedDomainRepository.cs`; `TrackedDomainCommandHandlers.cs`). |
| `RiskyUrl` | **Not implemented** | No production entity/DbSet; similarly named surfing data is not the required discovery record. |
| `RiskyUrlPage` | **Not implemented** | No production type or mapping was found. |
| `RiskyDomain` | **Not implemented** | No production entity/DbSet; `TrackedDomain` is not equivalent. |
| `RiskyDomainPage` | **Not implemented** | No production type or mapping was found. |
| `BlacklistedPhoneNumber` | **Implemented** | Entity, DbSet, repository and query handler are host-wired (`BlacklistedPhoneNumber.cs`; `AppDbContext.cs`; `ASPSBackend/Program.cs`). |
| `ExtensionConfiguration` | **Not implemented** | No versioned Backend, Agent or Extension type/configuration chain was found. |
| Versioned JSON `SystemConfiguration` | **Not implemented** | Runtime `IConfiguration` event and the Admin cache-reload page do not provide the required persisted/versioned JSON object (`DomainEvents.cs:403`; `Pages/SystemConfigurations/Index.cshtml.cs`). |
| `Simulation` | **Implemented** | Persisted entity/repository, handlers, UI and hosted runner exist (`Simulation.cs`; `AppDbContext.cs`; `ASPSBackend/Program.cs`). |
| `SimulationStep` | **Implemented** | Model is serialized into `SimulationStepsJson` and consumed by handlers/runner (`SimulationStep.cs`; `SimulationCommandHandlers.cs`). |
| `VersionUpdateRequired` | **Not implemented** | No production type or flow was found. |
| `VersionUpdateRequest` | **Not implemented** | No production type or flow was found. |
| Notification persistence/delivery entity | **Not implemented** | Publisher sends directly; there is no persisted pending queue or ACK state (`NotificationPublisher.cs`). |

### 9.3 Required Domain Events

| Required event | Status | Production status |
|---|---|---|
| `DeviceAlertReceived` | **Implemented** | Raised by authenticated intake and consumed by runtime handlers (`RealTimeAlertListener.cs:526`; `DomainEvents.cs:75`). |
| `AnalysisResultReceived` | **Implemented** | Raised by analysis and consumed by persistence/notification actors (`UDAnalysis.cs:326`; `AnalysisPersistenceActor.cs:30`). |
| `RiskyUrlFound` | **Not implemented** | No event type or producer exists. |
| `RiskyUrlPagesAdded` | **Not implemented** | No event type or producer exists. |
| `RiskyDomainFound` | **Not implemented** | No event type or producer exists. |
| `ScamInProgressAdded` | **Not implemented** | No event or underlying entity exists. |
| `ImmediateDangerDetected` | **Implemented** | Same-device remote/sensitive correlation produces, persists and republishes it (`UDUserAnalyzer.cs:690`; `ImmediateDangerPersistanceActor.cs:36`). |
| `ImmediateDangerEnded` | **Implemented** | Produced on danger closure, persisted and notified (`UDUserAnalyzer.cs:734`; `ImmediateDangerPersistanceActor.cs:46`). |
| `OtpInterceptionTriggered` | **Partial** | Event and standalone detector exist, but no production DI registration/caller exists (`DomainEvents.cs:276`; `OtpInterceptionDetector.cs`). |
| `BlackScreenActivated` | **Partial** | Event and manager exist, but no runtime producer/caller exists (`DomainEvents.cs:325`; `BlackScreenManager.cs`). |
| `SetTrackedDomains` | **Implemented** | Admin/risk paths produce it and the notification actor fans it out (`TrackedDomainCommandHandlers.cs`; `NotificationPublisherActor.cs`). |
| `UserIsTargetedAlertReceived` | **Partial** | Event and first-alert helper exist, but no intelligence producer/caller exists (`DomainEvents.cs:365`; `UserIsTargetedAlertHandler.cs`). |
| `UserDeviceChanged` | **Not implemented** | No domain event type or producer exists. |

### 9.4 Required Configuration

#### System configuration

| Required setting | Status | Production status |
|---|---|---|
| `RiskyUrlScoreThreshold` | **Partial** | A similar `TrackUrl:RiskThresholdToEnableTracking` key is active, but the canonical setting/path is absent and an older tracking block is commented out. |
| `HighRiskThreshold` | **Partial** | Represented by `Analysis:SeverityScoreThresholdHigh`; naming/fallbacks differ between consumers. |
| `MediumRiskThreshold` | **Partial** | Similar severity key exists, but consumers use inconsistent fallback values. |
| `LowRiskThreshold` | **Not implemented** | No setting exists; low risk is an implicit `else`. |
| `UrlAlertSilenceIntervalMinutes` | **Not implemented** | No production setting/consumer was found. |
| `UrlAnalysisResultExpirationDays` | **Partial** | Generic `DeviceAlertExpiryDays` filters views/history but does not archive/delete analysis results. |
| `RiskyDomainPageScrapingExpirationDays` | **Not implemented** | No risky-domain-page crawler or setting exists. |
| `EmailScanIntervalMin` | **Not implemented** | No email scanning scheduler/setting exists. |
| `CheckCloaking` | **Not implemented** | No cloaking comparison or switch exists. |
| `AggregationPeriodDays` | **Partial** | Stored per `UserRiskProfile`, not as system configuration; its production service chain is incomplete. |
| `TimeDecayFactor` | **Partial** | Stored on the profile, but the profile method is not used by the active calculator path. |
| `NormalizationCap` | **Not implemented** | Score normalization is hard-coded to 100. |
| `LatestVersion_Agent_Win` | **Not implemented** | No setting, comparison or update flow exists. |
| Notification `MaxForDevice` / `OutdateAge` | **Not implemented** | No keys, ACK tracker, retry queue or retention entity exists. |
| Protective-action defaults | **Partial** | Active behavior is hard-coded in `ProtectiveActionsFactory`, including unconditional URL-analysis actions carrying literal `test message`; an alternate matrix is not production-referenced and neither path is configurable. The emitted actions do not dispatch on the Agent because the subject field names differ across the wire contract. |
| Global domain exceptions | **Implemented** | Global `SafeDomain` persistence/repository and URL-analysis bypass are active. |
| Global phone exceptions | **Not implemented** | Blacklisted numbers are a threat list, not a phone allow-list/exception mechanism. |

#### User configuration

| Required user setting | Status | Production status |
|---|---|---|
| Protective-action preferences by band/scenario | **Not implemented** | No user preference entity/repository/consumer exists. |
| User-specific domain/phone exceptions | **Not implemented** | No per-user exception model or lookup exists. |
| Locale and timezone | **Implemented** | Persisted on `User`, accepted by admin commands and exposed in the UI (`User.cs:23`; `AdminCommandHandlers.cs`). |
| Configured email accounts for scanning | **Partial** | Generic email `UserAccount` exists, but no scanning consent/OAuth/scheduler/runtime exists. |
| Emergency-contact notification preferences | **Not implemented** | A dormant action flag exists, but no contact/preference persistence or active notification path exists. |

### 9.5 Non-Functional Requirements

| Area | Requirement | Status | Audit finding |
|---|---|---|---|
| Reliability | Notifications survive disconnects and Backend restarts | **Not implemented** | Direct PUB/SUB has no ACK, retry, replay or persisted delivery state; Agent/Extension queues are volatile/best-effort. |
| Reliability | User risk and scam-journey state are reconstructable | **Partial** | Some risk profile/history is persisted; scam journeys do not exist and parts of ImmediateDanger/client state are in memory. |
| Reliability | URL/domain discovery workers are idempotent | **Not implemented** | Required discovery/crawler pipelines do not exist; alternate Analyzer cache is plaintext/non-atomic and not the Backend CLI path. |
| Security | External device channels are token-validated and encrypted | **Partial** | Ports 50001/50002 support token validation/CURVE, but bootstrap is circular without a pre-provisioned key, CURVE does not authenticate client identity, and reconnect state is incomplete. |
| Security | Update download validates `VersionRequestId` and authorization | **Not implemented** | No update message/download flow exists. |
| Security | Crawled/message content has enforced retention controls | **Not implemented** | No message-scanning retention model exists; Analyzer logs/cache can retain full URLs without enforced policy. |
| Security | Sensitive account data is not overexposed | **Partial** | OIDC exists, but secrets are committed, claims/full URLs/tokens can be logged, and external feedback sends full URL/User-Agent. |
| Privacy/Consent | Email/SMS/WhatsApp/OTP/link scanning requires consent | **Not implemented** | Consent/preferences are absent; most listed scanning flows are absent, while broad Extension collection has no local consent gate. |
| Privacy/Consent | Emergency-contact notifications follow user configuration | **Not implemented** | No emergency-contact preference model or active flow exists. |
| Privacy/Consent | DOM redaction/black screen is constrained to ImmediateDanger | **Not implemented** | Event/manager scaffolding exists but is not production-wired; no enforceable consent/condition path exists. |
| Observability | Admin exposes component versions, simulations, delivery state and cache/view tools | **Partial** | Simulation and some version/cache pages exist; client version inventory and notification delivery state do not. |
| Observability | Critical danger/update/journey/targeting events are auditable | **Partial** | ImmediateDanger is persisted; update and scam-journey events do not exist, and targeting has no production producer. |

### 9.6 Additional Security and Operational Findings

| Concern | Status | Evidence summary |
|---|---|---|
| Backend↔WebApi confidentiality/authentication | **Not implemented** | Ports 5555/5556 bind externally without CURVE/authentication. |
| Admin TLS | **Not implemented** | Compose publishes HTTP only; no certificate or HTTPS port is provisioned. |
| Analyzer SSRF protection | **Not implemented** | Validator permits private/link-local/loopback destinations and does not control rebinding/private redirects. |
| Hostile-content isolation | **Not implemented** | Chromium sandbox/web security/site isolation are disabled and TLS/CSP protections are bypassed. |
| Analyzer contract reliability | **Partial** | Success is lossy and error JSON is incompatible with the C# model. |
| Analyzer timeout/process cleanup | **Partial** | 30-second Backend limit conflicts with 60-second scraper behavior; only the parent process is killed. |
| Secret management | **Not implemented** | Runtime DB/OIDC secrets are committed rather than injected by a secret store. |
| Container least privilege | **Not implemented** | Services run as root without read-only filesystems/capability/resource restrictions. |
| Local Extension transport authentication | **Not implemented** | No WebSocket client authentication, Origin allow-list, pairing or signed handshake. |
| Privacy/log minimization | **Partial** | Full URLs, claims and device/auth data can be logged or sent to external services. |

---

## 10. Code-Only, Dormant and Legacy Inventory

Classification:

- **Production-reachable** — invoked by the inspected host/client runtime.
- **Dormant** — code exists but the normal runtime does not invoke or expose it.
- **Dead/broken** — incomplete, no-op or contract-incompatible.
- **Legacy** — still reachable but superseded or inconsistent with the canonical architecture.

| Component | Capability/behavior not represented accurately in the prior specification | Reachability | Classification / impact |
|---|---|---|---|
| Backend | Parallel raw NetMQ business channel on port 5555 | Production-reachable | **Legacy**; overlaps typed CQRS and is externally bound without CURVE |
| Backend | Automatic WebsiteCategory creation method | Production-reachable call | **Dead/broken**; method only logs a TODO and does not persist |
| Backend | Risk-triggered `EnableUrlTracking` action block | Dormant | **Legacy**; implementation is commented out |
| Backend | Every URL result receives Display, Sound and Email actions containing literal `test message` | Production-reachable | **Dead/test behavior in production path**; action output is emitted regardless of risk |
| WebApi | Hard-coded administrator usernames | Production-reachable | **Legacy/security debt**; bypasses exclusive group-based administration |
| WebApi | Anonymous `/DebugClaims` and authenticated claim logging | Production-reachable | **Code-only diagnostic behavior** with information-exposure risk |
| Desktop | Centered, always-on-top danger toast with explicit Dismiss/Close control | Production-reachable | **Code-only feature** richer than the prior generic notification description; the window-manager close control is blocked, but the in-window dismiss button is always present |
| Desktop | Browser history collection | Production-reachable | **Broken**; entries are marked seen before the monitor submits them |
| Desktop | Deep remote-session log watchers and forensic parsers | Dormant | **Code-only capability**; `start_realtime_monitoring()` is not called by normal startup |
| Desktop | AnyDesk disconnect helper | Dormant | **Code-only capability** with no production caller |
| Desktop | Detection configuration for 10 remote tools, including RustDesk | Production-reachable | **Code/spec drift**; the configured set has 10 entries, but tool identifiers/mappings still differ between clients |
| Backend ↔ Desktop | Protective-action subject field (`SubjectKey` vs `Subject`) | Production-reachable | **Broken wire contract**; the Agent receives actions but defaults the missing `Subject` to `0`, so no action dispatch branch runs |
| Desktop | `tcp_client.py`, `signalr_client.py`, `google_auth.py`, `remote_monitor_backup.py` | Dormant | **Legacy alternate paths**, not implemented production features |
| Extension | Cookie + DOM login detection with confidence/signals in four languages | Production-reachable | **Code-only telemetry feature** |
| Extension | Authentication-cookie change re-evaluation | Production-reachable | **Code-only feature** |
| Extension | `TabChangedAlert`/`TabClosedAlert` under remote-control gate | Production-reachable | **Code-only monitoring behavior**; state is volatile |
| Extension | Session-storage priority queue, hostname cache and connection badge | Production-reachable | **Code-only resilience/UI features**; delivery remains best-effort |
| Extension | Form-field classification for password/email/payment fields | Production-reachable | **Code-only feature**; detailed metadata is discarded by Agent forwarding |
| Extension | External user feedback to Google Apps Script | Production-reachable | **Code-only data flow** carrying full URL, risk fields and User-Agent |
| Extension | `TrackerService.js` and `FormMonitorService.js` | Dormant | **Legacy duplicates**; runtime equivalents live in `content.js` |
| Extension | `determineAction()`, `shouldTrackDomain()`, `tabActivationTimes` | Dormant/dead | Unused policy/state code and not evidence of implemented requirements |
| URL Analyzer | FastAPI runtime on port 8000 | Dormant | **Alternate unauthenticated attack surface**; Backend uses CLI and compose does not start it |
| URL Analyzer | URL structural result and scam-type classification | Production-executed | **Contract-loss feature**; Python emits fields that C# discards |
| URL Analyzer | Reputation score adjustment | Production-executed lookup | **Dead result**; adjustment is calculated but not applied to final risk |
| URL Analyzer | Plaintext file cache | Dormant for Backend CLI | Active only in alternate API path; non-atomic and lacks enforced max entries |
| URL Analyzer | `--whois-only` | Dead | CLI argument has no execution branch |
| URL Analyzer | `--explain` / LLM explainer | Broken/dormant | Missing result input/method; module is not in the pipeline |
| Repository | Analyzer file named `=8.0` and migration `.bak` | Dead artifacts | Captured package output and backup file; not runtime capabilities |

---

## 11. Remaining Architecture Decisions and Remediation Queue

The following are unresolved decisions, not unknown implementation status:

| # | Decision / remediation | Primary scope |
|---|---|---|
| 1 | Replace `TypeNameHandling.Auto` with explicit safe discriminators | Backend messaging/CQRS |
| 2 | Define `ASView` concurrency ownership and locking model | Backend read model |
| 3 | Collapse or authenticate/encrypt the 5555/5556 internal transport boundary | Backend/WebApi deployment |
| 4 | Define secure CURVE public-key provisioning, rotation, device identity and revocation | Backend/Desktop/Mobile |
| 5 | Decide whether Users Portal and Mobile Agent remain product scope; neither has implementation | Product/architecture |
| 6 | Define consent, redaction and retention for browser telemetry, feedback, WHOIS/reputation and future message scanning | Privacy/security |
| 7 | Remove or formally support dormant FastAPI, alternate Desktop transports and duplicate Extension services | Repository maintenance |
| 8 | Establish one cross-language enum/protocol source of truth, including remote-access application IDs | Backend/Desktop/Extension |
| 9 | Establish one risk scale, threshold source and versioned configuration model | Backend/Analyzer/clients |
| 10 | Design durable notification identity, ACK, retry, replay and expiry semantics | Backend/Desktop/Extension |
| 11 | Decide the deployment target and implement TLS, secret injection, migrations, health/readiness and least privilege | DevOps/security |
| 12 | Replace hard-coded Admin authorization and diagnostic claim exposure with environment-gated, policy-based behavior | WebApi/Keycloak |

---

*Last updated: 2026-07-22. Audit baseline: repository code under `C:\Jobs\ASPS\GitHub\Software\`; `docs/system-specifications/ASPS_Unified_System_Requirements_2026-07-15.md`; `docs/ASPS_DATA_FLOW.md`; `docs/system-specifications/ASPS_System_Overview.md`; and component-specific Markdown files under `docs/` and `docs/system-specifications/`. Ticket/design status was not treated as proof of implementation.*
