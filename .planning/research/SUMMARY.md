# Project Research Summary

**Project:** ASPS Score Flow Repair
**Domain:** Multi-process distributed system debugging (Chrome Extension + Python Desktop App + .NET Backend)
**Researched:** 2026-02-12
**Confidence:** HIGH

## Executive Summary

The ASPS system is a three-process anti-phishing score pipeline where URL analysis results must traverse multiple inter-process communication boundaries: Chrome Extension (JavaScript) communicates with Python Desktop App via WebSocket, which forwards alerts to .NET Backend via ZMQ REQ/REP, receives asynchronous analysis results via ZMQ PUB/SUB, and broadcasts scores back to the Extension via WebSocket. The system used to work but scores stopped reaching the extension. Research identified five critical failure points ranked by likelihood, with CurveMQ encryption mismatch and asyncio event loop conflicts as the top candidates.

The recommended repair approach is systematic link-by-link verification starting from the extension and working backwards to the backend, using targeted debugging tools (ZMQ proxy sniffers, PUB/SUB listeners, WebSocket frame inspection) to isolate the exact break point in the 18-step score flow. The architecture reveals that the Desktop App serves as a critical bridge between async WebSocket (event loop) and sync ZMQ operations (background threads), creating a thread-safety hazard when notifications from the ZMQ SUB thread attempt to broadcast to WebSocket clients on a different asyncio event loop.

Key risks center on silent failures: ZMQ PUB/SUB is lossy by design (slow subscriber problem), ZMQ REQ/REP has strict alternation requirements that corrupt on timeout, CurveMQ rejects unencrypted clients without error messages, and Chrome MV3 service workers terminate after 30s of inactivity destroying all WebSocket connections. The repair must address these reliability issues in addition to restoring basic functionality.

## Key Findings

### Recommended Stack

The existing stack is appropriate for the multi-process architecture: Chrome Extension (Manifest V3) for user-facing UI, Python with pyzmq/websockets for the bridge layer, and .NET with NetMQ for the analysis backend. The critical missing component is CurveMQ client implementation in the Python Desktop App.

**Core technologies:**
- **pyzmq (Python ZMQ bindings)**: REQ/REP client for alert submission, SUB client for notifications — currently lacks CURVE encryption support that backend requires
- **websockets (Python asyncio library)**: WebSocket server for Extension communication — runs on asyncio event loop, creating thread-safety issues with ZMQ background threads
- **NetMQ (.NET ZMQ bindings)**: REP server for alerts (port 50001), PUB server for notifications (port 50002) — has CurveMQ enabled by default, causing silent connection failures
- **Chrome MV3 Service Worker**: Background script for extension — terminates after 30s inactivity, destroying WebSocket connections and in-memory state

**Version requirements:**
- None specified in research (existing versions assumed compatible)

**Critical discovery:** The backend's `appsettings.json` has `"CurveEnabled": true` but the Python `zmq_client.py` and `notification_client.py` have ZERO CurveMQ client code. This means unencrypted Python clients cannot connect to encrypted backend sockets. TCP connection succeeds (async) but ZMQ CURVE handshake fails silently, causing all messages to be dropped.

### Expected Features

The system has a single critical feature: end-to-end score delivery from URL submission to icon update in under 5 seconds. All other features (caching, retry, reconnection) exist only to support this primary flow.

**Must have (table stakes):**
- **REQ/REP round-trip working** — Python Desktop App can send alerts to .NET Backend and receive acknowledgment
- **PUB/SUB notification delivery** — Python Desktop App receives analysis results published by Backend
- **WebSocket persistence** — Extension maintains connection to Desktop App despite MV3 service worker terminations
- **Link-by-link health monitoring** — Each communication boundary has diagnostic output to isolate failures

**Should have (competitive):**
- **CurveMQ encryption** — Secure ZMQ communication between Python and .NET (currently broken due to missing client implementation)
- **Graceful degradation** — System provides cached results or error states when pipeline is partially broken
- **Async-to-sync bridging** — Notification from ZMQ background thread correctly broadcasts to WebSocket async event loop

**Defer (v2+):**
- Token refresh mechanism (currently uses hardcoded UUID)
- Circuit breaker for backend failures
- Message persistence/replay for lost PUB/SUB notifications

### Architecture Approach

