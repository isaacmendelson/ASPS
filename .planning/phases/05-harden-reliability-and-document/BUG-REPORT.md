# ASPS Score Flow Repair - Bug Report

**Date:** 2026-02-12
**Author:** Claude (automated analysis)
**Scope:** Phases 1-4 of score flow pipeline repair
**Repos:** `apps` (Desktop App + Chrome Extension), `ASPSBackend14_J` (Backend)

---

## Executive Summary

ASPS (Anti-Scam Protection System) -- score pipeline -
URL -> analysis -> score -

,
pipeline
 ,
 .

:

1. **asyncio.run() bridge failure** ( Phase 2) -- `notification_handler.py` `asyncio.run()` event loop WebSocket clients . 100% .
2. **ZMQ recv() ** ( Phase 2) -- ZMQ SUB `recv()` `recv_multipart()`, race condition .
3. **Hardcoded Token UUID** ( Phase 3) -- Desktop App UUID `"12345678-1234-1234-1234-123456789012"` Backend , Backend .
4. **CurveMQ key format/ordering** ( Phase 1 + Phase 4) -- CurveMQ encryption Python (pyzmq) C# (NetMQ) silent failures , Phase 1 .

 (Phase 5 )
 4 .

---

## Bug 1: CurveMQ Key Format Mismatch

**Phase:** 1 (diagnosed) + Phase 4 (fixed)
**Severity:** Critical -- complete communication failure
**Component:** `zmq_client.py`, `notification_client.py`, `appsettings.json`

### ?

CurveMQ (Curve25519) encryption Desktop App (pyzmq) Backend (NetMQ) silent failure . ZMQ socket `connect()` handshake , timeout .

### ?

:

1. **Key format :** Python pyzmq Z85-encoded keys bytes, C# NetMQ strings. , key distribution .
2. **CURVE options ordering:** ZMQ CURVE options (publickey, secretkey, serverkey) `socket.connect()` . , CURVE .
3. **Client keypair :** Desktop App client keypairs , Backend .

 CurveMQ timeout , error message log entry . -- Backend Desktop App , , , .

### ?

#### Phase 1: CurveMQ
- `appsettings.json` `CurveEnabled: false`
- , ZMQ (phases 2-3)

#### Phase 4: CurveMQ
- `apply_curve_client()` helper function `zmq_client.py`:
  ```python
  def apply_curve_client(socket, server_public_key_z85: str):
      client_public, client_secret = zmq.curve_keypair()
      socket.curve_publickey = client_public
      socket.curve_secretkey = client_secret
      socket.curve_serverkey = server_public_key_z85.encode('ascii')
  ```
- **Ephemeral client keypairs:** `zmq.curve_keypair()` connection (ZAP authenticator )
- **CURVE before connect:** `apply_curve_client()` `socket.connect()`
- `notification_client.py` `apply_curve_client` import
- `config.py` `SERVER_PUBLIC_KEY_Z85` `CURVE_ENABLED` , env var

### Impact

Communication failure. CurveMQ Desktop App Backend , ZMQ messages , timeout .

### Commits

| Hash | Repo | Message |
|------|------|---------|
| `2f47fdc` | ASPSBackend14_J | feat(01-01): disable CurveMQ for Phase 1-3 diagnostics |
| `d038234` | apps | feat(04-01): add CurveMQ client encryption to ZMQ REQ and SUB sockets |
| `776b0a7` | ASPSBackend14_J | feat(04-01): re-enable CurveEnabled in Backend appsettings.json |

### Recommendation

1. **Backend CURVE handshake logging :** NetMQ CurveMQ CURVE handshake , client connection event (success + failure) . CurveMQ timeout .
2. **Server public key Backend :** RegisterDevice/RequestToken response server public key , Desktop App . hardcoded key .
3. **CURVE key rotation :** key rotation , server public key ( deployment).

### Files Modified

- `apps/desktop/win/src/zmq_client.py` -- `apply_curve_client()` helper, CURVE params constructor
- `apps/desktop/win/src/notification_client.py` -- CURVE params constructor, `apply_curve_client` import
- `apps/desktop/win/src/config.py` -- `SERVER_PUBLIC_KEY_Z85`, `CURVE_ENABLED` settings
- `apps/desktop/win/src/core/container.py` -- CURVE settings wiring clients
- `ASPSBackend14_J/ASPSBackend/appsettings.json` -- `CurveEnabled: true`

---

## Bug 2: asyncio.run() Thread-to-Asyncio Bridge Failure

**Phase:** 2 (plan 02)
**Severity:** Critical -- 100%
**Component:** `notification_handler.py`, `main.py`

### ?

Backend ZMQ PUB (port 50002) notification WebSocket clients . Desktop App terminal notification , Chrome Extension .

