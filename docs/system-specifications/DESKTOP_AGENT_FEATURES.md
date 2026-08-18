# Desktop Agent — Complete Feature Documentation

**Version:** 0.1.1.1 | **Date:** 2026-06-30 | **Status:** Authoritative  
**Source:** `apps/desktop/win/src/`  
**Parent spec:** [`ASPS_System_Specification.md §3`](ASPS_System_Specification.md)

---

## Table of Contents

1. [Overview](#1-overview)
2. [Architecture & Component Map](#2-architecture--component-map)
3. [Startup Sequence](#3-startup-sequence)
4. [Authentication](#4-authentication)
5. [Device Identification](#5-device-identification)
6. [URL Scanning](#6-url-scanning)
7. [Remote Access Monitoring](#7-remote-access-monitoring)
8. [Browser History Monitoring](#8-browser-history-monitoring)
9. [ImmediateDanger Mode](#9-immediatedanger-mode)
10. [Browser Tabs Policy](#10-browser-tabs-policy)
11. [Extension Communication (WebSocket)](#11-extension-communication-websocket)
12. [Backend Communication (ZMQ)](#12-backend-communication-zmq)
12a. [Backend Communication (WebSocket, cloud path)](#12a-backend-communication-websocket-cloud-path)
13. [Protective Actions](#13-protective-actions)
14. [Notification System](#14-notification-system)
15. [System Tray](#15-system-tray)
16. [Event Logging](#16-event-logging)
17. [Cache Management](#17-cache-management)
18. [Dependency Injection Container](#18-dependency-injection-container)
19. [Data Models](#19-data-models)
20. [Configuration](#20-configuration)
21. [Shutdown Sequence](#21-shutdown-sequence)
22. [Edge Cases & Notable Behaviors](#22-edge-cases--notable-behaviors)
23. [Known Gaps / TODOs](#23-known-gaps--todos)

---

## 1. Overview

The Desktop Agent (`AntiScam.exe`) is a Python 3.11 Windows application that:

- Bridges the Chrome extension (via local WebSocket) and the Backend (via ZMQ CURVE)
- Monitors remote-access software running on the device
- Forwards URL alerts to the Backend for analysis
- Receives analysis results and executes protective actions locally
- Operates in the Windows system tray with minimal user friction

**Entry point:** `apps/desktop/win/src/main.py` — class `AntiScamApp`  
**Distribution:** PyInstaller single-file EXE, installed via Inno Setup (`installer.iss`)

---

## 2. Architecture & Component Map

```
main.py (AntiScamApp)
├── core/container.py          ← DI container (singleton); selects transport via TRANSPORT_MODE (ASPS-718)
│   ├── ZMQClient              ← REQ socket → Backend port 50001 (TRANSPORT_MODE="zmq", default)
│   ├── NotificationClient     ← SUB socket ← Backend port 50002 (TRANSPORT_MODE="zmq", default)
│   ├── WSClient (ws_client.py)← wss:// → WebApi /ws/agent gateway (TRANSPORT_MODE="ws"; ASPS-718)
│   ├── ExtensionServer        ← WebSocket server (ports 8080–8484)
│   ├── AuthManager            ← Token lifecycle (RequestToken/RefreshToken)
│   │   └── hardware_id.py     ← Stable device UID from hardware serials
│   ├── CacheManager           ← URL result cache (domain-keyed, TTL)
│   ├── RemoteAccessMonitor    ← psutil-based detection (10 apps)
│   │   ├── detection/tools.py ← Tool config registry
│   │   ├── detection/geolocation.py  ← GeoIP lookup (ip-api.com)
│   │   ├── detection/direction.py    ← Session direction (incoming/outgoing)
│   │   └── detection/confidence.py  ← Detection confidence score
│   ├── BrowserHistoryMonitor  ← Chrome/Edge/Firefox history polling
│   ├── EventLogger            ← Audit trail (events.jsonl)
│   ├── TrayIcon               ← System tray icon + popup
│   │   ├── NotificationManager ← Windows toast notifications
│   │   └── tray_popup.py      ← Status popup window
│   ├── ScanService            ← URL scanning orchestration
│   ├── ProtectionService      ← Protective action execution
│   └── MonitorService         ← Background monitoring tasks
├── handlers/extension_handler.py    ← Extension message dispatcher
└── handlers/notification_handler.py ← Backend notification dispatcher
```

**All components are lazily instantiated** by `Container` (singleton). Dependencies injected at construction time; no component creates its own dependencies.

---

## 3. Startup Sequence

`AntiScamApp.start()` runs in a background async thread; the main thread blocks on `tray_icon.run_blocking()`.

```
1. Print startup banner (version)
2. Start ExtensionServer → scan ports 8080→8484, bind first free
3. Wire NotificationHandler → ExtensionServer (for cross-thread broadcasting)
4. Authenticate with Backend
   a. ensure_authenticated() — up to 3 retries with 2s/4s/8s backoff
   b. On success: apply CURVE keys to NotificationClient
   c. On failure: start background reconnect loop (retry every 30s)
5. Start NotificationClient in background thread (ZMQ SUB, port 50002)
6. Start MonitorService background tasks:
   a. _monitor_remote_access() loop
   b. _monitor_browser_history() loop
   c. _update_tray_status() loop
7. Send initial remote access status (startup_scan → late_detection alerts)
8. Update tray icon
9. Print "AntiScam Desktop is running!"
10. Loop: asyncio.sleep(1) until _running=False
```

---

## 4. Authentication

**File:** `auth_manager.py`

### Flow

```
RequestToken → backend responds:
  TokenCreated      → store token + expiry
  ExistingToken     → store token + expiry
  DeviceNotRecognized → open browser to login page (once per session)
  TokenExpired      → send RefreshToken
  InvalidToken      → full re-auth
```

### Token storage

| Storage | Mechanism | Content |
|---------|-----------|---------|
| Primary | OS keyring (Windows Credential Manager) | Token value |
| Fallback | `%APPDATA%\AntiScam\auth.json` | email, expires_at, user_id |
| Not persisted | — | server_public_key (always read fresh from file) |

**Why server key is not cached:** respects CurveEnabled toggling — if CURVE is disabled in a new build, a cached key would wrongly apply encryption.

### Token lifecycle

- **Expiry buffer:** 5 minutes (is_expired() returns True 5 min before actual expiry)
- **ensure_authenticated():** retries up to 3× with exponential backoff (2s → 4s → 8s)
- **Per-alert check:** every `send_url_alert()` / `send_remote_access_alert()` validates token first
- **InvalidToken / TokenExpired in alert response:** trigger refresh → fallback to re-auth

---

## 5. Device Identification

**File:** `hardware_id.py`

### Generation strategy (priority order)

1. **Hardware serials** via PowerShell `Get-CimInstance`:
   - Win32_BaseBoard (motherboard serial)
   - Win32_BIOS (BIOS serial)
   - Win32_DiskDrive (disk serial)
   - Generic/OEM placeholder values filtered out (e.g., "to be filled by o.e.m.", "not applicable")
2. **Windows MachineGuid** from `HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid`
3. **Last resort:** `platform.node()` + `platform.machine()`

**Hashing:** SHA-256 of combined serials → first 16 hex chars  
**Format:** `PC-{16 hex chars}` (e.g., `PC-a1b2c3d4e5f60000`)  
**Cache:** `%APPDATA%\AntiScam\device_id` (plain text, generated once)

---

## 6. URL Scanning

**File:** `services/scan_service.py`

### Per-URL flow

```
Extension sends url_check
  ↓
1. Is local URL? (localhost / 127.0.0.1) → skip, return safe
2. Cache hit? (keyed by domain) → return cached result
3. Auth check → re-auth if needed
4. Send UrlAlert to Backend (ZMQ REQ, port 50001)
5. Mark URL as pending (_pending_urls dict with timestamp)
6. Process response:
   New format → {"analyzing": true} (async; result arrives via SUB socket)
   Old format → parse Score/RiskType/ProtectiveAction, cache result, return
   Auth error → retry auth, resend
```

### Pending URL tracking

- Tracks URLs awaiting async backend response
- Thread-safe (`Lock`)
- Auto-cleanup: entries older than 60s removed
- Matched against incoming notification (correlation key)

### Cache key

Domain (`urllib.parse.urlparse(url).netloc`) — not full URL. Same domain reuses cached result.

### Alert message structure (ZMQ REQ)

```json
{
  "AlertType": "UrlAlert",
  "DeviceInfo": { "DeviceUid": "PC-...", "DeviceType": 1, "OperatingSystem": 1 },
  "Timestamp": "...",
  "Token": "<64-hex>",
  "Url": "https://...",
  "Trackers": [...],
  "IFrameDomains": [...],
  "IPAddress": "...",
  "TabId": "..."
}
```

### Supplementary alert types

| Type | Trigger | Fields |
|------|---------|--------|
| `TrackUrlAlert` | URL navigation (Url changed in tab) | Url, FromUrl, Duration, scam_key, timezone, IP |
| `TabClosedAlert` | Tab closed during ImmediateDanger | tabId, url |
| `TabChangedAlert` | URL changed in tab during ImmediateDanger | tabId, url, isSensitiveWebsite, isLoggedIn |

---

## 7. Remote Access Monitoring

**Files:** `remote_monitor.py`, `detection/tools.py`, `detection/geolocation.py`, `detection/direction.py`, `detection/confidence.py`

### Monitored applications (10)

| App | Detection signals |
|-----|-----------------|
| AnyDesk | Process, listening port, log file parsing |
| TeamViewer | Process, listening port, log file parsing |
| Chrome Remote Desktop | Process |
| RDP (Windows) | Process (`mstsc.exe`), service |
| Splashtop | Process |
| LogMeIn | Process |
| ConnectWise Control | Process |
| RemotePC | Process |
| VNC | Process, port |
| QuickAssist | Process |

### Detection fields per app

```
is_running, has_active_session, process_count, connection_count
direction: incoming | outgoing | unknown
confidence: low | medium | high
remote_country, remote_country_code   (GeoIP via ip-api.com, cached)
remote_os, remote_version             (from log parsing)
connection_type: direct | relay       (from AnyDesk/TeamViewer logs)
file_transfer_active, file_transfer_count
remote_id, remote_name, logged_user, connection_id, software
```

### Direction determination

- `incoming` (victim/controlled): app is acting as server (listening port active, session received)
- `outgoing` (controller): app is acting as client
- Signal: port analysis + log indicators + process role

### Adaptive polling intervals

| State | Interval |
|-------|----------|
| No apps running (idle) | 30s |
| App running / active session | 5s |
| Debounce active (close ticking) | 1s |
| ImmediateDanger mode | 2s (override) |

### State change events

`check_all_with_changes()` tracks previous state and emits:
- `app_opened` / `app_closed`
- `session_started` / `session_ended`
- Direction change

### Startup scan

`startup_scan()` runs at boot, marks all detections with `late_detection=True` — alerts sent to backend are flagged as "already running when agent started" (not newly appeared).

### GeoIP

- Service: `ip-api.com` (free, no key required)
- Cached per IP to avoid repeated lookups
- Graceful degradation: if lookup fails, fields left empty

---

## 8. Browser History Monitoring

**File:** `browser_history.py`

### Supported browsers

| Browser | DB path | Table |
|---------|---------|-------|
| Chrome | `%LOCALAPPDATA%\Google\Chrome\User Data\Default\History` | `urls` |
| Edge | `%LOCALAPPDATA%\Microsoft\Edge\User Data\Default\History` | `urls` |
| Firefox | `%APPDATA%\Mozilla\Firefox\Profiles\*.default*\places.sqlite` | `moz_historyvisits` |

### Timestamp conversion

- Chrome/Edge: microseconds since 1601-01-01
- Firefox: microseconds since Unix epoch (1970-01-01)

### Windows file locking workaround

Browser processes hold a lock on their History/Places SQLite files.  
Solution: `temp_database_copy()` context manager creates a temp copy, reads from it, then cleans up.

### Deduplication

`mark_url_as_sent()` tracks URLs already forwarded — prevents re-sending on next poll.

---

## 9. ImmediateDanger Mode

**File:** `services/danger_mode.py`, `handlers/notification_handler.py`

### Activation

Triggered by `ImmediateDangerNotification` from Backend (via ZMQ SUB).

### Effects when active

| System | Normal | DangerMode |
|--------|--------|------------|
| Remote access polling | adaptive (5–30s) | 2s |
| Debounce | enabled | bypassed |
| Alert priority | per-risk | elevated to CRITICAL |
| RA alert cadence | on-change | every 10s (loop) |
| BrowserTabs in alerts | per-policy | per-policy (policy unchanged) |

### Periodic alert loop

- Fires every `IMMEDIATE_DANGER_ALERT_INTERVAL_SECONDS` (10s, configurable)
- Each iteration: query extension for fresh BrowserTabs → send RemoteAccessAlert with tabs
- Tab query timeout: 3s

### Deactivation

Triggered by `ImmediateDangerEndedNotification` from Backend:
1. `danger_mode.deactivate()`
2. Stop periodic alert loop
3. Clear BrowserTabs from future alerts
4. Show clearance notification to user

### Thread safety

`DangerMode` is a thread-safe singleton (`threading.Lock` on read/write).

---

## 10. Browser Tabs Policy

**File:** `services/browser_tabs_policy.py`

Controls when open browser tabs are included in `RemoteAccessAlert` messages.

| Policy | Behavior |
|--------|----------|
| `incoming_only` (default) | Include tabs only when direction = incoming |
| `always` | Include tabs in all RA alerts |
| `never` | Never include tabs |

- Policy can be overridden at runtime via `SetBrowserTabsPolicyNotification` from Backend
- Override includes `valid_until` timestamp; policy resets after expiry
- Override does **not** persist across agent restarts

### Tab collection

1. Extension server sends `"get_browser_tabs"` to all connected clients
2. Each client responds with `"browser_tabs_response"` (within 3s)
3. Tabs from all clients merged and sorted
4. URL filter applied: remove `localhost`, `127.0.0.1`, empty URLs

---

## 11. Extension Communication (WebSocket)

**File:** `extension_server.py`

### Port selection

Scans `[8080, 8181, 8282, 8383, 8484]` in order; binds first available port.  
Server binds to `127.0.0.1` only.

### Message protocol (Extension → Agent)

| Type | Payload | Response |
|------|---------|----------|
| `url_check` | url, trackers[], iframes[] | `url_result` (score, riskType, protectiveAction, cached) |
| `track_url_alert` | Url, FromUrl, Duration, scam_key | ACK |
| `tab_closed_alert` | tabId, url | ACK |
| `tab_changed_alert` | tabId, url, isSensitiveWebsite, isLoggedIn | ACK |
| `ping` | — | pong + email + local IP |
| `user_auth` | email | stored to disk |
| `get_user` | — | device_id, email, authenticated |
| `user_signout` | — | session cleared |
| `browser_tabs_response` | requestId, tabs[] | resolves pending Future |
| `heartbeat_ping` | — | `heartbeat_pong` |
| `keepalive` | — | (silent) |

### Message protocol (Agent → Extension)

| Type | When sent |
|------|-----------|
| `url_result` | Backend analysis result received |
| `remote_access_alert` | Remote access state change |
| `immediate_danger_started` | ImmediateDanger activated |
| `immediate_danger_ended` | ImmediateDanger deactivated |
| `get_browser_tabs` | Agent needs open tabs |
| `heartbeat_pong` | Response to heartbeat_ping |

### Extension late-connect handling

Race condition: extension takes seconds to start after agent boots; initial RA alerts sent before extension connects (BrowserTabs=[]).

Resolution:
1. `on_client_connect` callback fires when extension first connects
2. If an incoming session is currently active → wait 2s (Chrome MV3 service worker startup) → query tabs → re-emit RA alert with fresh tabs
3. Single retry on tab query timeout

### Cross-thread broadcasting

`notification_client` runs in a background thread (blocking ZMQ recv).  
Broadcasting to the async WebSocket server uses `asyncio.run_coroutine_threadsafe(server.broadcast(...), event_loop)`.  
Event loop reference captured at `set_extension_server()` time.

---

## 12. Backend Communication (ZMQ)

**Files:** `zmq_client.py`, `notification_client.py`

### Outbound (ZMQ REQ, port 50001)

**File:** `zmq_client.py`

- Per-request socket lifecycle: `connect → send → recv → close` (context reused)
- Thread-safe via `threading.Lock`
- Timeout: 5000ms (`zmq.RCVTIMEO`)
- CURVE: ephemeral client keypair generated per connection; server public key loaded from auth

**Message types sent:**

| Type | When |
|------|------|
| `UrlAlert` | URL check |
| `TrackUrlAlert` | URL navigation tracking |
| `RemoteAccessAlert` | Remote access state change |
| `TabClosedAlert` | Tab closed during ImmediateDanger |
| `TabChangedAlert` | Tab URL changed during ImmediateDanger |
| `RequestToken` | Auth flow |
| `RefreshToken` | Token renewal |

**Local IP detection strategy:**
1. UDP trick: `socket.connect("8.8.8.8", 80)` → `getsockname()`
2. Hostname resolution
3. `getaddrinfo` enumeration
4. Fallback: `""` (empty string)

### Inbound (ZMQ SUB, port 50002)

**File:** `notification_client.py`

- Topic filter: `device:{device_uid}` (only receives messages for this device)
- Multipart message: `[topic_bytes, message_bytes]`
- 5s recv timeout (for checking `_running` flag)
- Heartbeat log every ~2 minutes when idle
- CURVE applied after successful auth

**Notification types received:**

| Type | Handler |
|------|---------|
| `ImmediateDangerNotification` | Activate danger mode, start alert loop |
| `ImmediateDangerEndedNotification` | Deactivate danger mode |
| `SetBrowserTabsPolicyNotification` | Update browser tabs policy |
| `SetTrackedDomainsNotification` | Update tracked domains list |
| URL analysis result (legacy) | Cache result, broadcast to extension |

---

## 12a. Backend Communication (WebSocket, cloud path)

**Files:** `ws_client.py`, `alert_builders.py`, `config_azure.py` (ASPS-718)

**Status:** New feature; alternate transport to the ZMQ path in §12. Full protocol: `docs/architecture/WS-AGENT-PROTOCOL.md`. Design rationale: `docs/architecture/decisions/ADR-004-ASPS-718-WEBSOCKET-GATEWAY.md`.

### Why

In Azure Container Apps, the Backend's ZMQ ports (50001/50002) are not externally reachable — the Envoy sidecar does not forward raw ZMTP. `ws_client.py` connects instead to a WebSocket gateway (`/ws/agent`, subprotocol `asps-agent-v1`) hosted by WebApi, which bridges to the same Backend ZMQ sockets on `localhost`. Backend itself is unchanged.

### Transport selection

`config.py` exposes `TRANSPORT_MODE` (`"zmq"` default, local/on-prem) or `"ws"` (Azure). `config_azure.py` is a build-time override that sets `TRANSPORT_MODE="ws"` and `WS_URL`. `core/container.py` reads `TRANSPORT_MODE` and wires either the existing `ZMQClient` + `NotificationClient` pair, or a single shared `WSClient` instance, into the same consumer interfaces (`scan_service.py`, `notification_handler.py`, etc.) — no other component needs to know which transport is active.

### `WSClient`

Combines the `ZMQClient` (outbound alert/token requests) and `NotificationClient` (inbound SUB) roles over one persistent `wss://` connection, instead of two separate sockets:

- Outbound: same alert/token payloads as `zmq_client.py`, built via the shared `alert_builders.py` (extracted so both transports stay byte-for-byte identical — DRY).
- Inbound: same notification types as `notification_client.py` (§12 table).

### Security

- **TLS replaces CURVE** as the transport-security boundary for the cloud path — `wss://` terminates at the Container Apps managed certificate. CURVE key provisioning is skipped when `TRANSPORT_MODE="ws"` (`config.py`).
- **Device-token auth** is still enforced at the application layer — at the gateway (WebApi) and again at the Backend — independent of transport.
- **Reconnection:** auto-replays auth and re-subscribes on reconnect, with exponential backoff from 1s up to 30s.

### Config.py additions (ASPS-718)

| Setting | Default | Purpose |
|---------|---------|---------|
| `TRANSPORT_MODE` | `"zmq"` | `"zmq"` (local ZMQ REQ/SUB) or `"ws"` (cloud WebSocket via `/ws/agent`) |
| `WS_URL` | unset (zmq mode) / set by `config_azure.py` | `wss://` endpoint for `/ws/agent` |

---

## 13. Protective Actions

**File:** `services/protection_service.py`

Actions dispatched by `subject` field:

| Subject | Action type | Implementation |
|---------|-------------|----------------|
| Device | DisplayNotification | Toast notification via tray |
| Device | SoundAlert | TODO |
| Device | BlockUrl | Cache URL with block result |
| Device | QuarantineDevice | Show notification (enforcement TODO) |
| User | DisplayNotification | Toast notification |
| User | EmailNotification | TODO |
| Protector | EmailNotification | TODO |

**Action map (ProtectiveActionType → cache value):**

| Action | Cache value |
|--------|-------------|
| DisplayNotification | 0 |
| EmailNotification | 1 |
| SoundAlert | 2 |
| BlockUrl | 3 |
| QuarantineDevice | 4 |
| BlockRemoteAccess | 4 |

---

## 14. Notification System

**File:** `notification_manager.py`

### Toast notifications (Windows)

Primary library: `winotify`  
Fallback: `win10toast`

**Risk levels and presentation:**

| Level | Icon | Audio |
|-------|------|-------|
| none | ✅ | Default |
| low | ℹ️ | Default |
| medium | ⚠️ | Reminder |
| high | 🚨 | LoopingAlarm |
| critical | 🛑 | LoopingAlarm |

**Risk score → level mapping:**

| Score range | Level |
|-------------|-------|
| 0–20 | none |
| 21–40 | low |
| 41–60 | medium |
| 61–80 | high |
| 81–100 | critical |

**Action buttons:** Added for high/critical ("View Details").

**Icon files:** `data/icons/risk_{level}.ico`

### URL alert notification

- Maps risk_type IDs to names: Phishing, Cloaking, Impersonation, etc.
- Title includes risk icon emoji + level name
- Message includes URL and risk types

### Remote access alert notification

- `incoming` direction → `critical` level
- `outgoing` direction → `medium` level
- App name shown (AnyDesk, TeamViewer, etc.)
- `is_startup=True` flag changes message text ("was already running" vs "just started")

---

## 15. System Tray

**Files:** `tray_icon.py`, `tray_popup.py`, `ui/colors.py`

### Icon

Programmatically drawn shield shape (ellipse + inner circle + dot) using `Pillow`.

| Color | State |
|-------|-------|
| Gray | Disconnected / not running |
| Blue | Connected, no issues |
| Green | Protected |
| Red | Alert active |
| Orange | Warning |

### Menu

- **Show Status** (default left-click action) → opens `TrayPopup`
- Version string
- Email / device ID display
- Extension connection status
- Backend connection status
- **Dashboard** → print stats to log (device_id, auth status, cache stats, connections)
- **Preferences** submenu → Settings / View Logs
- **About**
- **Exit**

### Popup window (`TrayPopup`)

- Built with `customtkinter`
- Shows: protection status, protection color, remote access info (app name, direction), system status
- **Stop Session** button (implementation TODO)

---

## 16. Event Logging

**File:** `event_logger.py`

**Format:** JSON Lines (`%APPDATA%\AntiScam\events.jsonl`)

```json
{
  "timestamp": "2026-06-30 12:00:00",
  "type": "SuspiciousUrlAlert",
  "direction": "sent",
  "data": { ... }
}
```

**Directions:** `sent` | `received` | `local` | `error`

**Operations:**
- `log_sent()` / `log_received()` / `log_event()` / `log_error()` — convenience wrappers
- `get_recent_logs(n=100)` — last N entries
- `get_logs_by_type(event_type)` — filter by type
- `clear_old_logs(days=30)` — remove entries older than N days
- `get_stats()` — file size, line count, path

---

## 17. Cache Management

**File:** `cache_manager.py`

- **Key:** domain (`urllib.parse.urlparse(url).netloc`)
- **Storage:** `%APPDATA%\AntiScam\cache.json`
- **Entry fields:** url, score, risk_type, protective_action, ttl, saved_at
- **Expiry check:** `saved_at + ttl < now`
- **Load:** on startup, expired entries skipped
- **Save:** async after every `set()` / `remove()` call
- **Cleanup:** expired entries removed before each save

**Cache operations:**
- `get(url)` → `CacheEntry | None`
- `set(url, score, risk_type, action, ttl)` → stores + persists
- `has(url)` → `bool` (valid + not expired)
- `remove(url)` → delete specific entry
- `clear()` → wipe all entries
- `get_stats()` → count, file path

---

## 18. Dependency Injection Container

**File:** `core/container.py`

Singleton accessed via `Container.instance()`. All components are **lazily instantiated** — created on first access.

`apply_curve_keys()` — copies `server_public_key` from `auth_manager` to `notification_client` after successful auth.

`reset()` — clears singleton instance (for testing).

---

## 19. Data Models

**File:** `models.py`, `enums.py`

### Key models

| Class | Purpose |
|-------|---------|
| `DeviceInfo` | Device identification (id, version, IP, userAgent, timezone, OS type) |
| `DeviceAuthRequest` | Auth request payload |
| `DeviceAuthResponse` | Token + isAuthorized flag |
| `SuspiciousUrlAlert` | URL analysis request |
| `SuspiciousUrlAlertResponse` | score, riskType[], protectiveAction |
| `RemoteAccessAlert` | Full detection payload (see §7) |
| `TrackUrlAlert` | URL navigation tracking |
| `ExtensionUrlCheck` | Inbound from extension |
| `ExtensionUrlResponse` | Outbound to extension |
| `Tracker` | Web tracker (Type, Value) |

### Key enums (`enums.py`)

| Enum | Values |
|------|--------|
| `RemoteAccessApp` | Unknown, AnyDesk, TeamViewer, ChromeRemoteDesktop, RDP, Splashtop, LogMeIn, ConnectWise, RemotePC, VNC, QuickAssist |
| `ProtectiveActionType` | DisplayNotification, EmailNotification, SoundAlert, BlockUrl, QuarantineDevice, BlockRemoteAccess |
| `ProtectiveActionSubject` | Device, User, Protector |
| `DeviceType` | Unknown, PersonalComputer, SmartPhone, Other |
| `OperatingSystemType` | Unknown, Windows, Linux, Mac, Android, IOS |
| `DeviceMonitoringStatus` | Disabled, Enabled |
| `ConnectionStatus` | Unknown, Open, Closed |
| `CautionLevel` | Low, Medium, High |
| `AlertFlagType` | NONE, RemoteAccess_AppRunning, RemoteAccess_ConnectionOpen, RemoteAccess_SessionActive |
| `ResultStatusCode` | 200, 400, 401, 403, 404, 422, 500 |
| `Priority` | Low, Medium, High, Critical |
| `Severity` | Low, Medium, High, Critical |
| `AccountType` | Email, Communication, Social, Financial, Other |

---

## 20. Configuration

**File:** `config.py`

| Setting | Default | Override |
|---------|---------|----------|
| Backend host | `127.0.0.1` (dev) / `app.asps.io` (prod) | env var / config_override.py |
| Backend REQ port | `50001` | config |
| Backend SUB port | `50002` | config |
| `TRANSPORT_MODE` (ASPS-718) | `"zmq"` | `"zmq"` (local) or `"ws"` (Azure, via `config_azure.py`) |
| `WS_URL` (ASPS-718) | unset | set by `config_azure.py` when `TRANSPORT_MODE="ws"` |
| Extension ports | `[8080, 8181, 8282, 8383, 8484]` | config |
| Monitor interval | `5s` | env |
| ImmediateDanger alert interval | `10s` | env |
| Browser tabs policy | `incoming_only` | runtime |
| Data directory | `%APPDATA%\AntiScam` (Win) / `~/.antiscam` (Unix) | OS |
| CURVE server key | env `ANTISCAM_CURVE_PUBLIC_KEY` → `~/.antiscam/curve-public-key.txt` → `%LOCALAPPDATA%\ASPS\curve-server-public-key.txt` | — |

**Build-time overrides:** `config_override.py` (imported from `config_dev.py` or `config_prod.py` at build time) can override any setting.

---

## 21. Shutdown Sequence

Triggered by tray menu "Exit" → `_on_exit()` → `_running = False`.

```
1. MonitorService.stop() → cancel background tasks
2. ExtensionServer.stop() → close all client connections
3. NotificationClient.stop() → signal thread, wait 2s
4. ZMQClient.destroy() → close socket + terminate context
5. Print "AntiScam Desktop stopped"
```

---

## 22. Edge Cases & Notable Behaviors

### Token / CURVE bootstrap

First `RequestToken` sent **unencrypted** (no CURVE keys yet). Backend response includes the server public key. Subsequent messages use CURVE.  
CURVE key stored in file, NOT in keyring — intentionally re-read from file each session to pick up changes.

### ImmediateDanger priority escalation

During active ImmediateDanger, `_effective_priority()` upgrades all outbound alert priorities to `CRITICAL`, regardless of the original risk score.

### Extension MV3 service-worker delay

Chrome MV3 extensions have a lazy-starting service worker. When the agent boots before the extension, initial RA alerts have no browser tabs. When the extension finally connects, `on_client_connect` fires, waits 2s (to let the service worker fully initialize), queries tabs, and re-emits the RA alert.

### Pending URL auto-cleanup

`_pending_urls` entries older than 60s are removed on next access. Prevents memory accumulation if backend never responds.

### Browser history temp-copy strategy

Cannot read locked SQLite files. `temp_database_copy()` context manager:
1. Copies DB to temp file
2. Opens temp file for reading
3. Deletes temp file on exit (even if exception)

### Adaptive polling debounce

When a remote app closes, the monitor does NOT immediately declare session_ended. A 1s debounce tick continues polling to confirm the state is stable before emitting `session_ended` event.

### Local URL bypass

URLs matching `localhost` or `127.0.0.1` are never sent to the Backend. `_is_local_url()` check in `ScanService`.

### Google OAuth (unused)

`google_auth.py` implements full Google OAuth2 flow (browser → local redirect server → token exchange → credential storage). Currently not used — auth flow uses `RequestToken` (device-based auth) instead.

### SignalR client (status unknown)

`signalr_client.py` exists in `src/` — appears to be an alternative notification transport (WebSocket-based SignalR vs ZMQ PUB/SUB). Active use in current flow: **TBD** (see Open Items in parent spec).

---

## 23. Known Gaps / TODOs

| Area | Gap | File |
|------|-----|------|
| Session termination | "Stop Session" button → `_on_stop_session()` logs but does not terminate remote session | `main.py` |
| Sound alert | `SoundAlert` protective action not implemented | `protection_service.py` |
| Email notifications | User + Protector email actions not implemented | `protection_service.py` |
| Quarantine | QuarantineDevice notification shown but not enforced | `protection_service.py` |
| Preferences UI | `_on_preferences()` logs but no UI | `main.py` |
| SignalR client | Purpose and active use unclear | `signalr_client.py` |
| Browser history | `browser_history.py` present but active use in current flow TBD | `browser_history.py` |
| SCRUM-863 | Auto-update via Velopack (In Review) | — |
| SCRUM-901 | Code anti-reverse-engineering / obfuscation | — |
| SCRUM-902 | Authenticode code signing | — |
| WebSocket TLS | Extension ↔ Agent WebSocket is plain `ws://` (known security debt) | `extension_server.py` |
| BrowserTabs policy persistence | Policy override resets on agent restart | `browser_tabs_policy.py` |
