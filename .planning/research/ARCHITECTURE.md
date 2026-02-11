# ASPS Multi-Process Bridge Architecture Research

**Research Date:** 2026-02-12
**Dimension:** Architecture -- Multi-process communication repair
**Focus:** Desktop App bridge role, ZMQ + WebSocket integration, failure points

---

## 1. System Topology

The ASPS system is a four-process distributed architecture where the Desktop App serves as the critical bridge between the Chrome Extension (user-facing) and the Backend (analysis engine).

```
Chrome Extension  <--WebSocket-->  Desktop App  <--ZMQ REQ/REP-->  Backend (.NET)
  (JavaScript)                     (Python)     <--ZMQ PUB/SUB-->  (NetMQ)
                                                                     |
                                                                  External
                                                                  Analyzer
                                                                  (Python)
```

### Process Inventory

| Process | Technology | Role | Ports |
|---------|-----------|------|-------|
| Backend (ASPSBackend) | .NET / NetMQ | REP server (50001), PUB publisher (50002), CQRS REP (5555/5556) | 50001, 50002, 5555, 5556 |
| Desktop App | Python / pyzmq + websockets | REQ client (50001), SUB client (50002), WS server (8080-8484) | 8080-8484 |
| Chrome Extension | JavaScript (MV3 Service Worker) | WS client to Desktop App | N/A (client) |
| WebApi | ASP.NET Core | HTTP API + Admin Dashboard, CQRS client to Backend | 5001 |

### Port Map (Source-Verified)

- **50001**: Backend `RealTimeAlertListener` binds REP socket. Desktop App `ZMQClient` connects REQ socket.
- **50002**: Backend `NotificationPublisher` binds PUB socket. Desktop App `NotificationClient` connects SUB socket.
- **5555**: Backend `NetMQMessageProcessor` binds REP socket. (Internal CQRS, not used by Desktop App.)
- **5556**: Backend `CQRSGateway` binds REP socket. WebApi connects as client.
- **8080-8484**: Desktop App `ExtensionServer` tries ports sequentially. Chrome Extension `ConnectionService` scans the same range.

---

## 2. Desktop App Bridge Architecture

The Desktop App (`apps/desktop/win/src/main.py`) is the critical bridge. It runs three concurrent subsystems in a single Python process:

### 2.1 Subsystem Overview

```
                       Desktop App (Python)
                    +--------------------------+
                    |                          |
  Extension <--WS-->| ExtensionServer (async)  |
                    |    port 8080-8484        |
                    |         |                |
                    |    ExtensionHandler      |
                    |         |                |
                    |    ScanService           |
                    |         |                |
                    |    ZMQClient (sync)      |---REQ---> Backend:50001
                    |                          |
                    |  NotificationClient      |---SUB---> Backend:50002
                    |    (background thread)   |
                    |         |                |
                    |  NotificationHandler     |
                    |         |                |
                    |    ExtensionServer       |---WS broadcast--> Extension
                    |         .broadcast()     |
                    +--------------------------+
```

### 2.2 Threading Model

**Source:** `main.py` lines 203-213, `notification_client.py` lines 70-78

- **Main thread**: System tray icon (`tray_icon.run_blocking()`) -- blocks in pystray event loop.
- **Async thread**: `asyncio.run(self.start())` runs the entire async subsystem including WebSocket server, monitor tasks.
- **Notification thread**: `NotificationClient._listen()` runs a blocking ZMQ SUB recv loop in a daemon thread.

**Critical implication**: The ZMQ SUB listener runs in a synchronous thread, but the WebSocket broadcast is async. The `NotificationHandler.handle()` method (called from the SUB thread) needs to bridge back to the async event loop to call `ExtensionServer.broadcast()`. This is done via `asyncio.get_running_loop().create_task()` with a fallback to `asyncio.run()`.

**Source:** `notification_handler.py` lines 66-72:
```python
try:
    loop = asyncio.get_running_loop()
    loop.create_task(self._broadcast_to_extension(analysis, cache_data))
except RuntimeError:
    # No running loop - run in new loop
    asyncio.run(self._broadcast_to_extension(analysis, cache_data))
```

### 2.3 Dependency Injection

**Source:** `core/container.py`