### ?

`notification_handler.py` `handle()` method notification ZMQ background thread:

```python
# BUG:
asyncio.run(self._broadcast_to_extension(analysis, cache_data))
```

`asyncio.run()` **event loop** . WebSocket clients **main** event loop `ExtensionServer` . event loop , broadcast `self.extension_server.broadcast()` **zero clients** .

:
- ZMQ SUB thread notification ( `self._on_notification_callback`)
- `handle()` `asyncio.run()` event loop
- event loop `ExtensionServer` WebSocket clients
- `broadcast()` `self.clients` empty set , message

 **thread-to-asyncio bridge** . `asyncio.run()` , event loop bridge .

### ?

`asyncio.run()` `asyncio.run_coroutine_threadsafe()` :

```python
# FIX:
if self._main_loop and self._main_loop.is_running():
    future = asyncio.run_coroutine_threadsafe(
        self._broadcast_to_extension(analysis, cache_data),
        self._main_loop
    )
    future.result(timeout=5.0)
```

:
1. `NotificationHandler` `set_event_loop()` method `_main_loop`
2. `main.py` `start()` `asyncio.get_running_loop()` handler inject
3. `run_coroutine_threadsafe()` coroutine main event loop schedule
4. `future.result(timeout=5.0)` ZMQ thread block broadcast

**Startup ordering:** `extension_server.start()` -> `set_event_loop()` -> `notification_thread.start()`

### Impact

** pipeline .** notification Backend , Desktop App , Chrome Extension . .

### Commits

| Hash | Repo | Message |
|------|------|---------|
| `656843a` | apps | feat(02-02): fix NotificationHandler thread-to-asyncio bridge |
| `1fdd193` | apps | feat(02-02): inject event loop into NotificationHandler at startup |

### Recommendation

1. **Backend notification delivery :** Backend notification publish , delivery callback mechanism . Desktop App notification , Backend log ("notification published to device X, no ACK within Ns").
2. **Thread-to-asyncio bridge pattern :** codebase `asyncio.run()` thread context , `run_coroutine_threadsafe()` . `asyncio.run()` explicit "this creates a new event loop" .

### Files Modified

- `apps/desktop/win/src/handlers/notification_handler.py` -- `asyncio.run()` -> `run_coroutine_threadsafe()`, `set_event_loop()` method
- `apps/desktop/win/src/main.py` -- event loop injection startup

---

## Bug 3: Hardcoded Token UUID

**Phase:** 3 (plan 01)
**Severity:** High -- Backend token validation bypass
**Component:** `zmq_client.py`, `auth_manager.py`, `config.py`

### ?

Desktop App Backend hardcoded test UUID token:

```python
# BUG:
"Token": "12345678-1234-1234-1234-123456789012"
```

token `send_url_alert()` `send_remote_access_alert()` Backend . Backend token , silent rejection alert (analysis result ).

### ?

Development/testing hardcoded UUID placeholder. production ,  Backend . UUID token validation , Backend alert silently rejected analysis .

token acquisition flow :
- `auth_manager.py` `authenticate()` always `True` token check
- `is_valid()` always `True`
- token `"12345678-1234-1234-1234-123456789012"` hardcoded

### ?

**RegisterDevice/RequestToken two-step flow:**

1. `zmq_client.py` `request_token()` method:
   - `RegisterDevice` message Backend ZMQ REQ
   - Backend token response -> token
   - device registered -> `RequestToken` message fresh socket
2. `auth_manager.py`:
   - `authenticate()` `zmq_client.request_token()` real token
   - `is_valid()` non-empty token + expiration check ( always-True)
   - `_save_token()` `auth.json` persist
3. `config.py`:
   - `USER_EMAIL` env var ( `user@example.com`)
   - `DEVICE_MAC` constant device identification
4. Hardcoded UUID :
   - `send_url_alert()` `send_remote_access_alert()` warning + empty string fallback

### Impact

Backend alerts token validation . URL , Backend analysis , score . ( Bug 2 notification bridge ).

### Commits

| Hash | Repo | Message |
|------|------|---------|
| `7674144` | apps | feat(03-01): add request_token method and remove hardcoded UUID fallback |
| `003981e` | apps | feat(03-01): wire token acquisition into AuthManager and startup flow |

### Recommendation

1. **Backend token validation error messages:** Backend token validation , , generic error ( "Invalid request"). Desktop App token .
   - : `"Token expired"`, `"Token not found"`, `"Device not registered"`
2. **Token expiration refresh:** token expiration , Desktop App token refresh . Backend push notification token invalidation .
3. **Startup health check:** Desktop App startup , Backend " " ZMQ message (token check ). startup connection .

### Files Modified

