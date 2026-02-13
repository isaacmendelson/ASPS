# Phase 7: Fix Communication Bugs - Research

**Researched:** 2026-02-13
**Domain:** Python async/sync bridging, Chrome Extension MV3 messaging, WebSocket broadcast reliability, cross-component message type alignment
**Confidence:** HIGH

## Summary

Phase 7 fixes 4 specific bugs that prevent the end-to-end scan result flow from working correctly. These bugs were identified during v1.1 code review and are distinct from the v1.0 bugs (async bridge, token, CURVE) which have already been fixed. The bugs span the Desktop App (Python) and Chrome Extension (JavaScript) and prevent the Extension from receiving, matching, and displaying scan results.

The bugs are: (1) missing `url` field in scan_service.py responses preventing the Extension from caching results or resolving pending scans, (2) sync handler methods called from async context in extension_handler.py that block the asyncio event loop during ZMQ I/O, (3) message type constant mismatch in content.js where `getPageInfo` is used instead of `page:info:request`, and (4) silent broadcast failure in notification_handler.py where broadcast errors are logged but not retried.

All four bugs are straightforward code fixes. No new libraries, no architectural changes, no external dependencies. The primary risk is regression in the async handler fix (COMM-06) since it involves changing the calling pattern of methods that do synchronous ZMQ network I/O.

**Primary recommendation:** Fix each bug in isolation with targeted changes, then verify end-to-end flow with a fresh (uncached) URL.

## Standard Stack

### Core (Already in Codebase -- No Changes)
| Library | Version | Purpose | File |
|---------|---------|---------|------|
| websockets | >=12.0 | WebSocket server for Extension communication | `extension_server.py` |
| pyzmq | >=25.1.0 | ZMQ REQ/REP and PUB/SUB with Backend | `zmq_client.py`, `notification_client.py` |
| asyncio (stdlib) | Python 3.10+ | Event loop, `run_coroutine_threadsafe()` | `extension_handler.py`, `notification_handler.py` |
| Chrome Extension MV3 | Manifest V3 | Service worker, content scripts, messaging | `background.js`, `content.js`, `ScanService.js` |

### Supporting (Already in Codebase -- No Changes)
| Library | Version | Purpose | When Used |
|---------|---------|---------|-----------|
| json (stdlib) | Python 3.10+ | Message serialization | All message handling |
| threading (stdlib) | Python 3.10+ | ZMQ SUB listener thread | `notification_client.py` |
| chrome.tabs API | MV3 | Send messages to content scripts | `ScanService.js`, `background.js` |
| chrome.runtime API | MV3 | Content script <-> background messaging | `content.js` |

**Installation:** No new packages needed. All fixes use existing dependencies.

## Architecture Patterns

### Current Communication Flow (With Bug Locations Marked)

```
Chrome Extension                    Desktop App (Python)                Backend (.NET)
===============                     ====================                ==============

ScanService.scan(tabId, url)
  |
  | chrome.tabs.sendMessage(tabId, {type: 'page:info:request'})
  |     |
  |     v
  |   content.js: MSG.PAGE_INFO_REQUEST = 'getPageInfo'  <-- BUG: COMM-07
  |   (mismatch: ScanService sends 'page:info:request',
  |    content.js listens for 'getPageInfo')
  |
  | connectionService.send({type: 'url_check', url, trackers, iframes})
  |     |
  |     v (WebSocket)
  |   extension_server.py -> _handle_client() -> _on_message_callback()
  |     |
  |     v
  |   extension_handler.py: handle_message() [async]
  |     |
  |     v
  |   _handle_url_check() [sync]  <-- BUG: COMM-06
  |     calls scan_service.check_url() [sync, does ZMQ I/O]
  |     BLOCKS asyncio event loop during ZMQ send/recv
  |     |
  |     v
  |   scan_service.py: check_url() -> _create_result()
  |     returns {type, score, riskType, protectiveAction, cached}
  |     MISSING 'url' field  <-- BUG: COMM-05
  |     |
  |     v (WebSocket response)
  |   ScanService.handleResult(data)
  |     data.url is undefined -> can't cache, can't resolve pending scan
  |
  |  ... Meanwhile, async analysis continues on Backend ...
  |
  |   notification_client.py (ZMQ SUB thread) receives notification
  |     |
  |     v
  |   notification_handler.py: handle() -> _broadcast_to_extension()
  |     broadcast fails? Only logged, not retried  <-- BUG: COMM-08
  |     |
  |     v (WebSocket broadcast)
  |   background.js: handleUrlResult(data)
  |     data.url present (notification_handler includes it)
  |     scanService.handleResult(data) -> resolves pending scan
```

