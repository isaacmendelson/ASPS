# External Integrations

**Analysis Date:** 2026-02-13

## APIs & External Services

**Google OAuth 2.0:**
- Google Accounts API - User authentication for desktop application
  - SDK/Client: `requests` library (Python)
  - Auth: `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` env vars
  - Endpoints: `https://accounts.google.com/o/oauth2/auth`, `https://oauth2.googleapis.com/token`, `https://www.googleapis.com/oauth2/v2/userinfo`
  - Implementation: `c:/Users/pc/Desktop/asps/apps/desktop/win/src/google_auth.py`
  - Flow: OAuth2 authorization code flow with localhost redirect (port 8912)
  - Scopes: `email profile`

**WHOIS Lookup:**
- WHOIS domain information service
  - SDK/Client: `python-whois` 0.8.0+
  - Implementation: `c:/Users/pc/Desktop/asps/basic-url-analyzer/basic-url-analyzer/basic-url-analyzer/core/whois_checker.py`
  - Purpose: Domain age verification, scam detection

**Web Search (Optional):**
- DuckDuckGo Search API
  - SDK/Client: `duckduckgo_search` 4.0.0+
  - Purpose: Domain reputation checking
  - Implementation: `c:/Users/pc/Desktop/asps/basic-url-analyzer/basic-url-analyzer/basic-url-analyzer/core/reputation_checker.py`

**Browser Automation:**
- Playwright Browser Service
  - SDK/Client: `playwright` 1.40.0+
  - Purpose: JavaScript-rendered page scraping, screenshot capture
  - Implementation: `c:/Users/pc/Desktop/asps/basic-url-analyzer/basic-url-analyzer/basic-url-analyzer/scrapers/playwright_scraper.py`

**LLM (Optional):**
- Ollama Local LLM
  - SDK/Client: `ollama` 0.4.0+
  - Purpose: AI-powered scam explanations
  - Implementation: `c:/Users/pc/Desktop/asps/basic-url-analyzer/basic-url-analyzer/basic-url-analyzer/core/llm_explainer.py`
  - Note: Local deployment, no API key required

**IP Geolocation (Optional):**
- GeoIP2 Database
  - SDK/Client: `geoip2` 4.8.0+
  - Implementation: `c:/Users/pc/Desktop/asps/apps/desktop/win/src/config.py` references
  - Purpose: IP location for threat analysis
  - Note: Gracefully degrades if missing

## Data Storage

**Databases:**
- MySQL 8.0+
  - Connection: `ConnectionStrings:DefaultConnection` in `c:/Users/pc/Desktop/asps/ASPSBackend14_J/ASPSBackend/appsettings.json`
  - Default: `server=127.0.0.1;port=3306;database=ASPSBackend2DB;user=root;password=***;AllowPublicKeyRetrieval=True;SslMode=None`
  - Client: Pomelo.EntityFrameworkCore.MySql 7.0.0 via Entity Framework Core 7.0.20
  - Database: ASPSBackend2DB
  - Tables: Users, UserDevices, UserAccounts, AnalysisResults, AlertFlags, DeviceAlerts, KnownPhishingWebsites, SafeDomains
  - Schema: `c:/Users/pc/Desktop/asps/ASPSBackend14_J/create-database.sql`

**File Storage:**
- Local filesystem only
  - Desktop app data: `~/.antiscam/` - Stores Google OAuth tokens (`c:/Users/pc/Desktop/asps/apps/desktop/win/src/google_auth.py`)
  - Browser history: Read-only access to Chrome/Firefox history databases for URL monitoring
  - Python analyzer cache: File-based caching for analysis results
  - No cloud storage integration

**Caching:**
- In-memory caching
  - Desktop app: `c:/Users/pc/Desktop/asps/apps/desktop/win/src/cache_manager.py` - URL analysis cache
  - Chrome extension: `c:/Users/pc/Desktop/asps/apps/extension/chrome/services/CacheService.js` - Browser localStorage
  - Backend: ASView in-memory cache for Users/Devices/Accounts (`c:/Users/pc/Desktop/asps/ASPSBackend14_J/Business/`)
  - URL Analyzer: FastAPI in-memory cache (configurable: `use_cache=True`)

