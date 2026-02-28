# External Integrations

**Analysis Date:** 2026-02-16

## APIs & External Services

**Web Analysis (Python Analyzer):**
- WHOIS Lookup - Domain registration information via python-whois library
- Google Safe Browsing - Implicit integration for phishing detection (via scikit-learn model)
- DuckDuckGo Search - Domain reputation via duckduckgo_search library
- Ollama (Optional) - Local LLM for AI-powered explanations
  - SDK/Client: ollama >= 0.4.0
  - Graceful fallback if not available
  - Runs locally, no external API calls

**Browser Automation:**
- Chromium (Headless) - Web content extraction via Playwright
  - Installed in Docker: `/app/analyzer/.venv/bin/playwright install chromium`
  - Requires Playwright system dependencies (libnss3, libatk1.0-0, etc.)

## Data Storage

**Databases:**
- MySQL 8.0
  - Provider: Pomelo.EntityFrameworkCore.MySql 7.0.0
  - Connection: `ConnectionStrings:DefaultConnection` in appsettings.json
  - Schema: `ASPSBackend2DB`
  - Initialization: SQL dump at `aspsbackend2db_20260130.sql` (loaded on first Docker run)
  - Tables: Users, UserDevices, AnalysisResults, DeviceAlerts, AlertFlags, KnownPhishingWebsites, SafeDomains
  - ORM: Entity Framework Core 7.0.20

**File Storage:**
- Local filesystem only
  - Python analyzers at `C:\Jobs\ASPS\Software\Analyzers` (Windows development)
  - Docker mount: `/app/analyzer/` for production

**Caching:**
- In-memory caching - Configuration flag: `Analysis:CacheEnabled` (true/false)
- TokenStore - In-memory device authentication tokens
- No Redis or external cache

## Authentication & Identity

**Auth Provider:**
- Keycloak (implied via database schema)
  - User entity field: `KeycloakUserId` (varchar 100)
  - Reference: `ASPSBackend14_J/Business/Data/EF/AppDbContext.cs:56`
  - Custom implementation for token generation and validation

**Authentication Method:**
- Device Token-based:
  - Issued via `TokenStore` service
  - Expiration: `TokenManagement:TokenExpirationPeriod` (1440 minutes default = 24 hours)
  - Max expiration: `TokenManagement:MaxExpiration` (10080 minutes default = 7 days)
  - Transmitted via NetMQ with CURVE encryption

**Encryption:**
- ZMQ CURVE (elliptic curve) - All NetMQ inter-process communication
  - Server keys generated on first run by `CurveKeyManager.cs`
  - Server public key: `Security:ServerPublicKey` (base64)
  - Server public key Z85: `Security:ServerPublicKeyZ85` (Z85-encoded variant)
  - Desktop app bootstrap uses Z85 key to connect securely

## Monitoring & Observability

**Error Tracking:**
- Not detected - No Sentry, Application Insights, or error aggregation service

**Logs:**
- Console logging via Microsoft.Extensions.Logging
- Configurable per-namespace: `Logging:LogLevel` in appsettings.json
- Default level: Information
- EF Core SQL logging enabled in debug builds
- Sensitive data logging enabled in debug mode

**Metrics:**
- Not detected - No Prometheus, StatsD, or metrics collection

## CI/CD & Deployment

**Hosting:**
- Docker Compose orchestration on Linux container host
- No cloud platform detected (AWS, Azure, GCP)
- Database persists to volume: `mysql_data`
- Services restart policy: `unless-stopped`

**CI Pipeline:**
- Not detected - No GitHub Actions, GitLab CI, or Jenkins configuration

**Deployment:**
- Multi-stage Docker build (separate Dockerfile.backend and Dockerfile.webapi)
- Docker Compose file for local and VM deployment
- Health checks: MySQL service has health check probe

## Environment Configuration

**Required env vars:**
- `ASPNETCORE_ENVIRONMENT` - Set to "Docker" in containers
- `DOTNET_ENVIRONMENT` - Set to "Docker" in containers
- Optional: `DEBUG_MODE` for desktop app (defaults to false)

**Secrets location:**
- MySQL credentials in `docker-compose.yml` environment section (hardcoded: zappa22)
- CURVE keys in `appsettings.json` (hardcoded for development)
- ZMQ server public key in `appsettings.json`
- `.env` file for desktop app (not committed to git)

## Webhooks & Callbacks

**Incoming:**
- None detected - System is request/response only

**Outgoing:**
- None detected - No external webhook calls

## Messaging & IPC

**NetMQ (Inter-Process Communication):**
- **CQRS Pattern:** WebApi sends Commands/Queries to ASPSBackend
  - Endpoint: `tcp://localhost:5556` (CQRS Gateway)
  - WebApi has ZERO database access - all data operations through NetMQ
  - Pattern: REQ/REP (request/response)
  - See: `ASPSBackend14_J/WebApi/Program.cs:16-30`

- **Real-Time Alerts:** Desktop clients receive alerts from ASPSBackend
  - Endpoint: `tcp://localhost:50001` (RealTime Alert Listener)
  - Pattern: REQ/REP (two-way communication) or PUB/SUB (configurable)
  - Mode: `NetMQ:RealTimeListenerMode` in appsettings.json
  - CURVE encryption enabled

- **Notifications:** Broadcast alerts to all connected clients
  - Endpoint: `tcp://localhost:50002` (NotificationPublisher)
  - Pattern: PUB/SUB (publish/subscribe)
  - CURVE encryption enabled

**SignalR (Web Real-Time):**
- WebSocket-based real-time notifications from WebApi to dashboard
- Hub: `/notificationshub` at `http://localhost:5001`
- Class: `WebApi.Hubs.NotificationsHub`
- Enables live alert push to admin dashboard

**WebSocket (Desktop Extension):**
- Local WebSocket server on ports: 8080, 8181, 8282, 8383, 8484
- Communication between desktop app and Chrome extension
- Configuration: `apps/desktop/win/src/config.py:24`

## Data Stores Summary

| Service | Type | Protocol | Location | Auth |
|---------|------|----------|----------|------|
| MySQL | Database | TCP | localhost:3306 | root:zappa22 |
| ASPSBackend CQRS | Message Queue | NetMQ CURVE | localhost:5556 | Token + CURVE |
| Alert Listener | Message Queue | NetMQ CURVE | localhost:50001 | Token + CURVE |
| Notification Publisher | PUB/SUB | NetMQ CURVE | localhost:50002 | CURVE |
| WebApi SignalR | WebSocket | HTTP/WS | localhost:5001 | SignalR negotiate |
| Extension WebSocket | WebSocket | WS | localhost:8080-8484 | None (local) |

## Security Model

**Layers:**
1. **Device → Backend:** NetMQ CURVE encryption + device tokens
2. **Desktop App ↔ Extension:** WebSocket over localhost (no encryption needed)
3. **WebApi ↔ Backend:** NetMQ CURVE encryption (IPC, no auth tokens needed)
4. **WebApi ↔ Browser:** SignalR over HTTPS (when deployed)
5. **Database:** Username/password (no SSL in docker-compose example)

**Key Materials:**
- CURVE server keys: Generated by `CurveKeyManager.cs` on first startup
- Client keys: Desktop app generates keys for secure bootstrap
- Token validation: TokenStore verifies device authenticity

---

*Integration audit: 2026-02-16*
