# Feature Landscape: Anti-Phishing Score Flow Pipeline

**Domain:** ASPS URL analysis score flow -- end-to-end from Extension to Backend and back
**Researched:** 2026-02-12
**Confidence:** HIGH (derived from direct codebase analysis of all three applications)

## Executive Summary

The ASPS score flow is a 7-stage pipeline spanning three applications (Chrome Extension, Python Desktop App, C# Backend). The flow operates in two modes: synchronous (REQ/REP returns immediate score) and asynchronous (REQ/REP returns acknowledgment, PUB/SUB delivers score later). The system used to work; the score stopped reaching the extension. This document maps every stage, its expected behavior, health indicators, failure modes, and the exact message formats at each boundary.

---

## Pipeline Overview

```
STAGE 1          STAGE 2          STAGE 3          STAGE 4
Extension     -> Desktop App   -> Backend REQ    -> Backend Analysis
(WebSocket)      (WS Handler)     (ZMQ REQ/REP)    (Indicators+Score)

STAGE 7          STAGE 6          STAGE 5
Extension     <- Desktop App   <- Backend PUB
(popup.js)       (Notify Hndlr)   (ZMQ PUB/SUB)
```

**Two response paths exist:**
- **Path A (Sync):** Backend returns Score/RiskType/ProtectiveAction in the REQ/REP response. Desktop App immediately returns score to Extension.
- **Path B (Async):** Backend returns `{success: true}` in REQ/REP. Analysis runs asynchronously. Result published via PUB/SUB. Desktop App receives notification, broadcasts to Extension.

---

## Table Stakes

Features that MUST work for the system to be functional.

| # | Feature | Why Required | Current Code Location |
|---|---------|-------------|----------------------|
| 1 | Extension connects to Desktop App via WebSocket | Without this, no messages flow at all | `ConnectionService.js` -> `extension_server.py` |
| 2 | Extension sends `url_check` message with URL, trackers, iframes | Desktop App needs the URL to analyze | `ScanService.js:scan()` -> `extension_handler.py:_handle_url_check()` |
| 3 | Desktop App forwards URL to Backend via ZMQ REQ | Backend cannot analyze without receiving the alert | `scan_service.py:check_url()` -> `zmq_client.py:send_url_alert()` |
| 4 | Backend receives alert, runs analysis, returns response | Core value: risk assessment | `RealTimeAlertListener.cs:ProcessAlertAsync()` |
| 5 | Desktop App processes response and returns `url_result` to Extension | Extension needs the score | `scan_service.py:_process_response()` -> `extension_server.py:broadcast()` |
| 6 | Desktop App receives PUB/SUB notification with analysis result | Async path: final score delivery | `notification_client.py:_handle_notification()` -> `notification_handler.py:handle()` |
| 7 | Extension receives `url_result` and updates popup UI | User sees the score | `background.js:handleUrlResult()` -> `popup.js:RiskDisplay.update()` |
| 8 | Score persisted to `chrome.storage.local` per-tab and globally | Popup reads from storage, not directly from WebSocket | `ScanService.js:handleResult()` sets `currentPageScore` |
| 9 | Loading animation starts before scan and stops on result | User knows scanning is happening | `background.js:startLoadingState()/stopLoadingState()` |

---

## Stage-by-Stage Specification

### STAGE 1: Extension Detects URL and Sends to Desktop App

**Trigger:** `chrome.tabs.onUpdated` (status=complete), `chrome.webNavigation.onCompleted`, or manual scan button.

**Expected behavior:**
1. `background.js:triggerScan()` fires
2. Calls `scanService.scan(tabId, url)`
3. ScanService checks local cache (`CacheService.get(url)`)
4. If cache miss: gathers page info (trackers, iframes) from content script
5. Sends WebSocket message to Desktop App

**Message format (Extension -> Desktop App):**
```json
{
  "type": "url_check",
  "url": "https://example.com/page",
  "trackers": [{"Type": "fbPixel", "Value": "12345"}],
  "iframes": ["ads.example.com"]
}
```

**Health indicators:**
- Console: `[ScanService] Scanning: example.com`
- Console: `[ConnectionService] Sending: url_check`
- Desktop App stdout: `<<< RECEIVED FROM EXTENSION [url_check]`

**Failure modes:**
| Failure | Symptom | Root Cause |
|---------|---------|-----------|
| WebSocket not connected | `[ScanService] Not connected to desktop app` in extension console | Desktop App not running, port mismatch, firewall |
| Message queued, never sent | `[ConnectionService] Disconnected - queueing: url_check` | WebSocket closed; queue flushed only on reconnect |
| Non-http URL skipped | No scan triggered | URL starts with `chrome://`, `about:`, `file://` |
| Duplicate scan suppressed | `[Background] Skipping duplicate scan for: ...` | Same URL scanned twice for same tab |
| Content script not ready | `[ScanService] Content script not ready` | Page hasn't loaded content script yet; trackers/iframes empty |
| 30-second timeout fires | Icon turns gray, `currentPageScore` set to null | No response received in time; timer in `background.js:triggerScan()` |

**Timeout/retry:**
- Scan timeout: 30 seconds (`ScanService.scanTimeout = 30000`)
- On timeout: icon goes gray, `currentPageScanning` set to false
- No automatic retry of the scan itself

---

### STAGE 2: Desktop App Receives and Routes to ScanService

**Trigger:** WebSocket message received by `ExtensionServer._handle_client()`

**Expected behavior:**
1. ExtensionServer parses JSON, identifies type `url_check`
2. Calls `_on_message_callback(data)` which is `AntiScamApp._handle_extension_message()`
3. Routes to `ExtensionHandler.handle_message(data)`
4. ExtensionHandler calls `scan_service.check_url(url, trackers, iframes)`

**Message flow:**
```
ExtensionServer._handle_client()
  -> AntiScamApp._handle_extension_message()
    -> ExtensionHandler.handle_message()
      -> ExtensionHandler._handle_url_check()
        -> ScanService.check_url()
```

**Health indicators:**
- Stdout: `[EXTENSION] Received: url_check`
- Stdout: `[SCAN] URL CHECK REQUEST`
- Stdout: `[SCAN] Step 1: Checking cache...`

**Failure modes:**
| Failure | Symptom | Root Cause |
|---------|---------|-----------|
| No extension clients | No messages received | Extension didn't connect; check `self.clients` set is empty |
| JSON decode error | `Invalid JSON from extension` in logs | Malformed message from extension |
| Unknown message type | Returns `{type: error, message: Unknown message type}` | Extension sent wrong `type` field |
| Callback not set | No routing occurs, response is `None`, nothing sent back | `_on_message_callback` is None; `on_message()` not called during setup |
| Exception in handler | `Error handling extension message` logged, continues | Any exception in the handler chain |

**Timeout/retry:**
- No explicit timeout at this stage
- WebSocket message handling is per-message; one bad message doesn't break others

---

### STAGE 3: Desktop App Sends UrlAlert to Backend via ZMQ REQ

**Trigger:** `ScanService.check_url()` calls `zmq_client.send_url_alert()`

**Expected behavior:**
1. ScanService checks cache (CacheManager) - if hit, returns cached result immediately
2. ScanService checks auth (`auth_manager.is_valid()` - always returns True in ZMQ mode)
3. ScanService calls `zmq_client.send_url_alert(device_uid, url, token, trackers, iframes)`
4. ZMQClient creates a new connection for each request (`connect()` -> `send_alert()` -> `close()`)
5. ZMQClient sends JSON alert, waits for response with 5-second timeout
6. ScanService tracks URL as pending via `ScanService.set_pending_url(url)`

**Message format (Desktop App -> Backend via ZMQ REQ):**
```json
{
  "AlertType": "UrlAlert",
  "DeviceInfo": {
    "DeviceUid": "PC-JOHN-001",
    "DeviceType": 1,
    "OperatingSystem": 1,
    "MAC": "00:11:22:33:44:55"
  },
  "Timestamp": "2026-02-12T10:30:00Z",
  "Priority": 1,
  "Token": "12345678-1234-1234-1234-123456789012",
  "Url": "https://example.com/page",
  "Trackers": [],
  "IFrameDomains": [],
  "UserAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
}
```

**Note:** Token defaults to a fixed UUID `"12345678-1234-1234-1234-123456789012"` when no auth token is available. The backend validates tokens via `TokenStore.ValidateToken()`.

**Health indicators:**
- Stdout: `[ZMQ] SENDING URL ALERT`
- Stdout: `[ZMQ] SUCCESS: Connected to tcp://127.0.0.1:50001`
- Stdout: `[ZMQ] SUCCESS: Alert sent!`
- Stdout: `[ZMQ] RECEIVED: Response received:`

**Failure modes:**
| Failure | Symptom | Root Cause |
|---------|---------|-----------|
| Backend unreachable | `[ZMQ] ERROR: Connection failed:` | Backend not running, wrong host/port, network issue |
| ZMQ timeout (5s) | `[ZMQ] WARNING: Timeout: No response after 5000ms` | Backend crashed, overloaded, or network partition |
| Token rejected | Backend returns `{status: "InvalidToken"}` or `{status: "TokenExpired"}` | Fixed UUID not registered in backend TokenStore |
| Device not found | Backend returns `{success: false, message: "DeviceNotFound"}` | DeviceUid not registered in backend database |
| User not found | Backend returns `{success: false, message: "UserNotFound"}` | Device exists but no associated user |
| ZMQ socket already in use | Exception on `connect()` | Previous request didn't close cleanly (ZMQ REQ/REP state machine broken) |
| `send_url_alert` returns None | `_process_response` receives None -> returns error | Any of the above failures |

**Critical implementation detail:** `zmq_client.send_url_alert()` creates a NEW connection for every request (`connect() -> send -> close()` in try/finally). This means every URL check opens and closes a ZMQ socket. This is intentional to avoid ZMQ REQ/REP state machine corruption.

**Timeout/retry:**
- ZMQ receive timeout: 5000ms (`self.timeout = 5000`)
- No retry logic - single attempt per request
- No circuit breaker

---

### STAGE 4: Backend Receives Alert and Runs Analysis

**Trigger:** `RealTimeAlertListener.ListenForAlerts()` receives ZMQ frame

**Expected behavior:**
1. Backend deserializes JSON as `UrlAlert`
2. Validates token via `TokenStore.ValidateToken(deviceUid, token)`
3. Looks up device via `ASView.FindUserDeviceByDeviceUid(deviceUid)`
4. Looks up user via `ASView.FindUserByKey(userDevice.UserKey)`
5. Creates `DeviceAlertReceived` domain event
6. Routes to `UDAnalysisManager` which runs analysis pipeline:
   - KnownPhishingIndicator (blacklist check)
   - WhoisDomainAgeIndicator
   - WhoisCountryIndicator
   - MlAnalysisIndicator (external Python analyzer)
   - ContentAnalysisIndicator
   - DomainBlacklistedIndicator
   - And others
7. Returns immediate ACK response via REQ/REP
8. Publishes analysis result notification via PUB/SUB asynchronously

**Response format (Backend -> Desktop App via ZMQ REP) - Async mode (current):**
```json
{
  "success": true,
  "message": "Alert processed successfully",
  "alertType": "UrlAlert",
  "deviceUid": "PC-JOHN-001",
  "timestamp": "2026-02-12T10:30:01Z",
  "priority": "Medium"
}
```

**Response format (Backend -> Desktop App via ZMQ REP) - Sync mode (legacy):**
```json
{
  "Score": 85,
  "RiskType": [0],
  "ProtectiveAction": 0,
  "HasError": false
}
```

**Response format (Backend -> Desktop App via ZMQ REP) - Error:**
```json
{
  "success": false,
  "message": "DeviceNotFound",
  "error": "Device not found: PC-JOHN-001"
}
```

**Health indicators:**
- Backend log: `Received message: {json}`
- Backend log: `Alert from device PC-JOHN-001 associated with user {userKey}`
- Backend log: `Alert routed to UDAnalysisManager for user: {userKey}`
- Backend log: `Device alert processed: PC-JOHN-001, Type: UrlAlert`

**Failure modes:**
| Failure | Symptom | Root Cause |
|---------|---------|-----------|
| Invalid JSON | Returns `{success: false, message: "Invalid JSON"}` | Malformed request |
| Unknown AlertType | Returns `{success: false, message: "Unknown alert type"}` | Missing or wrong `AlertType` field |
| Deserialization failed | Returns `{success: false, message: "Failed to deserialize alert"}` | JSON structure doesn't match expected model |
| Invalid token | Returns `{status: "InvalidToken"}` | Token not in TokenStore |
| Expired token | Returns `{status: "TokenExpired"}` | Token past expiration |
| Device not found | Throws `DomainException("DeviceNotFound")` | DeviceUid not in database |
| User not found | Throws `DomainException("UserNotFound")` | No user for device |
| No UDAnalysisManager | `No UDAnalysisManager found for device` logged; analysis doesn't run | UserDomainManagerService can't find or create manager |
| Analysis pipeline crash | Exception caught, logged; notification may never publish | Bug in any indicator |
| External analyzer timeout | ML indicator times out | Python analyzer unreachable |

---

### STAGE 5: Backend Publishes Result via ZMQ PUB/SUB

**Trigger:** Analysis completes in `UDAnalysisManager`, calls `NotificationPublisher.PublishAnalysisResult()`

**Expected behavior:**
1. NotificationPublisher creates notification envelope
2. Publishes to topic `device:{deviceUid}` as multipart: [topic_bytes, json_bytes]
3. May also publish to `user:{userKeyField}`

**Message format (Backend PUB -> Desktop App SUB):**
```
Frame 1: "device:PC-JOHN-001" (topic)
Frame 2: (JSON notification)
```

**Notification JSON structure:**
```json
{
  "Type": "AnalysisResult",
  "Timestamp": "2026-02-12T10:30:02Z",
  "DeviceUid": "PC-JOHN-001",
  "Data": {
    "AlertType": "UrlAnalysisComplete",
    "Severity": "Medium",
    "RiskAssessment": {
      "risk_score": 25,
      "risk_level": "High",
      "is_scam": true,
      "confidence": 0.85
    },
    "AnalysisResult": {
      "TypeName": "UrlAnalysisResult",
      "Url": "https://example.com/page",
      "Domain": "example.com",
      "analysis_time_ms": 1500,
      "IsFromCache": false,
      "risk_assessment": {
        "risk_score": 25,
        "risk_level": "High",
        "is_scam": true,
        "confidence": 0.85
      },
      "phishing_check": {
        "Is_known_phishing": true,
        "Source": "blacklist"
      },
      "Recommendation": "Block this URL"
    },
    "ProtectiveActions": [
      {
        "ActionType": 4,
        "Subject": 0,
        "Message": "Blocking dangerous URL",
        "Level": 2
      }
    ],
    "Indicators": [
      {
        "IndicatorType": "KnownPhishing",
        "Value": "true",
        "Level": "Critical",
        "Confidence": "High"
      }
    ]
  }
}
```

**Health indicators:**
- Backend log: `Published notification to topic 'device:PC-JOHN-001'`
- Desktop App stdout: `NOTIFICATION RECEIVED` banner
- Desktop App stdout: `Topic: device:PC-JOHN-001`

**Failure modes:**
| Failure | Symptom | Root Cause |
|---------|---------|-----------|
| NotificationPublisher not running | `NotificationPublisher is not running` logged; no publish | Publisher crashed or stopped |
| Both deviceUid and userKey empty | `Both deviceUid and userKeyField are null/empty, skipping` | Alert didn't have device info |
| Serialization error | `Error publishing notification` logged | Polymorphic type serialization failure (common with .NET TypeNameHandling) |
| No subscribers | Message published but nobody receives it | Desktop SUB socket not connected, wrong topic, timing issue |
| Topic mismatch | SUB subscribes to `device:PC-JOHN-001` but PUB sends to different device ID | DeviceUid inconsistency |
| Analysis never completes | No notification published | Analysis pipeline stalled or crashed silently |

**Timeout/retry:**
- PUB/SUB is fire-and-forget: no delivery guarantee
- If SUB is not connected when PUB sends, the message is LOST
- No message persistence or replay

---

### STAGE 6: Desktop App Receives Notification and Broadcasts to Extension

**Trigger:** `NotificationClient._listen()` receives multipart ZMQ message on SUB socket

**Expected behavior:**
1. NotificationClient receives [topic, message] frames
2. Parses JSON, calls `_on_notification_callback(notification)`
3. Callback is `NotificationHandler.handle(notification)`
4. NotificationHandler extracts analysis result (URL, risk_score, risk_assessment)
5. NotificationHandler updates cache via `CacheManager.set()`
6. NotificationHandler broadcasts to Extension via `ExtensionServer.broadcast(result_message)`

**Message format (Desktop App -> Extension via WebSocket broadcast):**
```json
{
  "type": "url_result",
  "url": "https://example.com/page",
  "score": 25,
  "riskType": ["Scam", "Phishing", "High"],
  "protectiveAction": 4,
  "fromCache": false
}
```

**Health indicators:**
- Stdout: `[NOTIFICATION] RECEIVED FROM SERVER!`
- Stdout: `[NOTIFICATION] Risk Score: 25`
- Stdout: `[NOTIFICATION] Updating cache: score=25`
- Stdout: `[NOTIFICATION] Broadcasted result to extension: score=25`

**Failure modes:**
| Failure | Symptom | Root Cause |
|---------|---------|-----------|
| SUB not connected | HEARTBEAT messages every 2 min but no notifications | SUB socket connected but subscribed to wrong topic, or PUB not publishing |
| Non-JSON notification | `[NOTIFY] WARNING: Non-JSON notification` | Backend sent malformed data |
| URL not in notification | `[NOTIFICATION] URL (from pending scan): ...` with fallback | Backend didn't include URL in AnalysisResult; falls back to pending URL |
| No risk_score | Cache update skipped, no broadcast | AnalysisResult missing `risk_assessment.risk_score` |
| Extension server has no clients | `broadcast()` returns without sending | Extension disconnected before notification arrived |
| asyncio event loop issue | `RuntimeError: No running event loop` caught, falls back to `asyncio.run()` | NotificationHandler runs on background thread, not on the main async loop |
| Pending URL mismatch | Wrong URL matched to notification | Multiple URLs scanned; most recent pending URL used as fallback |
| risk_score is 0 (falsy) | `if not risk_score:` triggers fallback incorrectly | Python truthiness: `0` is falsy, so score of 0 triggers fallback path |

**Critical bug candidate: `risk_score` falsy check**
In `notification_handler.py:_extract_analysis()` line 119:
```python
if not risk_score:
    risk_assessment = data.get('RiskAssessment', {})
```
A risk_score of `0` (perfectly safe URL) would trigger this fallback, potentially losing the real assessment.

**Timeout/retry:**
- SUB socket timeout: 5000ms (`zmq.RCVTIMEO, 5000`) - used to check `self.running` flag
- Heartbeat logged every ~2 minutes (24 * 5s) to confirm listener is alive
- No retry on missed notifications - PUB/SUB has no delivery guarantee

---

### STAGE 7: Extension Receives Result and Updates Popup UI

**Trigger:** WebSocket `onmessage` in `ConnectionService.handleMessage()` dispatches to registered handler for `url_result`

**Expected behavior:**
1. ConnectionService receives JSON, identifies `type: "url_result"`
2. Calls registered handler: `background.js:handleUrlResult(data)`
3. If `data.analyzing === true`, keeps loading state and returns (wait for final result)
4. Otherwise, stops loading animation
5. Calls `scanService.handleResult(data)` which:
   - Updates StateManager state
   - Saves score to `chrome.storage.local` (per-tab and global: `currentPageScore`, `currentPageRiskType`, `currentPageAction`)
   - Resolves pending scan promise
   - Caches result in CacheService
6. Updates icon color via `iconService.setColorByAction()`
7. Executes protective action via `protectionService.executeAction()`
8. Popup.js detects `chrome.storage.onChanged` event for `currentPageScore` and updates UI

**Health indicators:**
- Console: `[Background] URL result received: {url, score, action, analyzing}`
- Console: `[ScanService] Result received (from server): ...`
- Popup shows numeric score in risk circle
- Extension icon color changes (green/yellow/red)

**Failure modes:**
| Failure | Symptom | Root Cause |
|---------|---------|-----------|
| `analyzing: true` stuck | Loading animation never stops; 30s timeout fires | Backend accepted alert but never published notification; Desktop App sent intermediate `url_result` with `analyzing: true` but final result never came |
| No score in result | `[ScanService] No score in result, skipping` | Backend response missing `score` field |
| Error in result | `scan.error` state set, no score shown | Backend returned error; `data.error === true` |
| Pending scan already timed out | Result arrives but no pending scan to resolve | 30-second ScanService timeout expired before result arrived |
| Storage write fails | Score not persisted, popup shows stale data | `chrome.storage.local` API error |
| Popup not open | Score saved to storage but nobody reads it until popup opens | Normal behavior - popup reads on open |
| Tab switched before result | Per-tab score saved to wrong tab or global score stale | Active tab changed during scan |
| `url_result` handler not registered | Message received but no handler fires | `setupWebSocketHandlers()` not called or handler for `MSG.WS_URL_RESULT` missing |

**Timeout/retry:**
- 30-second timeout in `triggerScan()` and `ScanService.scan()` promise
- On timeout: icon turns gray, `currentPageScanning` set to false
- No automatic retry; user must click "Scan" again or navigate to trigger new scan

---

## Healthy Score Flow vs Broken Score Flow

### Healthy Flow (Expected Logs)

```
[Extension Console]
[ScanService] Scanning: example.com
[ConnectionService] Sending: url_check
...
[Background] URL result received: {url: "https://example.com", score: null, analyzing: true}
[Background] Analysis in progress, waiting for final result...
...
[ConnectionService] Received: url_result
[Background] URL result received: {url: "https://example.com", score: 85, action: 0}
[ScanService] Result received (from server): {score: 85, ...}

[Desktop App Stdout]
<<< RECEIVED FROM EXTENSION [url_check]
[SCAN] URL CHECK REQUEST
[SCAN] Step 1: Checking cache... CACHE MISS
[SCAN] Step 2: Checking authentication... Token is valid
[SCAN] Step 3: Sending to backend (ZMQ)...
[ZMQ] SUCCESS: Connected to tcp://127.0.0.1:50001
[ZMQ] SUCCESS: Alert sent!
[ZMQ] RECEIVED: Response received: {success: true, ...}
>>> SENT TO EXTENSION [url_result] {analyzing: true}
...
NOTIFICATION RECEIVED
[NOTIFICATION] Risk Score: 85
[NOTIFICATION] Updating cache: score=85
[NOTIFICATION] Broadcasted result to extension: score=85
```

### Broken Flow: Score Never Reaches Extension

**Scenario A: Backend returns `{success: true}` but never publishes notification**
```
Extension: Shows loading animation -> 30s timeout -> gray icon
Desktop: [ZMQ] RECEIVED: {success: true} -> Sent analyzing:true to Extension
         [NOTIFY] HEARTBEAT: Still listening... (no notification ever arrives)
```
Diagnosis: Backend accepted alert but analysis pipeline crashed or NotificationPublisher failed.

**Scenario B: Desktop App can't connect to Backend**
```
Extension: Shows loading animation -> eventually error or timeout
Desktop: [ZMQ] ERROR: Connection failed OR [ZMQ] WARNING: Timeout
         [SCAN] ERROR: No response
         >>> SENT TO EXTENSION [url_result] {error: true, message: "No response"}
Extension: [ScanService] scan.error state set
```

**Scenario C: Notification arrives but broadcast fails**
```
Desktop: NOTIFICATION RECEIVED, Risk Score: 85
         [NOTIFICATION] Broadcasted result to extension: score=85
         (but ExtensionServer.clients is empty - extension disconnected)
Extension: Never receives url_result, loading times out after 30s
```

**Scenario D: Token rejected by Backend**
```
Desktop: [ZMQ] RECEIVED: {status: "InvalidToken", ...}
         [SCAN] ERROR: InvalidToken
         >>> SENT TO EXTENSION [url_result] {error: true}
Extension: Error state shown, no score
```

**Scenario E: Extension disconnected during analysis**
```
Desktop: Sends analyzing:true -> Extension receives it
         Extension disconnects (tab closed, service worker killed)
         Notification arrives -> broadcast to empty client set
         Extension reconnects -> no pending scan, stale state
```

---

## Expected Message Formats Summary

### Extension -> Desktop App (WebSocket)

| Message Type | Format |
|-------------|--------|
| `url_check` | `{type: "url_check", url: string, trackers: array, iframes: array}` |
| `ping` | `{type: "ping"}` |
| `heartbeat_ping` | `{type: "heartbeat_ping"}` |
| `keepalive` | `{type: "keepalive"}` |
| `user_auth` | `{type: "user_auth", email: string}` |

### Desktop App -> Extension (WebSocket)

| Message Type | Format |
|-------------|--------|
| `url_result` (analyzing) | `{type: "url_result", analyzing: true, message: "Analysis in progress"}` |
| `url_result` (success) | `{type: "url_result", score: int, riskType: array, protectiveAction: int, cached: bool}` |
| `url_result` (from notification) | `{type: "url_result", url: string, score: int, riskType: array, protectiveAction: int, fromCache: false}` |
| `url_result` (error) | `{type: "url_result", error: true, message: string}` |
| `pong` | `{type: "pong", status: "ok"}` |
| `heartbeat_pong` | `{type: "heartbeat_pong"}` |

### Desktop App -> Backend (ZMQ REQ)

| Alert Type | Key Fields |
|-----------|-----------|
| `UrlAlert` | `AlertType, DeviceInfo{DeviceUid, DeviceType, OperatingSystem, MAC}, Timestamp, Priority, Token, Url, Trackers, IFrameDomains, UserAgent` |
| `RemoteAccessAlert` | Same DeviceInfo + `RemoteAccessApp, RunningProcesses, ConnectionUrl, ConnectionStatus, SessionStatus, Direction, Confidence, RemoteCountry, RemoteCountryCode` |

### Backend -> Desktop App (ZMQ REP)

| Response Type | Format |
|-------------|--------|
| Success (async) | `{success: true, message: "Alert processed successfully", alertType, deviceUid, timestamp, priority}` |
| Success (sync/legacy) | `{Score: int, RiskType: array, ProtectiveAction: int/string, HasError: false}` |
| Token error | `{status: "InvalidToken"/"TokenExpired", message: string}` |
| Domain error | `{success: false, message: "DeviceNotFound"/"UserNotFound", error: string}` |

### Backend -> Desktop App (ZMQ PUB)

| Envelope | Content |
|----------|---------|
| Topic: `device:{DeviceUid}` | `{Type, Timestamp, DeviceUid, Data: {AlertType, Severity, RiskAssessment, AnalysisResult, ProtectiveActions, Indicators}}` |

---

## Configuration Dependencies

| Parameter | Value | File |
|-----------|-------|------|
| WebSocket ports | `[8080, 8181, 8282, 8383, 8484]` | `config.py:EXTENSION_PORTS`, `ConnectionService.js:config.ports` |
| Backend host | `127.0.0.1` | `config.py:BACKEND_HOST` |
| Backend REQ/REP port | `50001` | `config.py:BACKEND_REQ_PORT`, `RealTimeAlertListener` constructor default |
| Backend PUB/SUB port | `50002` | `config.py:BACKEND_SUB_PORT`, `NotificationPublisher` config key `NetMQ:NotificationPublisherPort` |
| ZMQ receive timeout | `5000ms` | `zmq_client.py:self.timeout` |
| SUB receive timeout | `5000ms` | `notification_client.py` setsockopt |
| Extension scan timeout | `30000ms` | `ScanService.js:scanTimeout`, `background.js` setTimeout |
| Heartbeat interval | `10000ms` | `ConnectionService.js:heartbeatInterval` |
| Max missed heartbeats | `3` | `ConnectionService.js:maxMissedHeartbeats` |
| Keepalive interval | `20000ms` | `ConnectionService.js:keepaliveInterval` |
| Cache TTL | `3600s` (1 hour) | `scan_service.py`, `notification_handler.py` |
| Device ID | `"PC-JOHN-001"` | `main.py:AntiScamApp.__init__()` default |
| Fixed token | `"12345678-1234-1234-1234-123456789012"` | `zmq_client.py:send_url_alert()` fallback |

---

## Diagnostic Checklist

When the score stops reaching the extension, check in order:

### 1. Is the Extension connected to Desktop App?
- Extension console: Look for `[ConnectionService] Port XXXX connected!`
- Extension badge: Red `!` = disconnected, no text = connected
- Desktop App stdout: `[EXTENSION] Client connected`

### 2. Is the Desktop App receiving url_check messages?
- Desktop App stdout: `<<< RECEIVED FROM EXTENSION [url_check]`
- If missing: WebSocket connection issue (Stage 1)

### 3. Is the Desktop App sending to Backend?
- Desktop App stdout: `[ZMQ] SENDING URL ALERT`
- Desktop App stdout: `[ZMQ] SUCCESS: Alert sent!`
- If `[ZMQ] ERROR:` -> Backend unreachable (Stage 3)

### 4. What does the Backend respond?
- Desktop App stdout: `[ZMQ] RECEIVED: Response received:`
- If `{success: true}` -> Async mode, check PUB/SUB (Stage 5)
- If `{status: "InvalidToken"}` -> Token issue (Stage 4)
- If `{success: false, message: "DeviceNotFound"}` -> Device registration issue (Stage 4)
- If `{Score: 85, ...}` -> Sync mode, check response routing (Stage 2)
- If timeout -> Backend not responding (Stage 4)

### 5. Does the Desktop App send `url_result` back to Extension?
- Desktop App stdout: `>>> SENT TO EXTENSION [url_result]`
- If `{analyzing: true}` -> Waiting for notification (Stage 5/6)
- If `{score: X}` -> Score sent, check Extension reception (Stage 7)

### 6. Does the notification arrive via PUB/SUB?
- Desktop App stdout: `NOTIFICATION RECEIVED` banner
- If heartbeats only -> Backend never published, or wrong topic
- Check: Is `device_uid` consistent between REQ and SUB subscription?

### 7. Does the Extension receive and display the score?
- Extension console: `[Background] URL result received: {score: X}`
- Extension console: `[ScanService] Result received (from server): ...`
- Check `chrome.storage.local` for `currentPageScore`

---

## Open Questions for Repair

1. **Which response path is the Backend currently using?** If it changed from sync to async (or vice versa), the Desktop App's `_process_response()` may not handle the format correctly.
2. **Is the fixed token (`12345678-...`) registered in the Backend's TokenStore?** If the Backend was redeployed, token validation may reject all requests.
3. **Is the DeviceUid `PC-JOHN-001` registered in the Backend database?** If the database was reset, the device lookup fails.
4. **Is the NotificationPublisher actually running?** Check Backend logs for `NotificationPublisher started on tcp://*:50002`.
5. **Is there a race condition in the SUB subscription?** The SUB socket must be connected and subscribed BEFORE the PUB sends. If the notification publishes before the SUB subscribes (e.g., on first startup), it's lost.
6. **Does `notification_handler.py`'s `_broadcast_to_extension()` actually reach the Extension?** The asyncio event loop handling (try `get_running_loop()`, fallback to `asyncio.run()`) may fail silently in certain threading contexts.

---

*Research complete. This document feeds into requirements definition for score flow repair.*
