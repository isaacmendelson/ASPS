ASPS

Anti-Scam Protection System

System Architecture & Component Overview

February 2026

# Table of Contents

1. System Overview

2. Architecture Diagram

3. C# Backend (ASPSBackend14_J)

3.1 ASPSBackend Console Application

3.2 WebApi (Admin Dashboard & API)

3.3 Business Layer

3.4 Common & Interface Layers

3.5 Database

4. URL Analyzer (Python/FastAPI)

5. Real-Time Analysis Engine

6. Desktop Agent (Python/Windows)

7. Chrome Extension (JavaScript/MV3)

8. Communication Protocols

9. Security Features

10. Docker Deployment

11. End-to-End Flow Example

# 1. System Overview

ASPS (Anti-Scam Protection System) is a distributed real-time threat protection platform designed to protect users from online scams, phishing attacks, and unauthorized remote access sessions. The system monitors user devices continuously, analyzes suspicious URLs using machine learning and rule-based engines, detects remote access tool activity, and provides immediate protective actions to the user.

The platform consists of five main components working together in a distributed architecture:

| Component | Technology | Role |
| --- | --- | --- |
| ASPSBackend | C# / .NET 8 Console App | Core business logic engine, real-time analysis, messaging hub |
| WebApi | ASP.NET Core / Razor Pages | Admin dashboard, REST API, SignalR notifications (stateless, zero DB access) |
| URL Analyzer | Python / FastAPI | Deep URL analysis: ML classifier, WHOIS, rules engine, reputation checks |
| Desktop Agent | Python / System Tray | Windows endpoint monitor: remote access detection, browser history, URL scanning |
| Chrome Extension | JavaScript / Manifest V3 | Browser integration: real-time URL scanning, warning overlays, tracker detection |

# 2. Architecture Diagram