The `Container` class provides lazy-loaded singleton components:
- `zmq_client` -> `ZMQClient(BACKEND_HOST, BACKEND_REQ_PORT)` -- connects to 127.0.0.1:50001
- `notification_client` -> `NotificationClient(device_id, BACKEND_HOST, BACKEND_SUB_PORT)` -- connects to 127.0.0.1:50002
- `extension_server` -> `ExtensionServer()` -- binds to localhost:8080-8484
- `scan_service` -> depends on zmq_client, cache, auth_manager, etc.
- `extension_handler` -> depends on scan_service
- `notification_handler` -> depends on protection_service, cache

---

## 3. Message Flow Analysis

### 3.1 URL Check Flow (Happy Path)

```
Step 1: Extension -> Desktop App (WebSocket)
  Message: { type: "url_check", url: "https://...", trackers: [...], iframes: [...] }
  Handler: ExtensionServer._handle_client -> ExtensionHandler._handle_url_check

Step 2: Desktop App -> Backend (ZMQ REQ)
  Message: { AlertType: "UrlAlert", DeviceInfo: {...}, Url: "...", Token: "...", ... }
  Handler: ScanService.check_url -> ZMQClient.send_url_alert
  Note: ZMQClient.send_url_alert() calls connect(), send_alert(), close() on EACH request

Step 3: Backend processes alert
  Handler: RealTimeAlertListener.ProcessAlertAsync -> UDAnalysisManager.Handle
  Returns REP: { success: true, message: "Alert processed successfully", ... }

Step 4: Desktop App receives REQ/REP response
  Handler: ScanService._process_response
  Returns to Extension: { type: "url_result", analyzing: true, message: "Analysis in progress" }

Step 5: Backend publishes analysis result (async, after analysis completes)
  Publisher: NotificationPublisher.PublishAnalysisResult
  Topic: "device:PC-JOHN-001"
  Message: { Type: "AnalysisResult", DeviceUid: "...", Data: { AlertType, RiskAssessment, ... } }

Step 6: Desktop App receives PUB/SUB notification
  Handler: NotificationClient._listen -> NotificationHandler.handle
  Extracts: URL, risk_score, risk_assessment, protective_actions

Step 7: Desktop App -> Extension (WebSocket broadcast)
  Message: { type: "url_result", url: "...", score: 85, riskType: [...], protectiveAction: 4 }
  Handler: NotificationHandler._broadcast_to_extension -> ExtensionServer.broadcast
```

### 3.2 Remote Access Alert Flow

```
Step 1: MonitorService._monitor_remote_access detects state change
Step 2: MonitorService._handle_new_session -> ZMQClient.send_remote_access_alert (via asyncio.to_thread)
Step 3: Backend REP returns ACK
Step 4: Backend PUB publishes analysis result
Step 5: NotificationClient receives, NotificationHandler processes
Step 6: MonitorService also directly broadcasts to Extension:
  Message: { type: "remote_access_alert", toolId: 1, direction: "incoming", ... }
```

**Key difference**: Remote access alerts are broadcast to the extension BOTH directly by MonitorService (immediate) AND later via the PUB/SUB notification pipeline (with analysis results).

---

## 4. ZMQ Socket State Machine Issues

### 4.1 REQ/REP Strict Alternation

ZMQ REQ/REP sockets enforce a strict send-recv-send-recv alternation. Breaking this sequence puts the socket in an unrecoverable error state.

**Current ASPS pattern (ZMQClient):**

**Source:** `zmq_client.py` lines 206-212:
```python
if not self.connect():
    return None
try:
    return self.send_alert(alert)
finally:
    self.close()
```

**Observation**: The `ZMQClient.send_url_alert()` and `send_remote_access_alert()` methods create a NEW connection for every single request (connect-send-recv-close). This is the safest pattern for REQ/REP because it avoids state machine corruption from timeouts.

**However, there are failure scenarios:**

#### Failure Point 1: REQ timeout without REP response

**Source:** `zmq_client.py` lines 63-64, 148-150:
```python
self.socket.setsockopt(zmq.RCVTIMEO, self.timeout)  # 5000ms
# ...
except zmq.Again:
    print(f"[ZMQ] WARNING: Timeout: No response after {self.timeout}ms")
    return None
```

When `zmq.Again` fires (5-second timeout), the REQ socket has sent but not received. The socket is now in an invalid state. However, because `send_url_alert()` always calls `self.close()` in the `finally` block, this socket is destroyed and a fresh one is created next time. **This is safe but wastes time on the 5-second timeout.**

#### Failure Point 2: Backend REP socket crash after receiving

