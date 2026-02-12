---
phase: 05-harden-reliability-and-document
plan: 01
subsystem: communication
tags: [zmq, websocket, lazy-pirate, retry, pending-results, reliability]

# Dependency graph
requires:
  - phase: 04-restore-curvemq-security
    provides: CurveMQ encryption on ZMQ REQ and SUB sockets
  - phase: 02-fix-async-notification-bridge
    provides: Thread-safe asyncio bridge for broadcasting results to Extension
provides:
  - Lazy Pirate retry pattern on ZMQ REQ socket (timeout recovery without corrupting FSM)
  - PendingResults store in Desktop App WebSocket server (no results lost during Extension disconnection)
affects:
  - 05-02 (Chrome Extension resilience -- Extension side of reconnection)
  - 05-03 (Bug report documentation -- REL-01 and REL-02 now fixed)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Lazy Pirate: poll() before recv() on REQ socket, _reset_socket() on timeout, retry loop"
    - "PendingResults: time-bounded store with flush-on-connect for WebSocket reconnection"

key-files:
  created: []
  modified:
    - apps/desktop/win/src/zmq_client.py
    - apps/desktop/win/src/extension_server.py

key-decisions:
  - "Use socket.poll() instead of recv+RCVTIMEO to detect timeout without corrupting REQ FSM"
  - "PendingResults: 5-minute TTL, 50-item max (matches Extension MessageQueueService parameters)"
  - "notification_handler.py unchanged -- broadcast() in extension_server.py handles the no-clients case"

patterns-established:
  - "Lazy Pirate recovery: _reset_socket() destroys and recreates socket on same context"
  - "PendingResults store-and-flush: store on broadcast with no clients, flush on client connect"

# Metrics
duration: 3min
completed: 2026-02-12
---

# Phase 5 Plan 1: ZMQ Lazy Pirate Recovery and WebSocket Pending Results Summary

**Lazy Pirate retry with poll()/reset_socket() on ZMQ REQ, PendingResults store with flush-on-connect in WebSocket server**

## Performance

- **Duration:** ~3 min
- **Started:** 2026-02-12T21:58:24Z
- **Completed:** 2026-02-12T22:01:08Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- ZMQ REQ socket recovers from timeout via Lazy Pirate: poll() detects timeout, _reset_socket() destroys and recreates socket, retry loop attempts up to 3 times (REL-01)
- Analysis results stored in PendingResults when no Extension client connected (5min TTL, 50 max), flushed immediately on reconnect (REL-02)
- Socket state machine never corrupted -- poll() does not change FSM state, so timeout is safe to handle
- close() now sets LINGER=0 to prevent blocking during shutdown

## Task Commits

Each task was committed atomically:

1. **Task 1: Add Lazy Pirate retry pattern to ZMQ REQ socket** - `6b7a012` (feat)
2. **Task 2: Add PendingResults store and flush on WebSocket reconnect** - `dd64a3d` (feat)

## Files Created/Modified
- `apps/desktop/win/src/zmq_client.py` - Added _reset_socket() method and refactored send_alert() to use poll+retry (Lazy Pirate pattern), close() sets LINGER=0
- `apps/desktop/win/src/extension_server.py` - Added PendingResults class, integrated into ExtensionServer.__init__, flush on client connect, store on broadcast with no clients

## Decisions Made
- **poll() over recv+RCVTIMEO:** poll() does not change the REQ socket state machine. If poll times out, we know no message arrived and can safely destroy the socket. With recv+RCVTIMEO, the socket is left in a corrupted "waiting for reply" state.
- **PendingResults parameters:** 5-minute TTL and 50-item max, matching the Extension's MessageQueueService parameters. Results older than 5 minutes are likely stale (different browsing context).
- **notification_handler.py unchanged:** The broadcast() method in extension_server.py now handles the "no clients" case by storing in pending_results. No changes needed in the notification handler.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- REL-01 (ZMQ REQ recovery) and REL-02 (WebSocket reconnection resilience) satisfied
- Ready for Plan 02: Chrome Extension service worker resilience (REL-03)
- Ready for Plan 03: Bug report documentation (DOC-01)

---
*Phase: 05-harden-reliability-and-document*
*Completed: 2026-02-12*
