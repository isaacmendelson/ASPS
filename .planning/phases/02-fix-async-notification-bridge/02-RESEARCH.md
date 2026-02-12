# Phase 2: Fix Async Notification Bridge - Research

**Researched:** 2026-02-12
**Domain:** Python asyncio thread-to-event-loop bridging, ZeroMQ PUB/SUB multipart messaging (pyzmq), WebSocket broadcasting
**Confidence:** HIGH (based on direct codebase reading, official Python docs, and pyzmq documentation)

## Summary

Phase 2 fixes the broken notification delivery path: when the Backend publishes analysis results via ZMQ PUB (port 50002), the Desktop App's ZMQ SUB client receives them in a background thread, but the `NotificationHandler.handle()` method fails to bridge the notification to the asyncio event loop where WebSocket clients are connected. Two distinct bugs were identified through codebase analysis.

**Bug 1 (Thread-to-asyncio bridge):** `NotificationHandler.handle()` (line 66-72 of `notification_handler.py`) tries `asyncio.get_running_loop()` which raises `RuntimeError` because `handle()` is called from the ZMQ background thread (via `NotificationClient._listen()`), not from an async context. The fallback `asyncio.run()` creates a **new, ephemeral event loop** in the ZMQ thread -- this loop has zero WebSocket clients connected to it, so `broadcast()` sends to nobody. The correct fix is to capture a reference to the main asyncio event loop (the one running `ExtensionServer`) and use `asyncio.run_coroutine_threadsafe()` to schedule the broadcast coroutine on that loop.

**Bug 2 (Non-atomic multipart receive):** `NotificationClient._listen()` (line 109-110 of `notification_client.py`) uses two separate `self.socket.recv()` calls instead of `self.socket.recv_multipart()`. While this works functionally for ZMQ PUB/SUB (frames are delivered atomically per message), using `recv_multipart()` is more robust, idiomatic, and handles edge cases where frame counts might vary.

**Primary recommendation:** Store a reference to the main asyncio event loop and use `asyncio.run_coroutine_threadsafe(coro, loop)` to schedule broadcast coroutines from the ZMQ thread. Replace dual `recv()` with `recv_multipart()`.

## Standard Stack

Phase 2 uses only libraries already in the codebase. No new dependencies needed.

### Core (Already in Codebase)
| Library | Version | Purpose | File |
|---------|---------|---------|------|
| pyzmq | >=25.1.0 | ZMQ SUB client receiving notifications | `notification_client.py` |
| websockets | >=12.0 | WebSocket server broadcasting to Extensions | `extension_server.py` |
| asyncio (stdlib) | Python 3.10+ | Event loop, `run_coroutine_threadsafe()` | `notification_handler.py`, `main.py` |
| threading (stdlib) | Python 3.10+ | Background thread for ZMQ listener | `notification_client.py`, `main.py` |

### Supporting (Already in Codebase)
| Library | Version | Purpose | When Used |
|---------|---------|---------|-----------|
| json (stdlib) | Python 3.10+ | Message serialization/deserialization | All notification handling |
| concurrent.futures (stdlib) | Python 3.10+ | `Future` returned by `run_coroutine_threadsafe()` | Thread bridge verification |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `run_coroutine_threadsafe()` | `loop.call_soon_threadsafe()` | `call_soon_threadsafe` only works with regular callbacks, not coroutines. Since `broadcast()` is `async def`, `run_coroutine_threadsafe()` is required |
| `run_coroutine_threadsafe()` | `asyncio.Queue` with async consumer | More complex architecture; overkill for simple notification forwarding |
| `recv_multipart()` | Two separate `recv()` calls (current) | Current approach works but is not idiomatic; `recv_multipart()` is safer and cleaner |

**Installation:** No new packages needed.

## Architecture Patterns

### Current Architecture (Broken)
```
ZMQ Background Thread                    Main Asyncio Thread (background threading.Thread)
--------------------                     -------------------------------------------
NotificationClient._listen()            asyncio.run(app.start())
  |                                       |
  | socket.recv() x2                      | ExtensionServer (websockets.serve)
  |                                       |   .clients = {ws1, ws2, ...}
  v                                       |   .broadcast() -- async, needs this loop
NotificationHandler.handle()             |
  |                                       |
  | asyncio.get_running_loop()  <-- FAILS: no loop in this thread
  | asyncio.run(broadcast)      <-- WRONG: creates NEW loop, no clients
  v
broadcast() runs on wrong loop --> nobody receives message
```