+--------------------+     WebSocket      +--------------------+
|  Chrome Extension  |<------------------>|  Desktop Agent     |
|  (JS, MV3)         |   localhost:8080   |  (Python)          |
+--------------------+                    +----------+---------+
                                                     |
                                          ZMQ REQ/REP (50001)
                                          ZMQ SUB    (50002)
                                          CURVE encrypted
                                                     |
                                          +----------v---------+
                                          |  ASPSBackend       |
                                          |  (C# Console)      |
                                          |  Business Logic     |
                                          |  Analysis Engine    |
                                          +---+----------+-----+
                                   NetMQ      |          |  EF Core
                                   5555/5556  |          |  MySQL
                                              v          v
                                     +-----------+   +------+
                                     |  WebApi   |   |  DB  |
                                     |  Admin UI |   +------+
                                     |  SignalR  |
                                     +-----------+
                                          ^
                      +-------------------+
                      |  Python subprocess
                      v
             +--------------------+
             |  URL Analyzer      |
             |  (Python/FastAPI)  |
             |  ML + Rules + WHOIS|
             +--------------------+

Communication flows: The Chrome Extension communicates with the Desktop Agent via WebSocket on localhost. The Desktop Agent sends alerts to ASPSBackend via ZMQ REQ/REP (port 50001, CURVE encrypted) and receives notifications via ZMQ PUB/SUB (port 50002). ASPSBackend communicates with WebApi via internal NetMQ channels (ports 5555/5556, localhost only). The URL Analyzer is invoked as a Python subprocess by the backend.

**Cloud deployment (ASPS-718, deployed 2026-08):** In Azure Container Apps, raw ZMQ ports are not externally reachable. Cloud-connected agents use a WebSocket gateway (`wss://.../ws/agent`, subprotocol `asps-agent-v1`) hosted by WebApi, which bridges to Backend ZMQ on localhost. The Desktop Agent selects transport via `TRANSPORT_MODE` (`"zmq"` for local, `"ws"` for cloud). See [`docs/architecture/WS-AGENT-PROTOCOL.md`](../../docs/architecture/WS-AGENT-PROTOCOL.md) and [`docs/architecture/decisions/ADR-004-ASPS-718-WEBSOCKET-GATEWAY.md`](../../docs/architecture/decisions/ADR-004-ASPS-718-WEBSOCKET-GATEWAY.md).

**V1 message envelope (ASPS-732):** All alert types use `MessageEnvelopeV1` (`schemaVersion: "1.0"`) with `messageType` discriminators (`url_scan.request`, `track_url.request`, `remote_access.request`, `tab_closed.request`, `tab_changed.request`). Azure Backend runs with `Messaging:AcceptLegacyV0=false`.

# 3. C# Backend (ASPSBackend14_J)

The backend is a .NET 8 solution with five projects in a layered architecture. The ASPSBackend console application hosts all business logic and runs as a long-lived process. The WebApi is a stateless presentation layer with zero direct database access — all operations route through CQRS commands/queries over NetMQ.

## Solution Structure

| Project | Type | Purpose |
| --- | --- | --- |
| ASPSBackend | Console App (.NET 8) | Main business logic engine, message processing, real-time analysis |
| WebApi | ASP.NET Core Web App | HTTP REST API, Admin UI (Razor Pages), SignalR Hub |
| Business | Class Library | Core logic, CQRS handlers, analysis pipeline, messaging, EF Core data access |
| Common | Class Library | Shared models, entities, enums, interfaces, exceptions |
| Interface | Class Library | Repository interfaces and DTOs |

## Key NuGet Dependencies

| Package | Version | Purpose |
| --- | --- | --- |
| NetMQ | 4.0.1.13 | ZeroMQ message queue for inter-process CQRS |
| Pomelo.EntityFrameworkCore.MySql | 7.0.0 | MySQL database provider for EF Core |
| Microsoft.EntityFrameworkCore | 7.0.20 | Object-relational mapper |
| Newtonsoft.Json | 13.0.3 | JSON serialization with polymorphic type handling |
| Swashbuckle.AspNetCore | 6.5.0 | Swagger/OpenAPI documentation |
| NetTopologySuite | 2.6.0 | Spatial data support |

## 3.1 ASPSBackend Console Application

A standalone long-running service that hosts all business logic, messaging, and the real-time analysis engine. It manages the complete lifecycle of device alerts, user analysis, and result publication.

### Startup Sequence

- Configure dependency injection container (Host Builder)

- Load persisted device tokens from MySQL into in-memory TokenStore

- Start ASView — loads entire domain model into memory (users, devices, alerts, analysis results, 506K+ phishing URLs)

- Start NetMQMessageProcessor — listens on tcp://*:5555 for internal CQRS messages

- Start RealTimeAlertListener — listens on tcp://*:50001 for device alerts (CURVE encrypted)

- Start CQRSGateway — listens on tcp://*:5556 for WebApi commands/queries

- Initialize per-user UDAnalysisManager instances for all active users

- Enter Host.RunAsync() — run indefinitely

### Key Registered Services

| Service | Lifetime | Purpose |
| --- | --- | --- |
| AppDbContext | Scoped | MySQL database access via Pomelo |
| ASView | Singleton | In-memory cache of entire domain model |
| TokenStore | Singleton | Write-through device token cache (memory + DB) |
| CurveKeyManager | Singleton | ZMQ CURVE encryption key management |
| RateLimiter | Singleton | Sliding window rate limiter for token endpoints |
| CQRSGateway | Singleton | Routes commands/queries from WebApi |
| RealTimeAlertListener | Singleton | Receives device alerts via ZMQ |
| UserDomainManagerService | Singleton | Manages per-user analysis managers |
| NotificationPublisher | Singleton | Publishes analysis results to subscribers |
| Repository implementations | Scoped | EF Core data access layer |
| CQRS Handlers | Scoped | Admin/User command and query handlers |

## 3.2 WebApi (Admin Dashboard & API)

A stateless ASP.NET Core web application with zero direct database access. All data operations are routed through CQRS commands/queries sent via NetMQ to the ASPSBackend process. This strict separation ensures the WebApi can be scaled independently and has no database credentials.

### Components

- Razor Pages: Admin dashboard at / — users, devices, alerts, analysis results, phishing database management

- REST Controllers: UsersController, UserDevicesController for programmatic access

- SignalR Hub: /notificationshub — real-time notifications to admin UI and device clients

- Swagger: API documentation at /swagger

- CQRSClient: Sends commands/queries to ASPSBackend via NetMQ (tcp://localhost:5556)

- NetMQClientService: Raw message communication to Business layer (tcp://localhost:5555)

### SignalR Hub Authorization

The NotificationsHub validates device connections using token-based authentication. When a device connects with deviceUid and token query parameters, the hub validates the token via a CQRS query to the backend's TokenStore. Invalid tokens result in connection abort. Admin page connections (no token) are allowed through. Authenticated devices can only subscribe to their own notification group, preventing cross-device eavesdropping.

## 3.3 Business Layer

### CQRS Pattern over NetMQ

Commands and queries are serialized as JSON with type information, sent via NetMQ Request/Reply sockets, processed by handler classes within DI scopes, and results serialized back. Two internal channels exist:

| Channel | Port | Purpose |
| --- | --- | --- |
| NetMQMessageProcessor | 5555 | Internal CQRS — routes commands/queries from WebApi |
| CQRSGateway | 5556 | Higher-level gateway — typed query/command routing with error handling |

### Available CQRS Commands

- CreateUserAdminCommand

- CreateUserDeviceCommand

- DeleteUserCommand

- UpdateUserCommand

### Available CQRS Queries

- GetDashboardStatsQuery

- GetUsersWithDeviceCountsQuery

- GetAllDevicesQuery

- GetRecentAlertsQuery

- GetUserByKeyQuery

- GetDeviceByKeyQuery

- GetDeviceByUidQuery

- GetDevicesByUserQuery

- GetAlertsByDeviceQuery

- GetAlertByKeyQuery

- GetAllAnalysisResultsQuery

- GetAnalysisResultByAlertKeyQuery

- GetAllPhishingWebsitesQuery

- ValidateDeviceTokenQuery

### Domain Events

| Event | Triggered By | Handlers |
| --- | --- | --- |
| DeviceAlertReceived | Device sends alert via port 50001 | ASView, AlertPersistenceActor, UDAnalysisManager |
| AnalysisResultReceived | Analysis pipeline completes | ASView, AnalysisPersistenceActor, NotificationPublisher |
| UserAdded / UserUpdated / UserDeleted | Admin commands | ASView (cache refresh) |

### Messaging: RealTimeAlertListener (Port 50001)

The primary device-facing endpoint. Accepts device alerts, manages authentication tokens, and routes alerts to the analysis pipeline. Supports REP (Request/Reply) and PULL (fire-and-forget) socket modes.

Message types handled:

| Message Type | Purpose | Rate Limit |
| --- | --- | --- |
| RequestToken | Device requests authentication token | 5/min per device |
| RegisterDevice | New device registers with user email | 3/min per device |
| RefreshToken | Device renews expired token | 5/min per device |
| UrlAlert | Device reports suspicious URL | Token validated |
| RemoteAccessAlert | Device reports remote access activity | Token validated |

### ASView (In-Memory Read Model)

A singleton that caches the entire domain model in memory for fast lookups. Loaded from the database on startup and kept synchronized via domain events. Thread-safe with lock-based collection updates.

Cached collections:

- Users

- User Devices

- User Accounts

- Device Alerts (view models)

- Analysis Results (URL and RemoteAccess typed views)

- Known Phishing Websites (506K+ records)

- Safe Domains

- Risky Domains

- Risky URL Surfing History

## 3.4 Common & Interface Layers

### Key Entities

| Entity | Inheritance | Key Fields |
| --- | --- | --- |
| User | Base entity | KeycloakUserId, Name, Email, Role (Self/Guardian/Other), IsDisabled |
| UserDevice | Abstract → PersonalComputer, SmartPhone (TPH) | DeviceUid, DeviceType, OS, MAC/IMEI, MonitoringStatus |
| DeviceAlertEntity | Abstract → RemoteAccessAlert, UrlAlert (TPH) | AlertType, Timestamp, DeviceInfo, Token |
| AnalysisResultContainer | Base entity | JSON payload, Discriminator, UserKey, Timestamp |
| KnownPhishingWebsite | Base entity | Url, Domain, Source, DateCreated (506K+ records) |
| DeviceTokenEntity | Base entity | DeviceUid (PK), TokenValue, UserKeyField, Expiration |

### Key Enums

- DeviceType: PC, Phone, Other

- OperatingSystemType: Windows, Linux, macOS, Android, iOS

- RemoteAccessApp: AnyDesk, TeamViewer, ChromeRemoteDesktop, QuickAssist, LogMeIn, ConnectWise, RustDesk, VNC, RDP

- UserRole: Self, Guardian, Other

- CautionLevel: Low, Medium, High

- ConnectionStatus / SessionStatus: Open, Closed

## 3.5 Database (MySQL 8.0)

| Table | Entity | Notes |
| --- | --- | --- |
| Users | User | Application users with Keycloak integration |
| UserDevices | UserDevice (TPH) | PC and Phone discriminated by type column |
| UserAccounts | UserAccount | User online accounts (email, social, financial) |
| DeviceAlerts | DeviceAlertEntity (TPH) | RemoteAccess and URL alerts |
| AnalysisResults | AnalysisResultContainer | Serialized JSON analysis results |
| KnownPhishingWebsites | KnownPhishingWebsite | 506,000+ phishing URL records |
| SafeDomains | SafeDomain | Whitelisted domains (skip analysis) |
| DeviceTokens | DeviceTokenEntity | Persisted authentication tokens |
| AlertFlags | AlertFlag | Alert state tracking flags |

# 4. URL Analyzer (Python/FastAPI)

A standalone Python microservice that performs deep URL analysis for scam and phishing detection. Called by the C# backend as a subprocess with a 30-second timeout. Combines multiple analysis techniques with machine learning for comprehensive threat assessment.

## Analysis Pipeline

| Stage | Module | What It Does |
| --- | --- | --- |
| 1. WHOIS Check | whois_checker.py | Domain age, registrar, country, privacy protection, risk scoring |
| 2. Content Extraction | content_extractor.py | HTML parsing: title, headings, forms, CTAs, links (via Playwright) |
| 3. Rules Engine | rules_engine.py | 30+ regex patterns for financial scam language, pressure tactics, fraud indicators |
| 4. ML Classifier | ml_classifier.py | scikit-learn LogisticRegression with TF-IDF vectorization |
| 5. Reputation Check | reputation_checker.py | DuckDuckGo search for domain mentions from reputable sources |
| 6. Category Classification | category_classifier.py | Classifies as crypto, investment, ecommerce, romance, banking, etc. |
| 7. Purpose Classification | purpose_classifier.py | Identifies scam type (investment, phishing, romance, etc.) |

## Output

- Risk score (0–100, where 0 = dangerous, 100 = safe)

- Risk level (LOW / MEDIUM / HIGH)

- Is scam flag + confidence score (0.0–1.0)

- Red flags list (human-readable)

- Domain age, detected language, website category

- Individual results from each analysis module

## Decision Logic

- Domain age overrides: established domains receive trust boost

- Reputation override: well-known sites protected from ML false positives

- Language-aware: skips ML for non-English content

- 24-hour result caching for repeated URLs

## API Endpoints

| Method | Path | Purpose |
| --- | --- | --- |
| POST | /analyze | Analyze URL for scams (JSON body) |
| GET | /analyze?url=<URL> | Browser-friendly analysis endpoint |
| GET | /health | Health check |
| GET | / | Service status |

## Key Dependencies

- beautifulsoup4, lxml — HTML parsing

- python-whois — WHOIS domain lookups

- playwright — Browser automation for content extraction

- scikit-learn, numpy — Machine learning

- duckduckgo_search — Web reputation checks

- fastapi, uvicorn — API server

- langdetect — Language detection

# 5. Real-Time Analysis Engine

The backend's C# analysis pipeline runs per-user with pluggable analyzers. Each active user gets a dedicated UDAnalysisManager that orchestrates multiple analyzers and publishes results via domain events.

## Architecture

UDAnalysisManager (per user)
  └─ UDAnalysis (orchestrator)
       ├─ UDPhishingAnalyzer     → checks 506K+ known phishing DB
       ├─ UDRemoteAccessAnalyzer → detects TeamViewer, AnyDesk, RDP, etc.
       ├─ UDUrlAnalyzer          → calls Python analyzer subprocess
       └─ UDUserAnalyzer         → user behavior patterns

## Indicator System (15+ Types)

| Indicator | Type | What It Detects |
| --- | --- | --- |
| KnownPhishingIndicator | URL/Domain | Database match against 506K+ known phishing URLs |
| RemoteAccessIndicator | Device | Remote access app activity, connection status |
| ContentAnalysisIndicator | URL | Page content patterns (forms, trackers, suspicious text) |
| MlAnalysisIndicator | URL | ML classifier scam probability |
| WhoisCountryIndicator | Domain | Domain registered in high-risk country |
| WhoisDomainAgeIndicator | Domain | Recently registered domain (< 30 days) |
| WhoisIsPrivacyProtectedIndicator | Domain | WHOIS privacy protection enabled |
| DomainBlacklistedIndicator | Domain | Domain on external blacklists |
| WebsiteTypeIndicator | URL | Website category (banking, dating, crypto, etc.) |
| UserIsTargetedIndicator | User | User appears in phishing target lists |
| KnownWebsiteTemplateIndicator | URL | Known phishing page template detected |
| NoMxRecordIndicator | Domain | Domain has no mail server records |

## Protective Actions

| Level | Action | Description |
| --- | --- | --- |
| 0 | None | No action needed — site is safe |
| 1 | Notify | Informational notification only |
| 2 | Warn Banner | Top-of-page warning banner |
| 3 | Warn Modal | Popup dialog with stay/leave buttons |
| 4 | Block | Full-page block overlay |

## Alert Lifecycle

- Active: 0–30 days (configurable via DeviceAlertExpiryDays)

- Expired: 30–90 days (marked inactive)

- Deleted: >90 days (removed, configurable via DeviceAlertDeletionDays)

# 6. Desktop Agent (Python/Windows)

A Python system tray application that runs on Windows PCs, providing real-time monitoring and threat detection. Communicates with the backend via ZMQ and with the Chrome extension via WebSocket.

## Core Capabilities

### Remote Access Detection

Continuously monitors for remote access tool activity using psutil process detection:

- AnyDesk

- TeamViewer

- Chrome Remote Desktop

- Quick Assist

- LogMeIn

- ConnectWise

- RustDesk

- VNC

- RDP (Remote Desktop)

- Ammyy Admin

For each detected tool: identifies running processes, active sessions, connection status, remote IP address, and geolocates the remote country using GeoIP2.

### URL Scanning & Protection

- Analyzes URLs from Chrome extension in real-time

- Caches results locally with configurable TTL

- Provides protective actions to extension: ignore, warn, block

### Browser History Monitoring

- Tracks visited URLs to detect scam/phishing sites

## Communication Channels

| Channel | Protocol | Port(s) | Purpose |
| --- | --- | --- | --- |
| Backend Alerts (local) | ZMQ REQ/REP | 50001 | Send alerts (UrlAlert, RemoteAccessAlert, etc.), manage tokens |
| Backend Notifications (local) | ZMQ PUB/SUB | 50002 | Receive analysis result notifications |
| Backend (cloud, ASPS-718) | WebSocket (`wss://`) | `/ws/agent` | Combined alert + notification channel via WebApi gateway; replaces ZMQ when `TRANSPORT_MODE="ws"` |
| Chrome Extension | WebSocket | 8080–8484 | Bidirectional communication with browser extension |

## Device Identification

Hardware-based stable device ID generated from motherboard serial, BIOS serial, and disk serial via Windows PowerShell. Falls back to Windows Registry MachineGuid if hardware values are generic. Format: PC-{16 hex characters}. Cached to disk at %APPDATA%\AntiScam\device_id.

## Authentication Flow

- 1. Generate hardware-based DeviceUid on first run

- 2. Send RegisterDevice with user email → receive token + server public key

- 3. Apply CURVE encryption keys to all ZMQ connections

- 4. Token attached to all subsequent alerts (24h expiration, 7-day refresh window)

- 5. Automatic token refresh before expiration

## Alert Format: UrlAlert

{
  "AlertType": "UrlAlert",
  "DeviceInfo": { "DeviceUid": "PC-...", "DeviceType": 1, "OperatingSystem": 1, "MAC": "..." },
  "Timestamp": "2026-02-26T10:30:00Z",
  "Token": "64-char-hex-token",
  "Url": "https://suspicious-site.com",
  "Trackers": ["FB_PIXEL_123", "G-ANALYTICS456"],
  "IFrameDomains": ["ad-network.com"],
  "UserAgent": "Chrome/120..."
}

## Alert Format: RemoteAccessAlert

{
  "AlertType": "RemoteAccessAlert",
  "DeviceInfo": { ... },
  "RemoteAccessApp": 1,
  "RunningProcesses": 2,
  "ConnectionUrl": "192.168.x.x",
  "ConnectionStatus": 1,
  "SessionStatus": 1,
  "Direction": "incoming",
  "Confidence": "high",
  "RemoteCountry": "USA",
  "RemoteCountryCode": "US"
}

## Key Source Files

| File | Purpose |
| --- | --- |
| main.py | Entry point, startup orchestration |
| config.py | Configuration constants (includes `TRANSPORT_MODE`, `WS_URL` for cloud) |
| zmq_client.py | Backend ZMQ REQ/REP communication (local transport) |
| notification_client.py | Backend ZMQ PUB/SUB notifications (local transport) |
| ws_client.py | Backend WebSocket communication (cloud transport, ASPS-718) |
| alert_builders.py | Shared alert/token payload builders — used by both ZMQ and WS transports (ASPS-721); v1 envelope wrapping for all alert types (ASPS-732) |
| extension_server.py | WebSocket server for Chrome extension |
| remote_monitor.py | Remote access software detection (psutil) |
| auth_manager.py | Token lifecycle management |
| hardware_id.py | Stable device ID generation |
| scan_service.py | URL scanning business logic |
| protection_service.py | Protective action execution |
| cache_manager.py | Local URL result caching |
| core/container.py | Dependency injection container (selects ZMQ vs WS transport) |

# 7. Chrome Extension (JavaScript/MV3)

A Manifest V3 Chrome extension that provides real-time browser protection against online scams. Written in plain JavaScript with no framework. Communicates with the Desktop Agent via WebSocket for URL analysis and remote access warnings.

## Core Features

### Automatic URL Scanning

Every page load is automatically scanned via three triggers:

- chrome.tabs.onUpdated — page load complete

- chrome.webNavigation.onCompleted — main frame navigation

- chrome.webNavigation.onHistoryStateUpdated — SPA navigation detection

### Tracker Detection

- Facebook Pixel detection (fbq init calls)

- Google Analytics (GA4: G-XXXXXX, Universal: UA-XXXXX)

- External iframe enumeration

### Warning System (4 Levels)

| Level | Type | Score Range | Description |
| --- | --- | --- | --- |
| 1 | Notify | 61–90 | Informational only |
| 2 | Banner | 61–90 | Top-of-page warning bar |
| 3 | Modal | 31–60 | Popup dialog with stay/leave buttons |
| 4 | Block | 0–30 | Full-page block overlay (Hebrew + English) |

### Remote Access Warning

When the Desktop Agent detects an incoming remote access session (e.g., TeamViewer, AnyDesk), it sends a remote_access_alert to the extension. The extension displays a Shadow DOM-protected overlay on all tabs with a friction mechanism:

- 7-second countdown timer before action buttons enable

- "I know this person and trust them" checkbox required

- Both conditions must be met to dismiss the warning

- Shadow DOM (closed root) prevents page JavaScript from removing the overlay

- MutationObserver re-injects if host element is removed (anti-tampering)

## Popup UI

- Connection status indicator (green/red/yellow)

- Risk score circle (color-coded)

- Domain being scanned

- Desktop app and server connection status

- Manual scan and reconnect buttons

- Feedback system (thumbs up/down to Google Sheets)

## Connection Management

The extension connects to the Desktop Agent via WebSocket on localhost. It tries ports 8080, 8181, 8282, 8383, 8484 and saves the successful port. Features automatic reconnection with exponential backoff (1s → 30s max), heartbeat pings every 10 seconds, keepalive messages every 20 seconds, and a message queue that buffers messages during disconnection.

## Key Files

| File | Purpose |
| --- | --- |
| manifest.json | MV3 configuration, permissions, service worker declaration |
| background.js | Service worker — tab lifecycle, WebSocket handlers, protective actions (705 lines) |
| content.js | Content script — tracker extraction, warning/block display (446 lines) |
| popup.js / popup.html | Popup UI — status, score, feedback (776 lines) |
| services/ConnectionService.js | WebSocket management, reconnection (476 lines) |
| services/ScanService.js | URL scanning orchestration, caching (242 lines) |
| services/ProtectionService.js | Warning execution based on risk score (141 lines) |
| services/CacheService.js | URL result caching (1h TTL, 1000 max entries) |
| warning/RemoteAccessWarning.js | Shadow DOM remote access overlay |
| warning/FrictionController.js | 7-second countdown + checkbox friction |
| state/StateManager.js | Reactive state with Chrome storage persistence |

# 8. Communication Protocols

| Channel | From → To | Protocol | Port | Encryption | Pattern |
| --- | --- | --- | --- | --- | --- |
| Device Alerts (local) | Agent → Backend | ZMQ REQ/REP | 50001 | CURVE (CurveZMQ) | Request/Reply |
| Notifications (local) | Backend → Agent | ZMQ PUB/SUB | 50002 | CURVE (CurveZMQ) | Publish/Subscribe |
| Agent Gateway (cloud, ASPS-718) | Agent → WebApi → Backend | WebSocket (`wss://`) | `/ws/agent` | TLS (managed cert) | Combined req/resp + push |
| Extension Comm | Extension ↔ Agent | WebSocket | 8080–8484 | None (localhost) | Bidirectional |
| Internal CQRS | WebApi → Backend | ZMQ REQ/REP | 5555 | None (localhost) | Request/Reply |
| CQRS Gateway | WebApi → Backend | ZMQ REQ/REP | 5556 | None (localhost) | Request/Reply |
| Admin Updates | Backend → Browser | SignalR/WS | 5001 | HTTPS (production) | Hub groups |
| URL Analysis | Backend → Analyzer | Subprocess | N/A | N/A | Stdin/Stdout (30s timeout) |

# 9. Security Features

## Transport Encryption (CURVE)

External device-facing channels (ports 50001, 50002) use CurveZMQ encryption. The server generates an elliptic-curve keypair on first run (stored in appsettings.json). Devices receive the server's public key (Z85-encoded) in token responses and generate ephemeral client keypairs on each connection. All messages are authenticated and encrypted at the socket level.

## Token-Based Authentication

- 64-character cryptographically secure hex tokens (RandomNumberGenerator)

- Bound to DeviceUid + UserKeyField

- Configurable expiration: 24 hours default, 7-day refresh window

- Write-through cache: in-memory ConcurrentDictionary + MySQL persistence

- Validated on every alert submission

- Persisted across backend restarts via DeviceTokens table

## Rate Limiting

- Sliding window rate limiter on token endpoints

- RegisterDevice: 3 requests/minute per device

- RequestToken / RefreshToken: 5 requests/minute per device

- Automatic cleanup of stale entries every 5 minutes

## SignalR Hub Authorization

- Device connections validated via CQRS query to backend TokenStore

- Invalid tokens cause immediate connection abort

- Authenticated devices restricted to their own notification group

- Admin connections (no token) allowed for dashboard access

## Extension Anti-Tampering

- Remote access warnings rendered in closed Shadow DOM

- MutationObserver re-injects overlay if host element is removed

- Friction mechanism: 7-second timer + checkbox before dismissal

# 10. Docker Deployment

The backend services are containerized via docker-compose.yml with three services:

| Service | Base Image | Ports | Notes |
| --- | --- | --- | --- |
| mysql | MySQL 8.0 | 3306 | Initialized with 62MB SQL dump (506K+ phishing records) |
| backend | .NET 8 + Python 3 | 5555, 5556, 50001, 50002 | Includes Python venv + Playwright for URL analyzer |
| webapi | .NET 8 | 5001 | Admin dashboard, API, SignalR hub |

# 11. End-to-End Flow Example

Scenario: User visits a phishing URL in Chrome.

- 1. User navigates to suspicious-bank.com in Chrome.

- 2. Chrome Extension content script extracts trackers (Facebook Pixel, Google Analytics) and external iframes from the page.

- 3. Extension sends url_check message via WebSocket to the Desktop Agent.

- 4. Desktop Agent sends a UrlAlert via ZMQ REQ to Backend port 50001 (CURVE encrypted), including the URL, trackers, iframes, device info, and authentication token.

- 5. Backend RealTimeAlertListener validates the token via TokenStore.

- 6. Backend looks up the device and associated user in ASView cache.

- 7. Alert is routed to the user's UDAnalysisManager.

- 8. UDPhishingAnalyzer checks the URL against 506,000+ known phishing records — match found.

- 9. UDUrlAnalyzer calls the Python analyzer subprocess — WHOIS shows 2-day-old domain, ML classifier returns 92% scam probability, rules engine detects 8 red flags.

- 10. Indicators generated: KnownPhishing=true, DomainAge=2 days, MlScore=8/100.

- 11. ProtectiveAction determined: Block (level 4).

- 12. AnalysisResultReceived domain event fired.

- 13. AlertPersistenceActor saves alert and result to MySQL.

- 14. ASView updates in-memory cache.

- 15. NotificationPublisher broadcasts result via ZMQ PUB (port 50002) → Desktop Agent.

- 16. Agent forwards result via WebSocket → Chrome Extension.

- 17. Extension displays full-page block overlay with bilingual warning (Hebrew + English).

- 18. Admin dashboard updates in real-time via SignalR.