### Fixed Communication Flow (Target)

```
Chrome Extension                    Desktop App (Python)                Backend (.NET)
===============                     ====================                ==============

ScanService.scan(tabId, url)
  |
  | chrome.tabs.sendMessage(tabId, {type: 'page:info:request'})
  |     v
  |   content.js: handles 'page:info:request' correctly  <-- FIXED: COMM-07
  |   Returns {trackers, iframes, url, title, domain}
  |
  | connectionService.send({type: 'url_check', url, trackers, iframes})
  |     v (WebSocket)
  |   extension_handler.py: handle_message() [async]
  |     |
  |     v
  |   _handle_url_check() [async, runs in executor]  <-- FIXED: COMM-06
  |     calls scan_service.check_url() in thread pool
  |     Does NOT block asyncio event loop
  |     |
  |     v
  |   scan_service.py: check_url() returns result WITH url  <-- FIXED: COMM-05
  |     {type, url, score, riskType, protectiveAction, cached}
  |     |
  |     v (WebSocket response)
  |   ScanService.handleResult(data)
  |     data.url present -> caches result, resolves pending scan
  |
  |  ... Async notification arrives ...
  |
  |   notification_handler.py: _broadcast_to_extension()
  |     On failure: retries once, then logs error  <-- FIXED: COMM-08
```

### Anti-Patterns to Avoid
- **Calling sync I/O from async context:** Never call `scan_service.check_url()` (which does synchronous ZMQ REQ/REP) directly from an `async def` handler. Use `loop.run_in_executor()` to offload to a thread pool.
- **Omitting identifying fields from responses:** Every message sent to the Extension MUST include the `url` field so the Extension can match results to pending requests.
- **Silent failure in broadcast:** Never swallow broadcast exceptions with just a log. At minimum retry once before giving up.
- **Dual message type constants:** Never define the same logical message type with two different string values in different files. Use a single source of truth.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Running sync code in async | Custom threading wrapper | `loop.run_in_executor(None, func, *args)` | stdlib, battle-tested, proper thread pool management |
| Retry logic for broadcast | Custom retry loop with sleep | Simple single-retry with try/except | Only need 1 retry, not exponential backoff; broadcast is to local WebSocket clients |
| Message type constants | Duplicate string literals | Import from single `MessageTypes.js` source | Already exists, just needs content.js to align |

**Key insight:** These are all minimal, targeted fixes. The architecture is sound (established in v1.0 phases 1-5). No new patterns or libraries needed.

## Common Pitfalls

### Pitfall 1: run_in_executor Signature Mismatch
**What goes wrong:** `loop.run_in_executor()` takes `(executor, func, *args)` -- not `(executor, func(args))`. Passing `func(args)` calls the function immediately in the current thread, defeating the purpose.
**Why it happens:** Developers write `await loop.run_in_executor(None, check_url(url, trackers, iframes))` instead of the correct form.
**How to avoid:** Use `functools.partial` for keyword args, or lambda: `await loop.run_in_executor(None, functools.partial(self.scan_service.check_url, url=url, trackers=trackers, iframes=iframes))`
**Warning signs:** Event loop still blocks during ZMQ I/O, no improvement in concurrency.

