# Phase 1: Diagnose and Verify Baseline Communication - Research

**Researched:** 2026-02-12
**Domain:** ZeroMQ (pyzmq/NetMQ) REQ/REP communication, Python WebSocket server, Chrome Extension MV3 WebSocket client, Windows port diagnostics
**Confidence:** HIGH (based on direct codebase reading plus verified library documentation)

## Summary

Phase 1 requires verifying that each communication link in the ASPS pipeline can independently send and receive messages: (1) Desktop App to Backend via ZMQ REQ/REP on port 50001, and (2) Chrome Extension to Desktop App via WebSocket. The codebase already has the infrastructure for both links fully built -- this phase is about **diagnostic verification**, not new feature development.

The primary risks discovered during research are: (a) CurveMQ is currently **enabled** in `appsettings.json` (`"CurveEnabled": true`), which will cause silent handshake failures because the Python desktop client does NOT implement CURVE client-side keys; (b) the hardcoded token `12345678-1234-1234-1234-123456789012` in `zmq_client.py` is NOT registered in the backend's in-memory `TokenStore`, so alert processing will return `InvalidToken`; and (c) the ZMQ REQ/REP strict alternating send/recv pattern means a timeout or error can permanently corrupt the socket state, requiring socket recreation.

**Primary recommendation:** Start by disabling CurveMQ (`"CurveEnabled": false`) in `appsettings.json`, then use existing standalone test scripts (`zmq_client.py __main__` and `extension_server.py __main__`) with enhanced diagnostic logging to verify each link independently. Use `netstat` or PowerShell `Get-NetTCPConnection` to verify backend ports before attempting ZMQ connections.

## Standard Stack

The system already uses specific library versions. Phase 1 does not add new libraries -- it diagnoses existing ones.

