# ASPS-757 — Rewrite quarantined `ConnectionService.test.js` against the current multi-port connect flow

**Parent epic:** ASPS-738
**Owner:** browser-extension
**Branch:** `asps-757-rewrite-connectionservice-tests` (cut from `main` @ `f56a860`, ASPS-756 merge)
**Status:** Implementation complete, pre-QA gate satisfied. Awaiting CEO-run QA + code review + security gate + merge (per `.claude/rules/task-workflow.md`). No PR opened per task instructions.

## What was done

Rewrote `apps/extension/chrome/tests/unit/services/ConnectionService.test.js` from scratch (6 → 19 tests, all against the current multi-port connect flow) and removed its baseline-exception entry from `scripts/test-baseline-exceptions.json`.

### Root cause of the original quarantine (confirmed by inspection)
The old suite mocked a single static WebSocket instance:
```js
mockWebSocket = { send: jest.fn(), close: jest.fn(), readyState: WebSocket.OPEN, onopen: null, onclose: null, onerror: null, onmessage: null };
global.WebSocket = jest.fn(() => mockWebSocket);
```
It never fired `onopen`/`onerror` on that instance. `ConnectionService.connect()` (`apps/extension/chrome/services/ConnectionService.js`) scans a *list* of ports (`config.ports = [8080, 8181, 8282, 8383, 8484]`), constructing a fresh `WebSocket` per port inside `tryPort()` and racing its `onopen`/`onerror` against a 2000ms `connectionTimeout` (`setTimeout`) — a port only resolves "connected" on `onopen`, or "failed, try next port" on `onerror` or the timeout. Since the mock never called either handler and the old test used real timers, `await connectionService.connect()` blocked until every port's real 2000ms timeout elapsed — 5 ports × 2s = 10s, blowing past Jest's default 5s test timeout. That is the exact "hang" the ticket and the baseline-exception `reason` field described.

### Current `ConnectionService.connect()` flow the rewritten tests exercise
(`apps/extension/chrome/services/ConnectionService.js` — **not modified**, read-only reference)
- `connect()` first tries a **saved port** (`getSavedPort()` → `chrome.storage.local.get(['connectedPort'], callback)`, callback-style) via `tryPort(savedPort)`. If that succeeds, `setupConnection()` runs and `connect()` returns `true` **without** calling `savePort()` again and **without** looping the full port list.
- If there's no saved port, or the saved-port attempt fails, `connect()` iterates `config.ports` in order, calling `tryPort(port)` for each. **Real (slightly redundant) behavior confirmed by this rewrite:** the saved port is not excluded from this subsequent full scan, so if the saved port also appears in `config.ports` it gets tried twice before the scan reaches a working port.
- `tryPort(port)` opens `new WebSocket('ws://localhost:' + port)`, races `onopen` (resolve `{ws, port}`) against `onerror` (resolve `null`) and a `config.connectionTimeout` (2000ms) `setTimeout` that calls `ws.close()` and resolves `null` if neither fires in time.
- On the first successful port, `connect()` calls `savePort(port)` (full-scan path only) then `setupConnection(ws, port)`, which: updates `stateManager` (`connection.desktop:true`, `connection.port`, `connection.reconnectAttempts:0`, `connection.reconnecting:false`), wires `onclose`/`onerror`/`onmessage`, starts ping/keepalive/heartbeat intervals, flushes the queued-message backlog, re-sends any stored email, and sends `WS_STATE_SYNC_REQUEST` + `WS_PING`.
- If every port fails, `connect()` returns `false` and calls `updateDisconnectedState()` (`connection.desktop:false`, `connection.port:null`).
- `onmessage` parses JSON, special-cases `heartbeat_pong` (never reaches `handleMessage`/registered handlers), otherwise calls `handleMessage(data)` which dispatches to handlers registered for `data.type` (or `data.jsonTypeName`) via `onMessage()`, plus any `'*'` wildcard handlers, passing the **full parsed message object** (not just `data.data`).
- `onclose` → `handleDisconnect()` → clears the websocket/timers, marks disconnected, and calls `scheduleReconnect()`: with 0 prior `connection.reconnectAttempts` it calls `attemptReconnect()` (i.e. `connect()`) **immediately**, with no `setTimeout`/alarm; only once `attempts > 0` does it compute an exponential-backoff delay and schedule a `chrome.alarms.create('reconnect', { delayInMinutes })`.
- `send()` writes JSON directly when the socket is `OPEN`, otherwise queues the message via `messageQueueService.enqueue()` and returns `false`.
- `disconnect()` clears the `reconnect`/`keepalive` alarms, stops ping/keepalive/heartbeat timers, clears `messageQueueService`, closes the socket, and marks disconnected. `reconnect()` force-closes any existing socket, resets `connection.reconnectAttempts` to 0, then calls `connect()`.

