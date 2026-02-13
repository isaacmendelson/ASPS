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
│  │ RealTimeAlertListener       │  │ NotificationPublisher │    │
│  │ ZMQ REP on port 50001       │  │ ZMQ PUB on port 50002 │    │
│  │ • token validation          │  │ • per-device topics    │    │
│  │ • device registration       │  │ • analysis results     │    │
│  │ • alert routing             │  │                        │    │
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
│  │ TokenStore          │ CQRSGateway (port 5556)            │   │
│  │ • issue tokens      │ • WebApi ↔ Backend bridge          │   │
│  │ • validate tokens   │ • commands & queries                │   │
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
│  │ CQRSClient (NetMQ REQ → Backend port 5556)              │   │
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

| Value | Name | Behavior |
|-------|------|----------|
| 0 | NONE | No action taken |
| 1 | NOTIFY | Silent notification |
| 2 | WARN_BANNER | Yellow banner at top of page |
| 3 | WARN_MODAL | Modal dialog overlay |
| 4 | BLOCK | Full page block |

### 3.7 Risk Types

| Value | Name |
|-------|------|
| 0 | SAFE |
| 1 | PHISHING |
| 2 | CLOAKING |
| 3 | IMPERSONATION |
| 4 | FAKE_DOMAIN |
| 5 | UNKNOWN |

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

Listens on ZMQ SUB (port 50002) for analysis results pushed by the Backend.

```
1. Receive notification on topic "device:{deviceUid}"
2. Parse analysis result
3. Extract: score, riskType, protectiveAction
4. Update local cache
5. Broadcast to all connected Extensions via WebSocket
6. On failure: retry once, then log error and raise
```

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

---

## 5. Backend

**Location:** `ASPSBackend14_J/ASPSBackend/` and `ASPSBackend14_J/Business/`
**Language:** .NET 8 / C#

### 5.1 Role

The Backend is the **brain** of the system. It handles authentication, alert processing, URL analysis (including calling the Python analyzer), phishing database lookups, and result distribution.

### 5.2 Services Started on Boot

| Service | Port | Protocol | Purpose |
|---------|------|----------|---------|
| NetMQMessageProcessor | 5555 | ZMQ | Legacy CQRS processor |
| RealTimeAlertListener | 50001 | ZMQ REP | Receives alerts from Desktop |
| NotificationPublisher | 50002 | ZMQ PUB | Pushes results to Desktop |
| CQRSGateway | 5556 | ZMQ | WebApi ↔ Backend bridge |
| ASView | — | In-memory | Read model (CQRS) |
| TokenStore | — | In-memory | Token issuance & validation |
| UDAnalysisManagers | — | Per-user | Analysis orchestration |

### 5.3 Alert Listener (`RealTimeAlertListener.cs`)

Receives all messages from Desktop Apps on port 50001.

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
| `Users` | User accounts | Key, Email, FirstName, LastName |
| `UserDevices` | Registered devices | DeviceUid, UserKey, DeviceType, MAC |
| `DeviceAlerts` | Incoming alerts | AlertType, Url, DeviceUid, Token, Priority |
| `AnalysisResults` | Analysis output | DeviceAlertKey, JsonValue, Severity, HasError |
| `KnownPhishingWebsites` | Phishing DB | Url, Domain, Source (506K+ records) |
| `SafeDomains` | Whitelisted domains | Domain |

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
    └── CQRSClient ──── NetMQ REQ ──── Backend:5556
         (commands & queries)
```

### 6.3 Endpoints

| URL | Type | Purpose |
|-----|------|---------|
| `http://localhost:5001` | Razor Pages | Admin Dashboard |
| `https://localhost:7001/swagger` | Swagger UI | API Documentation |
| `/notificationshub` | SignalR | Real-time notifications |

### 6.4 CQRS Pattern

```
WebApi sends a Command/Query to Backend:
  → CQRSClient.SendAsync(message) via NetMQ REQ to port 5556
  ← Backend's CQRSGateway processes and responds
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

  7     Backend              RealTimeAlertListener receives alert
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

  13    Backend → Desktop    NotificationPublisher (ZMQ PUB, port 50002):
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
| Desktop → Backend | ZMQ REQ/REP | 50001 | CURVE | JSON |
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
- **REQ/REP (port 50001):** Desktop sends alert, Backend responds immediately
- **PUB/SUB (port 50002):** Backend publishes results, Desktop subscribes by device ID
- **Topic format:** `device:{deviceUid}` (e.g., `device:PC-eeb83c93e3ccac4b`)
- **CURVE encryption:** All ZMQ traffic encrypted with CurveZMQ (NaCl-based)

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
Server Public Key: qPsk#8DY:n9ovp[vQ!YcOnOX[f/.i@.g^f#b:!ik
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
    "CurveEnabled": true,
    "ServerPublicKeyZ85": "qPsk#8DY:n9ovp[vQ!YcOnOX[f/.i@.g^f#b:!ik"
  }
}
```

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
    "CurveEnabled": true,
    "ServerPublicKeyZ85": "qPsk#8DY:n9ovp[vQ!YcOnOX[f/.i@.g^f#b:!ik"
  }
}
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
| 5555 | Backend CQRS Processor | ZMQ |
| 5556 | Backend CQRS Gateway | ZMQ |
| 7001 | WebApi (HTTPS/Swagger) | HTTPS |
| 8080–8484 | Desktop WebSocket | WS |
| 50001 | Backend Alert Listener | ZMQ REP |
| 50002 | Backend Notification Publisher | ZMQ PUB |

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

*Last updated: 2026-02-13*
