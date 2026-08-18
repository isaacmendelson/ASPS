# ASPS-721: Desktop Agent — WebSocket Transport Layer

**JIRA:** ASPS-721 (sub-task of ASPS-718, under ASPS-693 Epic)
**Agent:** desktop-agent
**Branch:** `main` (infrastructure task, per CEO decision — see task instructions)
**Status:** Implementation complete, tests green, not yet QA-reviewed
**Last updated:** 2026-08-19

---

## Summary

Added a WebSocket transport option (`TRANSPORT_MODE = "ws"`) to the Python
desktop agent, implementing the agent side of
`docs/architecture/WS-AGENT-PROTOCOL.md` (ADR-004 / ASPS-718). The ZMQ
transport remains the default (`TRANSPORT_MODE = "zmq"`) and is unaffected.

## Files changed

### New
- `apps/desktop/win/src/ws_client.py` — `WSClient`: combines the ZMQClient
  (request/response) and NotificationClient (push subscription) roles over a
  single persistent WebSocket connection. Background thread + own asyncio
  event loop; public methods are synchronous/blocking (same contract as
  `ZMQClient.send_alert()`), marshalled onto the loop via
  `asyncio.run_coroutine_threadsafe`. Implements: frame build/parse, request/
  response correlation by `id` (`PendingRequests`, backed by
  `concurrent.futures.Future`), `subscribe`/`ping`/`pong`/`error` frame
  handling, reconnect with exponential backoff (1s,2s,4s,8s,16s,30s + jitter,
  per protocol §8), auto re-authenticate + re-subscribe after reconnect.
- `apps/desktop/win/src/alert_builders.py` — pure, transport-agnostic
  functions that build the exact JSON payloads for every alert/token message
  type. Extracted from `zmq_client.py` (no behavior change) so `ZMQClient`
  and `WSClient` share one source of truth and always produce byte-identical
  payloads (protocol §10 requirement). Also carries `Priority`,
  `effective_priority`, `attach_immediate_danger` (ImmediateDanger stamping).
- `apps/desktop/win/src/config_azure.py` — env override:
  `TRANSPORT_MODE = "ws"`, `WS_URL = "wss://ca-webapi-dev.purplesand-dfb51ae4.northeurope.azurecontainerapps.io/ws/agent"`.
  Applied via the existing generic `python build_release.py --env azure`
  (no changes needed to `build_release.py` — it already copies
  `config_<env>.py` → `config_override.py` for any `<env>` name).
- `apps/desktop/win/src/tests/test_ws_client.py` — 51 tests (see below).

### Modified
- `apps/desktop/win/src/config.py` — added `TRANSPORT_MODE` (env
  `TRANSPORT_MODE`, default `"zmq"`) and `WS_URL` (env `WS_URL`, default
  `""`). `_load_curve_public_key()` now short-circuits to `""` when
  `TRANSPORT_MODE == "ws"` instead of raising `SystemExit` (CURVE is not
  used on this transport — TLS/`wss://` is the boundary instead, per ADR-004
  "Security analysis"). The `BACKEND_SERVER_PUBLIC_KEY_Z85 = _load_curve_public_key()`
  call was **moved from near the top of the file to the very end** (after
  the `config_override` import) — this is load-bearing: it makes
  `TRANSPORT_MODE` reflect the environment override (e.g. `config_azure.py`)
  *before* the CURVE lookup decides whether to run at all. Without this
  move, an azure build would still abort startup looking for a CURVE key it
  will never use.
- `apps/desktop/win/src/auth_manager.py` — `AuthManager.__init__` now
  branches on `config.TRANSPORT_MODE` (read via `getattr(_config,
  'TRANSPORT_MODE', 'zmq')` so it stays compatible with existing test
  doubles that stub a bare `config` module without that attribute): in
  `"ws"` mode it skips the CURVE-key-required `RuntimeError` guard and the
  `set_server_public_key()` call entirely (WSClient's version of that method
  is a documented no-op anyway). `"zmq"` mode behavior is unchanged.
- `apps/desktop/win/src/core/container.py` — `zmq_client` / `notification_client`
  properties now check `TRANSPORT_MODE`: in `"ws"` mode both resolve to one
  shared, lazily-created `WSClient` instance (new `_ws_client` property); in
  `"zmq"` mode, unchanged (`ZMQClient` + `NotificationClient` as before).
- `apps/desktop/win/src/zmq_client.py` — refactored (behavior-preserving) to
  call the new `alert_builders` functions instead of building alert dicts
  inline. Removed now-dead local `_Priority` / `_effective_priority` /
  `_attach_immediate_danger` / `_is_danger_active` (moved to
  `alert_builders.py`) and the now-unused `uuid`/`datetime`/envelope imports.
  `get_local_ip()` is untouched (still imported by `monitor_service.py`,
  `extension_handler.py` regardless of transport).
- `apps/desktop/win/src/tests/test_curve_bootstrap.py` — extracted
  `_run_loader` (was a `TestConfigCurveKeyLoader` method) into a
  module-level `_run_curve_loader()` helper so it can be shared without
  triggering duplicate test collection via inheritance. Added
  `TestConfigWSTransportCurveToleration` (4 tests: WS mode returns `""` with
  no/empty/present key sources; default `zmq` mode still raises) and
  `TestAuthManagerWSTransportCurveToleration` (3 tests: WS mode doesn't
  raise / doesn't call `set_server_public_key`; a config stub with no
  `TRANSPORT_MODE` attribute at all still defaults to strict `zmq`
  behavior).