### Pitfall 2: Missing url in ALL Code Paths
**What goes wrong:** Developer adds `url` to `_create_result()` but forgets `_create_error()` or the intermediate `analyzing: True` response in `_process_response()`.
**Why it happens:** Three different code paths return messages to the Extension: success, error, and "analyzing" (async in progress).
**How to avoid:** Add `url` parameter to `_create_result()`, `_create_error()`, and the inline dict in `_process_response()` at line 176.
**Warning signs:** Extension can cache successful results but not match errors or "analyzing" messages.

### Pitfall 3: content.js Backward Compatibility
**What goes wrong:** Changing `MSG.PAGE_INFO_REQUEST` value from `'getPageInfo'` to `'page:info:request'` in content.js could break if older code sends `'getPageInfo'`.
**Why it happens:** content.js already has a dual-case handler (`case MSG.PAGE_INFO_REQUEST: case 'getPageInfo':`) for exactly this reason.
**How to avoid:** Keep the dual-case handler but change the `MSG.PAGE_INFO_REQUEST` constant to `'page:info:request'`. The `case 'getPageInfo':` fallback handles any legacy callers.
**Warning signs:** Page info collection fails silently, trackers and iframes always empty.

### Pitfall 4: Broadcast Retry Creates Race Condition with Cache
**What goes wrong:** If broadcast retry succeeds but the first attempt partially succeeded (e.g., sent to some clients), the Extension receives duplicate results.
**Why it happens:** WebSocket broadcast sends to multiple clients. Some may succeed while others fail.
**How to avoid:** The Extension's `ScanService.handleResult()` is idempotent (same score/url overwrites cache). Duplicate broadcasts are safe. The retry should re-broadcast to ALL clients, not try to track which ones failed.
**Warning signs:** None -- idempotent handling makes this safe.

### Pitfall 5: run_in_executor Changes Return Value Timing
**What goes wrong:** After moving `check_url()` to executor, the response still needs to be `await`ed and returned to the WebSocket client. If the `await` is missing, the handler returns `None`.
**Why it happens:** `run_in_executor` returns a coroutine that must be `await`ed.
**How to avoid:** Always `result = await loop.run_in_executor(...)` and `return result`.
**Warning signs:** Extension receives empty/null responses for all url_check requests.

## Code Examples

Verified patterns from codebase analysis:

### COMM-05: Add url Field to scan_service.py Responses

```python
# File: apps/desktop/win/src/services/scan_service.py
# Source: Direct codebase analysis

# CURRENT (BROKEN) - _create_result at line 222:
def _create_result(self, score, risk_type, protective_action, cached):
    return {
        'type': 'url_result',
        'score': score,
        'riskType': risk_type,
        'protectiveAction': protective_action,
        'cached': cached
        # MISSING: 'url' field
    }

# FIXED - add url parameter:
def _create_result(self, url, score, risk_type, protective_action, cached):
    return {
        'type': 'url_result',
        'url': url,  # Extension needs this for cache key and pending scan resolution
        'score': score,
        'riskType': risk_type,
        'protectiveAction': protective_action,
        'cached': cached
    }

# CURRENT (BROKEN) - _create_error at line 238:
def _create_error(self, message):
    return {
        'type': 'url_result',
        'error': True,
        'message': message
        # MISSING: 'url' field
    }

# FIXED - add url parameter:
def _create_error(self, url, message):
    return {
        'type': 'url_result',
        'url': url,  # Extension needs this even for errors
        'error': True,
        'message': message
    }

# CURRENT (BROKEN) - _process_response inline dict at line 176:
return {
    'type': 'url_result',
    'analyzing': True,
    'message': 'Analysis in progress - waiting for results'
    # MISSING: 'url' field
}

# FIXED:
return {
    'type': 'url_result',
    'url': url,  # url parameter already available in _process_response scope
    'analyzing': True,
    'message': 'Analysis in progress - waiting for results'
}
```

**Call sites that need updating (pass url to _create_result and _create_error):**
- `check_url()` line 108-113: `_create_result(url=url, score=cached.score, ...)`
- `check_url()` line 126: `_create_error(url=url, message="Not authenticated")`
- `_process_response()` line 167: `_create_error(url=url, message="Authentication failed")`
- `_process_response()` line 209-213: `_create_result(url=url, score=score, ...)`
- `_process_response()` line 220: `_create_error(url=url, message=str(error_msg))`

