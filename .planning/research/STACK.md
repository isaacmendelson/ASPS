# Stack Research: Debugging ZMQ and WebSocket Communication in ASPS

**Research Date:** 2026-02-12
**Scope:** ZMQ (NetMQ/pyzmq) and WebSocket debugging for multi-process anti-phishing system repair
**Prerequisite:** Existing codebase knowledge from `.planning/codebase/` -- this document does NOT re-describe architecture.

---

## 1. The Exact Communication Chain (Score Flow)

The score must traverse these links to reach the extension. A break at ANY point kills the flow.

```
Chrome Extension                Python Desktop App              .NET Backend
===============                 ==================              ============

[1] url_check ----WebSocket---> [2] ScanService.check_url()
                                [3] ZMQClient.send_url_alert() --REQ/REP--> [4] RealTimeAlertListener
                                                                             [5] RouteMessage -> ProcessAlertAsync
                                                                             [6] UDAnalysisManager.Handle()
                                                                             [7] UDAnalysis.AnalyzeAsync()
                                                                             [8] UDUrlAnalyzer.RunPythonAnalyzerAsync()
                                                                              |
                                [9] REP response <--------REP------------ [4b] SendResponse({success:true})
                                [10] Returns {analyzing: true}
                                                                             |
                                                                             [11] AnalysisResultReceived event fires
                                                                             [12] NotificationPublisherActor receives event
                                                                             [13] NotificationPublisher.PublishAnalysisResult()
                                                                              |
                                [14] NotificationClient <--PUB/SUB------ [13b] PUB socket sends topic + JSON
                                [15] NotificationHandler.handle()
                                [16] _extract_analysis() -> score
                                [17] _broadcast_to_extension()
                                     |
[18] handleUrlResult() <--WS--- [17b] extension_server.broadcast()
[19] ScanService.handleResult()
[20] Icon/popup updated
```

**Key insight:** There are TWO separate ZMQ patterns in use for the score flow:
- **REQ/REP** (port 50001): Alert submission. Returns `{success: true}` immediately -- NOT the score.
- **PUB/SUB** (port 50002): Score delivery. The score arrives asynchronously via notification.

A third REQ/REP channel exists on port 5555/5556 for CQRS admin queries -- irrelevant to the score flow.

---

## 2. Debugging Tools -- Specific and Actionable

### 2.1. ZMQ Message Tracing

#### Tool: `zmq_monitor` socket events (built into pyzmq/NetMQ)

pyzmq has socket monitoring built in. Add this to the Python ZMQ client to trace connection lifecycle:

```python
# In zmq_client.py, after creating socket:
import zmq.utils.monitor as zmq_monitor

monitor = self.socket.get_monitor_socket()

# In a background thread:
while True:
    evt = zmq.utils.monitor.recv_monitor_message(monitor)
    print(f"[ZMQ-MONITOR] Event: {evt['event']} Value: {evt['value']} Endpoint: {evt['endpoint']}")
    if evt['event'] == zmq.EVENT_MONITOR_STOPPED:
        break
```

Events to watch for:
- `EVENT_CONNECTED` / `EVENT_CONNECT_DELAYED` / `EVENT_CONNECT_RETRIED` -- connection health
- `EVENT_DISCONNECTED` / `EVENT_CLOSED` -- unexpected drops
- `EVENT_HANDSHAKE_FAILED_AUTH` -- CURVE authentication failure (critical for this system)

#### Tool: Standalone ZMQ Proxy Sniffer

Create a simple Python script that sits between client and server to log all messages:

```python
# debug_zmq_proxy.py - Place between Python client and .NET backend
import zmq
import json
import sys

context = zmq.Context()

# Frontend: where the Python client connects
frontend = context.socket(zmq.ROUTER)
frontend.bind("tcp://*:60001")  # Client connects here instead of 50001

# Backend: connect to the real .NET server
backend = context.socket(zmq.DEALER)
backend.connect("tcp://127.0.0.1:50001")

poller = zmq.Poller()
poller.register(frontend, zmq.POLLIN)
poller.register(backend, zmq.POLLIN)

print("[PROXY] Sniffing ZMQ REQ/REP on 60001 <-> 50001")

while True:
    sockets = dict(poller.poll())

    if frontend in sockets:
        msg = frontend.recv_multipart()
        identity = msg[0]
        data = msg[-1]
        print(f"\n>>> CLIENT -> SERVER:")
        try:
            print(json.dumps(json.loads(data), indent=2))
        except:
            print(data)
        backend.send_multipart(msg)

    if backend in sockets:
        msg = backend.recv_multipart()
        data = msg[-1]
        print(f"\n<<< SERVER -> CLIENT:")
        try:
            print(json.dumps(json.loads(data), indent=2))
        except:
            print(data)
        frontend.send_multipart(msg)
```

