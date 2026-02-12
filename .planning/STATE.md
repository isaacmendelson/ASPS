# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-11)

**Core value:** Full score flow must work: URL -> analysis -> score displayed in Chrome Extension
**Current focus:** Phase 5 — Harden Reliability and Document

## Current Position

Phase: 5 of 5 (Harden Reliability and Document)
Plan: 1 of 3 in current phase
Status: In progress
Last activity: 2026-02-12 -- Completed 05-01-PLAN.md (ZMQ Lazy Pirate + WebSocket PendingResults)

Progress: [########*.] 85%

## Performance Metrics

**Velocity:**
- Total plans completed: 8
- Average duration: ~3 min per plan
- Total execution time: ~27 min

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 1. Diagnose Baseline | 2 | ~10 min | ~5 min |
| 2. Fix Async Notification Bridge | 2 | ~4 min | ~2 min |
| 3. Restore End-to-End Score Flow | 2 | ~7 min | ~3.5 min |
| 4. Restore CurveMQ Security | 1 | ~3 min | ~3 min |
| 5. Harden Reliability and Document | 1 | ~3 min | ~3 min |

**Recent Trend:**
- Last 5 plans: 03-01 (complete), 03-02 (complete), 04-01 (complete), 05-01 (complete)
- Trend: Consistent fast execution on targeted fixes

*Updated after each plan completion*

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- [Roadmap]: Repair proceeds link-by-link (diagnose -> bridge fix -> end-to-end -> security -> reliability)
- [Roadmap]: CurveMQ temporarily disabled during Phases 1-3, re-enabled in Phase 4
- [Research]: Top suspected root causes: (1) CurveMQ mismatch, (2) asyncio event loop bridge failure
- [Phase 1]: CurveMQ disabled in appsettings.json (CurveEnabled: false)
- [Phase 1]: Diagnostic scripts created for ZMQ and WebSocket verification
- [Phase 1]: Boundary logging added to zmq_client.py and extension_server.py
- [Phase 2]: recv_multipart() replaces dual recv() in notification_client.py for atomic frame reception
- [Phase 2]: Diagnostic logging added to notification_client.py SUB boundary ([NOTIFY-DIAG] prefix)
- [Phase 2]: asyncio.run() replaced with run_coroutine_threadsafe() in notification_handler.py (PRIMARY BUG FIX)
- [Phase 2]: Event loop injected from main.py start() into NotificationHandler before thread start
- [Phase 3]: RegisterDevice/RequestToken two-step flow for token acquisition from Backend
- [Phase 3]: Hardcoded UUID removed; empty token fallback with clear warning instead
- [Phase 3]: USER_EMAIL configurable via env var (default: user@example.com)
- [Phase 4]: CurveMQ re-enabled: CurveEnabled=true in appsettings.json
- [Phase 4]: Ephemeral client keypairs per socket via zmq.curve_keypair() (no ZAP authenticator needed)
- [Phase 4]: apply_curve_client() shared helper, CURVE options set BEFORE socket.connect()
- [Phase 4]: SERVER_PUBLIC_KEY_Z85 configurable via ZMQ_SERVER_PUBLIC_KEY env var
- [Phase 4]: CURVE_ENABLED defaults to true (secure by default), disableable for debugging
- [Phase 5]: Lazy Pirate: poll() before recv() on REQ socket, _reset_socket() on timeout, retry up to 3 times
- [Phase 5]: PendingResults store (5min TTL, 50 max) in extension_server.py, flush on client connect
- [Phase 5]: notification_handler.py unchanged -- broadcast() handles no-clients case

### Pending Todos

- Runtime verification of full pipeline (requires starting Backend + Desktop App)

### Blockers/Concerns

- [RESOLVED by 03-01]: Hardcoded token UUID replaced with real RegisterDevice flow
- [Research]: Extension cache (1-hour TTL) can mask broken pipeline during testing -- use fresh URLs
- [Phase 3]: Default email user@example.com must match active Backend user, or RegisterDevice will fail
- [Phase 1]: Runtime testing deferred -- user to run diag_zmq_test.py and diag_ws_test.py when services are started
- [Phase 4]: CURVE mismatch symptoms are silent timeouts, not error messages -- if pipeline times out, check key sync

## Phase 1 Completion Notes

**What was delivered:**
1. CurveMQ disabled in ASPSBackend14_J/ASPSBackend/appsettings.json
2. Standalone ZMQ diagnostic script: apps/desktop/win/src/diag_zmq_test.py
3. Standalone WebSocket diagnostic script: apps/desktop/win/src/diag_ws_test.py
4. Diagnostic boundary logging in zmq_client.py and extension_server.py

**Verification:** 8/8 code must-haves verified. Runtime testing is manual (requires running services).

**Commits (apps repo):**
- bb23741: feat(01-01): add diagnostic logging to zmq_client.py
- d6170ed: feat(01-01): create standalone ZMQ REQ/REP diagnostic test script
- 5651758: feat(01-02): add diagnostic logging to extension_server.py
- 07f8e64: feat(01-02): create WebSocket diagnostic test script

**Commits (ASPSBackend14_J repo):**
- 2f47fdc: feat(01-01): disable CurveMQ for Phase 1-3 diagnostics

## Phase 2 Completion Notes

**What was delivered:**
1. Atomic recv_multipart() in notification_client.py (02-01)
2. Thread-safe asyncio bridge via run_coroutine_threadsafe() in notification_handler.py (02-02)
3. Event loop injection from main.py into NotificationHandler at startup (02-02)
4. Diagnostic boundary logging across all notification pipeline components

**Primary bug fixed:** asyncio.run() in handle() created isolated event loop with zero WebSocket clients. Now uses run_coroutine_threadsafe() to schedule broadcast on the main event loop where WebSocket clients are connected.

**Verification:** 7/7 must-haves verified (COMM-02, COMM-04 satisfied). See 02-VERIFICATION.md.

**Commits (apps repo):**
- 2cd1c63: feat(02-01): fix ZMQ SUB to use atomic recv_multipart with diagnostic logging
- 656843a: feat(02-02): fix NotificationHandler thread-to-asyncio bridge
- 1fdd193: feat(02-02): inject event loop into NotificationHandler at startup

## Phase 3 Completion Notes

**What was delivered:**
1. ZMQClient.request_token() sends RegisterDevice, falls back to RequestToken (03-01)
2. AuthManager.authenticate() acquires real token from Backend at startup (03-01)
3. AuthManager.is_valid() properly checks non-empty + non-expired (03-01)
4. Hardcoded UUID "12345678-..." fully eliminated from token flow (03-01)
5. USER_EMAIL and DEVICE_MAC configured in config.py (03-01)
6. Pre-flight code validation passed for all E2E paths (03-02)

**Runtime testing DEFERRED:** User cannot test live pipeline now. All code artifacts verified.

**Verification:** 9/9 code must-haves verified. See 03-VERIFICATION.md.

**Commits (apps repo):**
- 7674144: feat(03-01): add request_token method and remove hardcoded UUID fallback
- 003981e: feat(03-01): wire token acquisition into AuthManager and startup flow

## Phase 5 Plan 1 Completion Notes

**What was delivered:**
1. Lazy Pirate retry pattern in zmq_client.py: _reset_socket() + poll()-based retry loop in send_alert() (05-01)
2. PendingResults class in extension_server.py: store/flush/cleanup with 5min TTL, 50 max (05-01)
3. broadcast() stores pending results when no clients (instead of silently dropping) (05-01)
4. _handle_client() flushes pending results to reconnected Extension client (05-01)
5. close() sets LINGER=0 before socket close to prevent blocking (05-01)

**Verification:** All 6 verification checks passed. REL-01 and REL-02 satisfied.

**Commits (apps repo):**
- 6b7a012: feat(05-01): add Lazy Pirate retry pattern to ZMQ REQ socket
- dd64a3d: feat(05-01): add PendingResults store and flush on WebSocket reconnect

## Phase 4 Completion Notes

**What was delivered:**
1. CurveMQ client encryption on ZMQ REQ socket via apply_curve_client() in zmq_client.py (04-01)
2. CurveMQ client encryption on ZMQ SUB socket via apply_curve_client() in notification_client.py (04-01)
3. Backend CurveEnabled flipped to true in appsettings.json (04-01)
4. Z85 server public key synced across all 4 check points (config, zmq_client, notification_client, appsettings)
5. CURVE options verified BEFORE socket.connect() in both clients

**Verification:** All 6 verification checks + all 6 overall verification checks passed.

**Commits (apps repo):**
- d038234: feat(04-01): add CurveMQ client encryption to ZMQ REQ and SUB sockets

**Commits (ASPSBackend14_J repo):**
- 776b0a7: feat(04-01): re-enable CurveEnabled in Backend appsettings.json

## Session Continuity

Last session: 2026-02-12
Stopped at: Completed 05-01-PLAN.md (ZMQ Lazy Pirate + WebSocket PendingResults)
Resume file: None