### COMM-06: Fix Async/Sync Mismatch in extension_handler.py

```python
# File: apps/desktop/win/src/handlers/extension_handler.py
# Source: Direct codebase analysis

# CURRENT (BROKEN) - handle_message at line 22:
async def handle_message(self, data):
    # ...
    handler = handlers.get(msg_type)
    if handler:
        return handler(data)  # Calls sync _handle_url_check directly
        # _handle_url_check calls scan_service.check_url()
        # which does synchronous ZMQ send/recv
        # This BLOCKS the asyncio event loop

# FIXED - use run_in_executor for url_check:
import asyncio
import functools

async def handle_message(self, data):
    msg_type = data.get('type', '')
    print(f"\n[EXTENSION] Received: {msg_type}")

    handlers = {
        'url_check': self._handle_url_check,
        'ping': self._handle_ping,
        'user_auth': self._handle_user_auth,
        'get_user': self._handle_get_user,
        'user_signout': self._handle_user_signout,
    }

    handler = handlers.get(msg_type)
    if handler:
        # url_check does synchronous ZMQ I/O -- must not block event loop
        if msg_type == 'url_check':
            loop = asyncio.get_running_loop()
            return await loop.run_in_executor(
                None,
                handler,
                data
            )
        else:
            # Other handlers are fast, sync-safe
            return handler(data)

    logger.warning(f"Unknown message type: {msg_type}")
    return {'type': 'error', 'message': 'Unknown message type'}
```

**Why only url_check needs executor:** The other handlers (`_handle_ping`, `_handle_user_auth`, `_handle_get_user`, `_handle_user_signout`) are pure dict operations with no I/O. Only `_handle_url_check` calls `scan_service.check_url()` which does synchronous ZMQ REQ/REP (blocks for up to 5 seconds on timeout).

### COMM-07: Fix Message Type Constant in content.js

```javascript
// File: apps/extension/chrome/content.js
// Source: Direct codebase analysis

// CURRENT (WORKING BUT MISMATCHED):
const MSG = {
  PAGE_INFO_REQUEST: 'getPageInfo',  // Does not match MessageTypes.js
  // ...
};

// ScanService.js sends: {type: MSG.PAGE_INFO_REQUEST}
// where MSG.PAGE_INFO_REQUEST = 'page:info:request' (from MessageTypes.js)

// content.js currently handles BOTH (dual-case at line 379):
//   case MSG.PAGE_INFO_REQUEST:     // 'getPageInfo'
//   case 'getPageInfo':             // literal fallback

// FIXED - align constant with MessageTypes.js:
const MSG = {
  PAGE_INFO_REQUEST: 'page:info:request',  // Matches MessageTypes.js
  SHOW_WARNING: 'showWarning',
  BLOCK_PAGE: 'blockPage',
  REMOVE_WARNING: 'removeWarning'
};

// The dual-case handler already handles backward compatibility:
// case MSG.PAGE_INFO_REQUEST:  // now 'page:info:request' - matches ScanService
// case 'getPageInfo':           // keeps backward compatibility with any legacy callers
```

**Note:** The content.js already has the correct dual-case pattern at line 379-381:
```javascript
case MSG.PAGE_INFO_REQUEST:
case 'getPageInfo':
  sendResponse(TrackerService.getPageInfo());
  break;
```
The fix is simply changing the constant value. The dual-case ensures backward compatibility.

### COMM-08: Add Retry Logic to notification_handler.py Broadcast