If the backend crashes after `ReceiveFrameBytes()` but before `SendFrame()`, the Desktop App's REQ socket will hang until timeout. The backend's REP socket (upon restart) will also be in an inconsistent state until the old connection is cleaned up.

**Source:** `RealTimeAlertListener.cs` lines 101-165:
```csharp
messageBytes = _repSocket!.ReceiveFrameBytes();
// ... process ...
SendResponse(result);  // If this never executes, REP state is broken
```

The backend wraps everything in try/catch and always sends a response even on error (lines 153-163), which is good practice. However, unhandled exceptions or process crashes between recv and send will corrupt the REP state.

#### Failure Point 3: Concurrent REQ access

**Source:** `monitor_service.py` uses `asyncio.to_thread()` to call ZMQ methods:
```python
await asyncio.to_thread(
    self.zmq_client.send_remote_access_alert, ...
)
```

And `scan_service.py` calls ZMQ synchronously:
```python
response = self.zmq_client.send_url_alert(...)
```

Both use the SAME `zmq_client` instance from the Container. However, since `send_url_alert` and `send_remote_access_alert` each create a new socket (connect-send-recv-close), concurrent calls will create separate sockets. **This works for the REQ pattern but creates a race condition on the `zmq.Context` -- pyzmq contexts are thread-safe, so this is actually fine.**

BUT: If two concurrent calls overlap during the `connect()` -> `close()` lifecycle, the instance variables `self.socket` and `self.context` could be overwritten. The `connect()` method at line 61 sets `self.context = zmq.Context()` and `self.socket = ...` -- if two threads call connect() simultaneously, one thread's socket reference gets overwritten.

**Verdict**: The current pattern is mostly safe due to connect-per-request, but has a theoretical thread-safety issue in `ZMQClient.connect()` / `close()` with shared instance variables.

### 4.2 PUB/SUB Subscription Issues

#### Subscription Topic Matching

**Backend publishes (NotificationPublisher.cs lines 82-86):**
```csharp
var deviceTopic = $"device:{deviceUid}";
_publisherSocket.SendMoreFrame(deviceTopic).SendFrame(json);
```

**Desktop App subscribes (notification_client.py lines 95-96):**
```python
topic = f"device:{self.device_uid}"
self.socket.subscribe(topic.encode('utf-8'))
```

**Potential Issue**: ZMQ PUB/SUB topic matching is prefix-based. A subscription to `device:PC-JOHN` will match `device:PC-JOHN-001` AND `device:PC-JOHN-002`. This is actually beneficial in this case since each device has a unique UID, but it could be a bug source if device UIDs share prefixes.

#### Subscription Race Condition (Slow Subscriber)

ZMQ PUB/SUB has a well-known "slow subscriber" problem. When a SUB socket connects, there is a brief window before the subscription takes effect on the PUB side. Any messages published during this window are lost.

**In ASPS**: The Desktop App starts the notification client AFTER the WebSocket server:

**Source:** `main.py` lines 149-155:
```python
# Start Notification Client
notification_thread = threading.Thread(
    target=self._start_notification_client,
    daemon=True
)
notification_thread.start()
```

If a URL check is sent via REQ/REP BEFORE the SUB subscription has propagated, the analysis result notification will be lost. The python test client handles this with `time.sleep(0.5)` between starting the listener and sending the alert, but the Desktop App does not have this delay.

#### Multipart Message Receive

**Source:** `notification_client.py` lines 108-113:
```python
topic_bytes = self.socket.recv()
message_bytes = self.socket.recv()
```

The notification client receives topic and message as two separate `recv()` calls. This matches the backend's `SendMoreFrame(topic).SendFrame(json)` pattern. However, if only the topic frame arrives and the message frame is delayed or lost, the client will block on the second `recv()` until the RCVTIMEO (5 seconds) expires, at which point it catches `zmq.Again` and continues.

**Critical Issue**: After a timeout on the second `recv()`, the next `recv()` call might get the delayed message frame instead of a new topic frame, causing a deserialization mismatch. The SUB socket should use `recv_multipart()` instead of two separate `recv()` calls to ensure atomic multipart message reception.

---

## 5. WebSocket Bridge Failure Points

### 5.1 Port Discovery

**Extension side (ConnectionService.js lines 43-68):**
```javascript
async connect() {
    const savedPort = await this.getSavedPort();
    if (savedPort) {
        const result = await this.tryPort(savedPort);
        if (result) { ... }
    }
    for (const port of this.config.ports) {
        const result = await this.tryPort(port);
        if (result) { ... }
    }
}
```