**Usage:** Change `BACKEND_REQ_PORT` in config.py to 60001 temporarily.

#### Tool: PUB/SUB Sniffer

```python
# debug_sub_sniffer.py - Subscribe to ALL topics to verify publisher is working
import zmq
import json

context = zmq.Context()
socket = context.socket(zmq.SUB)
socket.connect("tcp://127.0.0.1:50002")
socket.subscribe(b"")  # Subscribe to ALL topics (empty = everything)

print("[SUB-SNIFFER] Listening on tcp://127.0.0.1:50002 for ALL topics...")

while True:
    topic = socket.recv_string()
    message = socket.recv_string()
    print(f"\n{'='*70}")
    print(f"Topic: {topic}")
    try:
        parsed = json.loads(message)
        print(json.dumps(parsed, indent=2))
    except:
        print(message)
    print(f"{'='*70}")
```

**Critical diagnostic:** If this sniffer receives nothing after sending an alert, the problem is in the .NET backend (event not firing, publisher not called, or publisher not started). If it receives data, the problem is in the Python SUB client (topic mismatch, connection issue).

#### Tool: NetMQ Console Logging (Already Partially Present)

The .NET backend already logs at various levels. Increase log level for the Messaging namespace:

In `appsettings.json`, change:
```json
"Business": "Debug"
```
to:
```json
"Business.Messaging": "Trace"
```

Key log lines to watch for in the backend console:
1. `"Received message: ..."` -- Alert received on REQ/REP (RealTimeAlertListener line 123)
2. `"Response sent: ..."` -- Response sent back (RealTimeAlertListener line 174)
3. `"Alert routed to UDAnalysisManager"` -- Alert dispatched to analyzer (line 545)
4. `"[NotificationPublisherActor] Published notification for device..."` -- Score published on PUB/SUB (NotificationPublisherActor line 81)
5. `"Published notification to topic..."` -- Actual PUB socket send (NotificationPublisher line 86)

**If you see 1-2 but not 3:** The `UDAnalysisManager` is not found for the device. Check `_userDomainService.GetManagerForDeviceAsync()`.
**If you see 3 but not 4:** The analysis failed, or the `AnalysisResultReceived` event never fires.
**If you see 4 but not 5:** The notification object is null or deviceUid/userKeyField are both empty.

### 2.2. WebSocket Debugging

#### Chrome DevTools

1. Open `chrome://extensions/` -> find AntiScam extension -> click "service worker" link
2. This opens DevTools for the background service worker
3. Console tab shows all `console.log` output from background.js
4. Network tab shows WebSocket frames (filter by "WS")

**Key console lines from extension:**
- `[ConnectionService] Port 8080 connected!` -- WebSocket connected
- `[ConnectionService] Received: url_result` -- Score received from desktop
- `[ScanService] Result received (from server):` -- Score being processed
- `[Background] URL result received:` -- Score about to update UI

#### Python WebSocket Server Logging

The extension_server.py already prints full messages. Look for:
- `<<< RECEIVED FROM EXTENSION [url_check]` -- Extension sent URL check
- `>>> SENT TO EXTENSION [url_result]` -- Result sent back to extension

**If you see the `<<<` but not `>>>`:** The `_on_message_callback` (which calls `extension_handler.handle_message`) returned None.
**If you see neither:** The WebSocket connection is not established. Check port matching.

#### WebSocket Frame Inspection

In Chrome DevTools Network tab:
1. Filter by "WS"
2. Click the WebSocket connection
3. Click "Messages" tab
4. Green arrows = sent FROM extension, red arrows = received BY extension
5. Verify `url_result` messages contain `score`, `riskType`, and `protectiveAction` fields

---

## 3. NetMQ/pyzmq Interop Gotchas

### 3.1. CURVE Encryption Mismatch (HIGH RISK FOR THIS SYSTEM)

The backend has CURVE enabled (`"CurveEnabled": true` in appsettings.json). The Python ZMQ client does NOT apply CURVE keys.

**Current code in `zmq_client.py`:**
```python
self.socket = self.context.socket(zmq.REQ)
self.socket.setsockopt(zmq.RCVTIMEO, self.timeout)
self.socket.connect(f"tcp://{self.host}:{self.port}")
```

**Missing:** CURVE client key setup. If the server applies CURVE (`_curveKeyManager?.ApplyServerCurve`), an unencrypted client will silently fail -- the connection appears to establish (TCP layer) but messages are never delivered (ZMQ CURVE handshake fails).