```python
# File: apps/desktop/win/src/handlers/notification_handler.py
# Source: Direct codebase analysis

# CURRENT (BROKEN) - handle() at line 72-80:
if self.extension_server and self._event_loop and cache_data:
    future = asyncio.run_coroutine_threadsafe(
        self._broadcast_to_extension(analysis, cache_data),
        self._event_loop
    )
    try:
        future.result(timeout=5)
    except Exception as e:
        print(f"[NOTIFICATION] ERROR: Broadcast failed: {e}")
        # Silent failure -- no retry

# FIXED - add single retry:
if self.extension_server and self._event_loop and cache_data:
    broadcast_success = False
    for attempt in range(2):  # Try up to 2 times (initial + 1 retry)
        try:
            future = asyncio.run_coroutine_threadsafe(
                self._broadcast_to_extension(analysis, cache_data),
                self._event_loop
            )
            future.result(timeout=5)
            broadcast_success = True
            break
        except Exception as e:
            if attempt == 0:
                print(f"[NOTIFICATION] WARNING: Broadcast attempt {attempt+1} failed: {e}, retrying...")
            else:
                print(f"[NOTIFICATION] ERROR: Broadcast failed after {attempt+1} attempts: {e}")
                logger.error(f"Broadcast failed after retry: {e}")

    if not broadcast_success:
        print("[NOTIFICATION] ERROR: Could not deliver result to extension")

# ALSO FIX _broadcast_to_extension - add error logging:
async def _broadcast_to_extension(self, analysis, cache_data):
    """Broadcast URL result to extension"""
    try:
        result_message = {
            'type': 'url_result',
            'url': analysis['url'],
            'score': cache_data['score'],
            'riskType': cache_data['risk_types'],
            'protectiveAction': cache_data['protective_action'],
            'fromCache': False
        }
        await self.extension_server.broadcast(result_message)
        print(f"[NOTIFICATION] Broadcasted result to extension: score={cache_data['score']}")
    except Exception as e:
        logger.error(f"Error broadcasting to extension: {e}")
        raise  # Re-raise so caller can retry
```

**Key change in _broadcast_to_extension:** The `except` block currently swallows the exception (logs it but does not re-raise). This means the `future.result()` call in `handle()` never sees the error. The fix adds `raise` to propagate the error so the retry loop can detect failure.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `asyncio.run()` in thread | `run_coroutine_threadsafe()` | v1.0 Phase 2 | Fixed notification bridge |
| Hardcoded UUID token | `request_token()` from backend | v1.0 Phase 3 | Real authentication |
| Single `recv()` calls | `recv_multipart()` | v1.0 Phase 2 | Atomic message receive |
| No ZMQ retry | Lazy Pirate pattern | v1.0 Phase 5 | REQ socket recovery |

**Already fixed (do NOT re-fix):**
- `asyncio.run()` -> `run_coroutine_threadsafe()` in notification_handler (Phase 2) -- DONE, current code confirms this
- Hardcoded token UUID (Phase 3) -- DONE
- CurveMQ encryption (Phase 4) -- DONE
- ZMQ REQ Lazy Pirate retry (Phase 5) -- DONE

**Still broken (Phase 7 scope):**
- Missing `url` field in scan_service.py responses (COMM-05)
- Sync handlers blocking asyncio in extension_handler.py (COMM-06)
- Message type mismatch in content.js (COMM-07)
- Silent broadcast failure in notification_handler.py (COMM-08)

## Detailed Bug Analysis

### Bug 1 (COMM-05): Missing `url` Field in scan_service.py

**Confidence:** HIGH -- verified by reading actual code

**Evidence:**
- `scan_service.py` `_create_result()` (line 222-236): Returns dict with keys `type`, `score`, `riskType`, `protectiveAction`, `cached` -- NO `url` key
- `scan_service.py` `_create_error()` (line 238-244): Returns dict with keys `type`, `error`, `message` -- NO `url` key
- `scan_service.py` `_process_response()` (line 176-180): Returns inline dict with keys `type`, `analyzing`, `message` -- NO `url` key
- Extension `ScanService.js` `handleResult()` (line 177): `if (!fromCache && data.url)` -- needs `data.url` to cache
- Extension `ScanService.js` `handleResult()` (line 182): `const domain = this.extractDomain(data.url)` -- needs `data.url` to resolve pending scan

**Impact:** Extension receives results but cannot match them to URLs, cannot cache them, and cannot resolve pending scan promises. This makes the scan appear to timeout even when results arrive.