- `apps/desktop/win/src/zmq_client.py` -- `request_token()` method, hardcoded UUID
- `apps/desktop/win/src/auth_manager.py` -- real `authenticate()`, proper `is_valid()`
- `apps/desktop/win/src/config.py` -- `USER_EMAIL`, `DEVICE_MAC`
- `apps/desktop/win/src/core/container.py` -- `USER_EMAIL` AuthManager wiring
- `apps/desktop/win/src/main.py` -- token acquisition startup

---

## Bug 4: recv() vs recv_multipart() on ZMQ SUB Socket

**Phase:** 2 (plan 01)
**Severity:** Medium -- race condition potential, frame mismatch
**Component:** `notification_client.py`

### ?

`notification_client.py` `_listen()` method ZMQ SUB socket `recv()` calls multipart message:

```python
# BUG:
topic_bytes = self.socket.recv()      # 1: topic
message_bytes = self.socket.recv()    # 2: message
```

`recv_multipart()` , two separate `recv()` calls :

1. `recv()` topic frame , `recv()` (timeout, partial receive)
2. , topic message

### ?

ZMQ PUB/SUB multipart messages: `[topic_frame, message_frame]`. `recv_multipart()` all frames , `recv()` frame .

ZMQ `recv()` , :
- topic frame `recv()` , timeout `recv()` (zmq.Again)
- connection interleaving

, ZMQ single-threaded access , multi-publisher framing .

### ?

`recv_multipart()` atomic receive:

```python
# FIX:
frames = self.socket.recv_multipart()
if len(frames) < 2:
    _diag_log("RECV", f"WARNING: Expected 2+ frames, got {len(frames)}")
    continue
topic_bytes = frames[0]
message_bytes = frames[1]
```

:
- `recv_multipart()` all frames
- Frame count validation (2 )
- Future multi-frame messages forward-compatible (`< 2` check `!= 2`)

### Impact

. `recv_multipart()` ZMQ multipart messages best practice, single-publisher single-subscriber race condition . 2+ frames format , .

### Commits

| Hash | Repo | Message |
|------|------|---------|
| `2cd1c63` | apps | feat(02-01): fix ZMQ SUB to use atomic recv_multipart with diagnostic logging |

### Recommendation

1. **ZMQ multipart frame format :** Backend PUB socket multipart frame format :
   - Frame 0: topic (`device:{DeviceUid}`)
   - Frame 1: JSON message body
   - ( frames)
2. **Backend side validation:** Backend (NetMQ) `SendMoreFrame()` + `SendFrame()` pattern , atomic multipart send .

### Files Modified

- `apps/desktop/win/src/notification_client.py` -- `recv()` x2 -> `recv_multipart()` + frame validation

---

## Phase 5 Reliability Improvements (Summary)

Phase 5 3 :

### REL-01: ZMQ REQ Socket Recovery (Lazy Pirate Pattern)

**:** ZMQ REQ socket `recv()` timeout , socket state machine ("waiting for reply") . `send()` `EFSM` error.

**:** Lazy Pirate pattern -- timeout socket close + recreate:
- `_reset_socket()` method: `LINGER=0`, socket close, recreate same context
- `send_alert()` `socket.poll()` + retry loop (3 )
- CURVE re-apply recreated socket

**File:** `apps/desktop/win/src/zmq_client.py`
**Commit:** `6b7a012`

### REL-02: WebSocket Pending Results Store

**: ( )** Desktop App `broadcast()` WebSocket clients , message drop. Extension reconnect results .

**: ** `PendingResults` class `extension_server.py` , client connect flush.

### REL-03: Chrome Extension Service Worker Hardening

**: ( )** `setInterval`-based keepalive/heartbeat service worker termination . `chrome.alarms` backup.

**: ** `chrome.alarms` keepalive backup, `MessageQueueService` persistence `chrome.storage.session`.

---

## Recommendations for Server Team

### 1. CURVE Handshake Logging

**Priority:** High
**Component:** ASPSBackend (NetMQ CurveServer)

CurveMQ handshake failure logging . , client connection CURVE , timeout error message. NetMQ CURVE handshake event logging :
- Handshake ( client IP, key fingerprint)
- Handshake ( , key mismatch details)
- connection

### 2. Notification Delivery Confirmation

**Priority:** High
**Component:** ASPSBackend NotificationPublisher

ZMQ PUB socket fire-and-forget -- subscriber . delivery confirmation :
- publish log (timestamp, device UID, topic)
- Desktop App acknowledgment message ( ZMQ REQ)
- unacknowledged notifications

### 3. Token Validation Error Messages

**Priority:** Medium
**Component:** ASPSBackend AlertController / TokenValidator

