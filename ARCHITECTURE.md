# ASPS — Anti-Scam Protection System

## Architecture & Technical Reference

---

## Table of Contents

1. [System Overview](#1-system-overview)
2. [Component Map](#2-component-map)
3. [Chrome Extension](#3-chrome-extension)
4. [Desktop App (Python)](#4-desktop-app)
5. [Backend (.NET)](#5-backend)
6. [WebApi (.NET)](#6-webapi)
7. [Python URL Analyzer](#7-python-url-analyzer)
8. [End-to-End Flow](#8-end-to-end-flow)
9. [Communication Protocols](#9-communication-protocols)
10. [Security & Authentication](#10-security--authentication)
11. [Message Reference](#11-message-reference)
12. [Configuration Reference](#12-configuration-reference)
13. [Running the System](#13-running-the-system)
14. [WebApi Admin UI Pages](#14-webapi-admin-ui-pages)
15. [Roadmap Module](#15-roadmap-module)
16. [Mobile Agents — Specification (to-be-built)](#16-mobile-agents--specification-to-be-built)

---

## 1. System Overview

ASPS is a real-time URL threat detection system. When a user visits a website, the Chrome Extension captures the URL, sends it through a Desktop bridge app to a .NET Backend for analysis, and displays a risk score back in the browser.

**Core flow:**
```
User visits URL → Extension captures it → Desktop App forwards to Backend
→ Backend runs analysis (DB + Python ML) → Result pushed back to Extension
→ User sees risk score + warning banner
```

**Tech stack:**
- **Chrome Extension** — Manifest V3, service worker architecture
- **Desktop App** — Python 3.14, asyncio + WebSocket + ZeroMQ
- **Backend** — .NET 8, Entity Framework Core, ZeroMQ, MySQL
- **WebApi** — .NET 8, Razor Pages, SignalR, CQRS over NetMQ
- **URL Analyzer** — Python, scikit-learn, WHOIS, web scraping

---

## 2. Component Map

```
┌─────────────────────────────────────────────────────────────────┐
│                         USER'S BROWSER                          │
│                                                                 │
│  ┌──────────────┐  chrome.runtime  ┌──────────────────────┐    │
│  │ content.js   │ ◄──────────────► │ background.js        │    │
│  │ (per tab)    │    messages       │ (service worker)     │    │
│  │              │                   │                      │    │
│  │ • page info  │                   │ • ScanService        │    │
│  │ • trackers   │                   │ • ConnectionService  │    │
│  │ • iframes    │                   │ • StateManager       │    │
│  │ • warnings   │                   │ • CacheService       │    │
│  └──────────────┘                   │ • ProtectionService  │    │
│                                     └──────────┬───────────┘    │
│  ┌──────────────┐                              │                │
│  │ popup.js     │ ◄───── state updates ────────┘                │
│  │ (UI panel)   │                                               │
│  └──────────────┘                                               │
└────────────────────────────────────────────────┬────────────────┘
                                                 │
                                        WebSocket (JSON)
                                    ws://localhost:8080-8484
                                                 │
┌────────────────────────────────────────────────┼────────────────┐
│                     DESKTOP APP (Python)        │                │
│                                                 │                │
│  ┌─────────────────────────────────────────────┐│                │
│  │ ExtensionServer (WebSocket)                 ││                │
│  │ Ports: 8080, 8181, 8282, 8383, 8484        │◄┘                │
│  └──────────────────┬──────────────────────────┘                │
│                     │                                            │
│  ┌──────────────────▼──────────────────────────┐                │
│  │ ExtensionHandler                            │                │
│  │ Routes: url_check, ping, user_auth, signout │                │
│  └──────────────────┬──────────────────────────┘                │
│                     │                                            │
│  ┌──────────────────▼──────────┐  ┌───────────────────────┐    │
│  │ ScanService                 │  │ NotificationHandler   │    │
│  │ • cache check               │  │ • ZMQ SUB listener    │    │
│  │ • auth check                │  │ • result processing   │    │
│  │ • ZMQ REQ to backend        │  │ • broadcast to ext    │    │
│  └──────────────────┬──────────┘  └───────────┬───────────┘    │
│                     │                          │                 │
│  ┌──────────────────▼──────────┐  ┌───────────▼───────────┐    │
│  │ ZMQClient (REQ)             │  │ ZMQ SUB Socket        │    │
│  │ tcp://127.0.0.1:50001       │  │ tcp://127.0.0.1:50002 │    │
│  │ CURVE encrypted             │  │ CURVE encrypted       │    │
│  └──────────────────┬──────────┘  └───────────┬───────────┘    │
└─────────────────────┼─────────────────────────┼────────────────┘
                      │                          │
            ZMQ REQ/REP                  ZMQ PUB/SUB
            (CURVE)                      (CURVE)
                      │                          │
┌─────────────────────┼─────────────────────────┼────────────────┐
│                  BACKEND (.NET)                 │                │
│                     │                          │                 │
│  ┌──────────────────▼──────────┐  ┌───────────▼───────────┐    │
│  │ NetMQAlertIngress            │  │ NetMQNotificationEgress│   │
│  │ (IAlertIngress, IHostedSvc) │  │ (INotificationEgress)  │   │
│  │ ZMQ ROUTER on port 50001    │  │ ZMQ PUB on port 50002 │    │
│  │ • token validation          │  │ • per-device topics    │    │
│  │ • device registration       │  │ • analysis results     │    │
│  │ • delegates parsing/routing │  │                        │    │
│  │   to AlertProcessor          │  │                        │    │
│  └──────────────────┬──────────┘  └───────────▲───────────┘    │
│                     │                          │                 │
│  ┌──────────────────▼──────────────────────────┤                │
│  │ UDAnalysisManager (per user)                │                │
│  │                                             │                │
│  │  ┌─────────────────┐  ┌─────────────────┐  │                │
│  │  │ UDPhishingCheck  │  │ UDUrlAnalyzer   │──┘                │
│  │  │ • known DB check │  │ • Python runner │                   │
│  │  └─────────────────┘  └────────┬────────┘                   │
│  └────────────────────────────────┼─────────────────────────────┘
│                                   │                              │
│  ┌────────────────────────────────▼─────────────────────────┐   │
│  │ ASView (in-memory read model)                            │   │
│  │ • users, devices, alerts, analysis results, phishing DB  │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ TokenStore          │ NetMQCqrsTransport (port 5556)     │   │
│  │ • issue tokens      │ (ICqrsTransport, IHostedService)   │   │
│  │ • validate tokens   │ • WebApi ↔ Backend bridge          │   │
│  │                     │ • dispatches via CqrsHandlerRegistry│   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ MySQL Database (ASPSBackend2DB)                          │   │
│  │ Tables: Users, UserDevices, DeviceAlerts, AnalysisResults│   │
│  │         KnownPhishingWebsites (506K records), SafeDomains│   │
│  └──────────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│                     WEBAPI (.NET)                                 │
│                                                                  │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────────────────┐  │
│  │ Controllers  │  │ Razor Pages  │  │ SignalR Hub           │  │
│  │ (REST API)   │  │ (Admin UI)   │  │ (/notificationshub)   │  │
│  └──────┬───────┘  └──────┬───────┘  └───────────────────────┘  │
│         │                  │                                      │
│  ┌──────▼──────────────────▼────────────────────────────────┐   │
│  │ NetMQCqrsClient (ICqrsClient, NetMQ REQ → Backend 5556)  │   │
│  │ exposed to the 39 existing consumers via the legacy       │   │
│  │ ICQRSClient contract through NetMQCqrsClientAdapter       │   │
│  │ ZERO database access — all data via NetMQ                │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
│  Ports: HTTP 5001 (Admin Dashboard), HTTPS 7001 (Swagger)       │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│                  PYTHON URL ANALYZER                              │
│                                                                  │
│  Invoked by Backend as subprocess:                               │
│  python analyze.py "https://example.com" --json                  │
│                                                                  │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────────────────┐  │
│  │ WHOIS        │  │ Content      │  │ ML Classifier         │  │
│  │ • domain age │  │ • patterns   │  │ • scikit-learn model  │  │
│  │ • registrar  │  │ • trackers   │  │ • feature extraction  │  │
│  │ • country    │  │ • forms      │  │ • confidence score    │  │
│  └──────────────┘  └──────────────┘  └───────────────────────┘  │
│                           │                                      │
│                    ┌──────▼──────┐                                │
│                    │ Risk        │                                │
│                    │ Assessor    │  → JSON output to stdout       │
│                    └─────────────┘                                │
└──────────────────────────────────────────────────────────────────┘
```

---

## 3. Chrome Extension

**Location:** `apps/extension/chrome/`

### 3.1 Architecture

The extension uses Manifest V3 with a service worker (`background.js`) as the central hub. Content scripts run in each tab, and the popup provides the user interface.

### 3.2 Key Services

| Service | File | Purpose |
|---------|------|---------|
| ScanService | `services/ScanService.js` | Orchestrates URL scanning |
| ConnectionService | `services/ConnectionService.js` | WebSocket to Desktop App |
| StateManager | `services/StateManager.js` | Centralized state (dot notation) |
| CacheService | `services/CacheService.js` | URL result cache (TTL: 1 hour) |
| ProtectionService | `services/ProtectionService.js` | Warning banners & blocks |
| AuthService | `services/AuthService.js` | User sign-in/sign-out |

### 3.3 Connection to Desktop App

```javascript
// ConnectionService tries ports in order until one works
const PORTS = [8080, 8181, 8282, 8383, 8484];
const ws = new WebSocket(`ws://localhost:${port}`);

// Heartbeat: every 10 seconds
// Keepalive: every 20 seconds (keeps service worker alive)
// Reconnect: exponential backoff (1s → 2s → 4s → ... → 30s max)
```

### 3.4 Content Script ↔ Background Communication

```javascript
// content.js listens for page info requests from background
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
    if (message.type === 'page:info:request') {
        sendResponse({
            trackers: extractTrackers(),
            iframes: extractIFrames(),
            title: document.title
        });
    }
});
```

### 3.5 URL Scan Flow (Extension Side)

```
1. Tab updated/activated → ScanService.scan(tabId, url)
2. Check cache → HIT? Return cached result
3. Request page info from content script (trackers, iframes)
4. Send to Desktop via WebSocket:
   { type: "url_check", url, trackers, iframes }
5. Receive intermediate: { type: "url_result", analyzing: true }
6. Receive final:
   { type: "url_result", url, score, riskType, protectiveAction }
7. Cache result (TTL: 3600s)
8. Execute protective action (banner/modal/block)
```

### 3.6 Protective Actions

The Backend emits one or more `ProtectiveAction` items per analysis result, each with a `type` (from the enum below) and an `ActionLevel` (`Device` / `User` / `Protector`). The Extension and Desktop App map them to UI / OS-level effects.

Source: [`Common/Enums/Enumerations.cs` → `ProtectiveActionType`](ASPSBackend14_J/Common/Enums/Enumerations.cs).

| Value | Name | Where it fires | Effect |
|-------|------|---------------|--------|
| 0 | None | — | No action |
| 1 | DisplayNotification | Backend / Extension | Banner or popup in the browser tab |
| 2 | EmailNotification | Backend | Email sent to user / protector |
| 3 | SoundAlert | Desktop | OS-level alert sound |
| 4 | BlockUrl | Extension | Replace page with block screen |
| 5 | UserDisplayNotification | Extension | Inline notification in current tab |
| 6 | QuarantineDevice | Backend | Mark device as quarantined (admin review) |
| 7 | BlockRemoteAccess | Desktop | Terminate active remote-access session |
| 8 | EnableUrlTracking | Backend | Switch URL into long-duration tracking mode |
| 9 | SetTrackMode | Backend | Adjust the tracking sensitivity for a flow |

### 3.7 Risk Model

The system **does not use a closed `RiskType` enum**. Risk is modelled as:

- `risk_score` — float in `0..100` (numeric severity)
- `risk_level` — string label, derived from score: `"LOW"` / `"MEDIUM"` / `"HIGH"`
- `is_scam` — boolean (final verdict)
- `confidence` — float in `0..1` (how sure the analyzer is)
- An open-ended array of risk **categories** returned by analyzers (e.g. `"phishing"`, `"impersonation"`, `"new-domain"`, `"urgency-language"`).

Source: [`Common/Models/RiskAssessment.cs`](ASPSBackend14_J/Common/Models/RiskAssessment.cs) and the Python analyzer's `risk_assessment` block (see §7.4).

Score → typical action mapping (configurable, see §5.5):

| Score | Default action |
|-------|---------------|
| 0–29 | None / DisplayNotification |
| 30–49 | DisplayNotification (banner) |
| 50–69 | UserDisplayNotification (modal-style) |
| 70–100 | BlockUrl |

---

## 4. Desktop App

**Location:** `apps/desktop/win/src/`
**Language:** Python 3.14 (asyncio)

### 4.1 Role

The Desktop App is a **bridge** between the Chrome Extension (WebSocket) and the Backend (ZeroMQ). It runs as a background process on the user's machine.

### 4.2 Startup Sequence (`main.py`)

```
1. Initialize dependency injection container
2. Generate device ID (hardware fingerprint: BIOS serial + motherboard)
3. Start WebSocket server (Extension Server)
4. Authenticate with Backend (get token + CURVE server key)
5. Apply CURVE encryption to ZMQ sockets
6. Start ZMQ SUB listener (notification client)
7. Start background monitors (remote access detection)
```

### 4.3 Extension Server

```python
# WebSocket server — tries ports in order
PORTS = [8080, 8181, 8282, 8383, 8484]

# Supports multiple simultaneous Extension connections
# Messages are JSON-encoded
```

### 4.4 Message Routing (`extension_handler.py`)

| Message Type | Handler | Execution |
|-------------|---------|-----------|
| `url_check` | `scan_service.check_url()` | Thread pool executor (non-blocking) |
| `ping` | Returns `pong` + user email | Direct |
| `user_auth` | Saves email to auth_manager | Direct |
| `user_signout` | Clears auth state | Direct |
| `heartbeat_ping` | Returns `heartbeat_pong` | Direct (no logging) |

### 4.5 Scan Service (`scan_service.py`)

**URL Check Flow:**
```
1. Cache check → HIT? Return cached result with url field
2. Auth check → NOT AUTHENTICATED? Return error with url field
3. Build alert payload (UrlAlert)
4. Send via ZMQ REQ to Backend (port 50001)
5. Handle response:
   - "InvalidToken" → refresh token, retry once
   - "TokenExpired" → refresh token, retry once
   - "success" → return "analyzing" status with url field
6. Final result arrives later via NotificationHandler (ZMQ PUB/SUB)
```

**Response format (all paths include `url` field):**
```python
# Success (analyzing)
{"type": "url_result", "url": "https://...", "analyzing": True, "message": "..."}

# Error
{"type": "url_result", "url": "https://...", "error": True, "message": "..."}

# Cached
{"type": "url_result", "url": "https://...", "score": 35, "fromCache": True, ...}
```

### 4.6 Notification Handler (`notification_handler.py`)

Listens on ZMQ SUB (port 50002) for messages pushed by the Backend. Dispatches by the top-level `Type` field, then falls through to the legacy URL-analysis path for untyped notifications.

| `Type` | Handler | Purpose |
|---|---|---|
| `ImmediateDangerNotification` | `_handle_immediate_danger_started` | Activates DangerMode + opens centered locked alert (DisplayNotification ProtectiveActions). |
| `ImmediateDangerEndedNotification` | `_handle_immediate_danger_ended` | Deactivates DangerMode + transforms locked alert to green CLEARED state. |
| `SetBrowserTabsPolicyNotification` | `_handle_set_browser_tabs_policy` | Applies a runtime override (Mode + ValidUntil) to BrowserTabsPolicy. |
| _other_ | URL-analysis path | Cache update + Extension broadcast (existing flow). |

### 4.7 ZMQ Client (`zmq_client.py`)

```python
# REQ/REP pattern with CURVE encryption
socket_type = zmq.REQ
endpoint = "tcp://127.0.0.1:50001"
timeout = 5000  # ms

# CURVE setup (ephemeral client keys)
client_public, client_secret = zmq.curve_keypair()
socket.curve_publickey = client_public
socket.curve_secretkey = client_secret
socket.curve_serverkey = server_public_key  # from config/auth response
```

### 4.8 Remote-Access Monitor (`remote_monitor.py`)

Detects active sessions of remote-access apps (AnyDesk, TeamViewer, Chrome Remote Desktop, VNC, RustDesk, RemotePC, Splashtop, RDP, QuickAssist, ConnectWise, LogMeIn) and emits `RemoteAccessAlert` to the Backend.

**Detection signals (combined):**
- Process scan (`psutil.process_iter`) by `process_names`.
- Established TCP connections owned by the matched processes.
- Listening ports (system-wide for service-hosted apps like RDP under `svchost`).
- Windows service status (e.g., `TermService` for RDP).
- Per-app log parsing (`ad.trace`, `ad_svc.trace`, `connection_trace.txt`, `Connections_incoming.txt`, …).

**Direction inference (priority order):**
1. Log parser (incoming/outgoing/session_started events with timestamps).
2. Topology fallback — `_infer_direction_from_processes` matches established conns vs `listen_ports`.
3. AnyDesk-specific fallback — most recent entry in `connection_trace.txt` (within 30 min).

**Backfill on startup** (`_backfill_app`): collects events from ALL log files for the app, sorts by timestamp, replays through the SessionTracker BEFORE starting tail threads — so a session that began before the agent started is still recognised.

**Adaptive poll interval** (`get_next_poll_interval`):

| State | Interval |
|---|---|
| Pending close/session-end (debounce ticking) | 1s |
| App running OR active session | 5s |
| Idle (no remote-access app detected) | 30s |
| **DangerMode active** (override) | **2s** |

**Debounced state transitions** (`DebouncedStateTracker`): close = 1s, session_end = 4s; both bypassed entirely while DangerMode is active.

### 4.9 DangerMode (`services/danger_mode.py`)

Process-wide flag flipped by ImmediateDanger notifications.

| Event | Action |
|---|---|
| `ImmediateDangerNotification` | `danger_mode.activate()` — fast 2s polling + debounce bypass |
| `ImmediateDangerEndedNotification` | `danger_mode.deactivate()` — revert to adaptive intervals + normal debounce |

Worst-case time from a state change to alert delivery during DangerMode: ≤ 2s.

### 4.10 BrowserTabs Policy (`services/browser_tabs_policy.py`)

Decides whether to attach `BrowserTabs` to a `RemoteAccessAlert`.

| Mode | Behaviour |
|---|---|
| `incoming_only` (default) | Attach tabs only when remote-access direction is `'incoming'` |
| `always` | Attach with every alert |
| `never` | Never attach |

Backend can override the default at runtime via `SetBrowserTabsPolicyNotification` (Mode + ValidUntil). After ValidUntil elapses, the agent reverts to its built-in default. Overrides are not persisted across restarts.

### 4.11 Centered Alert UI (`ui/centered_toast.py`)

Borderless, always-on-top, draggable alert window centered on the primary monitor. Singleton — at most one is on screen at any time.

| Mode | Used by | Behaviour |
|---|---|---|
| `locked` | ImmediateDanger active | No close button. Persistent. Periodic `lift()` every 2s defeats Windows topmost loss. Drag-and-drop on header zone. |
| `cleared` | ImmediateDanger ended | Green styling. **Close** button added. Same window transformed in place via `update_content()`. |

The `View Details` button opens an in-app `DangerDetailsWindow` (CTkToplevel) with the full ImmediateDanger payload — no browser, no admin login. Falls back to opening `WEBAPI_URL` only when the in-app details payload is unavailable.

### 4.12 Build & Distribution

Single-file EXE produced by PyInstaller:

```
python build_release.py [--env <name>]
```

| `--env` value | Effect |
|---|---|
| _omitted_ | Default `config.py` values (local testing — `127.0.0.1`) |
| `dev` | Overrides applied from `src/config_dev.py` |
| `prod` | Overrides applied from `src/config_prod.py` (AWS — `app.asps.io`) |

The build script copies `src/config_<env>.py` to `src/config_override.py` before PyInstaller runs; `config.py` does `try: from config_override import *` at the bottom. The override file is gitignored and removed by an `atexit` hook so the source tree stays clean.

---

## 5. Backend

**Location:** `ASPSBackend14_J/ASPSBackend/` and `ASPSBackend14_J/Business/`
**Language:** .NET 8 / C#

### 5.1 Role

The Backend is the **brain** of the system. It handles authentication, alert processing, URL analysis (including calling the Python analyzer), phishing database lookups, and result distribution.

### 5.2 Services Started on Boot

All messaging services run as `IHostedService`s, registered through `MessagingServiceRegistration.AddMessagingServices()` and `Program.cs`'s `AddHostedService` calls (ASPS-675 Messaging Refactoring). Each transport is exposed behind an interface in `Business/Messaging/Abstractions/` so the NetMQ implementation can be swapped without touching callers.

| Service | Port | Protocol | Purpose |
|---------|------|----------|---------|
| NetMQMessageProcessor | 5555 | ZMQ | Internal CQRS processor (IHostedService) |
| NetMQAlertIngress (`IAlertIngress`) | 50001 | ZMQ ROUTER (REQ-compatible) | Socket lifecycle + CURVE for alerts from Desktop; parsing/routing delegated to `AlertProcessor` |
| NetMQNotificationEgress (`INotificationEgress`) | 50002 | ZMQ PUB | Pushes results to Desktop |
| NetMQCqrsTransport (`ICqrsTransport`) | 5556 | ZMQ | WebApi ↔ Backend bridge; dispatches via `CqrsHandlerRegistry` |
| CqrsHandlerRegistry | — | In-memory | Type-safe command/query → handler dispatch map (registry-based, replaces the old per-message switch dispatch) |
| ASView | — | In-memory | Read model (CQRS) |
| TokenStore | — | In-memory | Token issuance & validation |
| UDAnalysisManagers | — | Per-user | Analysis orchestration |

> **Why ROUTER, not REP?** A `RepSocket` only handles one in-flight request per peer; with hundreds of devices, requests queue up serially. A `RouterSocket` is fully concurrent — each peer's identity frame is preserved so we can hold the request, do background analysis, and respond out of order. `RouterSocket` is wire-compatible with `RequestSocket` clients (which is what the Desktop App still uses), so this change required **zero client work**. See [`NetMQAlertIngress.cs`](ASPSBackend14_J/Business/Messaging/NetMQAlertIngress.cs) (socket/CURVE lifecycle) and [`AlertProcessor.cs`](ASPSBackend14_J/Business/Messaging/AlertProcessor.cs) (message parsing/routing, extracted in Phase 3 of the messaging refactoring).

### 5.3 Alert Listener (`NetMQAlertIngress.cs` + `AlertProcessor.cs`)

Receives all messages from Desktop Apps on port 50001. `NetMQAlertIngress` owns the ROUTER socket, CURVE setup, and the poll-with-timeout receive loop (`IHostedService.StartAsync`/`StopAsync`, cancellable); it delegates message parsing and routing to `AlertProcessor`.

**Message routing:**
```
MessageType = "RequestToken"   → Issue token for device
MessageType = "RegisterDevice" → Register device with email
MessageType = "RefreshToken"   → Renew expired token
AlertType = "UrlAlert"         → Process URL alert
AlertType = "RemoteAccessAlert"→ Process remote access alert
```

**Token validation on every alert:**
```
Valid token       → Process alert
InvalidToken      → { status: "InvalidToken", message: "Please authenticate." }
TokenExpired      → { status: "TokenExpired", message: "Token expired." }
DeviceNotFound    → { status: "DeviceNotRecognized" }
```

### 5.4 Analysis Pipeline

When a `UrlAlert` arrives, the Backend runs this pipeline:

```
Step 1: Token validation
   └─ Fail → return error response

Step 2: Device → User lookup (via ASView)
   └─ Find which user owns this device

Step 3: Route to UDAnalysisManager for that user
   └─ Creates DeviceAlert record

Step 4: Run analyzers in parallel:
   ├─ UDPhishingAnalyzer
   │   └─ Check against KnownPhishingWebsites table (506K+ records)
   │   └─ Check URL match and domain match
   │
   └─ UDUrlAnalyzer
       ├─ 4a. Whitelist check (SafeDomains table)
       ├─ 4b. Known phishing DB check
       ├─ 4c. Cache check (ASView)
       └─ 4d. Python analyzer (subprocess)
            └─ python analyze.py "https://..." --json
            └─ Timeout: 30 seconds

Step 5: Aggregate results
   └─ Combine all analyzer scores
   └─ Calculate overall severity

Step 6: Generate indicators + protective actions

Step 7: Fire events:
   ├─ ASView → update read model
   ├─ AlertPersistenceActor → save to DB
   ├─ AnalysisPersistenceActor → save to DB
   └─ NotificationPublisherActor → push to Desktop
```

### 5.5 Severity Levels

| Score Range | Severity | Typical Action |
|-------------|----------|----------------|
| 0–29 | Low | None or Notify |
| 30–49 | Medium | Warn Banner |
| 50–69 | High | Warn Modal |
| 70–100 | Critical | Block |

### 5.6 Database Schema (MySQL)

| Table | Purpose | Key Columns |
|-------|---------|-------------|
| `Users` | User accounts | Key, Email, FirstName, LastName, KeycloakUserId |
| `UserDevices` | Registered devices | DeviceUid, UserKey, DeviceType, OperatingSystem, MAC |
| `DeviceAlerts` | Incoming alerts (TPH; discriminator splits Url / TrackUrl / RemoteAccess) | AlertType, Url, DeviceUid, Token, Priority |
| `AnalysisResults` | Analysis output | DeviceAlertKey, JsonValue, Severity, HasError |
| `AlertFlags` | Per-alert review/triage flags | AlertKey, Type, Notes |
| `ImmediateDangers` | Persisted ImmediateDanger events (RemoteAccess + sensitive site open) | Key, UserKey, DeviceUid, RemoteAccessApp, SensitiveUrl, ProtectiveActionsJson, Timestamp, EndTime |
| `KnownPhishingWebsites` | Phishing DB | Url, Domain, Source (~500K records) |
| `SafeDomains` | Whitelisted domains | Domain |
| `TrackedDomains` | Long-duration URL tracking | Domain, IsActive, Source |
| `SensitiveSites` | Sensitive-category sites (banking/crypto/etc.) | Domain, IsActive |
| `BankWebsites` | Legitimate bank domains (ASPS-297) | Domain, BankName, Country, IsActive |
| `BlacklistedPhoneNumbers` | Known scam phone numbers (ASPS-282) | PhoneNumber, Source, Notes |
| `WebsiteCategories` | Hierarchical taxonomy (SCRUM-820/822) | Name, ParentId, Source |
| `Simulations` | Test scenarios for the dashboard | Name, Steps (JSON) |
| `DeviceTokens` | Active device tokens | DeviceUid, Token, ExpiresAt |
| `Roadmaps` | Product roadmap data (admin-only) | Name, Data (JSON), Version, LastUpdatedBy |

**RemoteAccessAlert columns** (TPH discriminator on `DeviceAlerts`):
`RemoteAccessApp`, `RunningProcesses`, `ConnectionUrl`, `ConnectionStatus`, `ConnectionsCount`, `SessionStatus`, `RemoteOS`, `RemoteVersion`, `ConnectionType`, `FileTransferActive`, `FileTransfers`,
`RemoteId`, `RemoteName`, `LoggedUser`, `ConnectionId`, `Software` (forensics — added in `AddRemoteSessionForensicsToRemoteAccessAlert`),
`Direction`, `Confidence`, `RemoteCountry`, `RemoteCountryCode` (wire fields — added in `AddDirectionAndGeoToRemoteAccessAlert`).

### 5.7 Notification Publishers

The Backend pushes typed messages to Agents via `NetMQNotificationEgress` (`INotificationEgress`, NetMQ PUB on port 50002). Topic format: `device:{deviceUid}` and/or `user:{userKey}`. Each method wraps its payload in `{ Type, Timestamp, DeviceUid, Data }`.

| Method | Wire `Type` | Trigger | Agent handler |
|---|---|---|---|
| `PublishAnalysisResult` | `AnalysisResult` | `AnalysisResultReceived` (URL/TrackUrl/RemoteAccess analyzer finished) | URL-analysis path |
| `PublishImmediateDangerEvent` | `ImmediateDangerNotification` | `ImmediateDangerEvent` raised by `UDUserAnalyzer` (one ProtectiveActions resolution per user) | `_handle_immediate_danger_started` |
| `PublishImmediateDangerEnded` | `ImmediateDangerEndedNotification` | `ImmediateDangerEnded` raised by `UDUserAnalyzer` when the underlying RemoteAccess condition clears | `_handle_immediate_danger_ended` |
| `PublishSetBrowserTabsPolicy` | `SetBrowserTabsPolicyNotification` | Backend-driven (admin command / future automation) — Mode + ValidUntil | `_handle_set_browser_tabs_policy` |

**ImmediateDanger flow** (single canonical raiser to avoid duplicates):

```
UDUserAnalyzer.DetectImmediateDanger()
  → ImmediateDangerDetected           [per-user UDAnalysis publisher]
    → ImmediateDangerPersistanceActor   (saves entity)
      → BuildPerUserHandlers(includeSingletons=true)
        → ImmediateDangerAdded         [singletons + UDAnalysisManager + UDAnalysis]
          → ASView.HandleImmediateDangerAdded     (cache update)
          → UDAnalysisManager.Handle              (delegates to UDUserAnalyzer)
            → UDUserAnalyzer.HandleImmediateDangerAdded
              → ImmediateDangerEvent               [own publisher]
                → NotificationPublisherActor       → PublishImmediateDangerEvent (the ONE notification)
          → UDAnalysis.Handle                      (log only — does NOT re-raise)

UDUserAnalyzer.DetectImmediateDanger() (clearing path)
  → ImmediateDangerEnded               [per-user UDAnalysis publisher]
    → ImmediateDangerPersistanceActor    (sets EndTime)
      → BuildPerUserHandlers(includeSingletons=false)   ← skips singletons; they already received
        → UDAnalysisManager + UDAnalysis             (per-user only)
    → NotificationPublisherActor (singleton, received from original raise)
      → PublishImmediateDangerEnded (the ONE notification)
```

### 5.8 Messaging Transport Architecture (ASPS-675 Messaging Refactoring)

All NetMQ transports in the Backend were extracted behind transport-agnostic interfaces during the ASPS-675 messaging refactoring, replacing what used to be a single monolithic `CQRSGateway` class with a 71-case manual switch dispatch. The goal: swap NetMQ for another transport without touching handler logic, and make every socket a testable, cancellable `IHostedService`.

**Abstractions** (`Business/Messaging/Abstractions/`):

| Interface | Purpose | NetMQ implementation |
|---|---|---|
| `ICqrsTransport` | WebApi ↔ Backend command/query bridge (server side) | `NetMQCqrsTransport` (port 5556) |
| `ICqrsClient` | WebApi-side command/query sender | `NetMQCqrsClient` |
| `IAlertIngress` | Receives alerts from Desktop Apps | `NetMQAlertIngress` (port 50001) |
| `INotificationEgress` | Pushes results/notifications to Desktop Apps | `NetMQNotificationEgress` (port 50002) |

**Handler dispatch — `CqrsHandlerRegistry`:** a type-safe `ConcurrentDictionary<string, Func<string, IServiceScope, Task<string>>>` mapping each `CommandType`/`QueryType` string to a dispatch delegate. Handlers are registered once at startup in `MessagingServiceRegistration.AddMessagingServices()` (commands) and `CqrsQueryRegistration.RegisterQueryHandlers()` (queries) — 21 command handlers and 50 query handlers, one line per type, replacing the former per-message `switch` statements in `CQRSGateway.Commands.cs`/`CQRSGateway.Queries.cs` (deleted in Phase 6, ASPS-690). `NetMQCqrsTransport` still owns CURVE setup, envelope authentication (`CqrsChannelSecurity`), and command authorization — those stay transport/gateway-level concerns — then calls `_registry.DispatchAsync(type, messageJson, scope)`.

**Alert ingress split (Phase 3):** `NetMQAlertIngress` owns the ROUTER socket, CURVE, and the receive loop; `AlertProcessor` owns message-type routing and token validation, called from the ingress loop.

**Lifecycle (Phase 5):** every transport implements `IHostedService`, registered via `services.AddHostedService(...)` in `Program.cs` and started/stopped by the generic host (`host.RunAsync()`), instead of being manually `Start()`/`Stop()`-ed from `Program.Main()`. Each receive loop polls with a timeout (`TryReceive*(TimeSpan.FromMilliseconds(500), ...)`) and checks both an internal `_running` flag and the `CancellationToken` passed to `StartAsync`, so `StopAsync` can signal shutdown cooperatively without blocking.

**WebApi-side CQRS client (Phase 4):** `NetMQCqrsClient` is the canonical implementation behind `ICqrsClient`. The 39 existing WebApi consumers still depend on the legacy `ICQRSClient` contract; `NetMQCqrsClientAdapter` bridges `ICQRSClient` → `ICqrsClient` so both resolve to the same singleton socket (see §6.2). Mechanical migration of those consumers from `ICQRSClient` to `ICqrsClient` directly was deferred past Phase 6 — see the handoff notes for details.

---

## 6. WebApi

**Location:** `ASPSBackend14_J/WebApi/`
**Language:** .NET 8 / C#

### 6.1 Role

The WebApi is the **admin interface**. It provides a web dashboard and REST API for managing the system. It has **zero direct database access** — all data operations go through NetMQ to the Backend.

### 6.2 Architecture

```
WebApi (Presentation Layer)
    │
    ├── Controllers (REST API)
    ├── Razor Pages (Admin Dashboard)
    ├── SignalR Hub (real-time updates)
    │
    └── ICQRSClient (39 consumers) ──── NetMQCqrsClientAdapter ──── ICqrsClient
              │                                                          │
              └──────────────────── NetMQCqrsClient ── NetMQ REQ ──── Backend:5556
                                    (commands & queries)
```

`NetMQCqrsClient` is the canonical transport implementation, registered behind the transport-agnostic `ICqrsClient` interface (`Business.Messaging.Abstractions`, ASPS-687). Existing WebApi consumers keep depending on the legacy `ICQRSClient` contract unmodified; `NetMQCqrsClientAdapter` bridges the two so both resolve to the same singleton NetMQ REQ socket (`Business` cannot reference `WebApi`, so the adapter — not the transport class itself — implements `ICQRSClient`).

### 6.3 Endpoints

| URL | Type | Purpose |
|-----|------|---------|
| `http://localhost:5001` | Razor Pages | Admin Dashboard |
| `https://localhost:7001/swagger` | Swagger UI | API Documentation |
| `/notificationshub` | SignalR | Real-time notifications |

### 6.4 CQRS Pattern

```
WebApi sends a Command/Query to Backend:
  → ICQRSClient.SendXxxAsync(message) → NetMQCqrsClientAdapter → NetMQCqrsClient
    via NetMQ REQ to port 5556
  ← Backend's NetMQCqrsTransport (ICqrsTransport, IHostedService) authenticates
    the envelope, then dispatches to a registered handler via CqrsHandlerRegistry
  → WebApi receives response and renders UI/API response
```

---

## 7. Python URL Analyzer

**Location:** `basic-url-analyzer/basic-url-analyzer/basic-url-analyzer/`
**Language:** Python 3.14

### 7.1 Role

External analysis engine invoked by the Backend as a subprocess. Performs deep URL analysis including WHOIS, content scraping, pattern matching, and ML classification.

### 7.2 Invocation

```bash
# Called by Backend's UDUrlAnalyzer.cs
python analyze.py "https://example.com" --json

# Flags:
#   --json      JSON output (required for Backend integration)
#   --verbose   Detailed logging
#   --no-cache  Bypass analyzer cache
#   --no-ml     Skip ML classifier
```

### 7.3 Analysis Modules

| Module | File | What It Does |
|--------|------|-------------|
| WHOIS | `core/whois_analyzer.py` | Domain age, registrar, country, privacy status |
| Content | `core/content_analyzer.py` | Page patterns, trackers, forms, urgency language |
| ML Classifier | `core/ml_classifier.py` | scikit-learn model, feature extraction |
| Risk Assessor | `core/risk_assessor.py` | Weighted score aggregation, final verdict |

### 7.4 Output Format

```json
{
  "url": "https://example.com",
  "domain": "example.com",
  "analyzed_at": "2026-02-13T12:00:00Z",
  "analysis_time_ms": 1250,
  "from_cache": false,
  "whois": {
    "success": true,
    "domain_age_days": 1825,
    "created_date": "2019-01-30",
    "registrar": "GoDaddy",
    "country": "US",
    "privacy_protected": false
  },
  "content_analysis": {
    "success": true,
    "title": "Example Store",
    "word_count": 450,
    "detected_patterns": [
      {
        "type": "urgency",
        "name": "limited_time",
        "matched_text": "Limited time offer!",
        "weight": 15
      }
    ]
  },
  "ml_analysis": {
    "enabled": true,
    "score": 0.25,
    "confidence": 0.80
  },
  "risk_assessment": {
    "risk_score": 35,
    "risk_level": "MEDIUM",
    "is_scam": false,
    "confidence": 0.75
  },
  "red_flags": [
    "New domain (< 6 months)",
    "Excessive urgency language"
  ]
}
```

### 7.5 Dependencies

Key packages: `playwright`, `beautifulsoup4`, `scikit-learn`, `numpy`, `python-whois`, `httpx`, `duckduckgo_search`, `ddgs`, `langdetect`, `validators`

---

## 8. End-to-End Flow

This is the complete journey of a URL scan from the moment a user visits a website to when they see the result.

```
 STEP   COMPONENT            ACTION
 ────   ─────────            ──────

  1     Browser              User navigates to https://example.com

  2     content.js           Tab load detected, extracts:
                             • Trackers (Facebook Pixel, Google Analytics, etc.)
                             • IFrame domains
                             • Page title

  3     background.js        ScanService.scan(tabId, url) triggered
                             → Checks cache: MISS
                             → Requests page info from content script
                             → Builds scan request

  4     Extension → Desktop  WebSocket message:
                             {
                               "type": "url_check",
                               "url": "https://example.com",
                               "trackers": [...],
                               "iframes": [...]
                             }

  5     Desktop              ExtensionHandler routes to ScanService
                             → Cache check: MISS
                             → Auth check: VALID

  6     Desktop → Backend    ZMQ REQ to port 50001 (CURVE encrypted):
                             {
                               "AlertType": "UrlAlert",
                               "DeviceInfo": {
                                 "DeviceUid": "PC-eeb83c93e3ccac4b",
                                 "DeviceType": 1,
                                 "OperatingSystem": 1,
                                 "MAC": "00:11:22:33:44:55"
                               },
                               "Token": "289624e9...",
                               "Url": "https://example.com",
                               "Trackers": [...],
                               "IFrameDomains": [...]
                             }

  7     Backend              NetMQAlertIngress receives alert, AlertProcessor routes it
                             → Validates token ✓
                             → Finds device owner (user lookup)
                             → Routes to UDAnalysisManager
                             → Returns: { success: true }

  8     Desktop → Extension  Intermediate result:
                             {
                               "type": "url_result",
                               "url": "https://example.com",
                               "analyzing": true,
                               "message": "Analysis in progress"
                             }
                             Extension shows "Checking..." spinner

  9     Backend              UDAnalysis runs pipeline:
                             a. UDPhishingAnalyzer → check 506K known URLs
                             b. UDUrlAnalyzer:
                                - Whitelist check
                                - Known phishing check
                                - Cache check
                                - Python analyzer (subprocess)

  10    Backend → Python     Subprocess call:
                             python analyze.py "https://example.com" --json

  11    Python Analyzer      Performs analysis:
                             • WHOIS lookup → domain age, registrar
                             • Content scrape → patterns, forms, language
                             • ML classifier → scam probability
                             • Risk assessment → weighted score
                             Returns JSON to stdout

  12    Backend              UDUrlAnalyzer parses Python output
                             → Combines with phishing DB results
                             → Calculates severity
                             → Generates indicators + protective actions
                             → Fires AnalysisResultReceived event

  13    Backend → Desktop    NetMQNotificationEgress (ZMQ PUB, port 50002):
                             Topic: "device:PC-eeb83c93e3ccac4b"
                             {
                               "Type": "AnalysisResult",
                               "DeviceUid": "PC-eeb83c93e3ccac4b",
                               "Data": {
                                 "Severity": "Medium",
                                 "AnalysisResult": { score, riskType, ... },
                                 "ProtectiveActions": [...]
                               }
                             }

  14    Desktop              NotificationHandler receives result
                             → Extracts score, risk type, action
                             → Updates local cache
                             → Broadcasts to all connected Extensions

  15    Desktop → Extension  Final result via WebSocket:
                             {
                               "type": "url_result",
                               "url": "https://example.com",
                               "score": 35,
                               "riskType": [1, 3],
                               "protectiveAction": 2
                             }

  16    Extension            ScanService receives result
                             → Updates state
                             → Caches result (TTL: 1 hour)
                             → ProtectionService executes action

  17    content.js           Protective action 2 = WARN_BANNER
                             → Injects yellow warning banner at top of page:
                             "⚠ Warning: Risk detected. Score: 35/100"

  18    Browser              User sees the warning and can make
                             an informed decision about the site
```

---

## 9. Communication Protocols

### 9.1 Protocol Summary

| Link | Protocol | Port(s) | Encryption | Format |
|------|----------|---------|------------|--------|
| Extension ↔ Desktop | WebSocket | 8080–8484 | None (localhost) | JSON |
| Desktop → Backend | ZMQ REQ → ROUTER | 50001 | CURVE | JSON |
| Backend → Desktop | ZMQ PUB/SUB | 50002 | CURVE | JSON |
| WebApi → Backend | ZMQ REQ/REP | 5556 | None (localhost) | CQRS JSON |
| Backend → Python | Subprocess | — | — | JSON (stdout) |
| Backend → MySQL | TCP | 3306 | None (localhost) | SQL |

### 9.2 WebSocket (Extension ↔ Desktop)

- **Library:** `websockets` (Python async)
- **Multi-client:** Server tracks all connected clients
- **Heartbeat:** Extension sends `heartbeat_ping` every 10s, expects `heartbeat_pong`
- **Keepalive:** Extension sends keepalive every 20s to prevent service worker shutdown
- **Reconnect:** Exponential backoff (1s → 30s max)
- **Queue:** Messages queued during disconnection, flushed on reconnect

### 9.3 ZeroMQ (Desktop ↔ Backend)

- **Library:** `pyzmq` (Python), `NetMQ` (.NET)
- **Port 50001 (alerts):** Desktop uses `RequestSocket` → Backend uses `RouterSocket`. The Backend ACKs immediately ("alert accepted, analysis in progress") and dispatches the heavy work to a background task — so a single Desktop App can send the next alert without waiting. Multi-device concurrency is handled by Router's identity frames.
- **Port 50002 (results):** Backend `PublisherSocket` → Desktop `SubscriberSocket`. Each Desktop subscribes only to its own topic, so devices don't see each other's traffic.
- **Topic format:** `device:{deviceUid}` (e.g., `device:PC-eeb83c93e3ccac4b`)
- **CURVE encryption:** All ZMQ traffic on 50001/50002 is encrypted end-to-end with CurveZMQ (NaCl-based). The localhost CQRS channel on 5556 is **not** CURVE-encrypted (same-host trust boundary).

---

## 10. Security & Authentication

### 10.1 Device Registration Flow

```
First time:
1. Desktop generates hardware-based Device ID
2. Desktop → Backend: RegisterDevice(deviceUid, email)
3. Backend creates UserDevice record
4. Backend → Desktop: { status: "DeviceRegistered", serverPublicKey }

Subsequent connections:
1. Desktop → Backend: RequestToken(deviceUid)
2. Backend validates device exists
3. Backend → Desktop: { status: "TokenCreated", token, expiration, serverPublicKey }
```

### 10.2 Token Management

| Setting | Value |
|---------|-------|
| Token expiration | 1440 minutes (24 hours) |
| Max expiration | 10080 minutes (7 days) |
| Storage | `%APPDATA%\AntiScam\token.json` |
| Refresh | Automatic on `TokenExpired` response |

### 10.3 CURVE Encryption (ZeroMQ)

CurveZMQ provides end-to-end encryption for all ZMQ traffic between Desktop and Backend.

```
Server side (Backend):
  - Static keypair stored in appsettings.json
  - Generated by CurveKeyManager.cs on first run
  - Public key shared with clients during auth

Client side (Desktop):
  - Ephemeral keypair generated per connection
  - Server public key obtained during token request
  - Applied to both REQ and SUB sockets
```

**Key format:** Z85-encoded (ZeroMQ standard)
```
Server Public Key: <generated at runtime — read from curve-server-public-key.txt>
```

---

## 11. Message Reference

### 11.1 Extension ↔ Desktop (WebSocket)

**Extension → Desktop:**

| Type | Payload | Purpose |
|------|---------|---------|
| `url_check` | `{ url, trackers[], iframes[] }` | Scan a URL |
| `ping` | `{}` | Connection check |
| `user_auth` | `{ email }` | Sign in user |
| `user_signout` | `{}` | Sign out user |
| `heartbeat_ping` | `{}` | Dead connection detection |

**Desktop → Extension:**

| Type | Payload | Purpose |
|------|---------|---------|
| `url_result` | `{ url, analyzing, score, riskType[], protectiveAction }` | Scan result |
| `pong` | `{ email }` | Ping response |
| `heartbeat_pong` | `{}` | Heartbeat response |
| `notification` | `{ ... }` | Server notification |
| `remote_access_alert` | `{ ... }` | Remote access warning |

### 11.2 Desktop → Backend (ZMQ REQ)

| MessageType/AlertType | Payload | Response |
|----------------------|---------|----------|
| `RequestToken` | `{ DeviceUid, Email }` | `{ status, token, expiration, serverPublicKey }` |
| `RegisterDevice` | `{ DeviceUid, Email, DeviceInfo }` | `{ status: "DeviceRegistered" }` |
| `RefreshToken` | `{ DeviceUid, OldToken }` | `{ status, token, expiration }` |
| `UrlAlert` | `{ AlertType, DeviceInfo, Token, Url, Trackers, IFrameDomains }` | `{ success: true }` |
| `RemoteAccessAlert` | `{ AlertType, DeviceInfo, Token, ProcessInfo }` | `{ success: true }` |

### 11.3 Backend → Desktop (ZMQ PUB)

**Topic:** `device:{deviceUid}`

```json
{
  "Type": "AnalysisResult",
  "Timestamp": "2026-02-13T15:27:51Z",
  "DeviceUid": "PC-eeb83c93e3ccac4b",
  "Data": {
    "AlertType": "UrlAlert",
    "Severity": "Medium",
    "AnalysisResult": {
      "Url": "https://example.com",
      "Domain": "example.com",
      "risk_assessment": {
        "risk_score": 35,
        "risk_level": "MEDIUM",
        "is_scam": false,
        "confidence": 0.75
      },
      "phishing_check": {
        "Is_known_phishing": false,
        "Is_known_phishing_domain": false
      }
    },
    "Indicators": [...],
    "ProtectiveActions": [
      {
        "ActionType": "UserDisplayNotification",
        "ActionLevel": "Device"
      }
    ]
  }
}
```

---

## 12. Configuration Reference

### 12.1 Backend (`ASPSBackend/appsettings.json`)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "server=127.0.0.1;port=3306;database=ASPSBackend2DB;..."
  },
  "NetMQ": {
    "BusinessEndpoint": "tcp://*:5555",
    "RealTimeListenerPort": 50001,
    "RealTimeListenerMode": "Rep",
    "NotificationPublisherPort": 50002
  },
  "Python": {
    "ExecutablePath": ".../.venv/Scripts/python.exe",
    "AnalyzersFolderPath": ".../basic-url-analyzer/basic-url-analyzer"
  },
  "Analysis": {
    "DeviceAlertExpiryDays": 30,
    "DeviceAlertDeletionDays": 90,
    "CacheEnabled": true
  },
  "TokenManagement": {
    "TokenExpirationPeriod": 1440,
    "MaxExpiration": 10080
  },
  "Security": {
    "CurveEnabled": true
  }
}
```
> `ServerPublicKeyZ85` הוסר — המפתח מנוהל ע"י `CurveKeyManager` בזמן ריצה.

### 12.2 WebApi (`WebApi/appsettings.json`)

```json
{
  "Urls": "http://0.0.0.0:5001",
  "CQRS": {
    "Endpoint": "tcp://localhost:5556"
  },
  "NetMQ": {
    "BusinessEndpoint": "tcp://localhost:5555"
  },
  "Security": {
    "CurveEnabled": true
  }
}
```
> `ServerPublicKeyZ85` הוסר — `DeviceLogin` קורא את המפתח מ-`CurveKeyManager` (DI).
```

### 12.3 Desktop App (`config.py`)

```python
EXTENSION_WS_PORTS = [8080, 8181, 8282, 8383, 8484]
BACKEND_ZMQ_ENDPOINT = "tcp://127.0.0.1:50001"
BACKEND_SUB_ENDPOINT = "tcp://127.0.0.1:50002"
ZMQ_TIMEOUT = 5000  # ms
```

### 12.4 Port Map

| Port | Service | Protocol |
|------|---------|----------|
| 3306 | MySQL | TCP |
| 5001 | WebApi (HTTP) | HTTP |
| 5555 | Backend internal CQRS processor (`NetMQMessageProcessor`) | ZMQ |
| 5556 | Backend CQRS transport (`NetMQCqrsTransport` / `ICqrsTransport`) | ZMQ |
| 7001 | WebApi (HTTPS/Swagger) | HTTPS |
| 8080–8484 | Desktop WebSocket | WS |
| 50001 | Backend alert ingress (`NetMQAlertIngress` / `IAlertIngress`) | ZMQ REP |
| 50002 | Backend notification egress (`NetMQNotificationEgress` / `INotificationEgress`) | ZMQ PUB |

---

## 13. Running the System

### 13.1 Prerequisites

- .NET 8 SDK
- Python 3.14+
- MySQL 8 (with `ASPSBackend2DB` database)
- Chrome browser (for Extension)

### 13.2 Start Order

Components can start in any order, but this is the recommended sequence:

```bash
# 1. MySQL must be running first
#    (usually runs as a system service)

# 2. Start Backend (processes alerts, runs analysis)
cd ASPSBackend14_J/ASPSBackend
dotnet run

# 3. Start WebApi (admin dashboard, optional)
cd ASPSBackend14_J/WebApi
dotnet run

# 4. Start Desktop App (bridges Extension ↔ Backend)
cd apps/desktop/win/src
python main.py

# 5. Load Extension in Chrome
#    chrome://extensions → Developer mode → Load unpacked
#    Select: apps/extension/chrome/
```

### 13.3 Verifying the System

1. **Backend logs should show:**
   ```
   ✓ Real-time alert listener started (tcp://*:50001, Mode: Rep)
   ✓ CQRS Gateway started (tcp://*:5556)
   ✓ UDAnalysisManagers initialized
   ```

2. **WebApi logs should show:**
   ```
   ✓ CQRS Client configured: tcp://localhost:5556
   Now listening on: http://0.0.0.0:5001
   ```

3. **Desktop App should show:**
   ```
   WebSocket server started on port 8080
   Token obtained successfully
   CURVE encryption applied
   Notification listener started
   ```

4. **Extension popup should show:** Connected (green indicator)

5. **Test:** Visit any URL → Extension should show a score after a few seconds

---

## 14. WebApi Admin UI Pages

The WebApi project (§6) hosts a Razor-based admin dashboard at `http://localhost:5001`. All pages live under `WebApi/Pages/` and are protected by `AuthorizeFolder("/", "AdminPolicy")` (requires `Admin` role from Keycloak). Public pages: `/Login`, `/Logout`, `/DeviceLogin`, `/AccessDenied`.

| Page | Path | Purpose |
|------|------|---------|
| Dashboard | `/` | Live stats (users / devices / alerts) via SignalR |
| Users | `/Users` | List, view, manage user accounts |
| Devices | `/Devices` | Registered devices (per user, per OS) |
| DeviceAlerts | `/DeviceAlerts` | Recent alerts feed (paged, filter by user/type) |
| AnalysisResults | `/AnalysisResults` | Browse stored analysis JSON, drill-down per alert |
| KnownPhishingWebsites | `/KnownPhishingWebsites` | View / search the ~500K phishing-DB rows |
| BankWebsites | `/BankWebsites` | CRUD for legitimate-bank whitelist (JIRA: ASPS-297) |
| BlacklistedPhoneNumbers | `/BlacklistedPhoneNumbers` | CRUD for scam phone numbers (JIRA: ASPS-282) |
| WebsiteCategories | `/WebsiteCategories` | Hierarchical category tree (banking / crypto / healthcare …) (JIRA: SCRUM-820/822) |
| TrackedDomains | `/TrackedDomains` | Long-duration tracking domains; tied to TrackUrlAlert |
| Simulations | `/Simulations` | Author and run simulated alert scenarios for QA |
| Roadmaps | `/Roadmaps` | Product roadmap editor (see §15) |
| SystemConfigurations | `/SystemConfigurations` | Runtime knobs (toggle features, tune thresholds) |
| DebugClaims | `/DebugClaims` | Show current user's claims (debug-only) |
| Downloads | `/Downloads` | Bundle / desktop-installer downloads |

All admin pages share a single layout (`Pages/Shared/_Layout.cshtml`) with sidebar navigation grouped into sections: **Operations**, **Blacklists**, **Planning**, **Testing**, **System**.

Every page communicates with the Backend via `ICQRSClient.SendQueryAsync<T>()` / `SendCommandAsync<T>()` (bridged to `ICqrsClient`/`NetMQCqrsClient` by `NetMQCqrsClientAdapter`), which transports JSON over NetMQ to the Backend's `NetMQCqrsTransport` on port 5556, where `CqrsHandlerRegistry` dispatches to the registered handler. **The WebApi never touches MySQL directly**.

---

## 15. Roadmap Module

**Location:** `ASPSBackend14_J/WebApi/Pages/Roadmaps/` and supporting types in `Common/Entities/Roadmap.cs`, `Business/{Queries,Commands,Handlers}/Roadmap*.cs`.

### 15.1 Purpose

A multi-project roadmap editor used internally by the team. Each roadmap stores its full state (slides, categories, items, drag order, JIRA links) as a **JSON blob** in a single MySQL row. The blob format is identical to the standalone HTML viewer at `docs/roadmap-presentation-editable.html`, so the same SPA code works in both places.

### 15.2 Storage — `Roadmaps` table

| Column | Type | Notes |
|--------|------|-------|
| `Key` | INT PK | Auto-increment id |
| `Name` | VARCHAR(100) | Display name |
| `Description` | VARCHAR(500) NULL | Short description |
| `Data` | LONGTEXT | JSON: `{ items, categories, slides, exportedAt }` |
| `Version` | INT | Optimistic-concurrency token (incremented on each save) |
| `DateCreated` | DATETIME | Set once on insert |
| `LastUpdatedAt` | DATETIME NULL | Updated on every save |
| `CreatedBy` / `LastUpdatedBy` | VARCHAR(255) NULL | `User.Identity.Name` (Keycloak `preferred_username`) |
| `IsArchived` | BOOL | Soft delete |

EF migration: `Business/Migrations/20260428192432_AddRoadmapsTable.cs`.

### 15.3 CQRS surface

**Queries** (`Business/Queries/RoadmapQueries.cs`):
- `GetRoadmapByIdQuery` → `GetRoadmapByIdQueryResult` (full data blob + metadata)
- `ListRoadmapsQuery` → `ListRoadmapsQueryResult` (id/name/version/lastUpdated for index page)

**Commands** (`Business/Commands/RoadmapCommands.cs`):
- `CreateRoadmapCommand` → returns new id
- `SaveRoadmapCommand` → optimistic-concurrency check on `ExpectedVersion`; returns `ConcurrencyConflict=true` + new server version on stale write
- `UpdateRoadmapMetadataCommand` → name/description without touching the blob
- `ArchiveRoadmapCommand` → sets `IsArchived=true`

**Handlers** registered in [`CqrsQueryRegistration.cs`](ASPSBackend14_J/Business/Messaging/CqrsQueryRegistration.cs) / [`MessagingServiceRegistration.cs`](ASPSBackend14_J/Business/Messaging/MessagingServiceRegistration.cs) against `CqrsHandlerRegistry`, dispatched at runtime by [`NetMQCqrsTransport`](ASPSBackend14_J/Business/Messaging/Transport/NetMQ/NetMQCqrsTransport.cs).

### 15.4 Razor pages

- **`/Roadmaps`** — list, create, archive
- **`/Roadmaps/Edit/{id}`** — the SPA editor; injects `window.RoadmapAdmin = { initial, save, markDirty, getCurrentData }` and the SPA bundle (`/css/roadmap-spa.css`, `/js/roadmap-spa.js`) reads/writes through it
- **`/Roadmaps/Edit/{id}?handler=Viewer`** — generates a self-contained `roadmap-{name}-{date}.html` (CSS+JS+JSON inlined; Heebo font via Google CDN) so non-admins can view offline

### 15.5 Save flow

```
User edit (drag/click/type) in SPA
   → state mutated → save() called → debounced 800ms
   → fetch POST /Roadmaps/Edit/{id}?handler=Save  body: {Id, ExpectedVersion, Data}
   → OnPostSaveAsync → SaveRoadmapCommand → RoadmapRepository.UpdateAsync
       (compare-and-swap on Version; bump on success)
   → JsonResult { success, newVersion, lastUpdatedAt, lastUpdatedBy, concurrencyConflict }
   → SPA shows "✓ נשמר" badge, updates header timestamp
```

Concurrency conflict UX: alert with "GitHub-has-newer / your local is older" prompt, offering reload.

---

## 16. Mobile Agents — Specification (to-be-built)

There is **no Android or iOS code in the repo today**. The Backend, however, is already mobile-aware: `DeviceType.MobilePhone = 2`, `OperatingSystemType.Android = 4`, `OperatingSystemType.IOS = 5` ([`Common/Enums/Enumerations.cs`](ASPSBackend14_J/Common/Enums/Enumerations.cs)). Tokens, alerts, notifications, and CURVE auth all work the same regardless of OS.

This section is the **target spec** for the mobile agents — their role mirrors the existing Desktop App: bridge between on-device monitoring and the Backend.

### 16.1 Scope

Each mobile agent (Android + iOS) must:

1. Generate a stable device id (Android: SSAID + hardware fingerprint; iOS: `identifierForVendor` + Keychain-stored UUID).
2. Authenticate against the Backend (`RegisterDevice` / `RequestToken`) over CURVE-encrypted ZMQ — same protocol as Desktop §10.
3. Monitor user activity, send `UrlAlert` / `RemoteAccessAlert` / `TrackUrlAlert` / SMS-scan / Email-scan / Phone-scan signals.
4. Subscribe to `device:{deviceUid}` PUB topic and execute the returned `ProtectiveAction` items in the OS-appropriate way.

### 16.2 Stack choices

| Concern | Android | iOS |
|---------|---------|-----|
| Language | Kotlin (Compose for UI) | Swift (SwiftUI) |
| ZMQ | `jeromq` (pure-Java NetMQ) | `SwiftyZeroMQ` or vendored `libzmq` |
| CURVE crypto | `libsodium-jni` | `Sodium-iOS` (libsodium) |
| URL hooks | Custom IME ✗ — use **Accessibility Service** to read URL bar of system browsers; intercept `VIEW` intents to flag links before app opens | **Network Extension** (Content Filter Provider) — requires `NEFilterControlProvider` + `NEFilterDataProvider`. Apple-only path; needs MDM or sideload for full filtering. App-extension fallback: Share Extension to manually scan a link |
| SMS scan | `BroadcastReceiver` on `SMS_RECEIVED_ACTION` (with `READ_SMS` perm) | Not allowed — iOS does **not** expose SMS to apps. Use Message Filter Extension (`ILMessageFilterExtension`) for **on-device** spam classification only |
| Email scan | Background sync via OAuth (Gmail / Outlook APIs) | Same — both via the user's mail-provider OAuth, not OS hooks |
| Phone-call scan | `CallScreeningService` (API 24+) for incoming-call number lookup against `BlacklistedPhoneNumbers` | `Call Directory Extension` — submit blocklist to OS before calls arrive |
| Remote-access detect | `AccessibilityService` flags packages like `com.anydesk`, `com.teamviewer.teamviewer.market.mobile`; combined with `UsageStatsManager` | iOS prevents this — **no API**. Best we can do: detect installation via `LSApplicationWorkspace` (private) — not App-Store-safe. Document as *not supported on iOS* |
| Background lifecycle | `Foreground Service` (notification) keeps ZMQ socket alive; `JobScheduler` for retries | `BGTaskScheduler` (iOS 13+); ZMQ socket only alive while app is foreground (push-triggered re-connect) |
| Notifications | `Notification Channel` per severity | `UNUserNotificationCenter`, `UNNotificationCategory` per severity |

### 16.3 Permissions

**Android (`AndroidManifest.xml`):**
- `INTERNET`, `ACCESS_NETWORK_STATE`
- `RECEIVE_SMS`, `READ_SMS` (only if SMS scan is enabled)
- `BIND_ACCESSIBILITY_SERVICE` (URL + remote-access detection)
- `BIND_CALL_REDIRECTION_SERVICE` (call screening)
- `FOREGROUND_SERVICE`
- `READ_PHONE_STATE` (optional — for call-state)
- `POST_NOTIFICATIONS` (Android 13+)

**iOS (`Info.plist`):**
- `NSUserTrackingUsageDescription` (where applicable)
- Network Extension entitlements (requires Apple approval)
- `com.apple.developer.networking.networkextension` (`content-filter-provider`, `app-proxy-provider`)
- `com.apple.developer.usernotifications.communication`

### 16.4 Project layout (proposed)

```
apps/
  android/
    app/
      src/main/java/io/asps/agent/
        MainActivity.kt
        services/
          ZmqClient.kt              ← jeromq + curve handshake
          MonitoringService.kt      ← foreground service hub
          AccessibilityHook.kt      ← URL extraction
          SmsObserver.kt
          CallScreeningService.kt
        ui/                         ← Compose screens (auth, settings, history)
        data/
          TokenStore.kt             ← EncryptedSharedPreferences
          LocalCache.kt             ← Room
      build.gradle.kts
    settings.gradle.kts

  ios/
    AspsAgent/
      AspsAgent.xcodeproj
      Sources/
        ZmqClient.swift
        Networking/
          AlertSender.swift
          NotificationListener.swift
        Extensions/
          ContentFilter/            ← NEFilterDataProvider
          MessageFilter/            ← ILMessageFilterExtension
          CallDirectory/            ← CXCallDirectoryProvider
        UI/                         ← SwiftUI screens
        Storage/
          KeychainTokenStore.swift
          CoreDataCache.xcdatamodeld
```

### 16.5 Wire-protocol parity with Desktop

Mobile agents **must use the exact same JSON payloads** as §11. The only field that changes is `DeviceInfo`:

```json
{
  "DeviceInfo": {
    "DeviceUid": "ANDROID-7c9f...",
    "DeviceType": 2,            ← MobilePhone
    "OperatingSystem": 4,       ← Android (or 5 for iOS)
    "MAC": "<best-effort, may be empty on Android 11+>",
    "AppVersion": "1.0.0"
  }
}
```

Token, CURVE handshake, alert types, notification topic format — **identical to Desktop**.

### 16.6 Build & sprint plan (rough)

| Sprint | Android | iOS |
|--------|---------|-----|
| 1 | Project scaffold, Compose login screen, ZMQ-CURVE client, RegisterDevice/RequestToken flow | Project scaffold, SwiftUI login, ZMQ-CURVE client, RegisterDevice/RequestToken flow |
| 2 | Foreground service + notification listener (port 50002) | App-extension scaffold (Content Filter, Message Filter, Call Directory) |
| 3 | Accessibility service for URL extraction → `UrlAlert` | Content Filter Provider sending `UrlAlert` |
| 4 | SMS observer + on-device classifier | Message Filter Extension + classifier |
| 5 | Call screening service + Blacklisted-Phone lookup | Call Directory Extension; submit blocklist |
| 6 | Remote-access app detection | (Skipped — not supported on iOS, document instead) |
| 7 | Hardening, battery profiling, beta release | App Store / TestFlight beta |

### 16.7 Open questions

- **iOS distribution:** App-Store policy is hostile to URL-filter apps that aren't built on `Network Extension` with explicit user-installed VPN profile. Decide: Network Extension app vs. enterprise-MDM-only build vs. partner with a MAM provider.
- **SMS scanning on iOS:** No on-device read access. Either accept "no SMS scanning on iOS", or build a separate user-driven flow ("paste a suspicious SMS to check").
- **Battery on Android:** Foreground service + `AccessibilityService` is power-hungry. Need a measured budget; might gate features behind a "balanced / aggressive" toggle.
- **Push channel:** ZMQ SUB over a long-lived connection drains battery. Consider FCM/APNs as a wake-up signal that triggers a short-lived ZMQ poll, instead of always-on SUB.

---

*Last updated: 2026-04-29 — drift fixes (ROUTER socket, ProtectiveAction enum, RiskType model, expanded DB schema), added §14 (WebApi UI Pages catalog), §15 (Roadmap module), §16 (Mobile agents target spec).*