### Fixed Architecture (Target)
```
ZMQ Background Thread                    Main Asyncio Thread
--------------------                     -------------------------------------------
NotificationClient._listen()            asyncio.run(app.start())
  |                                       |
  | socket.recv_multipart()               | ExtensionServer (websockets.serve)
  |                                       |   .clients = {ws1, ws2, ...}
  v                                       |   .broadcast() -- async
NotificationHandler.handle()             |
  |                                       |
  | asyncio.run_coroutine_threadsafe(     |
  |   broadcast_coro,                     |
  |   self._main_loop  --------+-------->| scheduled here
  | )                          |          |   broadcast() runs on correct loop
  |                            |          |   sends to all connected clients
  v                            |          v
Future.result(timeout=5)       +--------> ws1, ws2, ... receive message
```

### Pattern 1: Thread-Safe Asyncio Bridge
**What:** Schedule a coroutine from a non-async thread onto a running asyncio event loop.
**When to use:** Whenever a background thread (like the ZMQ listener) needs to invoke async code (like WebSocket broadcast) that lives on a different thread's event loop.
**How:**
```python
# Source: https://docs.python.org/3/library/asyncio-dev.html
# Step 1: Capture the event loop reference from within the async context
loop = asyncio.get_running_loop()
# Pass `loop` to the handler that will be called from another thread

# Step 2: From the background thread, schedule the coroutine
future = asyncio.run_coroutine_threadsafe(
    self._broadcast_to_extension(analysis, cache_data),
    self._main_loop
)

# Step 3 (optional): Wait for completion with timeout
try:
    future.result(timeout=5.0)
except TimeoutError:
    logger.error("Broadcast timed out")
except Exception as e:
    logger.error(f"Broadcast failed: {e}")
```

### Pattern 2: Event Loop Capture and Injection
**What:** The main asyncio context captures its event loop and injects it into components that will be called from other threads.
**When to use:** During application startup, before any background threads are started.
**How:**
```python
# In main.py's async start() method:
async def start(self):
    # Capture the running loop
    main_loop = asyncio.get_running_loop()

    # Inject into notification handler
    self.container.notification_handler.set_event_loop(main_loop)

    # ... then start notification client (which spawns background thread)
    self.container.notification_client.start()
```

### Pattern 3: Atomic Multipart Receive
**What:** Use `recv_multipart()` instead of separate `recv()` calls for PUB/SUB messages.
**When to use:** Always when receiving multipart ZMQ messages.
**How:**
```python
# Source: https://pyzmq.readthedocs.io/en/latest/api/zmq.html
# Instead of:
#   topic_bytes = self.socket.recv()
#   message_bytes = self.socket.recv()

# Use:
frames = self.socket.recv_multipart()
topic_bytes = frames[0]
message_bytes = frames[1]
```

### Anti-Patterns to Avoid
- **`asyncio.run()` from a background thread as a bridge:** Creates a new, isolated event loop in that thread. Any async objects (WebSocket connections, server state) from the main loop are not accessible. This is the primary bug in the current code.
- **`asyncio.get_running_loop()` from a non-async context:** Raises `RuntimeError`. There is no running loop in a plain thread that didn't call `asyncio.run()`.
- **`asyncio.get_event_loop()` from a background thread (Python 3.10+):** Deprecated behavior; will not return the main thread's loop. Emits a DeprecationWarning and may raise RuntimeError in Python 3.12+.
- **Calling `future.result()` from the event loop thread:** Causes deadlock. Only call from the background thread.
- **Storing `asyncio.get_running_loop()` result in a class variable at import time:** The loop doesn't exist yet. Must be captured at runtime inside an async function.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Thread-to-asyncio coroutine scheduling | Custom queue + consumer loop | `asyncio.run_coroutine_threadsafe()` | Built-in, thread-safe, returns Future for error handling |
| Thread-safe event loop callback | `threading.Event` + polling | `loop.call_soon_threadsafe()` | Built into asyncio, avoids polling overhead |
| ZMQ multipart message assembly | Manual `recv()` loop with `getsockopt(RCVMORE)` | `socket.recv_multipart()` | pyzmq handles frame counting, buffering, and atomicity |
| Cross-thread error propagation | Shared variables + locks | `concurrent.futures.Future` from `run_coroutine_threadsafe()` | Returns exceptions from the async side to the calling thread |