**Fix verification:** Check `CurveKeyManager.IsEnabled`. If true, the Python client needs:
```python
import zmq.auth

# Generate client keypair
client_public, client_secret = zmq.curve_keypair()
self.socket.curve_publickey = client_public
self.socket.curve_secretkey = client_secret
self.socket.curve_serverkey = server_public_key_bytes  # From backend config
```

Or disable CURVE temporarily for debugging: set `"CurveEnabled": false` in appsettings.json.

**Same issue applies to NotificationClient** on port 50002 -- the PUB socket also applies CURVE.

### 3.2. JSON Serialization Incompatibilities

**The `TypeNameHandling.Auto` problem:**

The .NET backend uses Newtonsoft.Json with `TypeNameHandling.Auto`. This adds `$type` metadata to polymorphic types:
```json
{
  "$type": "Business.Messaging.AnalysisResultNotification, Business",
  "AlertType": "UrlAlert",
  ...
}
```

Python's `json.loads()` will parse this fine (ignores `$type`), but:
- The `$type` field occupies space and can confuse field-name-based lookups
- Nested objects may have `$type` annotations that create unexpected keys
- The `AnalysisResult` field inside `AnalysisResultNotification` is typed as `IAnalysisResult` (interface), so its concrete type will be in `$type`

**Verification:** Print the raw JSON received by `NotificationClient._handle_notification()` before any parsing. Look for `$type` keys at every nesting level.

### 3.3. Multipart Message Handling (PUB/SUB)

The .NET publisher sends multipart messages:
```csharp
_publisherSocket.SendMoreFrame(deviceTopic).SendFrame(json);
```

The Python subscriber receives them:
```python
topic_bytes = self.socket.recv()
message_bytes = self.socket.recv()
```

**Potential issue:** If the topic frame is received but the message frame is lost (interrupted), the SUB socket enters an inconsistent state. The next `recv()` would get the topic of the NEXT message, not the data of the current one, creating a permanent frame-shift.

**Fix:** Use `recv_multipart()` instead of two separate `recv()` calls:
```python
frames = self.socket.recv_multipart()
topic_str = frames[0].decode('utf-8')
message_str = frames[1].decode('utf-8')
```

### 3.4. Topic Subscription Encoding

The .NET publisher sends topic as a string frame:
```csharp
var deviceTopic = $"device:{deviceUid}";
_publisherSocket.SendMoreFrame(deviceTopic)  // Sends UTF-8 by default
```

The Python subscriber subscribes with:
```python
topic = f"device:{self.device_uid}"
self.socket.subscribe(topic.encode('utf-8'))
```

**Potential issue:** ZMQ topic matching is prefix-based on raw bytes. If `deviceUid` doesn't match exactly (case, whitespace, encoding differences), no messages will be delivered.

**Verification:** Subscribe to `b""` (empty = all topics) temporarily. If messages arrive, compare the received topic string character-by-character with the expected topic.

### 3.5. REQ/REP Lockstep Violation

ZMQ REQ/REP sockets are strictly alternating: send, recv, send, recv. If a send or recv is missed (e.g., timeout with no recv), the socket enters a broken state and ALL subsequent operations fail silently.

**Current code in zmq_client.py:**
```python
self.socket.send(message_bytes)
response_bytes = self.socket.recv()  # Timeout may cause issue
```

If `recv()` times out (zmq.Again exception), the socket is now in a state where it expects recv but the next call is send. The socket is permanently broken.

**Fix pattern:** On timeout or error, close and recreate the socket:
```python
except zmq.Again:
    # MUST close and recreate - socket state is now invalid
    self.socket.close()
    self.socket = self.context.socket(zmq.REQ)
    self.socket.setsockopt(zmq.RCVTIMEO, self.timeout)
    self.socket.connect(f"tcp://{self.host}:{self.port}")
```

**Note:** The current code in `send_url_alert()` creates a new connection each time (`connect/send/close`), which avoids this issue for the REQ side. But if `send_alert()` is called on a reused socket after a timeout, the lockstep will be broken.

### 3.6. Context Termination Hanging

`zmq.Context.term()` blocks until all sockets are closed. If a socket has a pending `recv()` in another thread, `term()` will hang forever.

**Current code in notification_client.py:**
```python
def _cleanup(self):
    if self.socket:
        self.socket.close()
    if self.context:
        self.context.term()  # Can hang if recv is blocking in _listen thread
```

**Fix:** Set `LINGER` to 0 before close:
```python
self.socket.setsockopt(zmq.LINGER, 0)
self.socket.close()
```

