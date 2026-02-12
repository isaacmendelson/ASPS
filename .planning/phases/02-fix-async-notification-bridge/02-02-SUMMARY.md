---
phase: 02-fix-async-notification-bridge
plan: 02
subsystem: notification
tags: [asyncio, threading, run_coroutine_threadsafe, event-loop-bridge, websocket, python]

# Dependency graph
requires:
  - phase: 01-diagnose-baseline
    provides: Diagnostic logging pattern (_diag_log) established
  - phase: 02-fix-async-notification-bridge/plan-01
    provides: Atomic recv_multipart in notification_client.py
provides:
  - Thread-safe asyncio bridge from ZMQ thread to main event loop via run_coroutine_threadsafe()
  - Event loop injection from main.py startup into NotificationHandler
  - Diagnostic logging across thread-to-asyncio boundary
affects: [03-end-to-end-flow-repair]

# Tech tracking
tech-stack:
  added: []
  patterns: [run_coroutine_threadsafe thread-to-asyncio bridge, event-loop injection at startup]

key-files:
  created: []
  modified: [apps/desktop/win/src/handlers/notification_handler.py, apps/desktop/win/src/main.py]

key-decisions:
  - "Used asyncio.run_coroutine_threadsafe() with future.result(timeout=5s) for synchronous blocking from ZMQ thread"
  - "Event loop injected via set_event_loop() called between set_extension_server() and notification thread start"
  - "Diagnostic label prefix [HANDLER-DIAG] distinguishes from [NOTIFY-DIAG] and [EXT-DIAG]"

patterns-established:
  - "Thread-safe bridge: run_coroutine_threadsafe(coro, injected_loop) with timeout and error handling"
  - "Startup ordering guarantee: extension_server -> event_loop -> notification_thread"

# Metrics
duration: 3min
completed: 2026-02-12
---

# Phase 2 Plan 2: Fix Async Notification Bridge - Thread-to-Asyncio Bridge Summary

**run_coroutine_threadsafe() replaces broken asyncio.run() fallback, with event loop injected from main.py before notification thread starts**

## Performance

- **Duration:** ~3 min
- **Started:** 2026-02-12T08:07:21Z
- **Completed:** 2026-02-12T08:10:00Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Removed the PRIMARY bug: `asyncio.run()` fallback in `handle()` that created a new event loop with zero WebSocket clients
- Replaced with `asyncio.run_coroutine_threadsafe()` that schedules broadcast on the main event loop where WebSocket clients are connected
- Added `self._main_loop` attribute and `set_event_loop()` method to NotificationHandler
- Added event loop injection in `main.py start()` using `asyncio.get_running_loop()` at the correct point in startup sequence
- Added `_diag_log()` module-level function with `[HANDLER-DIAG]` prefix for thread boundary tracing
- Added diagnostic logging at notification receive, broadcast scheduling, and broadcast completion/failure
- Blocking `future.result(timeout=5.0)` with proper exception handling for TimeoutError and general exceptions

## Task Commits

Each task was committed atomically:

1. **Task 1: Fix NotificationHandler thread-to-asyncio bridge** - `656843a` (feat)
2. **Task 2: Inject event loop reference in main.py startup** - `1fdd193` (feat)

## Files Created/Modified
- `apps/desktop/win/src/handlers/notification_handler.py` - Replaced asyncio.run() with run_coroutine_threadsafe(), added _main_loop/set_event_loop(), added _diag_log() with thread-boundary diagnostics
- `apps/desktop/win/src/main.py` - Added set_event_loop(asyncio.get_running_loop()) call between extension server setup and notification thread start

## Decisions Made
- Used `future.result(timeout=5.0)` to block the ZMQ thread synchronously while awaiting the async broadcast, providing clear error reporting if the broadcast fails or times out
- Event loop injection placed at line 138, between `set_extension_server` (line 135) and `notification_thread.start()` (line 158), ensuring the handler has both the server reference and loop reference before any notifications arrive
- `[HANDLER-DIAG]` prefix chosen to distinguish from `[NOTIFY-DIAG]` (notification_client.py) and `[EXT-DIAG]` (extension_server.py) diagnostic logs

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- The full notification pipeline is now wired: ZMQ SUB (atomic recv_multipart) -> JSON decode -> NotificationHandler.handle() -> run_coroutine_threadsafe -> broadcast() on main loop -> WebSocket clients
- Ready for Phase 3 (end-to-end flow repair) to verify the complete URL -> analysis -> score -> extension pipeline
- Runtime verification still requires starting Backend + Desktop App services

---
*Phase: 02-fix-async-notification-bridge*
*Completed: 2026-02-12*