### Core (Already in Codebase)
| Library | Version | Purpose | File |
|---------|---------|---------|------|
| pyzmq | >=25.1.0 | ZMQ REQ client for Backend communication | `apps/desktop/win/src/zmq_client.py` |
| websockets | >=12.0 | WebSocket server for Extension communication | `apps/desktop/win/src/extension_server.py` |
| NetMQ | 4.0.1.13 | ZMQ REP server in Backend (C# .NET 8) | `ASPSBackend14_J/Business/Messaging/RealTimeAlertListener.cs` |

### Supporting (Already in Codebase)
| Library | Version | Purpose | When Used |
|---------|---------|---------|-----------|
| python-dotenv | >=1.0.1 | Load `.env` for config | Desktop App startup |
| Newtonsoft.Json | 13.0.3 | JSON serialization in Backend | Alert deserialization |

### Diagnostic Tools (Available on Windows)
| Tool | Purpose | When to Use |
|------|---------|-------------|
| `netstat -an` | Show listening TCP ports | Before ZMQ connection test |
| `Get-NetTCPConnection -State Listen` | PowerShell port listing | Same, more parsable output |
| `Test-NetConnection localhost -Port 50001` | TCP connectivity test | Verify port reachable before ZMQ |
| Chrome DevTools (F12 > Console) | Extension background service worker logs | WebSocket diagnostic output |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Manual netstat | Python `socket.connect_ex()` probe | More automatable but netstat gives full picture |
| Standalone test scripts | pytest with fixtures | Overkill for Phase 1 diagnostic; standalone scripts already exist |

**Installation:** No new packages needed. Existing `requirements.txt` and NuGet packages cover everything.

## Architecture Patterns

### Existing Communication Architecture
```
Chrome Extension (MV3 Service Worker)
    |
    | WebSocket (ws://localhost:8080-8484)
    v
Desktop App (Python, asyncio + threading)
    |
    | ZMQ REQ/REP (tcp://127.0.0.1:50001) -- alerts & responses
    | ZMQ SUB     (tcp://127.0.0.1:50002) -- notifications (Phase 2)
    v
Backend (.NET 8, NetMQ)
    |
    |- Port 50001: RealTimeAlertListener (REP socket) -- receives alerts
    |- Port 50002: NotificationPublisher (PUB socket) -- publishes results
    |- Port 5555:  NetMQMessageProcessor (REP socket) -- CQRS commands
    |- Port 5556:  CQRSGateway (REP socket) -- WebApi CQRS bridge
```

### Pattern 1: ZMQ REQ/REP Round-Trip (Link 1)
**What:** Desktop App sends a JSON alert via ZMQ REQ, Backend's ResponseSocket deserializes it, processes it, and sends a JSON response back.
**Current flow (from codebase):**
1. `zmq_client.py:send_url_alert()` creates a new `zmq.Context()` + `zmq.REQ` socket each call (connect-per-call pattern)
2. Sends UTF-8 JSON bytes via `socket.send()`
3. Waits for response with `RCVTIMEO=5000ms`
4. Backend `RealTimeAlertListener.cs:ListenForAlerts()` calls `_repSocket.ReceiveFrameBytes()`
5. Routes message via `RouteMessageAsync()` -- either token management or alert processing
6. Sends JSON response via `_repSocket.SendFrame(bytes)`

**Critical detail:** The ZMQ client creates a fresh socket per `send_url_alert()` call and closes it after. This avoids EFSM state corruption but adds connection overhead.

### Pattern 2: WebSocket Bidirectional (Link 2)
**What:** Chrome Extension connects to Desktop App's WebSocket server, sends JSON messages, receives JSON responses.
**Current flow (from codebase):**
1. `extension_server.py:ExtensionServer` tries ports `[8080, 8181, 8282, 8383, 8484]` using `websockets.serve()`
2. Chrome Extension `ConnectionService.js:connect()` tries same ports in same order, with 2-second timeout per port
3. Extension sends `{type: "ping"}`, Desktop responds `{type: "pong", status: "ok"}`
4. Extension sends `{type: "url_check", url: "...", trackers: [], iframes: []}`, Desktop forwards to ZMQ backend

### Pattern 3: Backend Token Validation
**What:** Every alert sent to port 50001 goes through token validation before processing.
**Current flow:**
1. `RealTimeAlertListener` first checks `MessageType` field -- routes `RequestToken`, `RegisterDevice`, `RefreshToken`
2. For alerts (no `MessageType`), validates `Token` field against `TokenStore`
3. `TokenStore` is in-memory `ConcurrentDictionary<string, DeviceToken>` keyed by `DeviceUid`
4. Tokens are created via `RequestToken` or `RegisterDevice` messages
5. The hardcoded `"12345678-1234-1234-1234-123456789012"` in `zmq_client.py` will **always fail validation** because it was never created via `TokenStore.CreateToken()`

### Anti-Patterns to Avoid
- **Sending alerts without first requesting a token:** The backend will return `InvalidToken`. Must send a `RequestToken` or `RegisterDevice` message first to get a valid token from `TokenStore`.
- **Testing with CURVE enabled when client has no CURVE keys:** The connection will silently fail (no error, just timeout). CurveMQ MUST be disabled for Phase 1.
- **Using the same ZMQ REQ socket after a timeout:** If `recv()` times out (zmq.Again), the socket's internal FSM is in "waiting for reply" state. The next `send()` will raise EFSM error. Must close and recreate the socket. (Current code already does this by creating fresh sockets per call.)
- **Testing URL scanning with cached URLs:** The extension has a 1-hour TTL cache that will mask pipeline failures. Always use fresh/unique URLs for diagnostic testing.

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Port listening verification | Custom TCP probe script | `netstat -an \| findstr :50001` or PowerShell `Get-NetTCPConnection` | OS-level verification is more authoritative than app-level |
| ZMQ REQ/REP test | New test harness | Existing `zmq_client.py` standalone `__main__` block | Already built, just needs token registration first |
| WebSocket test | New WebSocket client | Existing `extension_server.py` standalone `__main__` + browser console `new WebSocket('ws://localhost:8080')` | Validates real path |
| ZMQ socket state recovery | Custom FSM tracking | Fresh socket per request (already implemented in `ZMQClient`) | ZMQ Guide's recommended approach for REQ/REP |
| JSON message formatting | Manual string building | Existing `send_url_alert()` / `send_remote_access_alert()` | Already handles all required fields |

**Key insight:** Phase 1 is a diagnostic phase. The communication code already exists. The work is about verifying it works and adding diagnostic evidence, NOT building new communication infrastructure.

## Common Pitfalls

### Pitfall 1: CurveMQ Silent Handshake Failure
**What goes wrong:** Backend has `"CurveEnabled": true` in `appsettings.json`. The `CurveKeyManager.ApplyServerCurve()` sets `socket.Options.CurveServer = true` on the REP socket. But the Python `zmq_client.py` does NOT set any CURVE client keys. The ZMQ connection will appear to succeed (`socket.connect()` returns without error in ZMQ) but `socket.send()` will hang until timeout because the CURVE handshake never completes.
**Why it happens:** ZMQ `connect()` is asynchronous and non-blocking -- it does not verify the handshake. The handshake happens on first message send. With CURVE mismatch, the handshake silently fails.
**How to avoid:** Set `"CurveEnabled": false` in `appsettings.json` and restart the backend BEFORE running any Phase 1 diagnostics. Re-enable in Phase 4.
**Warning signs:** ZMQ `send()` succeeds but `recv()` times out with `zmq.Again` after 5000ms. No error message on either side.
**Confidence:** HIGH -- verified by reading `CurveKeyManager.cs` line 24 (`_curveEnabled = configuration.GetValue<bool>("Security:CurveEnabled", true)`) and `appsettings.json` line 40 (`"CurveEnabled": true`), cross-referenced with `zmq_client.py` which has zero CURVE-related code.

### Pitfall 2: Hardcoded Token Not in TokenStore
**What goes wrong:** `zmq_client.py` line 183 uses hardcoded token `"12345678-1234-1234-1234-123456789012"`. Backend `TokenStore.ValidateToken()` checks against an in-memory dictionary. This token was never created via `CreateToken()`, so validation returns `InvalidToken`.
**Why it happens:** The token system requires a device registration flow: send `{MessageType: "RequestToken", DeviceUid: "PC-JOHN-001"}` first, get back a real token, then use that token in subsequent alerts.
**How to avoid:** Before sending a test alert, send a `RequestToken` message (or `RegisterDevice` if device isn't in the database). Use the returned token in subsequent alerts.
**Warning signs:** Backend responds with `{"status": "InvalidToken", "message": "Token is invalid. Please authenticate."}` -- this is actually a SUCCESS for Phase 1 diagnostics because it proves the REQ/REP round-trip works!
**Confidence:** HIGH -- verified by reading `TokenStore.cs` lines 66-72 (checks `ConcurrentDictionary`) and `zmq_client.py` lines 182-183 (hardcoded value).

### Pitfall 3: Device Not Found in ASView
**What goes wrong:** Even with a valid token, if the `DeviceUid` (e.g., `"PC-TEST-001"`) doesn't exist in the database's UserDevice table, the backend throws `DomainException("DeviceNotFound")`.
**Why it happens:** `ASView` loads user devices from the database at startup. Test device IDs must match actual database records.
**How to avoid:** Either use a `DeviceUid` that exists in the database, or use the `RegisterDevice` message type to register a new device first (requires a valid user email in the database).
**Warning signs:** Backend responds with `{"success": false, "message": "DeviceNotFound"}`.
**Confidence:** HIGH -- verified from `RealTimeAlertListener.cs` lines 463-470.

### Pitfall 4: ZMQ REQ Socket EFSM Corruption
**What goes wrong:** If a ZMQ REQ socket sends a message and the recv times out, the socket is stuck in "waiting for reply" state. Any subsequent `send()` call raises `zmq.error.ZMQError: Operation cannot be accomplished in current state`.
**Why it happens:** REQ/REP enforces strict send/recv/send/recv alternation. A timeout breaks this pattern.
**How to avoid:** The current `zmq_client.py` already avoids this by creating a fresh Context+Socket per call in `send_url_alert()` and `send_remote_access_alert()`. Maintain this pattern. For standalone testing, always create a new socket if the previous one timed out.
**Warning signs:** `zmq.error.ZMQError` with errno 156384763 (EFSM).
**Confidence:** HIGH -- this is well-documented ZMQ behavior, confirmed by ZMQ Guide Chapter 4.

### Pitfall 5: WebSocket Port Conflict
**What goes wrong:** Another application is already listening on port 8080 (common for web servers, proxies). The extension server falls back to 8181, 8282, etc. But the Chrome Extension tries ports in order starting from its saved port in `chrome.storage.local`.
**Why it happens:** Port 8080 is commonly used. If the Desktop App started on 8181 but the Extension has 8080 saved from a previous session, it will fail to connect.
**How to avoid:** Clear the Extension's saved port (`chrome.storage.local.remove(['connectedPort'])`) before diagnostic testing, so it does a full port scan. Check which port the Desktop App actually bound to from its startup log: `[EXT-SERVER] Successfully started on port XXXX`.
**Warning signs:** Extension console shows `[ConnectionService] Could not connect to desktop app`. Desktop App log shows `[EXT-SERVER] Successfully started on port 8181` (not 8080).
**Confidence:** HIGH -- verified from `extension_server.py` lines 93-112 and `ConnectionService.js` lines 43-69.

### Pitfall 6: asyncio Event Loop Not Running for Extension Handler
**What goes wrong:** The `ExtensionHandler._handle_url_check()` calls `self.scan_service.check_url()` which calls `self.zmq_client.send_url_alert()`. The ZMQ client is synchronous (blocking). If called from an async context, it blocks the entire event loop, preventing other WebSocket messages from being processed.
**Why it happens:** The `extension_handler.py:handle_message()` is `async def` but the handlers it calls (`_handle_url_check`, etc.) are synchronous. The ZMQ send/recv in `zmq_client.py` uses blocking calls with a 5-second timeout.
**How to avoid:** For Phase 1 diagnostics, this is acceptable -- we just need to verify the round-trip works. The 5-second timeout is short enough. But be aware that during a ZMQ call, the WebSocket server cannot process other messages.
**Warning signs:** Extension shows "connected" but messages seem delayed or unresponsive during URL scans.
**Confidence:** HIGH -- verified from code: `extension_handler.py` line 22 (`async def handle_message`) calls sync methods, and `zmq_client.py` uses blocking `socket.send()`/`socket.recv()`.

## Code Examples

Verified patterns from direct codebase reading:

### Diagnostic Test 1: Verify Backend Ports are Listening (PowerShell)
```powershell
# Source: Windows built-in commands
# Run BEFORE starting any ZMQ connections
Get-NetTCPConnection -State Listen | Where-Object {$_.LocalPort -in @(50001, 50002, 5555, 5556)} | Format-Table LocalAddress, LocalPort, OwningProcess
```

### Diagnostic Test 2: ZMQ REQ/REP Round-Trip with Token Request
```python
# Source: Adapted from zmq_client.py standalone test + RealTimeAlertListener.cs message routing
# This tests the MessageType routing, NOT alert processing
import zmq
import json

context = zmq.Context()
socket = context.socket(zmq.REQ)
socket.setsockopt(zmq.RCVTIMEO, 5000)
socket.setsockopt(zmq.LINGER, 0)
socket.connect("tcp://127.0.0.1:50001")

# Step 1: Request a token (tests round-trip without needing a valid token)
message = {
    "MessageType": "RequestToken",
    "DeviceUid": "PC-TEST-DIAG-001"
}
socket.send(json.dumps(message).encode('utf-8'))
response = json.loads(socket.recv().decode('utf-8'))
print(f"RequestToken response: {json.dumps(response, indent=2)}")
# Expected: {"status": "DeviceNotRecognized", "deviceUid": "PC-TEST-DIAG-001"}
# This PROVES the REQ/REP round-trip works even if device isn't registered!

socket.close()
context.term()
```

### Diagnostic Test 3: WebSocket Ping/Pong from Browser Console
```javascript
// Source: Adapted from ConnectionService.js connection flow
// Run in Chrome DevTools console (F12) on any page
const ws = new WebSocket('ws://localhost:8080');
ws.onopen = () => {
  console.log('Connected!');
  ws.send(JSON.stringify({ type: 'ping' }));
};
ws.onmessage = (event) => {
  console.log('Response:', JSON.parse(event.data));
  // Expected: {"type": "pong", "status": "ok"}
  ws.close();
};
ws.onerror = (e) => console.error('Error:', e);
ws.onclose = () => console.log('Closed');
```

### Diagnostic Test 4: Timestamp-Annotated Logging Pattern
```python
# Source: Pattern for diagnostic logging required by Phase 1 success criteria
import datetime

def diag_log(component: str, direction: str, message: str, data: dict = None):
    """Diagnostic log with ISO timestamp for Phase 1 verification"""
    ts = datetime.datetime.utcnow().isoformat() + "Z"
    prefix = ">>>" if direction == "SEND" else "<<<"
    print(f"[{ts}] [{component}] {prefix} {message}")
    if data:
        import json
        print(f"[{ts}] [{component}]     {json.dumps(data, indent=2)}")
```

### Diagnostic Test 5: Existing Standalone Tests
```bash
# ZMQ REQ/REP test (existing)
# Source: zmq_client.py __main__ block (line 303-348)
cd c:\Users\judaz\OneDrive\Desktop\asps\apps\desktop\win\src
python zmq_client.py localhost

# Extension WebSocket server test (existing)
# Source: extension_server.py __main__ block (line 175-201)
python extension_server.py
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Single global ZMQ socket | Fresh socket per call | Already in codebase | Avoids EFSM corruption |
| Chrome Extension MV2 persistent background | MV3 Service Worker (30s idle timeout) | Manifest V3 | Requires keepalive/alarms for WebSocket |
| NetMQ 3.x | NetMQ 4.0.1.13 | Already in codebase | ZMTP 3.x protocol, CURVE support |
| pyzmq sync only | pyzmq >=25.1 with asyncio support | Already available | Could use `zmq.asyncio` but current code uses sync |

**Deprecated/outdated:**
- None relevant to Phase 1. All libraries are current versions.

## Critical Codebase Facts for Planning

### Backend Port Map (from Program.cs and appsettings.json)
| Port | Socket Type | Component | Purpose |
|------|-------------|-----------|---------|
| 50001 | REP (ResponseSocket) | RealTimeAlertListener | Receives alerts, returns responses |
| 50002 | PUB (PublisherSocket) | NotificationPublisher | Publishes analysis results |
| 5555 | REP (ResponseSocket) | NetMQMessageProcessor | CQRS command/query processing |
| 5556 | REP (ResponseSocket) | CQRSGateway | WebApi CQRS bridge |

### WebSocket Port List (from config.py)
| Priority | Port | Fallback Order |
|----------|------|----------------|
| 1 | 8080 | First tried |
| 2 | 8181 | If 8080 busy |
| 3 | 8282 | If 8181 busy |
| 4 | 8383 | If 8282 busy |
| 5 | 8484 | Last resort |

### Token Flow (from RealTimeAlertListener.cs)
```
Desktop App                          Backend (port 50001)
    |                                     |
    |-- {MessageType: "RequestToken",  -->|
    |    DeviceUid: "PC-JOHN-001"}        |
    |                                     |-- Looks up device in ASView
    |                                     |-- If found: creates token in TokenStore
    |                                     |-- If not found: returns DeviceNotRecognized
    |<-- {status: "TokenCreated",      ---|
    |     token: "abc123...",             |
    |     expiration: "2026-..."}         |
    |                                     |
    |-- {AlertType: "UrlAlert",        -->|
    |    Token: "abc123...",              |
    |    Url: "https://example.com",...}  |-- Validates token
    |                                     |-- Processes alert
    |<-- {success: true,               ---|
    |     message: "Alert processed",     |
    |     alertType: "UrlAlert",...}       |
```

### Message Types Handled by RealTimeAlertListener
| MessageType Field | Handler | Required Fields |
|-------------------|---------|-----------------|
| `"RequestToken"` | `HandleRequestToken` | `DeviceUid` |
| `"RegisterDevice"` | `HandleRegisterDevice` | `DeviceUid`, `Email` |
| `"RefreshToken"` | `HandleRefreshToken` | `DeviceUid`, `Token` |
| _(none/absent)_ | `ProcessAlertAsync` | `AlertType`, `Token`, `DeviceInfo.DeviceUid` |

### CurveMQ Status (CRITICAL for Phase 1)
- **appsettings.json:** `"CurveEnabled": true` (line 40)
- **CurveKeyManager.cs:** Reads this config, applies CURVE to REP and PUB sockets
- **zmq_client.py:** NO CURVE implementation (zero references to curve, zmq.CURVE*, or key files)
- **Decision from STATE.md:** "CurveMQ temporarily disabled during Phases 1-3"
- **Action required:** Change `"CurveEnabled": false` in appsettings.json before Phase 1 testing

## Open Questions

Things that could not be fully resolved:

1. **Does the database have a user and device matching the test DeviceUid?**
   - What we know: `ASView` loads from MySQL database at startup. DeviceUid must match a record.
   - What's unclear: What DeviceUids actually exist in the local database. The default in `main.py` is `"PC-JOHN-001"`.
   - Recommendation: In Plan 01-01, first try `RequestToken` with `"PC-JOHN-001"`. If it returns `DeviceNotRecognized`, try `RegisterDevice` with a known email. If that also fails, the database may need a user row -- document the finding and the planner can address it.

2. **Is the backend MySQL database accessible and populated?**
   - What we know: Connection string points to `127.0.0.1:3306`, database `ASPSBackend2DB`, user `root`.
   - What's unclear: Whether MySQL is running and the database has the required tables and data.
   - Recommendation: Plan 01-01 should include a "pre-flight" check: verify MySQL is accessible before starting the Backend. Backend startup logs will show DB errors if connection fails.

3. **Which asyncio event loop does the WebSocket server run on?**
   - What we know: `main.py` runs `asyncio.run(self.start())` in a background thread, while `tray_icon.run_blocking()` runs in the main thread. The WebSocket server runs on the background thread's event loop.
   - What's unclear: Whether cross-thread calls from ZMQ (synchronous) to the async WebSocket broadcast work correctly.
   - Recommendation: This is Phase 2's problem (async bridge). For Phase 1, just verify WebSocket connectivity independently -- don't test the ZMQ-to-WebSocket pipeline yet.

## Sources

### Primary (HIGH confidence)
- Direct codebase reading: `zmq_client.py`, `extension_server.py`, `RealTimeAlertListener.cs`, `TokenStore.cs`, `CurveKeyManager.cs`, `ConnectionService.js`, `background.js`, `appsettings.json`, `config.py`, `main.py`, `container.py`, `auth_manager.py`, `notification_handler.py`, `scan_service.py` (both Python and JS), `NotificationPublisher.cs`, `NetMQMessageProcessor.cs`, `CQRSGateway.cs`, `Program.cs`
- [ZMQ Guide Chapter 4 - Reliable Request-Reply Patterns](https://zguide.zeromq.org/docs/chapter4/) - EFSM recovery patterns
- [ZMQ Socket API](https://zeromq.org/socket-api/) - REQ/REP semantics
- [CurveZMQ RFC 26](https://rfc.zeromq.org/spec:26/CURVEZMQ) - CURVE handshake protocol

### Secondary (MEDIUM confidence)
- [Microsoft Test-NetConnection docs](https://learn.microsoft.com/en-us/powershell/module/nettcpip/test-netconnection) - Windows port verification
- [NetMQ-PyZMQ interoperability issues (GitHub)](https://github.com/NetMQ/Samples/issues/16) - Known interop problems
- [Chrome Extension MV3 WebSocket tutorial](https://developer.chrome.com/docs/extensions/mv3/tut_websockets/) - Service worker WebSocket lifecycle
- [pyzmq ZeroMQ Python docs](https://zeromq.org/languages/python/) - pyzmq usage

### Tertiary (LOW confidence)
- [Python ZMQ "Operation Cannot Be Accomplished" Guide](https://copyprogramming.com/howto/python-zmq-operation-cannot-be-accomplished-in-current-state) - Community article on EFSM recovery (verified against ZMQ Guide)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Direct reading of existing codebase; no new libraries needed
- Architecture: HIGH - Complete message flow traced through all source files
- Pitfalls: HIGH - All pitfalls verified by cross-referencing multiple source files in the codebase
- Diagnostics: HIGH - Windows port verification tools are well-documented by Microsoft
- Token/Auth flow: HIGH - Traced through TokenStore.cs, RealTimeAlertListener.cs, and zmq_client.py

**Research date:** 2026-02-12
**Valid until:** 2026-03-12 (stable -- no library upgrades expected during repair work)