**Key insight:** Python's asyncio stdlib provides all the primitives needed for thread-to-async bridging. The current bug exists because the code tried to create a new async context instead of bridging into the existing one.

## Common Pitfalls

### Pitfall 1: `asyncio.run()` Creates an Isolated Event Loop
**What goes wrong:** `asyncio.run()` creates a brand new event loop, runs the coroutine on it, then closes it. Any references to objects on the main event loop (like `ExtensionServer.clients`) are unreachable from this new loop's perspective. The `broadcast()` call executes successfully but sends to zero clients because `self.clients` contains WebSocket connections bound to the main loop.
**Why it happens:** Developer confusion between "running async code" and "running async code on the correct loop." `asyncio.run()` is for top-level entry points, not for cross-thread scheduling.
**How to avoid:** Use `asyncio.run_coroutine_threadsafe(coro, target_loop)` which schedules the coroutine on an existing running loop.
**Warning signs:** No errors in logs, but WebSocket clients never receive messages. `broadcast()` appears to succeed but `self.clients` is empty from the wrong loop's perspective.
**Confidence:** HIGH -- verified by reading `notification_handler.py` lines 66-72 and Python asyncio documentation.

### Pitfall 2: Event Loop Reference Not Available in Handler
**What goes wrong:** `NotificationHandler` is created in `container.py` without a reference to the main asyncio event loop. The loop doesn't exist yet when the container initializes (it's created later by `asyncio.run(self.start())` in `main.py` line 206).
**Why it happens:** The container uses lazy initialization, but the event loop is only available after `asyncio.run()` starts in the background thread.
**How to avoid:** Add a `set_event_loop(loop)` method to `NotificationHandler`. Call it from within `AntiScamApp.start()` (which runs inside the asyncio loop) BEFORE starting the notification client. This is the same pattern already used for `set_extension_server()`.
**Warning signs:** `AttributeError: 'NotificationHandler' has no attribute '_main_loop'` if accessed before injection.
**Confidence:** HIGH -- verified from `main.py` line 135 (existing `set_extension_server` pattern) and `container.py` lines 264-270.

### Pitfall 3: Race Condition Between Loop Capture and Thread Start
**What goes wrong:** If the notification client starts before the event loop reference is injected into the handler, the handler has no loop to bridge to.
**How to avoid:** The startup sequence in `main.py` already handles this correctly: `set_extension_server()` is called at line 135, BEFORE `notification_thread.start()` at line 155. The `set_event_loop()` call must be added in the same location (between extension server start and notification client start).
**Warning signs:** Intermittent failures on application startup; handler sometimes has a loop reference, sometimes doesn't.
**Confidence:** HIGH -- verified from startup sequence in `main.py` lines 128-155.

### Pitfall 4: ZMQ SUB Topic Mismatch
**What goes wrong:** The SUB socket subscribes to `device:{device_uid}` but the PUB socket publishes with a different topic format, or the subscription doesn't match.
**Why it happens:** Topic prefix matching in ZMQ is byte-level. If the PUB side uses `"device:PC-JOHN-001"` and the SUB side subscribes to `"device:PC-JOHN-001"`, they match exactly. But any difference (whitespace, case, encoding) causes silent message drops.
**How to avoid:** Verify the exact topic string on both sides:
- Backend PUB (`NotificationPublisher.cs` line 84): `$"device:{deviceUid}"` -- uses `SendMoreFrame(deviceTopic).SendFrame(json)`
- Desktop SUB (`notification_client.py` line 95-96): `f"device:{self.device_uid}"` -- subscribes with `socket.subscribe(topic.encode('utf-8'))`
- Both must use the same `device_uid` value (e.g., `"PC-JOHN-001"` from `main.py` line 39)
**Warning signs:** ZMQ SUB socket connected but never receives messages. Heartbeat prints show "still listening" forever.
**Confidence:** HIGH -- verified from both source files.