**Desktop side (extension_server.py lines 114-122):**
```python
async def start(self) -> bool:
    for port in EXTENSION_PORTS:
        if await self._try_port(port):
            self._running = True
            return True
```

Both sides share the same port list `[8080, 8181, 8282, 8383, 8484]`. The Desktop App takes the first available port; the Extension tries saved port first, then scans all. **This can fail if another application occupies 8080 between Desktop App start and Extension connection**, causing them to bind different ports on subsequent restarts.

### 5.2 Message Format Translation

The WebSocket messages use a different schema than ZMQ messages. The Desktop App must translate between them.

**Extension sends** (ScanService.js):
```json
{ "type": "url_check", "url": "...", "trackers": [...], "iframes": [...] }
```

**Desktop App converts to ZMQ format** (zmq_client.py):
```json
{
    "AlertType": "UrlAlert",
    "DeviceInfo": { "DeviceUid": "PC-JOHN-001", ... },
    "Url": "...",
    "Token": "12345678-...",
    "Trackers": [...],
    "IFrameDomains": [...]
}
```

**Translation points that can break:**
1. Field name mismatch: Extension uses `iframes`, ZMQ expects `IFrameDomains`
2. Token injection: Desktop App adds a hardcoded UUID token if none exists (zmq_client.py line 183)
3. DeviceInfo injection: Extension has no concept of DeviceInfo; Desktop App adds it
4. Type field: Extension uses lowercase `type`, backend uses PascalCase `AlertType`

### 5.3 Async Response Bridging

The URL check flow has a critical async gap:

1. Extension sends `url_check` via WebSocket
2. Desktop App sends ZMQ REQ, gets synchronous ACK (not the analysis result)
3. Desktop App returns `{ analyzing: true }` to Extension immediately
4. Analysis result arrives later via ZMQ PUB/SUB
5. Desktop App must broadcast the result to the Extension

**The problem**: Between steps 3 and 5, there is no correlation ID linking the original WebSocket request to the PUB/SUB notification. The Desktop App uses `ScanService._pending_urls` (a class-level dict) to track which URLs are awaiting results, and `NotificationHandler` extracts the URL from the notification to match it.

**Source:** `notification_handler.py` lines 95-134:
```python
def _extract_analysis(self, data):
    # Try multiple fallback paths to find the URL
    url = analysis_result.get('Url')           # Primary
    url = analyzer_results.get('Url')          # Fallback 1
    url = ScanService.get_pending_url()        # Fallback 2 (most recent pending)
```

If the notification does not contain the URL (possible if the backend serialization changes), the fallback to `ScanService.get_pending_url()` returns the most recent pending URL, which could be wrong if multiple scans are in flight.

### 5.4 Connection Health Monitoring

**Extension side**: Three-layer health monitoring:
- **Ping**: Every 30s, sends `{ type: "ping" }` to Desktop App
- **Keepalive**: Every 20s, sends `{ type: "keepalive" }` to keep MV3 service worker alive
- **Heartbeat**: Every 10s, sends `{ type: "heartbeat_ping" }`, expects `heartbeat_pong`. After 3 missed pongs (30s), declares connection dead.

**Desktop side**: No active health monitoring of the ZMQ connections. ZMQ REQ uses a 5-second timeout per request. ZMQ SUB has a 5-second recv timeout with heartbeat logging every 2 minutes.

**Gap**: If the Backend dies, the Desktop App will only notice on the NEXT ZMQ REQ attempt (5s timeout) or when the SUB heartbeat logs "still listening" without ever receiving messages. There is no proactive health check of the ZMQ connections.

---

## 6. CurveMQ Encryption Analysis

### 6.1 Backend Implementation

**Source:** `CurveKeyManager.cs`

The backend supports optional CurveMQ encryption:
- Configuration: `Security:CurveEnabled` (default: `true`)
- Keys: Loaded from `appsettings.json` (`Security:ServerPublicKey`, `Security:ServerSecretKey`) as Base64
- Key format: Also stored/exposed as Z85 (`Security:ServerPublicKeyZ85`)
- Applied to both REP (50001) and PUB (50002) sockets via `ApplyServerCurve()`