The Desktop App is a three-subsystem bridge running in a single Python process: asyncio event loop (WebSocket server + monitors), ZMQ REQ client (synchronous, connect-per-request), and ZMQ SUB client (background thread with blocking recv loop). The critical integration point is when the ZMQ SUB thread receives a notification and must call `ExtensionServer.broadcast()` which is an async operation on a different event loop.

**Major components:**
1. **ExtensionServer (async)** — WebSocket server on ports 8080-8484, maintains set of connected extension clients, runs on main asyncio event loop
2. **ZMQClient (sync)** — REQ socket client that creates a fresh connection for every alert (connect-send-recv-close pattern to avoid REQ/REP state machine corruption)
3. **NotificationClient (thread)** — SUB socket listener in background daemon thread, receives [topic, message] multipart frames, calls notification handler callback
4. **NotificationHandler (bridge)** — Runs on ZMQ thread, extracts analysis from notification, attempts to bridge back to asyncio event loop to broadcast to extension (uses `asyncio.get_running_loop()` with fallback to `asyncio.run()` which creates a NEW loop without WebSocket clients)

**Data flow (happy path):**
```
Extension --WS--> ExtensionHandler --sync--> ScanService --ZMQ REQ--> Backend
Extension <-WS--- ExtensionServer <--async-- NotificationHandler <-ZMQ SUB-- Backend
```

**Critical architectural flaw:** `NotificationHandler._broadcast_to_extension()` runs on the ZMQ notification thread. It calls `asyncio.get_running_loop()` which raises `RuntimeError` (no loop in this thread), then falls back to `asyncio.run()` which creates a NEW event loop that does NOT have the `extension_server.clients` set, causing the broadcast to fail silently. The correct fix is to store a reference to the main event loop and use `loop.call_soon_threadsafe()` to schedule the broadcast on the correct loop.

### Critical Pitfalls

1. **CurveMQ encryption mismatch (CRITICAL)** — Backend has `"CurveEnabled": true` in appsettings.json and applies `CurveKeyManager.ApplyServerCurve()` to both REP and PUB sockets. Python clients have NO CURVE client code. TCP connection succeeds but ZMQ CURVE handshake fails silently. No error messages, just timeouts. **Quick test:** Set `"CurveEnabled": false` in appsettings.json and restart backend. If messages flow, CURVE was the blocker.

2. **Thread-to-async bridging fails (HIGH)** — `NotificationHandler.handle()` runs on background thread, tries to broadcast to WebSocket (async operation). Uses `asyncio.get_running_loop()` fallback to `asyncio.run()`, which creates a NEW loop without WebSocket connections. Broadcast executes but sends to empty client set. **Fix:** Store main event loop at startup, use `loop.call_soon_threadsafe(asyncio.create_task, coro)` to schedule on correct loop.

3. **ZMQ PUB/SUB slow subscriber race (HIGH)** — Notification client starts in background thread AFTER WebSocket server starts. If Extension sends URL check before SUB subscription propagates, the analysis result notification is published before the subscriber is ready and lost forever (PUB/SUB is lossy by design). **Mitigation:** Ensure notification client is fully connected before accepting WebSocket connections, or add 500ms startup delay like test client uses.

4. **Non-atomic multipart receive (MEDIUM)** — Notification client uses two separate `recv()` calls for topic and message frames. If second recv times out (5s RCVTIMEO), the next recv gets the delayed message frame instead of a new topic, causing permanent frame-shift desync. **Fix:** Use `recv_multipart()` for atomic reception.

5. **Service worker termination destroys connections (MEDIUM)** — Chrome MV3 kills service workers after 30s inactivity. When killed, WebSocket connection object is destroyed, all pending scans lost, extension has no knowledge of in-flight analyses. If notification arrives while worker is dead, Desktop App broadcasts to disconnected client. **Mitigation:** Extension uses keepalive timer (20s) and chrome.alarms (30s minimum) to stay alive, but Chrome can still kill it. Graceful degradation requires detecting stale connections and queuing results for reconnection.

## Implications for Roadmap

Based on research, the repair roadmap should proceed in three phases with link-by-link verification:

### Phase 1: Establish Baseline Communication
**Rationale:** Before fixing the async flow, verify that basic synchronous communication works. This phase isolates whether the problem is in the ZMQ layer, WebSocket layer, or the bridge between them.