### Pitfall 5: Non-Atomic Multipart Receive Under Timeout
**What goes wrong:** Current code uses two separate `recv()` calls (lines 109-110 of `notification_client.py`). With `RCVTIMEO=5000`, it's possible (though unlikely) for the first `recv()` to succeed (getting the topic frame) but the second `recv()` to time out if the ZMQ internal buffer is in a transient state. This would leave the topic bytes consumed but the message bytes lost.
**Why it happens:** ZMQ actually delivers multipart messages atomically, so in practice both frames are available together. But using `recv_multipart()` makes this guarantee explicit and handles any future changes to frame count.
**How to avoid:** Replace the two `recv()` calls with a single `recv_multipart()` call.
**Warning signs:** Occasional `zmq.Again` errors after receiving partial messages.
**Confidence:** MEDIUM -- ZMQ's atomicity guarantee means this is unlikely in practice, but `recv_multipart()` is still the correct API choice per pyzmq documentation.

### Pitfall 6: WebSocket Client Connected to Wrong Event Loop
**What goes wrong:** If `broadcast()` somehow runs on a different event loop than the one where WebSocket connections were accepted, `await client.send()` may raise errors or silently fail because the WebSocket protocol objects are bound to their creation loop.
**Why it happens:** This is a consequence of Pitfall 1 (using `asyncio.run()` instead of `run_coroutine_threadsafe()`).
**How to avoid:** Always broadcast on the same event loop that `websockets.serve()` is running on. Using `run_coroutine_threadsafe()` with the correct loop reference guarantees this.
**Warning signs:** `RuntimeError: Event loop is closed` or `RuntimeError: Non-thread-safe operation invoked on an event loop`.
**Confidence:** HIGH -- standard asyncio/websockets behavior.

## Code Examples

Verified patterns from official sources and codebase analysis:

### Fix 1: NotificationHandler with Event Loop Injection
```python
# Source: Codebase notification_handler.py + Python asyncio docs
# https://docs.python.org/3/library/asyncio-dev.html

class NotificationHandler:
    def __init__(self, protection_service, cache, extension_server=None):
        self.protection_service = protection_service
        self.cache = cache
        self.extension_server = extension_server
        self._main_loop = None  # NEW: event loop reference

    def set_extension_server(self, extension_server):
        self.extension_server = extension_server

    def set_event_loop(self, loop):  # NEW METHOD
        """Set the main asyncio event loop for thread-safe scheduling"""
        self._main_loop = loop

    def handle(self, notification):
        """Handle notification from backend (called from ZMQ thread)"""
        # ... existing notification processing ...

        if analysis['url'] and analysis['risk_score'] is not None:
            cache_data = self._update_cache(analysis, protective_actions)

            if self.extension_server and cache_data:
                # FIXED: Use run_coroutine_threadsafe instead of asyncio.run()
                if self._main_loop and self._main_loop.is_running():
                    future = asyncio.run_coroutine_threadsafe(
                        self._broadcast_to_extension(analysis, cache_data),
                        self._main_loop
                    )
                    try:
                        future.result(timeout=5.0)
                    except Exception as e:
                        logger.error(f"Broadcast failed: {e}")
                else:
                    logger.warning("No running event loop - cannot broadcast to extension")
```

### Fix 2: Event Loop Injection in main.py
```python
# Source: main.py startup sequence
# Add after line 135 (set_extension_server), before notification client start

async def start(self):
    # ... existing extension server start ...

    # Connect notification handler to extension server
    self.container.notification_handler.set_extension_server(
        self.container.extension_server
    )

    # NEW: Inject event loop reference for thread-safe bridging
    self.container.notification_handler.set_event_loop(
        asyncio.get_running_loop()
    )

    # ... existing notification client start ...
```