---

## 4. Chrome Manifest V3 Service Worker Issues

### 4.1. Service Worker Termination

Manifest V3 service workers are terminated after 30 seconds of inactivity. The current code uses three mechanisms to prevent this:
- **Keepalive** (20s interval) -- sends messages over WebSocket
- **Heartbeat** (10s interval) -- ping/pong with desktop app
- **chrome.alarms** -- for reconnection (survives termination)

**Known issue:** When the service worker IS terminated (e.g., Chrome decides to kill it despite keepalive), all in-memory state is lost:
- `stateManager` data -- gone
- `connectionService.websocket` -- gone
- `scanService.pendingScans` -- gone
- `cacheService` in-memory cache -- gone (depends on implementation)

**Impact on score flow:** If the service worker terminates between sending `url_check` and receiving `url_result`, the result arrives at a dead WebSocket. The desktop app's `extension_server.broadcast()` will fail silently (client removed from set), and when the extension reconnects, it has no knowledge of the pending scan.

**Diagnosis:** In Chrome DevTools for the service worker, check the "Status" indicator. If it says "Stopped", the worker was terminated.

### 4.2. WebSocket Reconnection Race Condition

After service worker restart, `connectionService.connect()` runs during `init()`. But the desktop app may have already cleaned up the old WebSocket client. The extension must:
1. Establish a new WebSocket connection
2. Re-register for any pending results

Currently, there is no mechanism to recover pending scans after reconnection. The user sees a "scanning" spinner that never resolves.

### 4.3. Multiple WebSocket Connection Ports

The extension tries ports `[8080, 8181, 8282, 8383, 8484]`. The Python server also tries these same ports. But if the desktop app started on port 8080 and later another app takes port 8080, the extension may fail to reconnect.

**Diagnosis:** Check `chrome.storage.local.get(['connectedPort'])` to see which port the extension thinks is active.

---

## 5. Link-by-Link Verification Procedure

### Step 1: Verify .NET Backend is Running and Listening

**Check ports:**
```powershell
netstat -ano | findstr "50001 50002 5555 5556"
```

Expected output: Four LISTENING entries on these ports.

**Check backend console:** Should show:
```
ASPSBackend System2 Starting...
+ ASView started
+ Real-time alert listener started (tcp://*:50001, Mode: Rep)
+ NotificationPublisher started on tcp://*:50002
```

### Step 2: Verify ZMQ REQ/REP (Port 50001)

**Test with standalone Python script:**
```python
# test_req_rep.py
import zmq, json

ctx = zmq.Context()
sock = ctx.socket(zmq.REQ)
sock.setsockopt(zmq.RCVTIMEO, 5000)
sock.connect("tcp://127.0.0.1:50001")

# Send a RequestToken message (simplest round-trip test)
msg = {"MessageType": "RequestToken", "DeviceUid": "PC-TEST-DEBUG"}
sock.send_json(msg)

try:
    reply = sock.recv_json()
    print(f"REPLY: {json.dumps(reply, indent=2)}")
    print("STATUS: REQ/REP channel is WORKING")
except zmq.Again:
    print("STATUS: TIMEOUT - REQ/REP channel is BROKEN")
finally:
    sock.close()
    ctx.term()
```

**Expected result:** `{"status":"DeviceNotRecognized","deviceUid":"PC-TEST-DEBUG"}` or `{"status":"TokenCreated",...}` if device is registered.

**If timeout:** CURVE encryption is blocking unencrypted clients, OR the backend is not running, OR port is not bound.

### Step 3: Verify ZMQ PUB/SUB (Port 50002)

Run the SUB sniffer from section 2.1, then trigger an alert via Step 2 (use a real registered device). Watch for any published message.

**If nothing arrives after 30+ seconds:**
1. Check backend logs for `NotificationPublisherActor` entries
2. Check that `NotificationPublisher` was started (look for "NotificationPublisher started" in console)
3. Check that the `AnalysisResultReceived` event is being raised after analysis

### Step 4: Verify Python Desktop App Receives Notifications

Start the desktop app. Check console for:
```
[NOTIFY] Subscribed to topic: 'device:PC-JOHN-001'
```

Send an alert via the standalone ZMQ test. Watch for:
```
NOTIFICATION RECEIVED
Topic: device:PC-JOHN-001
```

**If not received but SUB sniffer (Step 3) received it:**
- Topic mismatch: the device_uid in the subscription doesn't match what the publisher uses
- The notification thread crashed silently (check for Python exceptions)

### Step 5: Verify WebSocket (Extension <-> Desktop)

