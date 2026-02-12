# Phase 5: Harden Reliability and Document - Research

**Researched:** 2026-02-12
**Domain:** ZMQ reliability, WebSocket reconnection, Chrome MV3 service worker lifecycle, bug report documentation
**Confidence:** HIGH (code analysis) / MEDIUM (ZMQ patterns from official guide)

## Summary

Phase 5 addresses four reliability gaps and one documentation deliverable across the three-tier architecture (Chrome Extension <-> Desktop App <-> Backend). Research focused on analyzing the existing codebase to identify specific gaps, then verifying standard patterns for each requirement.

**Key finding:** The codebase already has substantial reliability infrastructure in place (heartbeat, keepalive, reconnect scheduling, message queue). The gaps are specific and targeted: (1) ZMQ REQ socket has no recovery after timeout -- it just returns None, (2) WebSocket server in Desktop App has no awareness of pending results that should be delivered after reconnection, (3) Chrome service worker keepalive depends on `setInterval` which does not survive termination. Each gap has a well-established solution pattern.

**Primary recommendation:** Apply the Lazy Pirate pattern (socket close/reopen) to ZMQ REQ, add pending-result storage to the Desktop App WebSocket server, and ensure Chrome alarms (not just setInterval) drive all periodic tasks including keepalive.

## Standard Stack

### Core

No new libraries needed. All work uses existing dependencies:

| Library | Version | Purpose | Already In Use |
|---------|---------|---------|---------------|
| pyzmq | (current) | ZMQ REQ/REP with CURVE | Yes - zmq_client.py |
| websockets | (current) | WebSocket server | Yes - extension_server.py |
| Chrome Extensions API | MV3 | Alarms, storage, service worker | Yes - background.js |

### Supporting