**Fix complexity:** Low -- add `url` parameter to `_create_result()`, `_create_error()`, and the inline dict. Update all call sites (5 locations in `check_url()` and `_process_response()`).

### Bug 2 (COMM-06): Sync Handlers Blocking Asyncio Event Loop

**Confidence:** HIGH -- verified by reading actual code

**Evidence:**
- `extension_handler.py` `handle_message()` (line 22): `async def handle_message(self, data):`
- `extension_handler.py` (line 40): `return handler(data)` -- calls sync handler directly
- `extension_handler.py` `_handle_url_check()` (line 45): `def _handle_url_check(self, data):` -- regular sync def
- `_handle_url_check()` calls `self.scan_service.check_url()` (line 51)
- `scan_service.py` `check_url()` calls `self.zmq_client.send_url_alert()` (line 137)
- `zmq_client.py` `send_url_alert()` does synchronous `socket.send()` + `socket.recv()` with 5 second timeout

**Impact:** The asyncio event loop is blocked for up to 5 seconds (ZMQ timeout) during every URL check. During this time, no WebSocket messages can be processed (pings, keepalives, other extension messages).

**Fix complexity:** Low-Medium -- wrap the `url_check` handler call with `loop.run_in_executor()`. Other handlers are pure dict operations and don't need executor.

### Bug 3 (COMM-07): Message Type Mismatch in content.js

**Confidence:** HIGH -- verified by reading actual code

**Evidence:**
- `content.js` (line 23): `PAGE_INFO_REQUEST: 'getPageInfo'`
- `MessageTypes.js` (line 25): `PAGE_INFO_REQUEST: 'page:info:request'`
- `ScanService.js` (line 89): `chrome.tabs.sendMessage(tabId, { type: MSG.PAGE_INFO_REQUEST })` -- sends `'page:info:request'`
- `content.js` (line 379-381): Handles both `MSG.PAGE_INFO_REQUEST` and `'getPageInfo'` in dual-case

**Impact:** Currently **partially working** because content.js already has the dual-case handler. However, the mismatch means the constant `MSG.PAGE_INFO_REQUEST` in content.js is misleading (it says `'getPageInfo'` when the actual message being sent is `'page:info:request'`). The dual-case handler at line 379-381 catches this, but it is fragile and confusing.

**Fix complexity:** Very Low -- change one constant value in content.js. The dual-case handler already provides backward compatibility.

**Important nuance:** Upon code inspection, this bug may NOT be blocking in practice because of the existing dual-case handler. However, it IS a bug in the constants that should be fixed for correctness and maintainability. The switch statement `case MSG.PAGE_INFO_REQUEST: case 'getPageInfo':` currently evaluates to `case 'getPageInfo': case 'getPageInfo':` (duplicate). After the fix it will correctly be `case 'page:info:request': case 'getPageInfo':`.

### Bug 4 (COMM-08): Silent Broadcast Failure in notification_handler.py

**Confidence:** HIGH -- verified by reading actual code

**Evidence:**
- `notification_handler.py` (line 72-80): Try/except wraps `future.result(timeout=5)`, catches exception, prints error message, but does NOT retry
- `notification_handler.py` `_broadcast_to_extension()` (line 91-105): Try/except catches all exceptions, logs with `logger.error()`, but does NOT re-raise
- Because `_broadcast_to_extension` swallows exceptions, the `future.result()` in `handle()` may never see errors at all

**Impact:** If the WebSocket broadcast fails (e.g., due to a temporarily disconnected client, network hiccup), the result is lost. The Extension never receives it and the scan times out. No retry is attempted.

**Fix complexity:** Low -- add retry loop (max 2 attempts) around the broadcast call. Add `raise` to `_broadcast_to_extension` so errors propagate. Note: `extension_server.py` `broadcast()` (line 144-163) already handles disconnected clients gracefully by removing them from the client set, so the main failure mode is more about event loop timing or temporary issues.

## Open Questions