token validation error generic error specific error messages:
- `"TokenExpired"` -- token expiration
- `"TokenNotFound"` -- token database
- `"DeviceNotRegistered"` -- device registration
- `"InvalidTokenFormat"` -- token UUID

Desktop App error messages handling .

### 4. ZMQ Multipart Frame Format Documentation

**Priority:** Medium
**Component:** ASPSBackend NotificationPublisher

PUB socket multipart frame format internal docs:
```
Frame 0: "device:{DeviceUid}" (UTF-8 topic string)
Frame 1: JSON notification body (UTF-8)
```
format :
- Python (pyzmq) `recv_multipart()`
- , C# client

### 5. Health Check Endpoint

**Priority:** Low
**Component:** ASPSBackend

Desktop App startup "ping" endpoint ZMQ REQ port:
- Backend
- version compatibility check
- (CURVE, token validation) state

:
```json
{
  "MessageType": "HealthCheck",
  "Response": {
    "Status": "OK",
    "Version": "14.1",
    "CurveEnabled": true,
    "Uptime": "2h 15m"
  }
}
```

### 6. RegisterDevice Response Standardization

**Priority:** Low
**Component:** ASPSBackend DeviceRegistration

RegisterDevice/RequestToken response format consistent JSON structure:
- : `status`, `token`, `expiration`, `deviceUid`, `serverPublicKey`
- PascalCase camelCase
- HTTP status code ZMQ (e.g., `"statusCode": 200` `"statusCode": 401`)

Desktop App response field `response.get("token") or response.get("Token")` -- convention .

---

## Appendix: Files Modified Across All Phases

### Desktop App (`apps/desktop/win/src/`)

| File | Phase(s) | Changes |
|------|----------|---------|
| `zmq_client.py` | 1, 3, 4, 5 | Diagnostic logging, `request_token()`, `apply_curve_client()`, Lazy Pirate retry |
| `notification_client.py` | 2, 4 | `recv_multipart()`, diagnostic logging, CURVE client encryption |
| `handlers/notification_handler.py` | 2 | `asyncio.run()` -> `run_coroutine_threadsafe()`, `set_event_loop()` |
| `extension_server.py` | 1 | Diagnostic logging WebSocket |
| `main.py` | 2, 3 | Event loop injection, token acquisition startup |
| `auth_manager.py` | 3, 4 | Real token flow, `is_valid()` check, server public key storage |
| `config.py` | 3, 4 | `USER_EMAIL`, `DEVICE_MAC`, `SERVER_PUBLIC_KEY_Z85`, `CURVE_ENABLED` |
| `core/container.py` | 3, 4 | `USER_EMAIL` wiring, CURVE settings wiring |
| `diag_zmq_test.py` | 1 | Standalone ZMQ diagnostic script (new file) |
| `diag_ws_test.py` | 1 | Standalone WebSocket diagnostic script (new file) |

### Backend (`ASPSBackend14_J/ASPSBackend/`)

| File | Phase(s) | Changes |
|------|----------|---------|
| `appsettings.json` | 1, 4 | `CurveEnabled: false` (Phase 1) -> `CurveEnabled: true` (Phase 4) |

### All Commits (Chronological)

| Phase | Hash | Repo | Message |
|-------|------|------|---------|
| 1 | `bb23741` | apps | feat(01-01): add diagnostic logging to zmq_client.py |
| 1 | `d6170ed` | apps | feat(01-01): create standalone ZMQ REQ/REP diagnostic test script |
| 1 | `5651758` | apps | feat(01-02): add diagnostic logging to extension_server.py |
| 1 | `07f8e64` | apps | feat(01-02): create WebSocket diagnostic test script |
| 1 | `2f47fdc` | backend | feat(01-01): disable CurveMQ for Phase 1-3 diagnostics |
| 2 | `2cd1c63` | apps | feat(02-01): fix ZMQ SUB to use atomic recv_multipart |
| 2 | `656843a` | apps | feat(02-02): fix NotificationHandler thread-to-asyncio bridge |
| 2 | `1fdd193` | apps | feat(02-02): inject event loop into NotificationHandler at startup |
| 3 | `7674144` | apps | feat(03-01): add request_token method and remove hardcoded UUID |
| 3 | `003981e` | apps | feat(03-01): wire token acquisition into AuthManager and startup |
| 4 | `d038234` | apps | feat(04-01): add CurveMQ client encryption to ZMQ sockets |
| 4 | `776b0a7` | backend | feat(04-01): re-enable CurveEnabled in Backend |
| 5 | `6b7a012` | apps | feat(05-01): add Lazy Pirate retry pattern to ZMQ REQ socket |

---

*Bug report generated: 2026-02-12*
*Total bugs documented: 4 (2 critical, 1 high, 1 medium)*
*Total commits: 13 (11 apps, 2 backend)*
