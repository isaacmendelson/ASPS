# ASPS-755 — Rewrite quarantined `extension-flow.test.js` against real background.js wiring

**Parent epic:** ASPS-738
**Owner:** browser-extension
**Branch:** `asps-755-rewrite-extension-flow-tests` (cut from `main` @ `8b6d329`, pushed to `origin`)
**Status:** Implementation complete, pre-QA gate satisfied. Awaiting CEO-run QA + code review + security gate + merge (per `.claude/rules/task-workflow.md`). No PR opened per task instructions.

## What was done

Rewrote `apps/extension/chrome/tests/integration/extension-flow.test.js` from scratch and removed its baseline-exception entry from `scripts/test-baseline-exceptions.json`.

### Root cause of the original quarantine (confirmed by inspection)
The old suite (9 tests) never imported `background.js`. It called `chrome.tabs.onUpdated.addListener.mockImplementation(...)` etc. and then asserted `expect(chrome.action.setIcon).toHaveBeenCalledWith(...)` / `expect(chrome.tabs.sendMessage).toHaveBeenCalledWith(...)` — but nothing in the test ever caused those calls to happen. The 5 tests that "passed" only did so because they asserted on `addListener.mock.calls.length` (which is `> 0` after the test's own `mockImplementation()` call, a no-op tautology) or because a `chrome.storage.local.set`/`chrome.tabs.sendMessage` mock call was still counted from a *different* test's `mockImplementation()` callback firing earlier in the same file. Pure test-ordering coupling, exactly as described in `scripts/test-baseline-exceptions.json`'s exception reason.

### Rewrite approach
The rewritten suite (`apps/extension/chrome/tests/integration/extension-flow.test.js`, 12 tests) genuinely imports `background.js` in every test and exercises the wiring its `init()` actually registers:

1. **Per-test isolation.** `beforeEach` calls `jest.resetModules()` (fresh singleton service instances every test) **and** explicitly clears jest-chrome's `Event` listener registries (`chrome.tabs.onUpdated.clearListeners()`, `chrome.runtime.onMessage.clearListeners()`, etc.). This second part matters: jest-chrome's event objects hold a module-scoped `Set()` of registered callbacks that survives `jest.resetModules()` (it's a `global.chrome` binding, not part of the ES module registry) — without clearing it, listeners from every previous test's `background.js` import would still fire on subsequent `callListeners()` calls. This is the same class of bug (persistent shared mock state across tests) that broke the original suite, just one layer deeper — fixing it is central to why this rewrite is order-independent (verified via `--randomize`).
2. **Real singletons, not re-implemented doubles.** `beforeEach` dynamically imports the real `connectionService`, `messageBus`, `stateManager`, `cacheService`, `iconService`, `messageQueueService`, `trackingService`, `scanService` via the same `@/...` alias path `background.js` itself resolves (so it's the identical module instance), *then* imports `background.js` last, so its `init()` wires every handler onto those exact objects.
3. **One deliberate stub, documented in the file header:** `connectionService.connect()` (the WebSocket handshake / port-scan) is replaced with `jest.spyOn(...).mockResolvedValue(true)`. That handshake is `ConnectionService`'s own contract and is already covered by `tests/unit/services/ConnectionService.test.js` (currently one of the 3 remaining quarantined suites, unrelated to this task). Stubbing it keeps this suite fast/deterministic while every handler `setupWebSocketHandlers()`/`setupMessageHandlers()`/the tab-event listeners register is still exercised for real.
4. **Driving messages the same way production does:** `connectionService.handleMessage(msg)` (exactly what `ws.onmessage` calls) and `messageBus.handleMessage(msg, sender, sendResponse)` (exactly what `chrome.runtime.onMessage`'s listener calls) — not re-implemented stand-ins. Tab events are dispatched via jest-chrome's real `chrome.tabs.onUpdated.callListeners(...)`, invoking background.js's actual registered listener.
5. **Assertions check real resulting state**, not mock call counts alone: real `cacheService.get()` cache entries, real `stateManager.get(...)` reads, real `iconService.getColor()`, real `messageQueueService.queue[...]` contents, real `buildStatusResponse()` output via `STATUS_GET`.

### Test list (12, all real-wiring, all independent)
1. `init()` registers the real chrome event listeners and message handlers (checks `chrome.tabs.onUpdated` has exactly 2 real listeners — the auto-scan one and the TabAlertService one — plus `onCreated`/`onRemoved`/`onActivated`/`alarms.onAlarm`/`cookies.onChanged`/`webNavigation.onCompleted`/`onHistoryStateUpdated`, and that `connectionService.connect` was invoked by `init()`)
2. `WS_PONG` → stores agent email + device IP
3. `WS_URL_RESULT` → full `ScanService.handleResult` → `CacheService.set` → `IconService.setColorByAction` pipeline
4. `WS_NOTIFICATION` → real `chrome.notifications.create`
5. `WS_IMMEDIATE_DANGER_STARTED`/`_ENDED` → real `DangerStateService.persist` → `chrome.storage.session.set`
6. `TRACKED_DOMAINS_SET` (WS) → real `TrackingService.rebuildFromBackend` + content-script broadcast to http tabs only
7. `STATUS_GET` (messageBus) → `buildStatusResponse()` reflecting state written by a prior `WS_URL_RESULT`
8. `CACHE_CLEAR` (messageBus) → real cache cleared + `chrome.storage.local.remove(['urlCache'])`
9. `SCAN_CURRENT` (messageBus) → drives real `ScanService.scanCurrentTab()`; asserts the real envelope message lands in `MessageQueueService`'s queue (since the suite never has a live websocket)
10. `AUTH_SIGN_IN` / `AUTH_SIGN_OUT` (messageBus) → real `StateManager.user.*` + queued `WS_USER_AUTH`/`WS_USER_SIGNOUT`
11. `REMOTE_ACCESS_WARNING_DISMISS` (messageBus) → real warning-state clear + dismiss broadcast to all open tabs
12. `chrome.tabs.onUpdated` (status: complete) → real auto-scan wiring calls `ScanService.scan(tabId, url)` with the correct args and updates the icon from the result

### Known gap, documented in-file (not a hollow assertion — a boundary choice)
`ConnectionService.connect()`'s real WebSocket port-scan handshake (`tryPort`/`setupConnection`) is not exercised end-to-end here — it's stubbed (see point 3 above). Driving it for real would mean either accepting the same real ~2s-per-port timeout `ConnectionService.test.js` already accepts for its own narrower scope, or building a fake WebSocket transport that adds no coverage over that suite. This is the only intentionally-skipped real-Chrome-API path; every message/listener handler `background.js` registers is otherwise exercised against the real singleton it's registered on.

One incidental discovery, not fixed here (out of scope — same reasoning applies to the un-rewritten `ConnectionService.test.js`/`CacheService.test.js`/`IconService.test.js`, listed below): `jest-chrome` 0.8.0 does not implement MV3's `chrome.action` namespace (only the MV2 `chrome.browserAction`). This suite polyfills `chrome.action` locally in its own `beforeEach` (scoped to this file only, does not touch `tests/setup/jest.setup.cjs` or any other suite) so `IconService`'s real `setColor()`/`drawLoadingFrame()` calls — exercised for real by several tests above — don't throw. `tests/unit/services/IconService.test.js` has the identical gap and is one of the three still-quarantined suites; if it's picked up for the same treatment later, this same polyfill (or a shared one in `jest.setup.cjs`) would fix it.

## Files changed
- `apps/extension/chrome/tests/integration/extension-flow.test.js` — full rewrite (410 insertions / 279 deletions net across both files in the commit)
- `scripts/test-baseline-exceptions.json` — removed the `integration/extension-flow.test.js` exception entry (9 expected failures, SHA `f26b595b...`). The other three entries (`unit/services/CacheService.test.js` — 6, `unit/services/ConnectionService.test.js` — 11, `unit/services/IconService.test.js` — 29) are untouched.

## Verification (pre-QA gate)

Exact commands run:

```bash
cd apps/extension/chrome/tests
node --experimental-vm-modules ./node_modules/jest/bin/jest.js integration/extension-flow.test.js
# → Test Suites: 1 passed, 1 total; Tests: 12 passed, 12 total

node --experimental-vm-modules ./node_modules/jest/bin/jest.js integration/extension-flow.test.js --randomize
# → same 12/12 pass in randomized order (confirms no cross-test ordering coupling)

node --experimental-vm-modules ./node_modules/jest/bin/jest.js
# → full extension suite: Test Suites: 3 failed, 14 passed, 17 total
#   Tests: 46 failed, 284 passed, 330 total
#   (the 3 failing suites are exactly the pre-existing quarantined
#    CacheService/ConnectionService/IconService suites — unchanged by this task)

npm test -- --runInBand --silent --json --outputFile="<repoRoot>/artifacts/asps626/extension.json"
# → 284 passed, 46 failed, 0 pending (written for the baseline checker)
```

Baseline gate (`scripts/check_test_baseline.py`), reusing the repo's existing `artifacts/asps626/{dotnet.trx,analyzer.xml,desktop.xml}` (unrelated components, not touched by this task — those artifacts predate this change but their content isn't part of the ASPS-755 diff) alongside the freshly-generated `extension.json`:

```bash
py -3.11 scripts/check_test_baseline.py \
  --manifest scripts/test-baseline-exceptions.json \
  --dotnet artifacts/asps626/dotnet.trx \
  --analyzer artifacts/asps626/analyzer.xml \
  --desktop artifacts/asps626/desktop.xml \
  --extension artifacts/asps626/extension.json
```

**Before** removing the manifest entry (proves the gate correctly detects a fixed exception):
```
Baseline gate: FAIL
- extension: expected exception groups no longer fail; remove/update manifest: ['integration/extension-flow.test.js']
```

**After** removing the manifest entry:
```
dotnet: 1470 passed, 0 failed, 0 not executed
analyzer: 348 tests, 0 failures, 0 errors, 5 skipped
desktop: 247 tests, 0 failures, 0 errors, 2 skipped
extension: 284 passed, 46 failed, 0 pending
Baseline gate: PASS; 46 exact known failures remain visible and expiry-controlled.
```

46 = CacheService(6) + ConnectionService(11) + IconService(29), exactly matching the 3 remaining exceptions — no complaint about the removed entry.

## Pre-QA gate checklist
- [x] Build/tests: N/A compiled build (JS), test suite itself is the build-equivalent check — passes.
- [x] Task tests exist and pass (12/12, verified order-independent).
- [x] Full extension component suite run — only pre-existing/expected failures remain (46, matching manifest).
- [x] No uncommitted changes — all committed to the task branch.
- [x] Branch cut from latest `main` (`8b6d329`, ASPS-759 merge) — already up to date, no merge/re-test needed.
- [x] Pushed to `origin/asps-755-rewrite-extension-flow-tests`.
- [x] Handoff created (this file).
- [ ] Spec documents — see below (implementer does not edit specs).

## Specification documents that may be affected
None. This is a test-infrastructure-only change (test suite rewrite + baseline-exception manifest update); it does not change any contract, protocol, enum, or documented behavior of the extension. No `docs/system-specifications/`, `docs/architecture/`, or `docs/ASPS_DATA_FLOW.md` content is affected. Flagging per the task-workflow rule for TechWriter/Architect awareness, but expect "no update needed."

## No scope creep
Only touched: the target test file, its baseline-exception entry, and this handoff. Did **not** touch `background.js` or any production service file, and did **not** touch the other 3 quarantined suites (`CacheService.test.js`, `ConnectionService.test.js`, `IconService.test.js`) even though the `chrome.action` gap noted above would affect a similar rewrite of `IconService.test.js` — flagged for a future follow-up, not fixed here.

## Continuation point for next agent (CEO / QA / security)
1. Run QA against this branch (functional review of the 12 rewritten tests vs. the real `background.js` wiring they claim to exercise — the "what real wiring is exercised" section above is the acceptance-criteria mapping).
2. Code review (orchestrator or delegate).
3. Security gate — this is a test-only change touching no production code, secrets, auth, or network surface; a "no security impact" determination is very likely appropriate here, but the security agent should make that call explicitly per `task-workflow.md`.
4. On approval: merge to `main`, delete branch, JIRA ASPS-755 → Done (41).