**Delivers:**
- ZMQ REQ/REP working (Python can send alert, receive ACK)
- WebSocket working (Extension can send url_check, receive immediate response)
- Diagnostic tools in place (ZMQ proxy sniffer, PUB/SUB listener, WebSocket frame inspector)

**Addresses:**
- CurveMQ encryption mismatch (either disable or implement client keys)
- Port conflicts and configuration mismatches
- Token validation issues

**Avoids:** Pitfall #1 (CurveMQ mismatch), Pitfall #8 (port mismatch), Pitfall #9 (token invalid)

**Tasks:**
1. Verify backend is running and ports 50001, 50002 are listening
2. Test ZMQ REQ/REP with standalone Python script (bypass Desktop App)
3. Temporarily disable CurveMQ (`"CurveEnabled": false`) or implement CURVE client in Python
4. Verify token validation (use real token or bypass for debugging)
5. Test WebSocket connection (Extension -> Desktop App ping/pong)
6. Deploy diagnostic tools (ZMQ proxy on port 60001, SUB sniffer subscribing to all topics)

### Phase 2: Fix Async Notification Bridge
**Rationale:** Once synchronous REQ/REP and WebSocket work independently, fix the async gap where PUB/SUB notifications must bridge from ZMQ background thread to asyncio WebSocket broadcast.

**Delivers:**
- Notification handler correctly broadcasts to extension
- Event loop stored at startup and passed to notification handler
- Thread-safe async scheduling via `call_soon_threadsafe()`
- End-to-end score flow verified with test URLs