## build_release.py

**No changes required.** It already generically supports
`python build_release.py --env <name>` by copying `src/config_<name>.py` →
`src/config_override.py`. Creating `config_azure.py` was sufficient for
`--env azure` to work.

## TDD evidence

- **Red→Green, extraction refactor:** before touching `zmq_client.py`, ran
  the full existing suite (254 passed) to establish a baseline; after
  extracting `alert_builders.py` and rewiring `zmq_client.py` to call it,
  re-ran — still 254 passed, 0 changed (behavior-preserving extraction,
  confirmed byte-for-byte via `TestWSClientPayloadParity` comparing
  `WSClient`'s and `alert_builders`' outputs directly).
- **New tests written before/alongside `ws_client.py`** (TDD per task
  instructions): `test_ws_client.py` covers frame
  serialization/deserialization, request/response correlation by `id`
  (including concurrent in-flight requests), frame dispatch (`response`/
  `error`/`notification`/`ping`), authentication-state tracking + auto-
  subscribe on success, reconnect replay (`_on_reconnected`), backoff timing
  (deterministic via a `jitter` override param), lifecycle (`connect`/
  `close`/`destroy`/`is_running`), and payload parity with
  `alert_builders`/ZMQ.
- **CURVE toleration** (both `config._load_curve_public_key()` and
  `AuthManager.__init__`) has explicit regression tests proving the
  **default `zmq` mode is unchanged** (still raises `SystemExit`/
  `RuntimeError`) alongside the new WS-mode tests proving it does **not**
  raise.

### Final test commands

```
cd apps/desktop/win
python -m pytest src/tests/test_ws_client.py -v        # 51 passed
python -m pytest src/tests/test_curve_bootstrap.py -v   # 25 passed
python -m pytest src/tests/ -q                           # 312 passed, 2 xfailed (pre-existing, unrelated — ASPS-562)
```

## Known environment issue (not introduced by this task, not fixed here)

On this dev machine, `%LOCALAPPDATA%\ASPS\curve-server-public-key.txt`
currently exists and is **empty** (stale from a prior local backend run with
`CurveEnabled=false`, or the local backend simply isn't running right now).
Because `config.py` unconditionally calls `_load_curve_public_key()` at
**import time**, any test file that does a real `import config` (e.g.
`test_browser_history.py`, first alphabetically) crashes the *entire*
pytest collection with `SystemExit` → `INTERNALERROR`, when
`TRANSPORT_MODE` defaults to `"zmq"`. **Reproduced against the unmodified,
pre-task `config.py` too** (via `git stash`) — this is a pre-existing
machine-state hazard, not a regression from this task. Workaround used for
full-suite verification: `TRANSPORT_MODE=ws python -m pytest src/tests/ -q`
(bypasses the CURVE lookup entirely; does not change any test's
assertions). Flagging for the orchestrator/QA — not fixed here per "no
silent side-fixes."  A real fix would live in `config.py`'s import-time
behavior (e.g. lazy-load the key on first use instead of at module import,
or make `test_browser_history.py`/similar stub `config` like
`test_curve_bootstrap.py` does) — out of this task's scope.

## Also discovered, not fixed (same "no silent side-fixes" reason)

`alert_builders._is_danger_active()` (and its predecessor in the original
`zmq_client.py`, identical code) does `except Exception: return False`
around the lazy `from services.danger_mode import danger_mode` import.
`SystemExit` is a `BaseException`, not an `Exception`, so if that lazy
import ever transitively hits `config.py`'s `SystemExit` path (see above),
it propagates uncaught through every `send_*_alert` call. Pre-existing in
`zmq_client.py` before this task; carried over verbatim (extraction, not
behavior change) into `alert_builders.py`. Worth a follow-up ticket.

## Definition of Done — status

- [x] `ws_client.py` implements WS transport with same interface as ZMQ client
- [x] `config.py` has `TRANSPORT_MODE` and `WS_URL` settings
- [x] `config_azure.py` created with Azure-specific settings
- [x] `container.py` selects transport based on config
- [x] `build_release.py` supports `--env azure` (no change needed — generic)
- [x] Request/response correlation works with concurrent requests (tested)
- [x] Notification subscription and callback works (tested; auto-subscribe on auth success)
- [x] Reconnection with exponential backoff works (backoff math tested; replay-on-reconnect tested via bare-loop-thread harness)
- [x] CURVE key loading doesn't abort when TRANSPORT_MODE is "ws" (config.py AND auth_manager.py — both tested)
- [x] Existing ZMQ transport unaffected — full suite green before/after, plus dedicated regression tests
- [x] Tests pass — 312 passed, 2 pre-existing unrelated xfail
- [x] No secrets in code — `config_azure.py` contains only a public `wss://` URL

## Not done / deferred

- No live end-to-end run against the real WebApi `/ws/agent` gateway (ASPS-720,
  owned by backend agent, may not be merged yet). All verification here is
  unit-level + a local smoke test against an unreachable WS endpoint
  (confirms connect-timeout, clean `destroy()`, no hangs). **E2E verification
  against a real gateway is ASPS-723 (QA)** once ASPS-720 lands.
- Did not modify `main.py` — no changes were needed; it already calls the
  transport-agnostic `container.zmq_client` / `container.notification_client`
  properties, which now resolve to the shared `WSClient` transparently in WS
  mode.

## Continuation point

Report to orchestrator: ready for QA. Coordinate with **backend** agent
(ASPS-720) for an actual E2E smoke test once the WebApi gateway is up, and
with **qa** for ASPS-723.