**Source:** `CurveKeyManager.cs` lines 41-49:
```csharp
public void ApplyServerCurve(NetMQSocket socket)
{
    if (!_curveEnabled || ServerSecretKey.Length == 0)
        return;
    socket.Options.CurveServer = true;
    socket.Options.CurveCertificate = new NetMQCertificate(ServerSecretKey, ServerPublicKey);
}
```

### 6.2 Desktop App Implementation

**CRITICAL FINDING**: The Desktop App's `ZMQClient` and `NotificationClient` have **NO CurveMQ implementation**.

**Source:** `zmq_client.py` lines 59-70:
```python
def connect(self) -> bool:
    self.context = zmq.Context()
    self.socket = self.context.socket(zmq.REQ)
    self.socket.setsockopt(zmq.RCVTIMEO, self.timeout)
    self.socket.connect(f"tcp://{self.host}:{self.port}")
```

**Source:** `notification_client.py` lines 86-92:
```python
self.context = zmq.Context()
self.socket = self.context.socket(zmq.SUB)
self.socket.setsockopt(zmq.RCVTIMEO, 5000)
self.socket.connect(f"tcp://{self.host}:{self.port}")
```

Neither client sets `zmq.CURVE_SERVERKEY`, `zmq.CURVE_PUBLICKEY`, or `zmq.CURVE_SECRETKEY`. This means:

- **If CurveMQ is enabled on the backend**: The Desktop App CANNOT connect. The backend will reject unencrypted connections silently (ZMQ drops frames from unauthenticated peers without error).
- **If CurveMQ is disabled on the backend**: Communication works but is unencrypted.

### 6.3 NetMQ vs pyzmq CurveMQ Interop

For CurveMQ to work between NetMQ (.NET) and pyzmq (Python):

**Backend (NetMQ server) needs:**
```csharp
socket.Options.CurveServer = true;
socket.Options.CurveCertificate = new NetMQCertificate(secretKey, publicKey);
```

**Desktop App (pyzmq client) needs:**
```python
# Generate client keypair
client_public, client_secret = zmq.curve_keypair()

# Set client CURVE options
socket.setsockopt(zmq.CURVE_SERVERKEY, server_public_key_binary)
socket.setsockopt(zmq.CURVE_PUBLICKEY, client_public)
socket.setsockopt(zmq.CURVE_SECRETKEY, client_secret)
```

**Key format considerations:**
- NetMQ uses raw 32-byte binary keys
- pyzmq accepts both Z85-encoded (40-char string) and raw binary
- The backend's `CurveKeyManager` stores keys as Base64 and Z85
- The Desktop App would need the server's public key in Z85 or binary format
- The `RequestToken` and `RegisterDevice` responses from the backend include `serverPublicKey` (Z85 format), which could be used for this purpose

### 6.4 CurveMQ Integration Path

The backend already returns the server public key in Z85 format in token responses:

**Source:** `RealTimeAlertListener.cs` lines 256-264:
```csharp
return new
{
    status = "TokenCreated",
    token = newToken.TokenValue,
    serverPublicKey = _curveKeyManager?.ServerPublicKeyZ85 ?? string.Empty
};
```

The intended flow appears to be:
1. Desktop App connects unencrypted initially
2. Sends `RegisterDevice` or `RequestToken` message
3. Receives server public key in response
4. Reconnects with CURVE enabled using the server public key

