# PITFALLS: ZMQ/WebSocket/Manifest V3 Debugging Guide

> When URL analysis scores stop reaching the Chrome extension, work through
> these pitfalls **in order**. They are ranked from most-likely to least-likely
> based on the ASPS architecture.

---

## Recommended Investigation Order

| Priority | Pitfall | Component | Typical time wasted |
|----------|---------|-----------|-------------------|
| 1 | [Service worker died](#1-chrome-mv3-service-worker-terminated) | Extension | 30 min+ |
| 2 | [WebSocket disconnected silently](#2-websocket-connection-silently-dead) | Extension / Python | 30 min+ |
| 3 | [ZMQ REQ/REP deadlock](#3-zmq-reqrep-socket-permanently-stuck) | Python / .NET | 1 hour+ |
| 4 | [ZMQ topic mismatch](#4-zmq-pubsub-topic-mismatch) | Python / .NET |  45 min+ |
| 5 | [Notification never broadcast to extension](#5-notification-received-but-never-broadcast-to-extension) | Python | 30 min+ |
| 6 | [Extension message handler mismatch](#6-extension-message-type-mismatch) | Extension / Python | 20 min+ |
| 7 | [CurveMQ encryption mismatch](#7-curvezmq-encryption-mismatch) | Python / .NET | 1 hour+ |
| 8 | [Port mismatch between components](#8-port-mismatch-between-components) | All | 20 min+ |
| 9 | [Token expired or invalid](#9-token-expiredinvalid-on-backend) | Python / .NET | 30 min+ |
| 10 | [Stale cache masking real failures](#10-stale-cache-masking-real-failures) | Extension / Python | 30 min+ |
| 11 | [asyncio event loop conflict](#11-asyncio-event-loop-conflict-in-python-app) | Python | 45 min+ |
| 12 | [Message queue TTL expiry](#12-extension-message-queue-ttl-expiry) | Extension | 20 min+ |

---

## Pitfalls in Detail

---

### 1. Chrome MV3 Service Worker Terminated

The Manifest V3 service worker is killed by Chrome after ~30 seconds of
inactivity. When this happens, the WebSocket connection object is destroyed,
all `setInterval` timers are lost, and no messages can be received.

**Symptoms**
- Extension badge shows `!` (red/disconnected) after a period of no browsing.
- Console logs stop appearing in `chrome://extensions` service worker inspector.
- Clicking a new page triggers a fresh connection attempt (scan works), but
  scores for pages visited while the worker was dead are lost.
- `chrome.alarms` reconnect fires, but the WebSocket object is `null`.

**Quick diagnosis (<5 min)**
1. Open `chrome://extensions` > AntiScam Protection > "Service Worker" link.
2. If the link says "Inactive", the worker is dead. Click to restart.
3. In the DevTools console, type: `connectionService.isConnected()`. If `false`
   while the Python desktop app is running, the worker died and reconnected
   without getting a score delivery.
4. Check `chrome.storage.local.get(['currentPageScanning'])` -- if stuck at
   `true`, the worker died mid-scan.

**Fix approach**
- The keepalive timer (`20s interval`) in `ConnectionService.js` (line 276) is
  designed to keep the worker alive. Verify it is actually running:
  ```js
  // In service worker console:
  connectionService.keepaliveTimer  // should not be null when connected
  ```
- If the keepalive is not preventing termination, use `chrome.alarms` (minimum
  30s in production) as a backup wake-up. The current `heartbeat` alarm
  (line 64-73 of `background.js`) already handles this.
- Ensure the `"alarms"` permission is present in `manifest.json` (it is, line 12).

**Component to check:** `apps/extension/chrome/background.js`,
`apps/extension/chrome/services/ConnectionService.js`

---

### 2. WebSocket Connection Silently Dead

The WebSocket's `readyState` stays `OPEN` even when the Python desktop app
has crashed or the network path is broken. No `onclose` event fires because
TCP keepalives have not yet detected the failure.

**Symptoms**
- `connectionService.isConnected()` returns `true` but messages sent via
  `connectionService.send()` disappear into the void.
- The Python `ExtensionServer` console shows no `<<< RECEIVED FROM EXTENSION`
  output for messages the extension believes it sent.
- Extension icon stays green (connected) but scans time out after 30s.
- The heartbeat missed counter climbs silently.

**Quick diagnosis (<5 min)**
1. In the extension service worker console:
   ```js
   connectionService.missedHeartbeats  // >0 means pongs are not coming back
   ```
2. On the Python side, check if `[EXTENSION] Client connected` / `Client
   disconnected` messages appear. If the last connected message was hours ago,
   the connection is stale.
3. Send a manual ping from the extension console:
   ```js
   connectionService.send({ type: 'ping' });
   ```
   If no `pong` response arrives within 2 seconds, the connection is dead.

**Fix approach**
- The heartbeat mechanism (10s interval, 3 missed = dead, lines 295-367 of
  `ConnectionService.js`) should catch this. If `maxMissedHeartbeats` (currently
  3) is too generous, reduce to 2 for faster detection.
- On the Python side, `ExtensionServer._handle_client` (line 46-49 of
  `extension_server.py`) responds to `heartbeat_ping` with `heartbeat_pong`.
  Verify this code path is not throwing an exception silently.
- After detecting a dead connection, `handleDeadConnection()` force-closes and
  schedules reconnect. Verify `scheduleReconnect()` actually fires by checking
  `chrome://alarms-internals`.

**Component to check:** `apps/extension/chrome/services/ConnectionService.js`,
`apps/desktop/win/src/extension_server.py`

---

### 3. ZMQ REQ/REP Socket Permanently Stuck

ZMQ REQ/REP enforces strict send-receive alternation. If the Python client
sends a request and the .NET backend crashes, hangs, or takes longer than the
5000ms `RCVTIMEO`, the Python `socket.recv()` raises `zmq.Again` -- but the
socket is now in a "waiting for response" state. The **next** `socket.send()`
will raise `zmq.ZMQError` because REQ expects to receive before it can send
again.

**Symptoms**
- The Python console shows `[ZMQ] WARNING: Timeout: No response after 5000ms`
  followed by `[ZMQ] ERROR: Error: ...` on subsequent send attempts.
- Backend `.NET` logs show the alert was received and processed, but the
  response was sent after the Python client already timed out.
- All subsequent URL checks fail until the Python app is restarted.
- The extension shows "Analysis in progress" indefinitely.

**Quick diagnosis (<5 min)**
1. Look at Python console for `[ZMQ] WARNING: Timeout` messages.
2. Try sending a second URL check -- if it immediately fails with a ZMQ error
   (not a timeout), the socket is stuck.
3. Check backend logs for processing times. If analysis takes >5s, the REQ
   socket will have timed out.

**Fix approach**
- The current `ZMQClient` (line 206-212 of `zmq_client.py`) calls
  `self.connect()` and `self.close()` for **every** `send_url_alert` call.
  This is actually a safeguard -- each request uses a fresh socket. Verify
  this pattern is still in place and has not been "optimized" to reuse sockets.
- If socket reuse is introduced later, the fix is to destroy the socket and
  create a new one after any timeout:
  ```python
  except zmq.Again:
      self.socket.close()
      self.socket = self.context.socket(zmq.REQ)
      self.socket.connect(...)
  ```
- On the .NET side, `RealTimeAlertListener.SendResponse()` (line 167-180 of
  `RealTimeAlertListener.cs`) sends the response. If `ProcessAlertAsync` hangs
  (database call, external API), the response is delayed past the timeout.
  Check that no blocking call exceeds 5 seconds.

**Component to check:** `apps/desktop/win/src/zmq_client.py`,
`ASPSBackend14_J/Business/Messaging/RealTimeAlertListener.cs`

---

### 4. ZMQ PUB/SUB Topic Mismatch

The .NET `NotificationPublisher` publishes to topic `"device:{DeviceUid}"`.
The Python `NotificationClient` subscribes to `"device:{self.device_uid}"`.
If the device UID is different between the two (even by case or whitespace),
no notifications arrive.

**Symptoms**
- Python console shows `[NOTIFY] HEARTBEAT: Still listening for notifications...`
  every 2 minutes but never `NOTIFICATION RECEIVED`.
- .NET logs show `Published notification to topic 'device:PC-JOHN-001'`.
- The ZMQ REQ/REP request/response works fine (scores arrive in the immediate
  response), but async notifications from analysis never arrive.

**Quick diagnosis (<5 min)**
1. Check the Python desktop app startup output for `[NOTIFY] Subscribed to
   topic: 'device:...'`. Note the exact device UID.
2. Check the .NET backend logs for the topic it publishes to.
3. Compare them character-by-character. Common mismatches:
   - Trailing whitespace or newline in one
   - Different case (`PC-John-001` vs `PC-JOHN-001`)
   - Python uses a hardcoded default `"PC-JOHN-001"` (line 39 of `main.py`)
     while backend registered a different UID during device registration.
4. In Python console, verify: the `device_uid` used by `NotificationClient`
   matches the `device_uid` used by `ZMQClient.send_url_alert`.

**Fix approach**
- Ensure both sides use the exact same device UID. The Python app gets it from
  `Container.__init__` (line 45 of `container.py`) which defaults to
  `"PC-JOHN-001"`. The backend gets it from the alert's `DeviceInfo.DeviceUid`.
- Add a startup log line that prints the exact subscription topic.
- For debugging, temporarily subscribe to `""` (empty string = all topics) to
  confirm notifications are being published at all.

**Component to check:** `apps/desktop/win/src/notification_client.py` (line 95),
`ASPSBackend14_J/Business/Messaging/NotificationPublisher.cs` (line 84)

---

### 5. Notification Received but Never Broadcast to Extension

The Python desktop app receives the ZMQ notification, but the broadcast to
the Chrome extension via WebSocket fails silently.

**Symptoms**
- Python console shows the full `NOTIFICATION RECEIVED` block with score, risk
  assessment, and indicators.
- But the extension never shows the score (stuck on "scanning" or times out
  to gray).
- No `>>> SENT TO EXTENSION [url_result]` block appears in Python console.

**Quick diagnosis (<5 min)**
1. Check Python console: after the `!!!` notification block, look for
   `[NOTIFICATION] Broadcasted result to extension: score=...`. If missing,
   the broadcast failed.
2. Check if `extension_server` is `None` in `NotificationHandler`. Look for
   `[STARTUP] Starting extension server...` in startup logs -- if the
   extension server started after the notification handler was created,
   `set_extension_server` may not have been called.
3. Check if `self.clients` in `ExtensionServer.broadcast()` is empty (no
   extension connected when the notification arrived).
4. Check for `RuntimeError` in the `_broadcast_to_extension` method (line
   68-72 of `notification_handler.py`) -- the asyncio loop might not be running.

**Fix approach**
- The `NotificationHandler.handle()` method (line 66-72 of
  `notification_handler.py`) tries to get the running event loop with
  `asyncio.get_running_loop()`. If it fails, it falls back to `asyncio.run()`.
  However, `asyncio.run()` creates a **new** event loop, while the WebSocket
  server is running on a **different** loop. This causes the broadcast to run
  on the wrong loop and fail silently.
- The notification callback runs in a **background thread** (started by
  `NotificationClient._listen` on line 77 of `notification_client.py`). This
  thread does not have the main asyncio event loop. The `asyncio.run()` fallback
  creates a new loop that cannot access the WebSocket connections.
- Fix: store a reference to the main event loop at startup and use
  `loop.call_soon_threadsafe(asyncio.ensure_future, coro)` to schedule the
  broadcast on the correct loop.

**Component to check:** `apps/desktop/win/src/handlers/notification_handler.py`,
`apps/desktop/win/src/main.py`

---

### 6. Extension Message Type Mismatch

The extension sends a message with `type: 'url_check'` but the Python handler
expects a different type string, or vice versa for results.

**Symptoms**
- Extension sends a scan request but Python console shows
  `Unknown message type: ...`.
- Or Python sends `url_result` but the extension's `ConnectionService` does
  not have a handler registered for that type.

**Quick diagnosis (<5 min)**
1. In the extension service worker console, check what the scan sends:
   ```js
   // MSG.WS_URL_CHECK should be 'url_check'
   ```
2. In `extension_handler.py` (line 30-36), verify the handler map includes
   the exact type string.
3. For results coming back: check `MSG.WS_URL_RESULT` in the extension
   matches `'url_result'` which is what the Python `notification_handler.py`
   broadcasts (line 83).

**Fix approach**
- Ensure message type constants are identical on both sides. The extension uses
  `MSG.WS_URL_CHECK` = `'url_check'` and `MSG.WS_URL_RESULT` = `'url_result'`.
  The Python side uses literal strings `'url_check'` and `'url_result'`.
- Add a wildcard handler in the extension for debugging:
  ```js
  connectionService.onMessage('*', (data, type) => {
    console.log('ALL WS MESSAGES:', type, data);
  });
  ```

**Component to check:** `apps/extension/chrome/messaging/MessageTypes.js`,
`apps/desktop/win/src/handlers/extension_handler.py`

---

### 7. CurveZMQ Encryption Mismatch

The .NET backend uses CurveZMQ encryption on its REP and PUB sockets. If the
Python client does not configure the correct server public key, or if CURVE
is enabled on one side but not the other, connections silently fail.

**Symptoms**
- Python `[ZMQ] SUCCESS: Connected to tcp://...` appears (ZMQ `connect()` always
  succeeds even with wrong keys -- it is async).
- But `[ZMQ] WARNING: Timeout: No response after 5000ms` immediately follows.
- .NET backend logs show `Failed to decode message as UTF-8` or no message
  received at all.
- The PUB/SUB notification client also receives nothing.

**Quick diagnosis (<5 min)**
1. Check .NET startup logs for `CURVE encrypted` vs `unencrypted`.
2. Check the .NET `appsettings.json` for `Security:CurveEnabled`. If `true`,
   the Python client MUST configure CURVE client keys.
3. Check if the Python `zmq_client.py` sets any CURVE options on the socket.
   Currently (as of the code reviewed), it does **not** set CURVE options.
4. If CurveEnabled is `true` on backend but Python has no CURVE config, this
   is the problem.

**Fix approach**
- Either disable CURVE on the backend (`"CurveEnabled": false` in
  `appsettings.json`) or add CURVE client configuration to the Python ZMQ
  client:
  ```python
  import zmq.auth
  server_public_key = b"..."  # Z85 encoded, from backend config
  client_public, client_secret = zmq.curve_keypair()
  socket.curve_serverkey = server_public_key
  socket.curve_publickey = client_public
  socket.curve_secretkey = client_secret
  ```
- The `CurveKeyManager.cs` (line 22-24) loads keys from config. The
  `ServerPublicKeyZ85` must be shared with the Python client.

**Component to check:** `ASPSBackend14_J/Business/Services/CurveKeyManager.cs`,
`apps/desktop/win/src/zmq_client.py`,
`apps/desktop/win/src/notification_client.py`

---

### 8. Port Mismatch Between Components

Three separate port pairs must agree: WebSocket (extension <-> Python),
ZMQ REQ/REP (Python <-> .NET), and ZMQ PUB/SUB (Python <-> .NET).

**Symptoms**
- Extension shows disconnected (red badge) even though Python desktop is running.
- Or Python connects to ZMQ but never gets a response.
- Connection works on one machine but not another.

**Quick diagnosis (<5 min)**
1. WebSocket ports: Extension tries `[8080, 8181, 8282, 8383, 8484]`
   (ConnectionService.js line 14). Python tries the same list
   (config.py line 36). If another app is using 8080, both sides should
   skip to the same fallback port -- but if the Python server grabbed 8181
   and the extension found 8080 open from another process, they will connect
   to different things.
2. ZMQ REQ port: Python config says `50001` (config.py line 40). Backend
   `RealTimeAlertListener` uses constructor parameter `port = 50001`
   (RealTimeAlertListener.cs line 55).
3. ZMQ PUB port: Python config says `50002` (config.py line 41). Backend
   `NotificationPublisher` reads from config `NetMQ:NotificationPublisherPort`
   with default `50002`.

**Fix approach**
- Check `appsettings.json` on the backend for any port overrides.
- Use `netstat -an | findstr 50001` (Windows) or `ss -tlnp | grep 50001` to
  verify the ports are actually bound and listening.
- For WebSocket: the extension saves the last successful port in
  `chrome.storage.local` (key: `connectedPort`). If the Python app switches
  ports, the extension may try the old port first and waste 2 seconds on
  timeout before trying others.

**Component to check:** `apps/desktop/win/src/config.py`,
`apps/extension/chrome/services/ConnectionService.js`,
`ASPSBackend14_J/Business/Messaging/RealTimeAlertListener.cs`,
`WebApi/publish/appsettings.json`

---

### 9. Token Expired/Invalid on Backend

The backend validates tokens before processing alerts. If the token is
expired or invalid, the alert is rejected and no analysis (or notification)
is produced.

**Symptoms**
- Python console shows `[ZMQ] RECEIVED: Response received:` with content like
  `{"status": "TokenExpired", "message": "Token has expired. Please refresh."}`.
- Or `{"status": "InvalidToken", ...}`.
- The extension sees an error response or the "analyzing" state never resolves.

**Quick diagnosis (<5 min)**
1. Check the ZMQ response in Python console output. The `send_alert` method
   prints the full response.
2. Look for `TokenExpired` or `InvalidToken` in the response.
3. Check `AuthManager.is_valid()` state -- if it returns `True` but the token
   is actually expired on the backend side, the clocks may be out of sync.

**Fix approach**
- The Python `ZMQClient.send_url_alert` (line 182-184 of `zmq_client.py`)
  uses a hardcoded fallback token `"12345678-1234-1234-1234-123456789012"` when
  no real token is provided. This will fail token validation on the backend.
- Ensure `AuthManager.ensure_authenticated()` is called before any scan and
  that the token is passed through `ScanService.check_url()`.
- If tokens expire during long sessions, implement token refresh using the
  `RefreshToken` message type supported by `RealTimeAlertListener`.

**Component to check:** `apps/desktop/win/src/auth_manager.py`,
`apps/desktop/win/src/services/scan_service.py` (lines 122-128),
`ASPSBackend14_J/Business/Messaging/RealTimeAlertListener.cs` (lines 446-459)

---

### 10. Stale Cache Masking Real Failures

Both the Python desktop app and the Chrome extension maintain URL caches.
A cached result may be served even though the live pipeline is broken.

**Symptoms**
- Some URLs show scores (cached ones) but new/unknown URLs never get scores.
- Debugging appears to show "everything works" because the test URL was
  already cached.
- After clearing the cache, nothing works.

**Quick diagnosis (<5 min)**
1. In the extension service worker console:
   ```js
   cacheService.size()  // if > 0, results may be from cache
   cacheService.clear() // clear and re-test
   ```
2. In the extension, check if the result has `fromCache: true` in the
   `handleUrlResult` function logs.
3. On the Python side, check for `[SCAN] CACHE HIT!` in the console output.
4. Test with a URL you have never visited before.

**Fix approach**
- Always test with a fresh, never-before-seen URL when debugging the pipeline.
- Python cache TTL is 3600 seconds (1 hour). Extension cache TTL comes from
  the server response `data.ttl` (default 3600).
- Clear both caches during debugging:
  - Extension: `cacheService.clear()` in service worker console
  - Python: restart the desktop app (cache is in-memory)

**Component to check:** `apps/extension/chrome/services/CacheService.js`,
`apps/desktop/win/src/cache_manager.py`

---

### 11. asyncio Event Loop Conflict in Python App

The Python desktop app runs the asyncio event loop in a background thread
(line 209 of `main.py`), while the ZMQ notification listener runs in its
own thread (line 77 of `notification_client.py`). When the notification
handler tries to broadcast to the WebSocket (an asyncio operation), it
may fail because it is not running on the asyncio event loop thread.

**Symptoms**
- Python console shows `NOTIFICATION RECEIVED` but no broadcast to extension.
- An error like `RuntimeError: no running event loop` or
  `RuntimeError: This event loop is already running` appears in logs.
- Intermittent: sometimes works (if the loop happens to be available),
  sometimes does not.

**Quick diagnosis (<5 min)**
1. Add `logging.basicConfig(level=logging.DEBUG)` and look for asyncio errors.
2. Check if `[NOTIFICATION] Broadcasted result to extension` appears after
   notification receipt.
3. Check if the `_broadcast_to_extension` method's `except` block (line 93
   of `notification_handler.py`) is silently swallowing the error.

**Fix approach**
- In `main.py`, store the main event loop reference:
  ```python
  self._loop = asyncio.get_event_loop()
  ```
- Pass it to `NotificationHandler` and use it for cross-thread scheduling:
  ```python
  self._loop.call_soon_threadsafe(
      asyncio.ensure_future,
      self._broadcast_to_extension(analysis, cache_data)
  )
  ```
- Alternatively, use `asyncio.run_coroutine_threadsafe(coro, loop)` which
  returns a `Future` that can be checked for errors.

**Component to check:** `apps/desktop/win/src/handlers/notification_handler.py`,
`apps/desktop/win/src/main.py`

---

### 12. Extension Message Queue TTL Expiry

When the extension is disconnected from the desktop app, messages are queued
in `MessageQueueService` with a 5-minute TTL and 100-message max. If
reconnection takes longer than 5 minutes, all queued messages are silently
dropped.

**Symptoms**
- Extension reconnects after a disconnection but queued scan requests are
  gone (no results arrive for URLs visited during the outage).
- The `[MessageQueue] Flushed: 0 messages (5 expired)` log appears after
  reconnection.

**Quick diagnosis (<5 min)**
1. In the extension service worker console:
   ```js
   messageQueueService.getStats()
   ```
2. Check `oldestAgeSeconds` -- if close to 300 (5 min), messages are about
   to expire.
3. After reconnection, check the flush log for expired count.

**Fix approach**
- The 5-minute TTL (line 10 of `MessageQueueService.js`) is intentional to
  prevent stale data. If longer disconnections are expected, increase it.
- Priority messages (url_check, url_result, risk_alert) are preserved over
  non-priority ones when the queue overflows, but they still expire.
- Consider re-scanning the current tab after reconnection rather than relying
  on queued messages.

**Component to check:** `apps/extension/chrome/services/MessageQueueService.js`

---

## Common Red Herrings

These look like the problem but usually are not:

### Red Herring: "The backend is down"
**Why it misleads:** The ZMQ REQ/REP `connect()` call always succeeds even if
nothing is listening. The timeout 5 seconds later is the real failure signal.
People waste time checking firewall rules and network connectivity when the
backend process is actually running fine but just slow to respond.

**What to check instead:** Look at the response content, not the connection
status. A timeout with no response vs. a response with `success: false` are
completely different problems.

### Red Herring: "The extension has a bug"
**Why it misleads:** The extension icon showing gray/neutral (no score) could
be caused by any upstream failure. Debugging the extension's JavaScript when
the real problem is a Python-to-.NET communication issue wastes significant
time.

**What to check instead:** Work backwards from the data source. Does the .NET
backend produce the notification? Does the Python app receive it? Does the
Python app broadcast it? Only then check the extension.

### Red Herring: "ZMQ SUB is not receiving messages"
**Why it misleads:** ZMQ PUB/SUB is lossy by design -- if the subscriber
connects after the publisher sends, the message is gone forever. People
assume the PUB/SUB link is broken when really the subscriber just was not
connected at the moment the message was published.

**What to check instead:** The ASPS system uses REQ/REP for the initial alert
AND PUB/SUB for the asynchronous analysis result notification. Make sure the
SUB socket was connected BEFORE the REQ message was sent. Check the startup
order in `main.py` (line 151-155): notification client starts in a thread
AFTER the extension server. There is a race condition if a scan request is
sent before the notification client finishes connecting.

### Red Herring: "The WebSocket library is broken"
**Why it misleads:** The `websockets` Python library and Chrome's native
WebSocket API are both battle-tested. Connection failures are almost always
caused by port conflicts, firewall, or antivirus -- not library bugs.

**What to check instead:** Run `netstat -an | findstr 8080` (Windows) to see
if the expected port is actually bound. Check if Windows Defender or another
antivirus is blocking localhost WebSocket connections.

---

## Quick Debugging Flowchart

```
Score not reaching extension?
|
+-- Is extension connected to desktop app?
|   |   Check: connectionService.isConnected() in SW console
|   |
|   +-- NO --> Check WebSocket (Pitfall #2, #8, #1)
|   |
|   +-- YES
|       |
|       +-- Does Python desktop receive the url_check message?
|           |   Check: "[EXTENSION] Received: url_check" in Python console
|           |
|           +-- NO --> Message type mismatch (Pitfall #6)
|           |
|           +-- YES
|               |
|               +-- Does ZMQ REQ/REP get a response from backend?
|               |   |   Check: "[ZMQ] RECEIVED: Response received:" in Python console
|               |   |
|               |   +-- NO --> ZMQ stuck or backend down (Pitfall #3, #7, #8)
|               |   |
|               |   +-- YES, but says "analyzing: true"
|               |       |
|               |       +-- Does ZMQ SUB receive the notification?
|               |           |   Check: "NOTIFICATION RECEIVED" in Python console
|               |           |
|               |           +-- NO --> Topic mismatch or CURVE issue (Pitfall #4, #7)
|               |           |
|               |           +-- YES
|               |               |
|               |               +-- Is it broadcast to extension?
|               |                   |   Check: "Broadcasted result to extension" in Python console
|               |                   |
|               |                   +-- NO --> asyncio loop issue (Pitfall #5, #11)
|               |                   |
|               |                   +-- YES
|               |                       |
|               |                       +-- Does extension receive and process it?
|               |                           |   Check: "[Background] URL result received:" in SW console
|               |                           |
|               |                           +-- NO --> Service worker died (Pitfall #1)
|               |                           |
|               |                           +-- YES --> Check cache/UI update (Pitfall #10)
```

---

## Environment-Specific Notes

**Windows-specific:**
- Port 8080 is commonly used by other software (Fiddler, WAMP, etc.).
  If another app grabs it, the Python server may start on 8181 while the
  extension connects to the other app on 8080.
- Windows Firewall may block localhost ZMQ connections (unusual but possible
  with custom rules). Check `wf.msc` > Inbound Rules.
- OSError `errno 10048` = "address already in use" on Windows (handled in
  `extension_server.py` line 107).

**Development vs. Production:**
- `chrome.alarms` has a minimum interval of 30 seconds in production but
  allows shorter intervals in development with the extension loaded unpacked.
- ZMQ `RCVTIMEO` of 5000ms (line 64 of `zmq_client.py`) may be too short
  if the backend is under load. For debugging, temporarily increase to 15000ms.