### Fix 3: Atomic Multipart Receive in NotificationClient
```python
# Source: pyzmq docs - https://pyzmq.readthedocs.io/en/latest/api/zmq.html
# Replace notification_client.py lines 108-113

while self.running:
    try:
        # FIXED: Use recv_multipart for atomic multipart message receipt
        frames = self.socket.recv_multipart()

        if len(frames) < 2:
            print(f"[NOTIFY] WARNING: Expected 2 frames, got {len(frames)}")
            continue

        topic_str = frames[0].decode('utf-8')
        message_str = frames[1].decode('utf-8')

        self._handle_notification(topic_str, message_str)
        heartbeat_counter = 0

    except zmq.Again:
        # Timeout - normal, no messages received
        heartbeat_counter += 1
        if heartbeat_counter >= 24:
            print(f"[NOTIFY] HEARTBEAT: Still listening...")
            heartbeat_counter = 0
        continue
```

### Diagnostic: Verify Event Loop Identity
```python
# Add to notification_handler.py for Phase 2 verification
import asyncio

def handle(self, notification):
    """Handle notification - diagnostic version"""
    import threading
    print(f"[NOTIFY-DIAG] handle() called from thread: {threading.current_thread().name}")
    print(f"[NOTIFY-DIAG] Main loop reference: {self._main_loop}")
    print(f"[NOTIFY-DIAG] Main loop running: {self._main_loop.is_running() if self._main_loop else 'N/A'}")

    # ... rest of handle logic ...

    if self._main_loop and self._main_loop.is_running():
        future = asyncio.run_coroutine_threadsafe(
            self._broadcast_to_extension(analysis, cache_data),
            self._main_loop
        )
        print(f"[NOTIFY-DIAG] Scheduled broadcast on loop, future: {future}")
        try:
            result = future.result(timeout=5.0)
            print(f"[NOTIFY-DIAG] Broadcast completed successfully")
        except Exception as e:
            print(f"[NOTIFY-DIAG] Broadcast error: {e}")
```