Open Chrome DevTools for the extension service worker. Check console for:
```
[ConnectionService] Port 8080 connected!
```

In the Python desktop app console, check for:
```
[EXTENSION] Client connected (ID: ...)
```

**Trigger a scan:** Navigate to any URL. In the extension DevTools console:
```
[ScanService] Scanning: example.com
[ConnectionService] Sending: url_check
```

In the Python console:
```
<<< RECEIVED FROM EXTENSION [url_check]
```

### Step 6: Verify End-to-End Score Delivery

After sending a URL alert (Step 5 triggers this), watch these in sequence:

1. **Python console:** `[SCAN] Step 3: Sending to backend (ZMQ)...`
2. **.NET console:** `Received message: ...` (alert received)
3. **.NET console:** `Alert routed to UDAnalysisManager` (processing started)
4. **.NET console:** `Analyzer basic-url-analyzer completed` (Python script finished)
5. **.NET console:** `[NotificationPublisherActor] Published notification` (score published)
6. **Python console:** `NOTIFICATION RECEIVED` (notification handler triggered)
7. **Python console:** `[NOTIFICATION] Broadcasted result to extension: score=...`
8. **Extension DevTools:** `[Background] URL result received: { url: ..., score: ... }`

**The break point is wherever the chain stops.**

---

## 6. Most Likely Failure Points (Based on Code Analysis)

### 6A. CURVE Encryption (VERY HIGH PROBABILITY)

`appsettings.json` has `"CurveEnabled": true` and CURVE keys configured. The `CurveKeyManager` is injected into both `RealTimeAlertListener` and `NotificationPublisher`. Both call `ApplyServerCurve()`.

Neither `zmq_client.py` nor `notification_client.py` implement CURVE client keys. This means:
- TCP connection succeeds (SYN/ACK at TCP layer)
- ZMQ CURVE handshake fails silently
- No messages are delivered in either direction
- pyzmq `recv()` times out or blocks forever

**Quick test:** Set `"CurveEnabled": false` in appsettings.json and restart the backend. If messages flow, CURVE was the blocker.

### 6B. Async Score Flow Not Reaching Extension

The `_process_response()` in `scan_service.py` returns `{analyzing: True}` when the REQ/REP response is `{success: true}`. The actual score comes later via PUB/SUB -> NotificationHandler -> broadcast.

The `_broadcast_to_extension()` method uses `asyncio.get_running_loop()` with a fallback to `asyncio.run()`. Since the notification callback runs in a threading.Thread (not an asyncio event loop), `get_running_loop()` will raise `RuntimeError`, and `asyncio.run()` will create a NEW event loop. This new loop does NOT have the `extension_server`'s WebSocket connections because those live in the MAIN asyncio loop.

**This is likely the critical bug:** The broadcast cannot reach WebSocket clients because it runs in a different event loop.

### 6C. `_isRunning` Flag in UDAnalysisManager

`UDAnalysisManager.Handle()` checks `if (!_isRunning) return;`. But `Start()` is never called in `InitializeAnalysisManagersAsync()`. The method `GetOrCreateManagerForUser()` creates the manager but doesn't call `Start()`.

If `_isRunning` is false (default), ALL events are silently dropped. No analysis happens. No score is generated.

### 6D. PUB/SUB Slow Joiner Problem

ZMQ PUB/SUB has a well-known "slow joiner" issue: if the SUB socket connects AFTER the PUB socket has already started sending, early messages are lost. This is by design in ZMQ.

In this system, the Python `NotificationClient` starts in a background thread AFTER the .NET backend is already running. If the backend sends a notification in the brief window before the SUB socket is fully connected, it's lost.

**Mitigation (already partially addressed):** The timeout + heartbeat in the current code handles this for steady-state. But for the FIRST scan after app startup, the notification could be lost.

---

## 7. Diagnostic Checklist Summary

```
[ ] Backend ports 50001 and 50002 are LISTENING (netstat)
[ ] Backend console shows "NotificationPublisher started"
[ ] CURVE is disabled for debugging OR Python clients have CURVE keys
[ ] REQ/REP round-trip works (test_req_rep.py)
[ ] PUB/SUB sniffer receives messages after alert (debug_sub_sniffer.py)
[ ] UDAnalysisManager._isRunning is true (check Start() is called)
[ ] Python desktop app receives notifications (console output)
[ ] NotificationHandler._broadcast_to_extension runs in correct event loop
[ ] WebSocket connection between extension and desktop is alive
[ ] Extension DevTools shows url_result messages arriving
```

---

*Research completed: 2026-02-12. Feeds into debugging and repair planning.*
