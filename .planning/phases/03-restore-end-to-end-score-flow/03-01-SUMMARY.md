---
phase: 03-restore-end-to-end-score-flow
plan: 01
subsystem: desktop-auth
tags: [zmq, token, auth, device-registration]

dependency-graph:
  requires: [01-01, 01-02, 02-01, 02-02]
  provides: [device-registration, token-acquisition, real-token-flow]
  affects: [03-02]

tech-stack:
  added: []
  patterns: [zmq-req-rep-token-exchange, register-then-request-token-fallback]

key-files:
  created: []
  modified:
    - apps/desktop/win/src/zmq_client.py
    - apps/desktop/win/src/auth_manager.py
    - apps/desktop/win/src/config.py
    - apps/desktop/win/src/core/container.py
    - apps/desktop/win/src/main.py

decisions:
  - id: AUTH-01
    choice: "RegisterDevice + RequestToken two-step flow for token acquisition"
    reason: "Backend may return token on RegisterDevice or require separate RequestToken for already-registered devices"
  - id: AUTH-02
    choice: "Empty token instead of hardcoded UUID on fallback"
    reason: "Clear error from Backend is better than silent rejection of fake UUID"
  - id: AUTH-03
    choice: "user@example.com as default email"
    reason: "Must match active Backend user; overridable via USER_EMAIL env var"

metrics:
  duration: "~3 min"
  completed: "2026-02-12"
---

# Phase 3 Plan 1: Device Registration and Token Acquisition Summary

**One-liner:** ZMQ RegisterDevice/RequestToken flow replaces hardcoded UUID with real Backend-issued token at startup.

## What Was Done

### Task 1: Add request_token method to ZMQClient and configure USER_EMAIL
- Added `ZMQClient.request_token()` method that sends `RegisterDevice` message and parses token response
- If device is already registered, automatically retries with `RequestToken` message on fresh socket
- Removed hardcoded UUID `"12345678-1234-1234-1234-123456789012"` from both `send_url_alert()` and `send_remote_access_alert()` -- replaced with warning + empty string fallback
- Added `USER_EMAIL` config with env var support (default: `user@example.com`)
- Added `DEVICE_MAC` constant for consistent MAC address usage

### Task 2: Wire token acquisition into AuthManager and startup flow
- Rewrote `AuthManager.authenticate()` to call `zmq_client.request_token()` and store the real token
- Changed `AuthManager.is_valid()` from always-True to checking non-empty token + expiration
- Updated `AuthManager.ensure_authenticated()` with descriptive logging
- Updated `Container` to import and pass `USER_EMAIL` to AuthManager
- Added diagnostic prints in `main.py` startup for real vs missing token identification

## Token Flow Chain (End-to-End)

```
config.py: USER_EMAIL = os.environ.get('USER_EMAIL', 'user@example.com')
    |
container.py: AuthManager(zmq_client, device_info, USER_EMAIL)
    |
main.py: ensure_authenticated() -> authenticate() -> request_token()
    |
zmq_client.py: request_token() sends RegisterDevice -> Backend returns token
    |
auth_manager.py: stores token, _save_token() persists to auth.json
    |
scan_service.py: get_token() returns real token -> send_url_alert(token=real)
```

## Deviations from Plan

None - plan executed exactly as written.

## Commits

| # | Hash | Message | Files |
|---|------|---------|-------|
| 1 | 7674144 | feat(03-01): add request_token method and remove hardcoded UUID fallback | zmq_client.py, config.py |
| 2 | 003981e | feat(03-01): wire token acquisition into AuthManager and startup flow | auth_manager.py, container.py, main.py |

## Verification Results

- [x] `request_token()` method exists with RegisterDevice message format
- [x] Hardcoded UUID `"12345678-1234-1234-1234-123456789012"` returns 0 matches in zmq_client.py
- [x] `is_valid()` checks non-empty token and expiration (not always True)
- [x] `authenticate()` calls `self.zmq_client.request_token()`
- [x] `USER_EMAIL` imported and passed to AuthManager in container.py
- [x] Import tests pass: `from zmq_client import ZMQClient` and `request_token in dir(c)` returns True
- [x] Config test passes: `from config import USER_EMAIL` returns `user@example.com`

## Next Phase Readiness

Plan 03-02 can now proceed. The Desktop App will attempt token acquisition at startup. Full end-to-end testing (with running Backend) will validate the complete flow in 03-02 or during manual testing.

**Remaining concern:** The default email `user@example.com` must match an active user in the Backend database. If it does not, RegisterDevice will fail and no token will be issued. The user can override via `USER_EMAIL` environment variable.