1. **Does COMM-07 actually block page info collection in practice?**
   - What we know: content.js has a dual-case handler that catches both `'getPageInfo'` and `'page:info:request'`. ScanService.js sends `'page:info:request'`.
   - What's unclear: Whether the dual-case evaluates correctly when `MSG.PAGE_INFO_REQUEST` equals `'getPageInfo'` -- it should, because `case 'page:info:request':` is NOT matched by `case MSG.PAGE_INFO_REQUEST:`, but `case 'getPageInfo':` IS the literal fallback. Wait -- this means the FIRST case (`case MSG.PAGE_INFO_REQUEST:` which is `case 'getPageInfo':`) does NOT match when ScanService sends `'page:info:request'`. The SECOND case is literally `case 'getPageInfo':` which also does NOT match. So neither case matches `'page:info:request'`!
   - **Resolution: This IS a blocking bug.** The dual-case is `case 'getPageInfo': case 'getPageInfo':` (both are the same string). Neither matches the incoming `'page:info:request'` from ScanService.js. Page info collection FAILS silently, meaning trackers and iframes are always null.
   - Recommendation: This is confirmed as a real, blocking bug. The fix (changing the constant) is essential.

2. **Could COMM-06 cause WebSocket disconnection under load?**
   - What we know: 5-second ZMQ timeout blocks the event loop. During this time, pings/keepalives cannot be processed.
   - What's unclear: Whether the Extension's heartbeat (10s interval, 3 missed = dead) would falsely trigger during a blocked loop.
   - Recommendation: Unlikely to cause disconnection in normal operation (heartbeat allows 30s total), but blocking the event loop for 5s is still bad practice and can cause observable lag. Fix is warranted.

3. **Is the broadcast failure in COMM-08 happening in practice?**
   - What we know: `_broadcast_to_extension` catches and swallows all exceptions. The Extension is connected via WebSocket to the same machine (localhost).
   - What's unclear: How often localhost WebSocket broadcasts actually fail.
   - Recommendation: Even if rare, the fix is low-effort and makes the system more robust. The `raise` fix in `_broadcast_to_extension` is critical regardless -- currently errors are invisible.

## Sources

### Primary (HIGH confidence)
- Direct codebase analysis of all files involved:
  - `apps/desktop/win/src/services/scan_service.py` -- _create_result, _create_error, _process_response
  - `apps/desktop/win/src/handlers/extension_handler.py` -- handle_message, _handle_url_check
  - `apps/desktop/win/src/handlers/notification_handler.py` -- handle, _broadcast_to_extension
  - `apps/extension/chrome/content.js` -- MSG constants, MessageHandler
  - `apps/extension/chrome/services/ScanService.js` -- scan, getPageInfo, handleResult
  - `apps/extension/chrome/messaging/MessageTypes.js` -- MSG.PAGE_INFO_REQUEST
  - `apps/desktop/win/src/extension_server.py` -- broadcast method
  - `apps/desktop/win/src/main.py` -- startup flow, threading model
  - `apps/desktop/win/src/notification_client.py` -- ZMQ SUB thread
  - `apps/desktop/win/src/zmq_client.py` -- synchronous ZMQ I/O

### Secondary (HIGH confidence)
- v1.0 Phase 2 Research (`02-RESEARCH.md`) -- async/sync bridge patterns already established
- v1.0 Bug Report (`BUG-REPORT.md`) -- context on prior fixes to avoid regression
- Python asyncio docs -- `run_in_executor()` semantics (verified against training data)

## Metadata

**Confidence breakdown:**
- Bug identification: HIGH -- all 4 bugs verified by reading actual source code
- Fix patterns: HIGH -- using stdlib patterns (`run_in_executor`, retry loops) already proven in codebase
- Impact assessment: HIGH -- code paths traced end-to-end through both Extension and Desktop App
- COMM-07 severity: HIGH -- re-analyzed during research, confirmed as blocking (not just cosmetic)

**Research date:** 2026-02-13
**Valid until:** Until Phase 7 is executed (fixes are specific to current code state)
