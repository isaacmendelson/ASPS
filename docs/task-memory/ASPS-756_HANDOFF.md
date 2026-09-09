# ASPS-756 — Rewrite quarantined `CacheService.test.js` against the current cache model

**Parent epic:** ASPS-738
**Owner:** browser-extension
**Branch:** `asps-756-rewrite-cacheservice-tests` (cut from `main` @ `bd7a307`, pushed to `origin`)
**Status:** Implementation complete, pre-QA gate satisfied. Awaiting CEO-run QA + code review + security gate + merge (per `.claude/rules/task-workflow.md`). No PR opened per task instructions.

## What was done

Rewrote `apps/extension/chrome/tests/unit/services/CacheService.test.js` from scratch and removed its baseline-exception entry from `scripts/test-baseline-exceptions.json`.

### Root cause of the original quarantine (confirmed by inspection)
The old suite (6 tests) targeted a legacy `CacheService` model that no longer exists:
- a `scanCache` storage key (current: `urlCache`, `config.persistKey`)
- a fixed 24h expiry computed from `result.timestamp` (current: per-entry TTL in seconds, default 3600s, computed from `entry.savedAt` at read time)
- callback-style `chrome.storage.local.get(keys, callback)` (current: `await chrome.storage.local.get([...])`, Promise-based, no callback)
- a `remove()` method (current: `delete()`)
- URL-keyed cache entries (current: domain-keyed via `extractDomain()` → `new URL(url).hostname`)

Every test in the old file called a mock or a method that either didn't exist on the current service or asserted a shape the service never produces, so all 6 failed for real (not a mock-signature nit).

### Current CacheService API the rewritten tests now exercise
(`apps/extension/chrome/services/CacheService.js` — **not modified**, read-only reference)
- `init()` — hydrates `this.cache` (a `Map`) from `chrome.storage.local.get(['urlCache'])`, filtering out any entry whose `savedAt + data.ttl*1000` is already in the past.
- `get(url)` — keyed by `extractDomain(url)` (hostname, or the raw string if `new URL()` throws); returns `{...entry.data, fromCache: true, cachedAt: entry.savedAt}` or `null` if missing/expired (expired entries are evicted from the Map on read).
- `set(url, data)` — stores `{data: {...data, ttl: data.ttl || config.defaultTTL}, savedAt: Date.now()}`; enforces `config.maxEntries` via `evictOldest()` (removes the oldest ~10%, at least 1) before inserting; schedules a debounced persist.
- `has(url)` — `get(url) !== null`.
- `delete(url)` — removes the domain entry, returns `true`/`false`; schedules persist on success.
- `clear()` — empties the Map, calls `chrome.storage.local.remove(['urlCache'])`, and calls `stateManager.update({'cache.size': 0, 'cache.lastCleared': Date.now()})`.
- `schedulePersist()` / `persist()` — 500ms debounce; `persist()` writes `{urlCache: {<domain>: entry, ...}}` to `chrome.storage.local.set`; errors are caught and logged, never thrown.
- `getStats()` — `{entries, expiringSoon (<5min remaining), totalSize, maxEntries}`.