### How the WebSocket mock models the multi-port handshake (and avoids the hang)
A scriptable `MockWebSocket` class replaces the static mock. Each test configures a `portScript` map (`{ 8080: 'open' | 'error' | 'timeout', ... }`, unlisted ports default to `'error'`). On construction, the mock reads the port out of the `ws://localhost:<port>` URL and, for `'open'`/`'error'`, resolves the handshake on a **native `Promise.resolve().then()` microtask** (not `queueMicrotask`, which Jest's modern fake timers *do* fake) — firing `onopen`/`onerror` exactly once the real assertion-relevant handler assignments in `tryPort()`'s executor have completed synchronously. `'timeout'` intentionally fires neither, leaving `ConnectionService`'s own `connectionTimeout` `setTimeout` as the only thing that resolves that port.

`jest.useFakeTimers()` runs for every test (so the ping/keepalive/heartbeat `setInterval`s and the connect timeout never fire on real wall-clock time), but because the 'open'/'error' outcomes are driven by a genuine native Promise microtask, `await connectionService.connect()` resolves deterministically for every non-timeout test **without any timer advance at all** — this is what eliminates the original hang. Only the one test that exercises the stalled-port path explicitly drives fake time forward with `await jest.advanceTimersByTimeAsync(2000)`.

### Test bootstrap pattern
Matches the currently-GREEN `CacheService.test.js` (ASPS-756) / `ScanService.test.js` (ASPS-753) rewrites: `StateManager` (default export) and `MessageQueueService` (**named** export `messageQueueService`, matching `ConnectionService.js`'s `import { messageQueueService } from './MessageQueueService.js'`) are mocked via `jest.unstable_mockModule` so their real singletons' own persistence/queueing side effects don't pollute assertions on `ConnectionService`. `connectionService` is imported once at module scope (top-level `await import`) since it's a true singleton; per-test isolation resets its mutable fields (`websocket`, `messageHandlers`, `missedHeartbeats`, `deviceIpAddress`, the three timer handles) in `beforeEach` instead of re-importing. `chrome.storage.local.get` is stubbed to support both call shapes `ConnectionService.js` actually uses — callback-style (`getSavedPort()`) and un-awaited-callback/Promise-style (`sendStoredEmail()` → `chrome.storage.local.get(['userEmail'])`) — since the jest-chrome stub has no default implementation and an un-invoked callback was part of why the original suite hung.

### Test list (19, all against the current model)
- **Multi-port connect flow (6):** connects on the first port that opens; falls back through failing ports until one opens; tries the saved port first and skips the full scan on success; falls back to the full scan (re-trying the saved port) when the saved port fails; returns `false` + marks disconnected when every port fails; a stalled port (`connectionTimeout`, fake-timer-driven) is treated as a failure and the scan advances to the next port.
- **setupConnection lifecycle (2):** a successful connect sends `WS_STATE_SYNC_REQUEST` and an initial `WS_PING`; `isConnected()` reflects the live socket readyState (and returns `null`, not `false`, when there is no socket — asserted as falsy to match the real `websocket && readyState === OPEN` expression).
- **Message handling (5):** dispatch to a type-registered handler with the full parsed message; wildcard `'*'` handlers receive `(data, type)`; the unsubscribe function `onMessage()` returns removes the handler; `heartbeat_pong` is intercepted before reaching `handleMessage`/registered handlers and resets `missedHeartbeats`; malformed JSON on `onmessage` is caught and neither throws nor dispatches.
- **send()/queueing (2):** `send()` writes JSON over an open socket and does not touch the queue; `send()` queues via `messageQueueService.enqueue()` and returns `false` when there is no open socket.
- **disconnect()/reconnect() (2):** `disconnect()` closes the socket, clears the `reconnect`/`keepalive` alarms, clears the message queue, and marks disconnected; `reconnect()` force-closes the existing socket, resets `connection.reconnectAttempts`, and re-invokes `connect()`.
- **Reconnection scheduling (2):** the first reconnect attempt (0 prior attempts) calls `attemptReconnect()` immediately instead of scheduling an alarm; `onclose` with prior attempts schedules an exponential-backoff `chrome.alarms.create('reconnect', {...})` alarm and marks disconnected.

No tautological or hang-prone assertions — every assertion is against a real return value, a real mock-socket call, or a real `stateManager`/`chrome.alarms`/`messageQueueService` call the production code actually makes.

### Not tested / documented boundary
- `sendAndWait()` (the response-correlated send/await helper) and the ping/keepalive/heartbeat `setInterval` cadence itself (as opposed to their one-time setup call) are not separately exercised — out of scope for the multi-port connect-flow fix this ticket targets, and not part of the original 11 broken tests' intent. Flagging as a possible follow-up, not a gap in the required rewrite scope.
- No production code was touched or needed to be — `ConnectionService.js`'s real behavior was fully testable as-is; the two intentionally-documented "not exactly what you might guess" real-behavior quirks (saved port retried in the full scan; `isConnected()` returns `null` not `false` when disconnected) are asserted as-is rather than smoothed over.

## Files changed
- `apps/extension/chrome/tests/unit/services/ConnectionService.test.js` — full rewrite (6 → 19 tests, all against the current multi-port connect flow)
- `scripts/test-baseline-exceptions.json` — removed the `unit/services/ConnectionService.test.js` exception entry (11 expected failures, SHA `4b368ffe...`). The remaining entry (`unit/services/IconService.test.js` — 29) is untouched.

## Verification (pre-QA gate)

Exact commands run (from `apps/extension/chrome/tests`):

```bash
node --experimental-vm-modules ./node_modules/jest/bin/jest.js unit/services/ConnectionService.test.js
# → Test Suites: 1 passed, 1 total; Tests: 19 passed, 19 total

node --experimental-vm-modules ./node_modules/jest/bin/jest.js unit/services/ConnectionService.test.js --randomize
# → same 19/19 pass; re-run 3 additional times with different --randomize seeds, all 19/19 pass
#   (confirms no cross-test ordering coupling, and no hang under any order)

node --experimental-vm-modules ./node_modules/jest/bin/jest.js --runInBand --silent --json \
  --outputFile="<repoRoot>/artifacts/asps757/extension.json"
# → full extension suite: Test Suites: 1 failed, 16 passed, 17 total
#   Tests: 29 failed, 322 passed, 351 total
#   (the 1 failing suite is exactly the pre-existing quarantined IconService
#    suite — unchanged by this task; ConnectionService's 19 tests are
#    counted in the 322 passed)
```

Baseline gate (`scripts/check_test_baseline.py`), reusing the repo's existing `artifacts/asps626/{dotnet.trx,analyzer.xml,desktop.xml}` (unrelated components, not touched by this task) alongside the freshly-regenerated `artifacts/asps757/extension.json`:

```bash
py -3.11 scripts/check_test_baseline.py \
  --manifest scripts/test-baseline-exceptions.json \
  --dotnet artifacts/asps626/dotnet.trx \
  --analyzer artifacts/asps626/analyzer.xml \
  --desktop artifacts/asps626/desktop.xml \
  --extension artifacts/asps757/extension.json
```

**Output (after removing the manifest entry):**
```
dotnet: 1470 passed, 0 failed, 0 not executed
analyzer: 348 tests, 0 failures, 0 errors, 5 skipped
desktop: 247 tests, 0 failures, 0 errors, 2 skipped
extension: 322 passed, 29 failed, 0 pending
Baseline gate: PASS; 29 exact known failures remain visible and expiry-controlled.
```

29 = IconService(29) only, exactly matching the 1 remaining exception — no complaint about the removed ConnectionService entry.

## Pre-QA gate checklist
- [x] Task tests exist and pass (19/19, verified order-independent via `--randomize`, 4 different seeds).
- [x] Full extension component suite run — only the pre-existing/expected IconService failures remain (29, matching manifest).
- [x] No uncommitted changes — all committed to the task branch.
- [x] Branch cut from latest `main` (`f56a860`, ASPS-756 merge) — up to date at cut time.
- [ ] Merge latest `main` + re-test + push — done after this handoff is written (see below).
- [x] Handoff created (this file).
- [ ] Spec documents — see below (implementer does not edit specs).

## Specification documents that may be affected
None. This is a test-infrastructure-only change (test suite rewrite + baseline-exception manifest update); it does not change any contract, protocol, enum, or documented behavior of the extension or the WebSocket protocol. No `docs/system-specifications/`, `docs/architecture/` (including `docs/architecture/WS-AGENT-PROTOCOL.md`), or `docs/ASPS_DATA_FLOW.md` content is affected. Flagging per the task-workflow rule for TechWriter/Architect awareness — "no update needed" is the expected determination.

## No scope creep
Only touched: the target test file, its baseline-exception entry, and this handoff. Did **not** touch `services/ConnectionService.js`, `services/MessageQueueService.js`, `state/StateManager.js`, or any other production file, and did **not** touch the remaining quarantined suite (`IconService.test.js`).

## Continuation point for next agent (CEO / QA / security)
1. Run QA against this branch (functional review of the 19 rewritten tests vs. the current `ConnectionService.js` multi-port connect flow they claim to exercise — the "Current `ConnectionService.connect()` flow" and "Test list" sections above are the acceptance-criteria mapping).
2. Code review (orchestrator or delegate).
3. Security gate — this is a test-only change touching no production code, secrets, auth, or network surface; a "no security impact" determination is very likely appropriate here, but the security agent should make that call explicitly per `task-workflow.md`.
4. On approval: merge to `main`, delete branch, JIRA ASPS-757 → Done (41).