## Authentication & Identity

**Auth Provider:**
- Google OAuth 2.0 (Primary)
  - Implementation: Desktop application only (`c:/Users/pc/Desktop/asps/apps/desktop/win/src/google_auth.py`)
  - Token storage: `~/.antiscam/google_token.json`
  - Auto-refresh: Refresh token persisted locally

**Legacy Auth System:**
- Keycloak (Referenced but not actively used)
  - Evidence: `KeycloakUserId` field in User entity (`c:/Users/pc/Desktop/asps/ASPSBackend14_J/Common/Entities/User.cs`)
  - Status: Appears to be legacy system, Google OAuth is primary

**Device Authentication:**
- Token-based device authentication
  - Token management: `TokenExpirationPeriod: 1440` minutes (24h), `MaxExpiration: 10080` minutes (7 days)
  - Config: `c:/Users/pc/Desktop/asps/ASPSBackend14_J/ASPSBackend/appsettings.json`
  - Device identification: Hardware ID (HWID) generated from motherboard/BIOS serial (`c:/Users/pc/Desktop/asps/apps/desktop/win/src/hardware_id.py`)

**Encryption:**
- CurveZMQ (Curve25519) for ZeroMQ message encryption
  - Enabled: `Security:CurveEnabled: true` in backend config
  - Server keys: `ServerPublicKey`, `ServerSecretKey`, `ServerPublicKeyZ85` in `c:/Users/pc/Desktop/asps/ASPSBackend14_J/ASPSBackend/appsettings.json`
  - Client key: `BACKEND_SERVER_PUBLIC_KEY_Z85` in `c:/Users/pc/Desktop/asps/apps/desktop/win/src/config.py`
  - Key generation: `c:/Users/pc/Desktop/asps/ASPSBackend14_J/Business/Services/CurveKeyManager.cs`
  - Implementation: `c:/Users/pc/Desktop/asps/apps/desktop/win/src/zmq_client.py` (lines 74-79)

## Monitoring & Observability

**Error Tracking:**
- Built-in logging only (no external service)
  - .NET: Microsoft.Extensions.Logging.Console 8.0.0
  - Python: Standard `logging` module

**Logs:**
- Console output and file logging
  - Backend log levels configured in `appsettings.json` per namespace
  - Desktop app: Event logger (`c:/Users/pc/Desktop/asps/apps/desktop/win/src/event_logger.py`)
  - No centralized log aggregation

## CI/CD & Deployment

**Hosting:**
- Self-hosted infrastructure
  - Backend server: `100.88.78.75` (production), `127.0.0.1` (dev)
  - WebApi: HTTP on port 5001, HTTPS on 7001
  - No cloud platform integration detected

**CI Pipeline:**
- None detected
  - Manual builds via `dotnet build` and `dotnet publish`
  - Desktop app: Manual PyInstaller builds (`c:/Users/pc/Desktop/asps/apps/desktop/win/src/build.py`)
  - Chrome extension: Manual packaging (no build step required)

**Deployment:**
- Manual deployment
  - WebApi publish output: `c:/Users/pc/Desktop/asps/WebApi/publish/`
  - IIS configuration: `web.config` present in publish directory

## Environment Configuration

**Required env vars (.NET Backend):**
- `ConnectionStrings:DefaultConnection` - MySQL connection string
- `NetMQ:BusinessEndpoint` - NetMQ business layer endpoint (default: `tcp://*:5555`)
- `NetMQ:RealTimeListenerPort` - Alert listener port (default: 50001)
- `NetMQ:NotificationPublisherPort` - Notification pub port (default: 50002)
- `Security:CurveEnabled` - Enable CurveZMQ encryption
- `Security:ServerPublicKey`, `Security:ServerSecretKey`, `Security:ServerPublicKeyZ85` - CURVE keys
- `Python:ExecutablePath` - Path to Python interpreter for analyzers
- `Python:AnalyzersFolderPath` - Path to URL analyzer scripts