| Library | Purpose | When to Use |
|---------|---------|-------------|
| `zmq.Poller` | Non-blocking poll before recv on REQ socket | REL-01: detect timeout without corrupting state |
| `chrome.alarms` | Survive service worker termination | REL-03: already used for reconnect, extend to keepalive |
| `chrome.storage.local` | Persist state across SW restarts | REL-03: already used, ensure all critical state persisted |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Lazy Pirate (close/reopen REQ) | ZMQ_REQ_RELAXED + ZMQ_REQ_CORRELATE | RELAXED has known bugs with reply matching (jeromq#442). Close/reopen is simpler and battle-tested. |
| DEALER socket | Replace REQ entirely | Over-engineering. REQ with Lazy Pirate is sufficient for our single-server case. |

## Architecture Patterns

### Pattern 1: Lazy Pirate (ZMQ REQ Recovery) -- REL-01

**What:** Close and reopen the REQ socket after a timeout, then retry the request. This is the canonical ZMQ pattern for client-side reliability with REQ/REP.

**When to use:** Every time a `socket.recv()` times out (zmq.Again exception) on a REQ socket.

**Why it works:** The REQ socket has a strict finite state machine: send -> recv -> send -> recv. If recv times out, the socket is stuck in "waiting for reply" state. No API can reset this state. The only recovery is to destroy and recreate the socket.

**Current gap in code:** `zmq_client.py` `send_alert()` catches `zmq.Again` on timeout but just returns `None`. The socket object is left in a corrupted state. Similarly, `send_url_alert()` and `request_token()` each call `self.close()` in `finally:` which destroys the socket, but they also call `self.connect()` at the start of each call which creates a fresh socket. This means the higher-level methods (`send_url_alert`, `request_token`) already have implicit recovery because they connect/close per call. But `send_alert()` itself, if used standalone or if the caller reuses the socket, would leave corrupted state.

**Implementation approach:**

```python
# Lazy Pirate pattern for ZMQ REQ
import zmq

REQUEST_TIMEOUT = 5000  # ms
REQUEST_RETRIES = 3

def send_with_retry(self, message_bytes, retries=REQUEST_RETRIES):
    """Send message with Lazy Pirate retry pattern"""
    for attempt in range(retries):
        # Ensure fresh socket
        if not self.socket:
            if not self.connect():
                continue

        try:
            self.socket.send(message_bytes)

            # Poll before recv to avoid corrupting state machine
            if self.socket.poll(REQUEST_TIMEOUT, zmq.POLLIN):
                return self.socket.recv()
            else:
                # Timeout -- socket state is now corrupted
                # Close and reopen (Lazy Pirate recovery)
                logger.warning(f"ZMQ timeout (attempt {attempt+1}/{retries})")
                self._reset_socket()

        except zmq.ZMQError as e:
            logger.error(f"ZMQ error: {e}")
            self._reset_socket()

    return None  # All retries exhausted

def _reset_socket(self):
    """Destroy and recreate socket (Lazy Pirate recovery)"""
    if self.socket:
        self.socket.setsockopt(zmq.LINGER, 0)
        self.socket.close()
        self.socket = None
    # Do NOT term context -- reuse it
```

**Source:** [ZeroMQ Guide Chapter 4 - Reliable Request-Reply](https://zguide.zeromq.org/docs/chapter4/)

### Pattern 2: Pending Result Store (WebSocket Reconnection) -- REL-02

**What:** Desktop App stores pending analysis results so they can be delivered when the Extension reconnects.

**When to use:** When a notification arrives from Backend (via ZMQ SUB) but no Extension client is connected to receive it.

**Current gap in code:** `notification_handler.py` `_broadcast_to_extension()` calls `extension_server.broadcast()` which silently drops the message if `self.clients` is empty (line 161: `if not self.clients: return`). If the Extension was disconnected when the Backend sent the result, the result is permanently lost.

**The Extension side is already prepared:** `ConnectionService.js` has `MessageQueueService` that queues outgoing messages during disconnection, and `flushQueue()` that sends them on reconnect. The gap is on the Desktop App side -- it needs to store results and deliver them when the Extension reconnects.

**Implementation approach:**

```python
# In extension_server.py or a new pending_results store
class PendingResults:
    """Store results for delivery after reconnection"""
    def __init__(self, max_age_seconds=300, max_items=50):
        self._results = []  # list of (timestamp, message)
        self.max_age = max_age_seconds
        self.max_items = max_items

    def store(self, message: dict):
        """Store a result for later delivery"""
        self._cleanup()
        self._results.append((time.time(), message))
        if len(self._results) > self.max_items:
            self._results.pop(0)

    def flush(self) -> list:
        """Get and clear all pending results"""
        self._cleanup()
        results = [msg for _, msg in self._results]
        self._results.clear()
        return results

    def _cleanup(self):
        """Remove expired results"""
        cutoff = time.time() - self.max_age
        self._results = [(t, m) for t, m in self._results if t > cutoff]
```

Then in `_handle_client` (extension_server.py), flush pending results when a new client connects.

### Pattern 3: Alarm-Driven Keepalive (Chrome Service Worker) -- REL-03

**What:** Use `chrome.alarms` instead of `setInterval` for periodic tasks that must survive service worker termination.

**When to use:** For any recurring task in MV3 service worker that must continue even if the worker is terminated and restarted.

**Current state analysis:**

The extension already has:
- `chrome.alarms` for reconnect scheduling (good -- survives termination)
- `setInterval` for keepalive (20s) -- **will NOT survive termination**
- `setInterval` for heartbeat (10s) -- **will NOT survive termination**
- `setInterval` for ping (30s) -- **will NOT survive termination**

The problem: When Chrome terminates the service worker after 30s of inactivity, all `setInterval` timers are lost. On restart, `init()` is called which calls `connectionService.connect()`, but the keepalive/heartbeat/ping timers are only set up in `setupConnection()` which is called after a successful connect. If the connect fails (Desktop App not running), the worker has no active timers and may terminate again.

**Chrome 116+ behavior:** WebSocket messages (send or receive) now reset the service worker idle timer. So the 20s keepalive interval is correctly timed (within the 30s window). BUT if the WebSocket is closed, the `setInterval` keepalive stops, and there's nothing keeping the worker alive to attempt reconnection except the `chrome.alarms` reconnect alarm.

**What actually needs fixing:** The keepalive via `setInterval` works while the WebSocket is open (because sending a message resets the idle timer). The risk is: if the WebSocket closes AND the service worker terminates BEFORE the reconnect alarm fires, the `setInterval`-based keepalive is lost. However, on worker restart, `init()` runs which triggers `connect()`. So the real question is: does `init()` reliably run on service worker restart?

**Answer:** Yes. Chrome re-runs the service worker script from scratch on restart, which will execute the top-level `init()` call. The concern is about the gap between termination and alarm-triggered restart. With `chrome.alarms`, the minimum period is 30 seconds in production, so there could be a 30-60s gap.

**Recommendation:** Convert heartbeat to use `chrome.alarms` as a backup (in addition to setInterval when connected). This ensures the heartbeat alarm fires even if the worker was restarted. The `setInterval` can remain for when the worker is active (faster cadence), with `chrome.alarms` as a safety net.

**Source:** [Chrome Extension Service Worker Lifecycle](https://developer.chrome.com/docs/extensions/develop/concepts/service-workers/lifecycle), [WebSockets in MV3 Service Workers](https://developer.chrome.com/docs/extensions/mv3/tut_websockets/)

### Anti-Patterns to Avoid

- **Anti-pattern: ZMQ_REQ_RELAXED for single-server:** REQ_RELAXED is designed for multi-server failover. For single-server (our case), it adds complexity and has known reply-matching bugs. Use Lazy Pirate (close/reopen) instead.
- **Anti-pattern: Keeping global state in service worker memory:** All critical state must be in `chrome.storage.local` or `chrome.storage.session`. The `stateManager` already does this. But `pendingScans` Map in `ScanService.js` (line 13) is in-memory only and will be lost on termination.
- **Anti-pattern: Relying solely on setInterval in MV3:** Any recurring task that must survive service worker termination needs `chrome.alarms` as backup. `setInterval` is fine for "while alive" optimization but cannot be the only mechanism.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| ZMQ REQ state recovery | Custom state machine reset | Lazy Pirate pattern (close/reopen socket) | ZMQ internal FSM has no public reset API. Close is the only reliable way. |
| WebSocket reconnect (Extension side) | Custom reconnection logic | Existing ConnectionService.js | Already built with exponential backoff, alarm-based scheduling, heartbeat detection. |
| Message persistence during disconnect | Custom file-based queue | chrome.storage.local + MessageQueueService | Already built. Just ensure it persists across SW termination. |
| Service worker keepalive | Custom background page workaround | chrome.alarms + WebSocket messages | Chrome's official recommended approach for MV3. |

**Key insight:** Most reliability infrastructure already exists in the codebase. The work is about closing specific gaps in existing code, not building new systems.

## Common Pitfalls

### Pitfall 1: ZMQ Context Termination During Recovery

**What goes wrong:** Calling `context.term()` while sockets are still open blocks indefinitely (or until LINGER expires).
**Why it happens:** When doing Lazy Pirate recovery, developers sometimes terminate the context along with the socket.
**How to avoid:** Only close the socket, reuse the context. Set `ZMQ_LINGER=0` on the socket before closing to prevent blocking.
**Warning signs:** Application hangs during shutdown or during ZMQ recovery.

### Pitfall 2: WebSocket Pending Results Race Condition

**What goes wrong:** Result arrives during the brief window when the Extension is reconnecting (WebSocket handshake in progress).
**Why it happens:** The Extension drops the old connection, tries ports sequentially, and there's a gap before `setupConnection` is called.
**How to avoid:** Store pending results on the Desktop App side (not just queue on Extension side). Flush stored results when a new client connects.
**Warning signs:** Intermittent missing results after reconnection.

### Pitfall 3: chrome.alarms Minimum Period

**What goes wrong:** `chrome.alarms.create` with `delayInMinutes` less than 0.5 (30 seconds) is silently clamped to 30 seconds in production.
**Why it happens:** Chrome enforces a minimum alarm period of 30 seconds for performance reasons.
**How to avoid:** Don't rely on alarms for sub-30-second intervals. Use `setInterval` for fast cadence while alive, alarms as recovery backup.
**Warning signs:** Alarms firing less frequently than expected in production (works in dev with `--disable-throttling`).

### Pitfall 4: Service Worker Restart Losing In-Memory State

**What goes wrong:** `pendingScans` Map in `ScanService.js` is lost when service worker terminates. Scans that were in-flight get no resolution callback, and the Promise never resolves.
**Why it happens:** Service worker restarts clear all JavaScript memory.
**How to avoid:** Either accept that in-flight scans are lost (user triggers new scan on the new page), or persist pending scan state to `chrome.storage.session`.
**Warning signs:** Scan results arriving after service worker restart are not reflected in the popup.

### Pitfall 5: ZMQ REQ Send After Timeout Without Socket Reset

**What goes wrong:** After `zmq.Again` timeout on recv, the next `send()` call throws `zmq.error.ZMQError: Operation cannot be accomplished in current state`.
**Why it happens:** REQ FSM is in "expecting recv" state. Calling send() violates the state machine.
**How to avoid:** Always close and reopen the socket after a timeout. Never try to send on a timed-out REQ socket.
**Warning signs:** `EFSM` errors in logs after a timeout.

## Code Examples

### Example 1: Lazy Pirate ZMQ REQ Recovery (Python)

```python
# Source: https://zguide.zeromq.org/docs/chapter4/
# Adapted for this project's zmq_client.py

def _reset_socket(self):
    """Close and recreate REQ socket (Lazy Pirate recovery)"""
    if self.socket:
        self.socket.setsockopt(zmq.LINGER, 0)
        self.socket.close()
        self.socket = None

    # Recreate on same context
    self.socket = self.context.socket(zmq.REQ)
    self.socket.setsockopt(zmq.RCVTIMEO, self.timeout)

    if self.curve_enabled and self.server_public_key:
        apply_curve_client(self.socket, self.server_public_key)

    self.socket.connect(f"tcp://{self.host}:{self.port}")
    logger.info("ZMQ REQ socket reset (Lazy Pirate recovery)")
```

### Example 2: Pending Results Delivery on WebSocket Reconnect (Python)

```python
# In extension_server.py _handle_client method
async def _handle_client(self, websocket):
    self.clients.add(websocket)

    # Deliver any pending results from while client was disconnected
    pending = self._pending_results.flush()
    for result in pending:
        try:
            await websocket.send(json.dumps(result))
            logger.info(f"Delivered pending result: {result.get('url', 'N/A')}")
        except Exception as e:
            logger.error(f"Error delivering pending result: {e}")

    # ... rest of handler
```

### Example 3: Alarm-Based Heartbeat Backup (JavaScript)

```javascript
// Source: https://developer.chrome.com/docs/extensions/develop/concepts/service-workers/lifecycle

// In background.js alarm listener (already at top level)
chrome.alarms.onAlarm.addListener((alarm) => {
  switch (alarm.name) {
    case 'reconnect':
      connectionService.attemptReconnect();
      break;
    case 'heartbeat':
      connectionService.sendHeartbeat();
      break;
    case 'keepalive':
      // Backup keepalive via alarm -- fires if setInterval was lost
      if (connectionService.isConnected()) {
        connectionService.send({ type: 'keepalive' });
      }
      break;
  }
});

// Create recurring keepalive alarm as backup
chrome.alarms.create('keepalive', { periodInMinutes: 0.5 }); // 30s minimum
```

### Example 4: Persist MessageQueue Across Service Worker Restarts

```javascript
// In MessageQueueService.js -- persist to chrome.storage.session
async persist() {
  await chrome.storage.session.set({
    messageQueue: this.queue.map(item => ({
      message: item.message,
      timestamp: item.timestamp,
      priority: item.priority
    }))
  });
}

async restore() {
  const data = await chrome.storage.session.get('messageQueue');
  if (data.messageQueue) {
    this.queue = data.messageQueue;
    console.log(`[MessageQueue] Restored ${this.queue.length} messages from storage`);
  }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| MV2 background page (persistent) | MV3 service worker (terminates) | Chrome 88+ | Must handle state loss, use alarms, persist state |
| WebSocket doesn't keep SW alive | WebSocket messages reset idle timer | Chrome 116 | Keepalive messages within 30s window work correctly |
| ZMQ REQ has no timeout recovery | Lazy Pirate (close/reopen) | Always | Standard ZMQ pattern since ZMQ Guide v1 |
| chrome.alarms min 1 minute | chrome.alarms min 30 seconds | Chrome 120 | Better alignment with service worker 30s lifecycle |

## Gap Analysis: Current Code vs Requirements

### REL-01: ZMQ REQ Recovery

| Aspect | Current State | Gap | Fix |
|--------|--------------|-----|-----|
| Timeout detection | RCVTIMEO=5000ms set | None | Already works |
| State recovery after timeout | `send_alert()` returns None, socket stays corrupted | **GAP** | Add `_reset_socket()` after zmq.Again |
| Retry logic | None in `send_alert()` | **GAP** | Add retry loop (Lazy Pirate) |
| Per-call connect/close | `send_url_alert()` and `request_token()` do this | Partial | Already recovers implicitly for these methods |
| LINGER on close | Not set (default -1 = infinite) | **GAP** | Set LINGER=0 before close in `_reset_socket()` |

### REL-02: WebSocket Reconnection

| Aspect | Current State | Gap | Fix |
|--------|--------------|-----|-----|
| Extension reconnect logic | Full: exponential backoff, alarm-based, heartbeat | None | Already built |
| Message queue during disconnect | Built in MessageQueueService.js | None | Already built |
| Queue flush on reconnect | Built in ConnectionService.flushQueue() | None | Already built |
| Desktop stores pending results | **Missing** - broadcast drops silently if no clients | **GAP** | Add PendingResults store, flush on client connect |
| Extension re-sends email on reconnect | Built in sendStoredEmail() | None | Already built |

### REL-03: Chrome Service Worker Survival

| Aspect | Current State | Gap | Fix |
|--------|--------------|-----|-----|
| init() runs on SW restart | Yes - top-level call | None | Already works |
| Reconnect survives termination | Yes - uses chrome.alarms | None | Already works |
| Keepalive survives termination | **No** - uses setInterval only | **GAP** | Add alarm-based backup |
| Heartbeat survives termination | **No** - uses setInterval only | **GAP** | Add alarm-based backup |
| In-memory state persisted | Partial - StateManager uses storage | **Minor gap** | Ensure pendingScans in ScanService is acceptable to lose |
| MessageQueue persisted | **No** - in-memory only | **GAP** | Add persist/restore via chrome.storage.session |

### DOC-01: Bug Report

| Aspect | Current State | Gap | Fix |
|--------|--------------|-----|-----|
| Bug report document | Does not exist | **GAP** | Write document covering all bugs found in Phases 1-4 |

## Open Questions

1. **MessageQueue persistence priority**
   - What we know: MessageQueueService.js stores messages in memory only. Service worker termination loses them.
   - What's unclear: How often does the service worker actually terminate while messages are queued? (Only when WebSocket is down AND no other events fire for 30s)
   - Recommendation: Add `chrome.storage.session` persistence as defensive measure. Low effort, prevents edge-case data loss.

2. **ZMQ retry count for send_alert vs higher-level methods**
   - What we know: `send_url_alert()` already does connect/close per call, providing implicit recovery. `send_alert()` has no retry.
   - What's unclear: Is there ever a code path that calls `send_alert()` directly without the connect/close wrapper?
   - Recommendation: Add Lazy Pirate retry to `send_alert()` itself for defense-in-depth. The higher-level methods benefit automatically.

3. **Pending result TTL and max size**
   - What we know: Results should be delivered within seconds normally. If Extension is disconnected for minutes, results become stale.
   - What's unclear: What's the maximum acceptable delay for result delivery after reconnection?
   - Recommendation: 5-minute TTL, 50-result max (same parameters as Extension's MessageQueueService). Results older than 5 minutes are likely from a different browsing context.

## Sources

### Primary (HIGH confidence)

- [ZeroMQ Guide Chapter 4 - Reliable Request-Reply Patterns](https://zguide.zeromq.org/docs/chapter4/) - Lazy Pirate pattern, REQ socket recovery
- [Chrome Extension Service Worker Lifecycle](https://developer.chrome.com/docs/extensions/develop/concepts/service-workers/lifecycle) - Termination rules, idle timer reset, state persistence
- [Chrome WebSockets in MV3 Service Workers](https://developer.chrome.com/docs/extensions/mv3/tut_websockets/) - WebSocket keepalive patterns, Chrome 116+ behavior
- [libzmq zmq_setsockopt documentation](https://libzmq.readthedocs.io/en/zeromq4-x/zmq_setsockopt.html) - ZMQ_REQ_RELAXED, ZMQ_LINGER, ZMQ_RCVTIMEO

### Secondary (MEDIUM confidence)

- [pyzmq Issue #132 - Can't timeout a REQ/REP connection](https://github.com/zeromq/pyzmq/issues/132) - Confirms close/reopen is the standard recovery
- [libzmq Issue #1227 - zmq_recv on REQ hangs when REP dies](https://github.com/zeromq/libzmq/issues/1227) - Confirms the stuck state problem
- [jeromq Issue #442 - ZMQ_REQ_RELAXED + ZMQ_REQ_CORRELATE = no reply](https://github.com/zeromq/jeromq/issues/442) - Known bugs with RELAXED approach

### Tertiary (LOW confidence)

- Web search results on Chrome MV3 keepalive patterns - Multiple community sources agree on alarm-based approach
- [MV3 Service Worker Keepalive Medium article](https://medium.com/@dzianisv/vibe-engineering-mv3-service-worker-keepalive-how-chrome-keeps-killing-our-ai-agent-9fba3bebdc5b) - Community experience with SW termination

## Codebase-Specific Findings

### Files That Need Changes

| File | Requirement | Change Type | Complexity |
|------|-------------|-------------|------------|
| `apps/desktop/win/src/zmq_client.py` | REL-01 | Add Lazy Pirate retry + socket reset | Medium |
| `apps/desktop/win/src/extension_server.py` | REL-02 | Add PendingResults store, flush on connect | Medium |
| `apps/desktop/win/src/handlers/notification_handler.py` | REL-02 | Store result in PendingResults when no clients | Small |
| `apps/extension/chrome/services/ConnectionService.js` | REL-03 | Add alarm-based keepalive backup | Small |
| `apps/extension/chrome/background.js` | REL-03 | Add keepalive alarm handler | Small |
| `apps/extension/chrome/services/MessageQueueService.js` | REL-03 | Add persist/restore via chrome.storage.session | Small |
| (new file) Bug report document | DOC-01 | Write comprehensive bug report | Medium |

### Files That Are Already Good

| File | Why |
|------|-----|
| `ConnectionService.js` reconnect logic | Exponential backoff, alarm-based scheduling, heartbeat detection all present |
| `MessageQueueService.js` queue logic | Priority queue, TTL, size limits all present |
| `notification_client.py` SUB socket | SUB sockets don't have the REQ state machine problem; reconnect is automatic |
| `background.js` alarm listener | Top-level registration, handles reconnect and heartbeat alarms |

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - No new libraries needed, all patterns well-documented
- Architecture: HIGH - Direct code analysis of existing gaps with verified fix patterns
- Pitfalls: HIGH - Based on ZMQ official guide and Chrome official documentation
- Code examples: MEDIUM - Adapted from official sources to this project's patterns

**Research date:** 2026-02-12
**Valid until:** 2026-03-12 (stable domain, patterns unlikely to change)
