# Phase 3: Restore End-to-End Score Flow - Research

**Researched:** 2026-02-12
**Domain:** Multi-component pipeline integration (Chrome Extension -> Desktop App -> C# Backend -> Desktop App -> Chrome Extension)
**Confidence:** HIGH

## Summary

Phase 3 is an integration/verification phase, not a greenfield build. The individual links (ZMQ REQ/REP, ZMQ PUB/SUB bridge, WebSocket) were repaired in Phases 1-2. This phase must wire them together end-to-end and resolve remaining blockers that prevent a URL submitted from the Chrome Extension from returning a visible score in the popup.

Research identified three critical blockers that must be resolved before the pipeline can work:

1. **Token authentication will fail**: The Desktop App sends a hardcoded UUID token (`12345678-1234-1234-1234-123456789012`) that is NOT registered in the Backend's in-memory TokenStore. The TokenStore is a `ConcurrentDictionary<string, DeviceToken>` that starts empty on each backend restart. The Backend's `RealTimeAlertListener.ProcessAlertAsync()` validates the token via `_tokenStore.ValidateToken()` and will return `InvalidToken`, causing the alert to be rejected before analysis even begins.

2. **Device registration is required**: The Backend looks up the device via `_asView.FindUserDeviceByDeviceUid(deviceUid)`. If the device "PC-JOHN-001" is not in the database and ASView in-memory cache, the alert is rejected with `DeviceNotFound`. The Desktop App needs to either register the device first (via `RegisterDevice` message) or the device must already exist in the database.

3. **Extension cache masks pipeline failures**: The Extension CacheService uses domain-based keys with 1-hour TTL. If a domain was previously tested (even with a broken pipeline), the cached result is returned without hitting the server. Testing MUST use the extension's "Clear Cache" function or use domains that have never been tested.

**Primary recommendation:** Implement a device registration + token acquisition handshake at Desktop App startup before any URL alerts are sent, and ensure the full pipeline is tested with cache-cleared state.

## Standard Stack

This phase does not introduce new libraries. The existing stack is already in place:

### Core (Already Installed)
| Library | Version | Purpose | Component |
|---------|---------|---------|-----------|
| pyzmq | Existing | ZMQ REQ/REP and PUB/SUB communication | Desktop App |
| websockets | Existing | WebSocket server for Extension | Desktop App |
| asyncio | stdlib | Async event loop for WebSocket | Desktop App |
| NetMQ | Existing | ZMQ server sockets (REP, PUB) | C# Backend |
| Newtonsoft.Json | Existing | JSON serialization | C# Backend |
| Chrome Extension MV3 | v3 | Service worker, WebSocket client | Extension |

### Supporting
| Library | Version | Purpose | When Used |
|---------|---------|---------|-----------|
| chrome.storage.local | MV3 API | Per-tab score state, cache persistence | Extension popup updates |
| chrome.action | MV3 API | Badge text/color, icon rendering | Score display on icon |

### No New Installations Required

All libraries are already installed and configured. This phase is purely about wiring existing components correctly and fixing the token/device registration gap.

## Architecture Patterns

### End-to-End Data Flow (Target State)

```
Chrome Extension                  Desktop App                    C# Backend
===============                  ===========                    ==========

1. Tab loads URL
   |
   v
2. background.js
   scanService.scan(tabId, url)
   |
   v
3. ConnectionService.send({
     type: 'url_check',
     url, trackers, iframes
   })
   --- WebSocket --->
                                4. extension_server.py receives
                                   _handle_client() -> callback
                                   |
                                   v
                                5. extension_handler.py
                                   _handle_url_check()
                                   |
                                   v
                                6. scan_service.py
                                   check_url() -> cache miss
                                   |
                                   v
                                7. zmq_client.py
                                   send_url_alert()
                                   --- ZMQ REQ --->
                                                               8. RealTimeAlertListener
                                                                  ProcessAlertAsync()
                                                                  - Validate token
                                                                  - Look up device
                                                                  - Route to UDAnalysisManager
                                                                  |
                                                                  v
                                                               9. Analysis runs
                                                                  Domain events fire
                                                                  |
                                                                  v
                                                               10. REP response:
                                                                   {success: true}
                                   <--- ZMQ REP ---
                                8a. scan_service returns
                                    {analyzing: true}
   <--- WebSocket ---
4a. handleUrlResult()
    data.analyzing === true
    Keep loading state...
                                                               11. NotificationPublisherActor
                                                                   handles AnalysisResultReceived
                                                                   |
                                                                   v
                                                               12. NotificationPublisher
                                                                   PublishAnalysisResult()
                                                                   topic: "device:PC-JOHN-001"
                                                                   --- ZMQ PUB --->
                                13. notification_client.py
                                    recv_multipart()
                                    [topic, json]
                                    |
                                    v
                                14. notification_handler.py
                                    handle() on ZMQ thread
                                    |
                                    v
                                15. run_coroutine_threadsafe()
                                    _broadcast_to_extension()
                                    --- WebSocket --->
5. ConnectionService.onMessage
   'url_result' handler
   |
   v
6. handleUrlResult()
   scanService.handleResult()
   |
   v
7. chrome.storage.local.set({
     currentPageScore: score,
     currentPageRiskType: [...],
     currentPageAction: N
   })
   |
   v
8. popup.js detects storage change
   RiskDisplay.update(score)
   IconService.setColorByAction()
   Badge updated
```

### Pattern 1: Token Acquisition Before Alerts

**What:** Desktop App must obtain a valid token from Backend before sending URL alerts.
**When to use:** At Desktop App startup, before any scan requests.

The Backend `RealTimeAlertListener` routes messages by checking the `MessageType` field:
- `"RequestToken"` -> Returns existing/new token for known devices
- `"RegisterDevice"` -> Registers new device and returns token
- No `MessageType` -> Treated as alert (requires valid token)

**Approach:**
The Desktop App must send a `RegisterDevice` or `RequestToken` message on ZMQ REQ at startup to get a valid token, then use that token for all subsequent URL alerts.

```python
# Token request message format (for known devices):
{
    "MessageType": "RequestToken",
    "DeviceUid": "PC-JOHN-001"
}

# Device registration message format (for new devices):
{
    "MessageType": "RegisterDevice",
    "DeviceUid": "PC-JOHN-001",
    "Email": "user@example.com",
    "DeviceType": 1,
    "OperatingSystem": 1,
    "MAC": "00:11:22:33:44:55"
}

# Success response:
{
    "status": "TokenCreated",  # or "Registered"
    "token": "abc123...",
    "expiration": "2026-02-13T12:00:00Z",
    "deviceUid": "PC-JOHN-001",
    "serverPublicKey": ""
}
```

### Pattern 2: Async Analysis Flow (Two-Phase Response)

**What:** The Backend uses asynchronous analysis. The REP response is an immediate acknowledgment (`{success: true}`), NOT the analysis result. The actual result arrives later via ZMQ PUB/SUB notification.

**Current Desktop App code handles this correctly:**
- `scan_service.py._process_response()` checks `response.get('success')` and returns `{analyzing: True}`
- Extension's `handleUrlResult()` checks `data.analyzing === true` and keeps the loading state
- The real result arrives via `notification_handler.py` -> broadcast -> Extension `url_result` handler

**This pattern is already implemented.** No changes needed to the async flow itself.

### Pattern 3: Notification Message Shape

**What:** The notification published by Backend follows this structure:
```json
{
    "Type": "AnalysisResult",
    "Timestamp": "2026-02-12T12:00:00Z",
    "DeviceUid": "PC-JOHN-001",
    "Data": {
        "AlertType": "UrlAnalysisComplete",
        "Severity": "Medium",
        "RiskAssessment": {
            "risk_score": 75,
            "risk_level": "Low",
            "is_scam": false,
            "confidence": 0.85
        },
        "AnalysisResult": {
            "TypeName": "UrlAnalysisResult",
            "Url": "https://example.com",
            "Domain": "example.com",
            "risk_assessment": { ... },
            "analysis_time_ms": 1234
        },
        "protectiveActions": [...],
        "Indicators": [...]
    }
}
```

The `notification_handler.py._extract_analysis()` method correctly handles multiple fallback paths for extracting url, risk_score, and risk_assessment from this structure.

### Anti-Patterns to Avoid

- **Using hardcoded tokens:** The current hardcoded UUID `12345678-1234-1234-1234-123456789012` in `zmq_client.py` will NEVER pass Backend token validation. All tokens must come from the TokenStore via `RequestToken` or `RegisterDevice`.

- **Testing with cached URLs:** Both the Extension CacheService (1h TTL, domain-keyed) and Desktop App CacheManager (1h TTL, domain-keyed) will mask pipeline failures. Always clear caches before testing.

- **Creating new ZMQ contexts per request:** The `zmq_client.py.send_url_alert()` calls `connect()` and `close()` for each request, creating a new ZMQ context each time. This works but is inefficient. Do NOT change this pattern in Phase 3 (it works, and reliability improvements belong in Phase 5).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Token management | Custom token generation | Backend's `RequestToken` / `RegisterDevice` MessageType | Backend already has TokenStore with secure token generation, expiration, and validation |
| Score calculation | Local risk score conversion | Server's `risk_score` directly | NotificationHandler already uses `int(risk_score)` from server; Extension uses server values directly |
| ZMQ multipart framing | Manual topic + message send | `recv_multipart()` (already fixed in Phase 2) | Atomic frame reception prevents topic/message desync |
| Thread-to-async bridge | `asyncio.run()` in threads | `run_coroutine_threadsafe()` (already fixed in Phase 2) | Phase 2's primary bug fix; do not regress |

**Key insight:** This phase is about making existing components work together, not building new ones. The code for every step already exists but the token/registration gap blocks the pipeline at the Backend door.

## Common Pitfalls

### Pitfall 1: Token Validation Rejection
**What goes wrong:** Backend returns `{"status": "InvalidToken"}` for every URL alert
**Why it happens:** The hardcoded token `12345678-1234-1234-1234-123456789012` is never registered in the Backend's in-memory TokenStore (which starts empty on each restart). The `ValidateToken()` method checks `_tokens.TryGetValue(deviceUid, ...)` and returns `InvalidToken` when no entry exists.
**How to avoid:** Send `RequestToken` or `RegisterDevice` message at startup to get a valid token before any alerts.
**Warning signs:** Backend logs show "Invalid token from device PC-JOHN-001" or "No token found for device PC-JOHN-001"

### Pitfall 2: Device Not Found in ASView
**What goes wrong:** Backend returns `{"success": false, "message": "DeviceNotFound"}`
**Why it happens:** `_asView.FindUserDeviceByDeviceUid("PC-JOHN-001")` returns null because the device is not in the database. ASView loads devices from DB at startup.
**How to avoid:** Use `RegisterDevice` message which creates the device record AND returns a token in one step. Requires a valid user email that exists in the database.
**Warning signs:** Backend logs show "Device not found: PC-JOHN-001"

### Pitfall 3: User Not Found for Registration
**What goes wrong:** Backend returns `{"status": "InvalidUser"}` when trying to register device
**Why it happens:** `RegisterDevice` requires an `Email` field matching an active user in the database. If the email doesn't exist or the user is disabled, registration fails.
**How to avoid:** Verify that at least one active user exists in the ASPSBackend2DB database. Check the SQL dump `aspsbackend2db_20260130.sql` for existing users, or create one.
**Warning signs:** Backend logs show "No active user found for email..."

### Pitfall 4: Extension Cache Masking Failures
**What goes wrong:** Extension shows a cached score instead of hitting the server, making it appear the pipeline works when it doesn't
**Why it happens:** CacheService uses domain-level keys with 1-hour TTL. A previous test (even a failed one that returned some default) gets cached.
**How to avoid:** Clear cache via Extension popup or `chrome.storage.local.clear()`. Use a fresh/unique URL domain for each test.
**Warning signs:** Extension console shows "[ScanService] Cache hit" but you expected a server round-trip

### Pitfall 5: Desktop App Cache Masking Failures
**What goes wrong:** Desktop App returns cached result without contacting Backend
**Why it happens:** `scan_service.py.check_url()` checks `self.cache.get(url)` first and returns immediately on cache hit
**How to avoid:** Clear Desktop App cache (delete `%APPDATA%/AntiScam/cache.json`) or use fresh domains
**Warning signs:** Desktop App logs show "[SCAN] CACHE HIT!" when expecting server contact

### Pitfall 6: Notification Topic Mismatch
**What goes wrong:** Desktop App SUB socket doesn't receive the notification
**Why it happens:** Desktop App subscribes to topic `"device:PC-JOHN-001"` but Backend publishes to `"device:{actual_device_uid}"`. If the device UID used in the alert differs from what the Desktop App subscribes to, the notification is lost.
**How to avoid:** Ensure the `device_uid` in the Container (`"PC-JOHN-001"`) matches what the Backend records for the device. The `NotificationClient.__init__()` uses `self.device_uid` for the subscription topic.
**Warning signs:** Backend logs show "Published notification to topic 'device:...'", but Desktop App shows no NOTIFY-DIAG recv messages

### Pitfall 7: Score Display Not Updating in Popup
**What goes wrong:** Score arrives at background.js but popup doesn't show it
**Why it happens:** The popup uses `chrome.storage.onChanged` listener AND initial `chrome.storage.local.get()`. If the score was set before the popup opened, it needs to be read from storage on init. If it arrives while popup is open, the listener handles it. Race conditions can cause the popup to show stale data.
**How to avoid:** The popup already handles both cases via `PageInfoService.update()` (initial load) and `EventHandlers.init()` (change listener). Verify both paths work.
**Warning signs:** Popup shows "--" or "Checking..." but background.js console shows the score was received

## Code Examples

### Token Acquisition at Startup (New Code Needed)

```python
# Source: Analysis of RealTimeAlertListener.RouteMessageAsync() and TokenStore.cs

def request_token(self) -> Optional[str]:
    """Request a token from Backend via ZMQ REQ.

    Uses RegisterDevice to both register the device and get a token.
    Requires a valid user email in the database.
    """
    if not self.connect():
        return None

    try:
        message = {
            "MessageType": "RegisterDevice",
            "DeviceUid": self.device_uid,
            "Email": self.email,  # Must match an active user in DB
            "DeviceType": 1,      # PersonalComputer
            "OperatingSystem": 1, # Windows
            "MAC": "00:11:22:33:44:55"
        }

        message_json = json.dumps(message)
        self.socket.send(message_json.encode('utf-8'))

        response_bytes = self.socket.recv()
        response = json.loads(response_bytes.decode('utf-8'))

        if response.get('status') in ('TokenCreated', 'Registered'):
            token = response['token']
            expiration = response['expiration']
            print(f"[AUTH] Got token: {token[:20]}... expires {expiration}")
            return token
        else:
            print(f"[AUTH] Token request failed: {response}")
            return None
    except Exception as e:
        print(f"[AUTH] Error requesting token: {e}")
        return None
    finally:
        self.close()
```

### Using the Acquired Token in URL Alerts

```python
# Source: zmq_client.py send_url_alert() -- Modified to use real token

def send_url_alert(self, device_uid, url, token, **kwargs):
    """Send URL alert with a valid token from TokenStore."""
    # The token parameter MUST be a real token from RequestToken/RegisterDevice
    # NOT the hardcoded "12345678-1234-1234-1234-123456789012"

    if not token or token == "12345678-1234-1234-1234-123456789012":
        print("[ZMQ] WARNING: No valid token! Request one first.")
        return None

    alert = {
        "AlertType": "UrlAlert",
        "DeviceInfo": {
            "DeviceUid": device_uid,
            "DeviceType": 1,
            "OperatingSystem": 1,
            "MAC": "00:11:22:33:44:55"
        },
        "Timestamp": datetime.utcnow().isoformat() + "Z",
        "Priority": 1,
        "Token": token,  # Real token from TokenStore
        "Url": url,
        "Trackers": kwargs.get('trackers', []),
        "IFrameDomains": kwargs.get('iframes', []),
        "UserAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
    }
    # ... send and receive as before
```

### Extension Badge Update on Score Receipt

```javascript
// Source: background.js handleUrlResult() -- Already implemented correctly

function handleUrlResult(data) {
  // Skip if still analyzing (wait for PUB/SUB notification)
  if (data.analyzing === true) {
    return;
  }

  // Stop loading animation
  stopLoadingState();

  // Process result using server values directly
  const result = scanService.handleResult(data);

  if (result) {
    // Update icon color based on protective action (with score fallback)
    iconService.setColorByAction(result.protectiveAction, result.score);

    // Execute protective action (banner, modal, block)
    protectionService.executeAction(
      result.protectiveAction, result.riskType, result.score
    );
  }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Hardcoded dummy token | Must acquire real token from TokenStore | Phase 3 (this phase) | Alerts were rejected; now they will be processed |
| `asyncio.run()` in ZMQ thread | `run_coroutine_threadsafe()` | Phase 2 | Notifications now bridge to main event loop correctly |
| Dual `recv()` for multipart | `recv_multipart()` | Phase 2 | Atomic frame reception prevents desync |
| CurveMQ encryption enabled | CurveMQ disabled (Phase 1-3) | Phase 1 | Simplified debugging; re-enabled in Phase 4 |

**Deprecated/outdated:**
- The hardcoded token `"12345678-1234-1234-1234-123456789012"` in `zmq_client.py` line 197 must be replaced with real token acquisition

## Open Questions

1. **Active user in database**
   - What we know: `RegisterDevice` requires a valid email matching an active user in the DB. The SQL dump `aspsbackend2db_20260130.sql` likely contains user records.
   - What's unclear: Which user email to use, and whether the user account is active/enabled.
   - Recommendation: Check the SQL dump or database for active users. If none exist, document how to create one. For now, the Desktop App config or `.env` should store the user email.

2. **Device UID consistency**
   - What we know: The Desktop App hardcodes `device_id = "PC-JOHN-001"` in `AntiScamApp.__init__()`. The NotificationClient subscribes to `"device:PC-JOHN-001"`. The Backend publishes to `"device:{deviceUid}"` from the alert.
   - What's unclear: Whether the device UID used during registration matches the one used in alerts and subscriptions. It should all be "PC-JOHN-001".
   - Recommendation: Verify all three components use the exact same device UID string. The current code appears consistent.

3. **Backend analysis completion time**
   - What we know: The success criteria requires round-trip under 10 seconds. Backend runs Python analyzers as subprocesses (configured in appsettings.json `Python:AnalyzersFolderPath`).
   - What's unclear: How long the actual URL analysis takes. If the Python analyzers are slow, the 10-second target may not be met for complex URLs.
   - Recommendation: Test with a simple/known URL (e.g., `https://google.com`) first to establish baseline timing. If too slow, this is a Backend performance issue, not a pipeline issue.

4. **AuthManager.get_token() returns empty string**
   - What we know: `AuthManager.authenticate()` sets `self.token = ""` (empty string) and `is_valid()` always returns `True`. When `scan_service.check_url()` calls `self.auth_manager.get_token()`, it gets `""` (empty string), which then falls through to the hardcoded UUID in `zmq_client.py`.
   - What's unclear: This is a design choice for "ZMQ backend doesn't require auth" but it inadvertently causes the hardcoded token to be used.
   - Recommendation: The token acquisition fix should update AuthManager to store and return the real token from the Backend's TokenStore.

## Sources

### Primary (HIGH confidence)
- Direct code analysis of all source files listed below (strongest evidence):
  - `apps/desktop/win/src/main.py` - Startup flow, event loop injection
  - `apps/desktop/win/src/zmq_client.py` - Hardcoded token at line 197
  - `apps/desktop/win/src/notification_client.py` - SUB topic subscription
  - `apps/desktop/win/src/handlers/notification_handler.py` - Thread-safe broadcast bridge
  - `apps/desktop/win/src/handlers/extension_handler.py` - url_check message handling
  - `apps/desktop/win/src/services/scan_service.py` - URL check flow, cache check, pending URL tracking
  - `apps/desktop/win/src/extension_server.py` - WebSocket server, broadcast
  - `apps/desktop/win/src/auth_manager.py` - Token storage, always-valid for ZMQ
  - `apps/desktop/win/src/config.py` - Ports, device settings
  - `apps/desktop/win/src/core/container.py` - DI container
  - `apps/extension/chrome/background.js` - Message handlers, scan triggering
  - `apps/extension/chrome/popup.js` - Score display, storage listeners
  - `apps/extension/chrome/services/ConnectionService.js` - WebSocket management
  - `apps/extension/chrome/services/ScanService.js` - Scan flow, cache check
  - `apps/extension/chrome/services/CacheService.js` - Domain-keyed cache with 1h TTL
  - `apps/extension/chrome/services/IconService.js` - Badge/icon color updates
  - `ASPSBackend14_J/Business/Messaging/RealTimeAlertListener.cs` - Token validation, alert routing
  - `ASPSBackend14_J/Business/Messaging/NotificationPublisher.cs` - PUB socket, topic publishing
  - `ASPSBackend14_J/Business/Messaging/NotificationPublisherActor.cs` - Domain event to notification
  - `ASPSBackend14_J/Business/Services/TokenStore.cs` - In-memory token store, validation
  - `ASPSBackend14_J/ASPSBackend/appsettings.json` - Port config, CurveMQ disabled

### Secondary (MEDIUM confidence)
- `.planning/STATE.md` - Phase 1-2 completion notes, accumulated decisions
- `.planning/ROADMAP.md` - Phase 3 success criteria and planned structure
- `.planning/REQUIREMENTS.md` - FLOW-01 through FLOW-04 requirement definitions

### Tertiary (LOW confidence)
- None. All findings are based on direct code analysis.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Direct code inspection confirms all libraries already in use
- Architecture: HIGH - Full data flow traced through all source files
- Pitfalls: HIGH - Token validation logic verified in TokenStore.cs and RealTimeAlertListener.cs source code
- Code examples: HIGH - Based on actual method signatures and message formats from source code

**Research date:** 2026-02-12
**Valid until:** 2026-03-12 (stable codebase, no external dependencies changing)