**Required env vars (Desktop App):**
- `GOOGLE_CLIENT_ID` - Google OAuth client ID
- `GOOGLE_CLIENT_SECRET` - Google OAuth client secret
- `DEBUG_MODE` - Optional, default: false

**Secrets location:**
- `.env` files (gitignored, never committed)
  - Desktop: `c:/Users/pc/Desktop/asps/apps/desktop/win/.env`
  - Example: `c:/Users/pc/Desktop/asps/apps/desktop/win/.env.example`
- `appsettings.json` (contains database password - should be externalized)

## Webhooks & Callbacks

**Incoming:**
- Google OAuth redirect
  - Endpoint: `http://localhost:8912` (desktop app temporary HTTP server)
  - Handler: `c:/Users/pc/Desktop/asps/apps/desktop/win/src/google_auth.py`

**Outgoing:**
- None detected
  - No external webhook integrations

## Messaging Systems

**ZeroMQ (NetMQ):**
- Distributed CQRS architecture via NetMQ 4.0.1.13
  - **Request/Response (REQ/REP):**
    - Business Layer: `tcp://*:5555` - Accepts commands/queries from WebApi
    - CQRS Gateway: `tcp://localhost:5556` - WebApi sends commands/queries
    - RealTime Listener: `tcp://*:50001` - Accepts device alerts
  - **Publish/Subscribe (PUB/SUB):**
    - Notification Publisher: `tcp://*:50002` - Broadcasts notifications to devices
    - Topic format: `device:{DeviceUid}`
  - **CURVE Encryption:**
    - All connections use CurveZMQ (enabled in production)
    - Client-server key exchange on connection
  - **Key Implementations:**
    - Backend: `c:/Users/pc/Desktop/asps/ASPSBackend14_J/Business/Messaging/RealTimeAlertListener.cs` - Alert receiver
    - Backend: `c:/Users/pc/Desktop/asps/ASPSBackend14_J/Business/Messaging/NotificationPublisher.cs` - Notification sender
    - Desktop Client: `c:/Users/pc/Desktop/asps/apps/desktop/win/src/zmq_client.py` - REQ/REP client
    - Desktop Client: `c:/Users/pc/Desktop/asps/apps/desktop/win/src/notification_client.py` - SUB client
    - WebApi: `c:/Users/pc/Desktop/asps/ASPSBackend14_J/WebApi/Services/NetMQClientService.cs` - Client wrapper
    - Python Test Client: `c:/Users/pc/Desktop/asps/python_clients/python-client-with-notifications.py`

**SignalR (WebSocket):**
- Real-time browser notifications
  - Hub: `/notificationshub` on WebApi (port 5001/7001)
  - Implementation: `c:/Users/pc/Desktop/asps/ASPSBackend14_J/WebApi/Hubs/NotificationsHub.cs`
  - Desktop Client: `c:/Users/pc/Desktop/asps/apps/desktop/win/src/signalr_client.py` (optional, not primary path)
  - Purpose: Admin dashboard real-time updates

**WebSocket (Native):**
- Chrome extension communication
  - Server: Desktop app WebSocket server on ports 8080-8484 (auto-discovery)
  - Implementation: `c:/Users/pc/Desktop/asps/apps/desktop/win/src/extension_server.py`
  - Client: `c:/Users/pc/Desktop/asps/apps/extension/chrome/services/ConnectionService.js`
  - Protocol: JSON messages with type-based routing
  - Message types: `url_scan_request`, `url_scan_response`, `heartbeat_ping`, `heartbeat_pong`, `remote_access_alert`, `app_closed`
  - Heartbeat: 30s intervals to keep service worker alive
  - Reconnection: Exponential backoff with alarm-based retry

**Inter-Process Communication:**
- Desktop App ↔ Chrome Extension: WebSocket (local)
- Desktop App ↔ Backend: ZeroMQ REQ/REP + PUB/SUB
- WebApi ↔ Backend Business Layer: ZeroMQ REQ/REP (CQRS)
- Admin Dashboard ↔ WebApi: SignalR (optional real-time)

---

*Integration audit: 2026-02-13*