### Backend PUB Message Format (for reference)
```json
// Topic frame (first ZMQ frame): "device:PC-JOHN-001"
// Message frame (second ZMQ frame):
{
    "Type": "AnalysisResult",
    "Timestamp": "2026-02-12T12:00:00Z",
    "DeviceUid": "PC-JOHN-001",
    "Data": {
        "TypeName": "AnalysisResultNotification",
        "AlertType": "UrlAlert",
        "Severity": "Medium",
        "RiskAssessment": {
            "risk_score": 75,
            "risk_level": "High",
            "is_scam": true,
            "confidence": 0.85
        },
        "AnalysisResult": {
            "TypeName": "UrlAnalysisResult",
            "Url": "https://example-phishing.com",
            "Domain": "example-phishing.com",
            "analysis_time_ms": 1234,
            "IsFromCache": false,
            "Recommendation": "Block",
            "risk_assessment": {
                "risk_score": 75,
                "risk_level": "High",
                "is_scam": true,
                "confidence": 0.85
            },
            "phishing_check": {
                "Is_known_phishing": false,
                "Source": "Internal"
            }
        },
        "Indicators": [...],
        "protectiveActions": [...],
        "AnalysisTimestamp": "2026-02-12T12:00:00Z"
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `asyncio.run()` from background thread | `asyncio.run_coroutine_threadsafe(coro, loop)` | Always was correct; codebase had a bug | Fixes the entire notification bridge |
| `asyncio.get_event_loop()` (deprecated in 3.10+) | `asyncio.get_running_loop()` (inside async context) | Python 3.10 | Must capture loop inside `async def start()`, not at import/init time |
| Two separate `socket.recv()` calls | `socket.recv_multipart()` | Always available in pyzmq | Atomic multipart receipt, cleaner API |

**Deprecated/outdated:**
- `asyncio.get_event_loop()` from non-main threads: Deprecated since Python 3.10. Use `asyncio.get_running_loop()` inside async contexts or pass loop references explicitly.

## Exact Changes Required

Summary of all code changes needed for Phase 2:

### File 1: `notification_handler.py`
| Line(s) | Current | Change To | Why |
|---------|---------|-----------|-----|
| 23 | `__init__(self, protection_service, cache, extension_server=None)` | Add `self._main_loop = None` | Store event loop reference |
| NEW | (none) | Add `set_event_loop(self, loop)` method | Inject loop from main.py |
| 66-72 | `asyncio.get_running_loop()` / `asyncio.run()` fallback | `asyncio.run_coroutine_threadsafe(coro, self._main_loop)` | Fix the bridge |

### File 2: `main.py`
| Line(s) | Current | Change To | Why |
|---------|---------|-----------|-----|
| After 135 | (none) | Add `self.container.notification_handler.set_event_loop(asyncio.get_running_loop())` | Inject loop before notification client starts |

### File 3: `notification_client.py`
| Line(s) | Current | Change To | Why |
|---------|---------|-----------|-----|
| 109-110 | `topic_bytes = self.socket.recv()` + `message_bytes = self.socket.recv()` | `frames = self.socket.recv_multipart()` | Atomic multipart receive |

## Open Questions

1. **Does the notification callback preserve the notification dict across threads?**
   - What we know: `NotificationClient._handle_notification()` parses JSON and calls `self._on_notification_callback(notification)` where `notification` is a Python dict. Dicts are thread-safe for single reads in CPython (GIL), and the dict is not modified after creation.
   - What's unclear: Whether there are any reference-counting edge cases with large nested dicts across threads.
   - Recommendation: Not a practical concern. Python's GIL and the fact that we pass a freshly-created dict make this safe. No action needed.

2. **Should `run_coroutine_threadsafe()` use a timeout?**
   - What we know: `future.result(timeout=5.0)` blocks the ZMQ thread for up to 5 seconds. During this time, no new ZMQ messages are processed.
   - What's unclear: Whether blocking is acceptable or if we should fire-and-forget.
   - Recommendation: Use a short timeout (5 seconds) with error logging. The ZMQ `RCVTIMEO` is also 5 seconds, so the thread is already designed to tolerate brief blocks. Alternatively, don't call `future.result()` at all (fire-and-forget) and just log the future for debugging. The planner should decide.

3. **Should we add a subscribe to empty string (subscribe to all topics) as a diagnostic?**
   - What we know: Subscribing to `b""` (empty bytes) receives ALL messages on the PUB socket. This is useful for debugging topic mismatches.
   - What's unclear: Whether this should be a diagnostic-only option or a permanent fallback.
   - Recommendation: For Plan 02-01, consider temporarily subscribing to `b""` to verify PUB/SUB connectivity, then switch to device-specific topic. This can be a diagnostic step, not a permanent change.

## Sources

### Primary (HIGH confidence)
- Direct codebase reading: `notification_handler.py`, `notification_client.py`, `extension_server.py`, `main.py`, `container.py`, `NotificationPublisher.cs`, `NotificationPublisherActor.cs`, `AnalysisResultNotification.cs`, `config.py`
- [Python asyncio - Developing with asyncio (official docs)](https://docs.python.org/3/library/asyncio-dev.html) - Thread-safe scheduling with `run_coroutine_threadsafe()` and `call_soon_threadsafe()`
- [Python asyncio - Event Loop (official docs)](https://docs.python.org/3/library/asyncio-eventloop.html) - `get_running_loop()` vs `get_event_loop()` semantics
- [pyzmq API documentation](https://pyzmq.readthedocs.io/en/latest/api/zmq.html) - `recv_multipart()` and `subscribe()` method signatures

### Secondary (MEDIUM confidence)
- [ZMQ Guide Chapter 2 - Sockets and Patterns](https://zguide.zeromq.org/docs/chapter2/) - PUB/SUB multipart messaging patterns
- [Super Fast Python - run_coroutine_threadsafe](https://superfastpython.com/asyncio-run_coroutine_threadsafe/) - Practical examples of thread-to-async bridging

### Tertiary (LOW confidence)
- None. All findings verified against official documentation.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - All libraries already in codebase, no new dependencies
- Architecture: HIGH - Bug root cause identified by direct code reading; fix pattern verified against official Python asyncio docs
- Pitfalls: HIGH - All pitfalls derived from reading actual code paths and verified against official documentation
- Code examples: HIGH - Patterns match official Python docs and pyzmq API

**Research date:** 2026-02-12
**Valid until:** 2026-03-12 (stable -- asyncio thread-safety APIs are stable since Python 3.7)