**Problem**: This bootstrap requires an initial unencrypted connection, which defeats the purpose if CurveMQ is enforced. The server public key would need to be distributed out-of-band (e.g., in the Desktop App's configuration file).

---

## 7. Identified Failure Points Summary

### 7.1 Bridge Failures (Desktop App)

| ID | Failure | Severity | Source Location |
|----|---------|----------|-----------------|
| B1 | Thread-to-async bridging in NotificationHandler can fail if no running event loop | High | `notification_handler.py:66-72` |
| B2 | ZMQClient instance variables not thread-safe for concurrent connect/close | Medium | `zmq_client.py:53-54, 59-70` |
| B3 | No correlation ID between WS request and PUB/SUB response | High | `notification_handler.py:95-134` |
| B4 | Pending URL fallback can match wrong URL under concurrent scans | Medium | `scan_service.py:59-65` |
| B5 | Port mismatch if external app occupies port between restarts | Low | `extension_server.py:114-122` |

### 7.2 ZMQ Socket State Machine Failures

| ID | Failure | Severity | Source Location |
|----|---------|----------|-----------------|
| Z1 | REQ socket state corruption on timeout (mitigated by connect-per-request) | Low | `zmq_client.py:148-150` |
| Z2 | PUB/SUB slow subscriber race -- subscription may not be active when first alert sent | High | `main.py:149-155` |
| Z3 | Non-atomic multipart receive can cause frame misalignment | High | `notification_client.py:108-113` |
| Z4 | No ZMQ connection health monitoring | Medium | `zmq_client.py` (entire file) |
| Z5 | Backend REP state corruption if process crashes between recv and send | Medium | `RealTimeAlertListener.cs:110-148` |

### 7.3 CurveMQ Failures

| ID | Failure | Severity | Source Location |
|----|---------|----------|-----------------|
| C1 | Desktop App has ZERO CurveMQ client code -- cannot connect if CURVE enabled | Critical | `zmq_client.py`, `notification_client.py` |
| C2 | No key distribution mechanism for server public key to Desktop App | High | N/A (missing) |
| C3 | Token handshake assumes unencrypted initial connection | Medium | `RealTimeAlertListener.cs:217-265` |

### 7.4 WebSocket Failures

| ID | Failure | Severity | Source Location |
|----|---------|----------|-----------------|
| W1 | Message format translation has no schema validation | Medium | `extension_handler.py`, `scan_service.py` |
| W2 | Async response gap: 5-30s between ACK and analysis result with no progress indication | Medium | `scan_service.py:153-164` |
| W3 | Extension 30s scan timeout may fire before PUB/SUB result arrives | Medium | `background.js:491-505`, `ScanService.js:73-83` |

---

## 8. Backend Processing Pipeline Detail

For debugging reference, the backend processes alerts through this pipeline:

```
RealTimeAlertListener.ListenForAlerts()
  -> ReceiveFrameBytes() on REP socket
  -> RouteMessageAsync()
     -> ProcessAlertAsync()
        -> Deserialize to UrlAlert or RemoteAccessAlert
        -> TokenStore.ValidateToken()
        -> ASView.FindUserDeviceByDeviceUid()
        -> UserDomainManagerService.GetManagerForDeviceAsync()
        -> UDAnalysisManager.Handle(DeviceAlertReceived)
           -> Creates UDAnalysis / UDPhishingAnalyzer
           -> Runs indicators (async)
           -> Fires AnalysisResultReceived event
        -> AlertPersistenceActor.Handle() -- saves to DB
        -> NotificationPublisherActor.Handle()
           -> NotificationPublisher.PublishAnalysisResult()
              -> PUB socket: SendMoreFrame(topic).SendFrame(json)
  -> SendResponse(result) on REP socket
```

**Important**: The REP response is sent BEFORE analysis completes. The analysis runs asynchronously via the domain event system. The PUB notification arrives later (milliseconds to seconds, depending on analyzer complexity).

---

## 9. Configuration Cross-Reference

### Backend Configuration (appsettings.json)

```json
{
    "NetMQ": {
        "RealTimeListenerPort": 50001,
        "RealTimeListenerMode": "Rep",
        "NotificationPublisherPort": 50002
    },
    "Security": {
        "CurveEnabled": true,
        "ServerPublicKey": "<base64>",
        "ServerSecretKey": "<base64>",
        "ServerPublicKeyZ85": "<z85>"
    }
}
```

### Desktop App Configuration (config.py)

```python
BACKEND_HOST = "127.0.0.1"
BACKEND_REQ_PORT = 50001
BACKEND_SUB_PORT = 50002
EXTENSION_PORTS = [8080, 8181, 8282, 8383, 8484]
```

### Chrome Extension Configuration (ConnectionService.js)

```javascript
this.config = {
    ports: [8080, 8181, 8282, 8383, 8484],
    reconnectDelay: 5000,
    connectionTimeout: 2000,
    heartbeatInterval: 10000,
    maxMissedHeartbeats: 3
};
```

---

## 10. Quality Gate Verification

- [x] **Bridge architecture failure points clearly identified** -- See Section 7.1 (B1-B5)
- [x] **ZMQ socket state machine documented** -- See Section 4 (REQ/REP alternation, timeout handling, concurrent access)
- [x] **PUB/SUB subscription issues covered** -- See Section 4.2 (topic matching, slow subscriber, multipart receive)
- [x] **CurveMQ interop between NetMQ and pyzmq addressed** -- See Section 6 (missing client implementation, key format, integration path)

---

*Research completed: 2026-02-12*
*Source files analyzed: 22 files across 4 subsystems*