**Addresses:**
- Thread-to-async bridging failure (Pitfall #2)
- PUB/SUB slow subscriber race (Pitfall #3)
- Multipart receive atomicity (Pitfall #4)

**Uses:** Python asyncio, threading module, pyzmq `recv_multipart()`

**Implements:** Desktop App bridge architecture fix

**Tasks:**
1. Store main event loop reference in `AntiScamApp.__init__`: `self._loop = asyncio.get_event_loop()`
2. Pass loop to NotificationHandler constructor
3. Replace `asyncio.get_running_loop()` fallback with `self._loop.call_soon_threadsafe(asyncio.create_task, coro)`
4. Replace `notification_client.py` two `recv()` calls with single `recv_multipart()`
5. Add startup delay or barrier to ensure SUB subscription completes before accepting WebSocket connections
6. Test end-to-end flow with debug logging at each link
7. Verify Extension receives url_result with score and updates icon

### Phase 3: Restore CurveMQ and Harden Reliability
**Rationale:** Once the core flow works, re-enable encryption and add resilience for production use.

**Delivers:**
- CurveMQ client implementation in Python (both REQ and SUB sockets)
- Server public key distribution mechanism
- Connection health monitoring
- Graceful handling of service worker terminations

**Addresses:**
- CurveMQ encryption (Pitfall #1, but now with proper client implementation)
- Service worker termination (Pitfall #5)
- REQ/REP deadlock recovery (Pitfall #3)
- Cache masking failures during testing (Pitfall #10)

**Implements:** Security layer, reliability layer

**Tasks:**
1. Implement CURVE client keys in `zmq_client.py`: `socket.setsockopt(zmq.CURVE_SERVERKEY, server_public_z85)`
2. Same for `notification_client.py` SUB socket
3. Distribute server public key (from backend's `ServerPublicKeyZ85`) to Desktop App config
4. Re-enable `"CurveEnabled": true` in backend and test end-to-end
5. Add ZMQ connection health checks (proactive ping, not just timeout on request)
6. Add Extension reconnection recovery (re-queue pending scans after WebSocket reconnect)
7. Clear caches during testing to avoid false positives

### Phase Ordering Rationale

- **Phase 1 first** because it isolates the problem domain. Without knowing which link is broken, debugging the async bridge is premature. Also, temporarily disabling CURVE allows testing the pure communication layer.
- **Phase 2 second** because fixing the async bridge requires working REQ/REP and WebSocket from Phase 1. The PUB/SUB notification is the last link in the chain; it depends on all previous links working.
- **Phase 3 last** because encryption and reliability features should not be enabled until the core flow is proven working. Re-enabling CURVE after fixing the bridge ensures the fix wasn't masking a CURVE issue.

**Dependency chain:**
- Phase 2 depends on Phase 1 (needs working ZMQ + WebSocket)
- Phase 3 depends on Phase 2 (needs working async notification flow)

**Risk mitigation:**
- Phase 1 uses standalone test scripts to bypass the Desktop App bridge (isolates backend issues)
- Phase 2 uses extensive logging at each step to verify the notification reaches the extension
- Phase 3 re-enables security without breaking the fix (CURVE is added on top of working flow)

### Research Flags

**Phases likely needing deeper research during planning:**
- **Phase 3 (CURVE implementation):** NetMQ-to-pyzmq CURVE interop is complex. Key format (Z85 vs binary), client keypair generation, and server public key distribution need validation. The backend already returns `serverPublicKey` in token responses, but the handshake flow needs testing.

**Phases with standard patterns (skip research-phase):**
- **Phase 1:** Standard ZMQ REQ/REP and WebSocket patterns, well-documented
- **Phase 2:** asyncio cross-thread scheduling is a common Python pattern, documented in asyncio library

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Existing stack appropriate; only CurveMQ client missing |
| Features | HIGH | Single critical feature (score delivery) clearly defined by codebase analysis |
| Architecture | HIGH | All components and message flows mapped from source code |
| Pitfalls | HIGH | Failure points identified by code inspection and ZMQ/asyncio domain knowledge; ranked by likelihood based on architectural analysis |

**Overall confidence:** HIGH

Research is based entirely on direct codebase analysis of `.planning/codebase/` files. No external sources needed. All ZMQ patterns, asyncio threading, WebSocket handling, and NetMQ interop issues are derived from reading actual implementation code. The top 5 pitfalls are not speculation — they are observable architectural issues in the source.

### Gaps to Address

**Minor gaps requiring validation during implementation:**

- **Actual CurveEnabled value in production:** Research assumes `"CurveEnabled": true` based on default in appsettings.json, but this should be verified in the running backend before implementing CURVE client. If CURVE is already disabled in production, Phase 1 completes faster.

- **Token handling during debugging:** The Python `zmq_client.py` uses a hardcoded UUID fallback `"12345678-1234-1234-1234-123456789012"`. Unknown whether this token is registered in backend's TokenStore or if token validation can be bypassed for debugging. May need to implement token registration flow before testing.

- **Backend analysis completion time:** Research assumes analysis completes quickly (milliseconds to seconds), but external analyzer (Python script) timeout is unknown. If analysis takes >5s, ZMQ REQ timeout fires before async notification publishes. May need to increase `RCVTIMEO` in `zmq_client.py` from 5000ms to 15000ms during debugging.

- **Extension cache behavior:** CacheService and CacheManager have 1-hour TTLs. During repair testing, must explicitly clear caches or use never-before-seen URLs to avoid false positives where cached results mask a broken pipeline.

**These gaps do not block the roadmap — they are "discover during Phase 1" items.**

## Sources

### Primary (HIGH confidence)
- `.planning/codebase/` directory — Full source code of all three applications (Extension, Desktop App, Backend) analyzed line-by-line
- `apps/extension/chrome/background.js` — Extension service worker lifecycle and WebSocket handling
- `apps/desktop/win/src/main.py` — Desktop App threading model and async subsystem initialization
- `apps/desktop/win/src/zmq_client.py` — ZMQ REQ client implementation, connect-per-request pattern
- `apps/desktop/win/src/notification_client.py` — ZMQ SUB client implementation, background thread
- `apps/desktop/win/src/handlers/notification_handler.py` — Thread-to-async bridging code with `asyncio.get_running_loop()` fallback
- `ASPSBackend14_J/Business/Messaging/RealTimeAlertListener.cs` — Backend REP server, alert routing
- `ASPSBackend14_J/Business/Messaging/NotificationPublisher.cs` — Backend PUB server, analysis result publishing
- `ASPSBackend14_J/Business/Services/CurveKeyManager.cs` — CurveMQ server-side configuration

### Secondary (MEDIUM confidence)
- ZMQ Guide (zmq.org) — REQ/REP state machine behavior, PUB/SUB slow subscriber problem, CURVE encryption handshake
- Python asyncio documentation — Cross-thread event loop scheduling patterns, `call_soon_threadsafe()` usage

### Tertiary (LOW confidence)
- None (no speculation; all findings from source code)

---
*Research completed: 2026-02-12*
*Ready for roadmap: yes*
