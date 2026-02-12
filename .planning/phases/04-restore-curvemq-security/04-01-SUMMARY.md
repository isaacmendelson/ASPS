---
phase: 04-restore-curvemq-security
plan: 01
subsystem: security
tags: [curvemq, curve25519, zmq, encryption, z85, pyzmq]

# Dependency graph
requires:
  - phase: 01-diagnose-verify-baseline-communication
    provides: "CurveEnabled disabled in appsettings.json, diagnostic logging in ZMQ clients"
  - phase: 03-restore-end-to-end-score-flow
    provides: "Working ZMQ REQ/REP token flow, RegisterDevice/RequestToken"
provides:
  - "CURVE client encryption on ZMQ REQ socket (zmq_client.py)"
  - "CURVE client encryption on ZMQ SUB socket (notification_client.py)"
  - "Backend CurveEnabled=true (appsettings.json)"
  - "Z85 server public key synced across Python config and Backend config"
  - "apply_curve_client() reusable helper for any ZMQ socket"
affects: [05-reliability-polish]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "CURVE options set BEFORE socket.connect() (critical ordering)"
    - "Ephemeral client keypairs via zmq.curve_keypair() per socket"
    - "Shared apply_curve_client() helper imported by notification_client from zmq_client"
    - "Z85 server key configurable via ZMQ_SERVER_PUBLIC_KEY env var"

key-files:
  created: []
  modified:
    - apps/desktop/win/src/config.py
    - apps/desktop/win/src/zmq_client.py
    - apps/desktop/win/src/notification_client.py
    - apps/desktop/win/src/core/container.py
    - apps/desktop/win/src/auth_manager.py
    - ASPSBackend14_J/ASPSBackend/appsettings.json

key-decisions:
  - "Ephemeral client keypairs (no persistent client keys needed, no ZAP authenticator)"
  - "Single apply_curve_client() helper shared between zmq_client and notification_client"
  - "Server public key default hardcoded from appsettings.json, overridable via env var"
  - "CURVE_ENABLED defaults to true (production stance), disableable via env var"

patterns-established:
  - "CURVE-before-connect: All ZMQ socket.connect() calls must be preceded by CURVE setup"
  - "Config-to-container wiring: CURVE settings flow config.py -> container.py -> client constructors"

# Metrics
duration: 3min
completed: 2026-02-12
---

# Phase 4 Plan 01: Restore CurveMQ Security Summary

**CurveMQ Curve25519 encryption on both ZMQ sockets (REQ+SUB) with Z85 server key sync and Backend re-enabled**

## Performance

- **Duration:** 3 min
- **Started:** 2026-02-12T21:28:01Z
- **Completed:** 2026-02-12T21:31:14Z
- **Tasks:** 2/2
- **Files modified:** 6 (across 2 repos)

## Accomplishments
- CurveMQ client encryption added to ZMQ REQ socket (zmq_client.py) and SUB socket (notification_client.py)
- Backend CurveEnabled flipped from false to true in appsettings.json
- Z85 server public key verified matching across all 4 points: config.py, container->zmq_client, container->notification_client, appsettings.json
- CURVE options correctly ordered BEFORE socket.connect() in both clients
- Reusable apply_curve_client() helper generates ephemeral keypairs per socket connection
- Server public key stored from token response in AuthManager for runtime validation

## Task Commits

Each task was committed atomically:

1. **Task 1: Add CURVE client support to Python ZMQ clients and wire through container** - `d038234` (feat) -- apps repo
2. **Task 2: Flip Backend CurveEnabled to true and verify cross-repo consistency** - `776b0a7` (feat) -- ASPSBackend14_J repo

## Files Created/Modified
- `apps/desktop/win/src/config.py` - Added SERVER_PUBLIC_KEY_Z85 and CURVE_ENABLED settings
- `apps/desktop/win/src/zmq_client.py` - Added apply_curve_client() helper, CURVE params in constructor, CURVE in connect()
- `apps/desktop/win/src/notification_client.py` - Added CURVE params in constructor, CURVE in _listen() before connect
- `apps/desktop/win/src/core/container.py` - Wired CURVE_ENABLED and SERVER_PUBLIC_KEY_Z85 to both ZMQ clients
- `apps/desktop/win/src/auth_manager.py` - Added server_public_key field, stores key from token response
- `ASPSBackend14_J/ASPSBackend/appsettings.json` - Flipped CurveEnabled: true, updated _CurveNote

## Decisions Made
- Ephemeral client keypairs generated per connection via zmq.curve_keypair() -- no persistent client key storage needed since Backend uses no ZAP authenticator
- Single shared apply_curve_client() helper in zmq_client.py, imported by notification_client.py -- avoids code duplication
- SERVER_PUBLIC_KEY_Z85 defaults to the Backend's key from appsettings.json, overridable via ZMQ_SERVER_PUBLIC_KEY env var
- CURVE_ENABLED defaults to true (secure by default), disableable via CURVE_ENABLED=false env var for debugging

## Deviations from Plan
None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required. CURVE keys are already generated in the Backend (CurveKeyManager.cs generates on first run).

## Next Phase Readiness
- All ZMQ communication is now CURVE-encrypted
- Phase 5 (Reliability/Polish) can proceed -- encrypted pipeline is the baseline
- Runtime testing requires starting Backend + Desktop App together; CURVE mismatch symptoms are silent timeouts (not error messages)
- If CURVE causes issues during runtime testing, set CURVE_ENABLED=false env var for graceful fallback

---
*Phase: 04-restore-curvemq-security*
*Completed: 2026-02-12*