### Test bootstrap pattern
Matches the currently-GREEN `ScanService.test.js` rewrite (ASPS-753): `StateManager` is mocked via `jest.unstable_mockModule('../../../state/StateManager.js', ...)` (its own real singleton also debounce-persists to `chrome.storage.local` under a different key — `appState` — which would otherwise pollute assertions on `CacheService`'s own persistence), then `cacheService` is imported once at module scope (top-level `await import`) since it's a true singleton. Per-test isolation is via `cacheService.cache.clear()` + config reset + `jest.clearAllMocks()` in `beforeEach`, not via re-import (re-importing an ESM singleton returns the same cached instance anyway).

Per-entry TTL and the 500ms debounced persist are tested with `jest.useFakeTimers()` / `jest.advanceTimersByTime()` / `jest.advanceTimersByTimeAsync()` (the latter needed because `persist()` is `async` and awaits the mocked `chrome.storage.local.set`).

### Test list (21, all against the current model)
- **Domain-keyed storage (3):** round-trip via `set()`/`get()` across two different paths on the same domain; `null` for an uncached domain; malformed-URL fallback to the raw string as the cache key (`extractDomain`'s catch branch).
- **Per-entry TTL expiry (4):** custom per-entry TTL respected (fake-timer advance just under/over the boundary); expired entry evicted from the Map on access (`size()` drops); default TTL (3600s) used when no per-entry TTL given; `has()` reflects expiry.
- **delete() (2):** removes an existing entry (`true` + gone from `get()`/`size()`); `false` for a non-existent domain.
- **clear() (2):** empties cache + calls `chrome.storage.local.remove(['urlCache'])`; calls `stateManager.update` with `cache.size: 0` and a `cache.lastCleared` timestamp.
- **Debounced persist to `urlCache` (4):** no synchronous persist on `set()`; a single `set()` persists once after 500ms with the correct `{urlCache: {<domain>: {data, savedAt}}}` shape; rapid successive `set()` calls coalesce into exactly one persist; a `set()` after the debounce window elapses triggers a second, independent persist.
- **init() hydrate (3):** loads non-expired entries from `urlCache`; drops already-expired entries found in storage instead of loading them; handles an absent/empty `urlCache` key without throwing.
- **Max-entries eviction (1):** with `config.maxEntries = 3`, a 4th `set()` evicts the oldest domain and caps size at 3.
- **getStats() (1):** entry count, `expiringSoon` count (TTL < 5min remaining), and `maxEntries` reflect real state.
- **Error handling (1):** a rejected `chrome.storage.local.set` inside the debounced `persist()` does not throw out of `set()` or leave an unhandled rejection (matches `persist()`'s own try/catch contract).

No tautological assertions (no bare `mock.calls.length > 0` checks); every assertion is against either the return value of a real method call or the real shape of a `chrome.storage.local.set` call.

### Not testable / out of scope
- `cleanup()` (proactive expired-entry sweep, not called from any other tested path) and `evictOldest()`'s exact 10%-removal math beyond the 4-entries-at-maxEntries-3 case were not separately exercised — the max-entries eviction test already covers the eviction contract that matters (oldest removed, cap respected) without re-deriving the `Math.floor(n*0.1)` formula test-by-test. Flagging as a possible small follow-up, not a gap in the required rewrite scope.
- No production code was touched or needed to be — the service's real behavior was fully testable as-is.

## Files changed
- `apps/extension/chrome/tests/unit/services/CacheService.test.js` — full rewrite (6 → 21 tests, all against the current model)
- `scripts/test-baseline-exceptions.json` — removed the `unit/services/CacheService.test.js` exception entry (6 expected failures, SHA `6bbb2f33...`). The other two entries (`unit/services/ConnectionService.test.js` — 11, `unit/services/IconService.test.js` — 29) are untouched. `extension-flow.test.js`'s exception was already removed on `main` by ASPS-755 prior to this branch.

## Verification (pre-QA gate)

Exact commands run (from `apps/extension/chrome/tests`):

```bash
node --experimental-vm-modules ./node_modules/jest/bin/jest.js unit/services/CacheService.test.js
# → Test Suites: 1 passed, 1 total; Tests: 21 passed, 21 total

node --experimental-vm-modules ./node_modules/jest/bin/jest.js unit/services/CacheService.test.js --randomize
# → same 21/21 pass in randomized order (confirms no cross-test ordering coupling)

npm test -- --runInBand --silent --json --outputFile="<repoRoot>/artifacts/asps626/extension.json"
# → full extension suite: Test Suites: 2 failed, 15 passed, 17 total
#   Tests: 40 failed, 304 passed, 344 total
#   (the 2 failing suites are exactly the pre-existing quarantined
#    ConnectionService/IconService suites — unchanged by this task)
```

Baseline gate (`scripts/check_test_baseline.py`), reusing the repo's existing `artifacts/asps626/{dotnet.trx,analyzer.xml,desktop.xml}` (unrelated components, not touched by this task) alongside the freshly-regenerated `extension.json`:

```bash
py -3.11 scripts/check_test_baseline.py \
  --manifest scripts/test-baseline-exceptions.json \
  --dotnet artifacts/asps626/dotnet.trx \
  --analyzer artifacts/asps626/analyzer.xml \
  --desktop artifacts/asps626/desktop.xml \
  --extension artifacts/asps626/extension.json
```

**Output (after removing the manifest entry):**
```
dotnet: 1470 passed, 0 failed, 0 not executed
analyzer: 348 tests, 0 failures, 0 errors, 5 skipped
desktop: 247 tests, 0 failures, 0 errors, 2 skipped
extension: 304 passed, 40 failed, 0 pending
Baseline gate: PASS; 40 exact known failures remain visible and expiry-controlled.
```

40 = ConnectionService(11) + IconService(29), exactly matching the 2 remaining exceptions — no complaint about the removed CacheService entry.

## Pre-QA gate checklist
- [x] Task tests exist and pass (21/21, verified order-independent via `--randomize`).
- [x] Full extension component suite run — only pre-existing/expected failures remain (40, matching manifest).
- [x] No uncommitted changes — all committed to the task branch.
- [x] Branch cut from latest `main` (`bd7a307`, ASPS-755 merge) — already up to date at cut time.
- [ ] Merge latest `main` + re-test + push — to be done after this handoff is written (see below).
- [x] Handoff created (this file).
- [ ] Spec documents — see below (implementer does not edit specs).

## Specification documents that may be affected
None. This is a test-infrastructure-only change (test suite rewrite + baseline-exception manifest update); it does not change any contract, protocol, enum, or documented behavior of the extension. No `docs/system-specifications/`, `docs/architecture/`, or `docs/ASPS_DATA_FLOW.md` content is affected. Flagging per the task-workflow rule for TechWriter/Architect awareness — "no update needed" is the expected determination.

## No scope creep
Only touched: the target test file, its baseline-exception entry, and this handoff. Did **not** touch `services/CacheService.js` or any other production file, and did **not** touch the two remaining quarantined suites (`ConnectionService.test.js`, `IconService.test.js`).

## Continuation point for next agent (CEO / QA / security)
1. Run QA against this branch (functional review of the 21 rewritten tests vs. the current `CacheService.js` API they claim to exercise — the "Current CacheService API" and "Test list" sections above are the acceptance-criteria mapping).
2. Code review (orchestrator or delegate).
3. Security gate — this is a test-only change touching no production code, secrets, auth, or network surface; a "no security impact" determination is very likely appropriate here, but the security agent should make that call explicitly per `task-workflow.md`.
4. On approval: merge to `main`, delete branch, JIRA ASPS-756 → Done (41).
