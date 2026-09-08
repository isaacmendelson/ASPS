# ASPS-750 — Tab-switch (onActivated) leaves stale score in stateManager

**Status:** Ready for QA (branch pushed, pre-QA gate complete)
**Component:** apps/extension/chrome (browser-extension)
**JIRA:** https://isaacmendelsonjira.atlassian.net/browse/ASPS-750 (In Progress → will move to In Review after PR opens)
**Branch:** `ASPS-750-tab-switch-stale-score`

## Bug

Reverse split-brain of ASPS-734: `chrome.tabs.onActivated` (`background.js`)
wrote the activated tab's score/riskType/action to `chrome.storage.local`
(`tab_*`, `currentPage*` keys) but never touched `stateManager`.
`buildStatusResponse()` (read by the popup's `StatusService`) pulls from
`stateManager`, not storage. `popup.js` `triggerReconnectIfNeeded()` re-runs
`StatusService.update()` ~1s after popup open whenever the desktop agent is
disconnected — that re-run served the **previous** tab's stale `stateManager`
score, overwriting the correct storage-derived value `PageInfoService.update()`
had just set.

## Fix

`apps/extension/chrome/background.js` — `chrome.tabs.onActivated` handler
(~lines 751-800): all three branches (per-tab stored score, cache hit, no
data) now also call `stateManager.update({...})` for `scan.score` /
`scan.riskType` / `scan.protectiveAction`, mirroring what `startLoadingState()`
already does for the navigation-direction bug (ASPS-734).

## Files

- `apps/extension/chrome/background.js` — `chrome.tabs.onActivated` listener
- `apps/extension/chrome/tests/unit/background/TabActivation.test.js` — new
  regression test (verbatim-extraction pattern, matching
  `LoadingState.test.js` / `TabStateReporting.test.js` conventions)

## TDD evidence

- **Red:** extracted the pre-fix `onActivated` body verbatim into the test
  file and asserted `stateManager.update` is called with the activated tab's
  score on all three branches — failed against the original code (never
  called `stateManager.update`).
- **Green:** applied the fix in `background.js`, updated the test's
  extraction to match, all 4 new tests pass.
- **Full suite:** `npm test` in `apps/extension/chrome/tests` —
  **265 passed, 55 failed, 320 total**. The 55 failures are 4 pre-existing
  suites (`ConnectionService.test.js`, `IconService.test.js`,
  `CacheService.test.js`, `integration/extension-flow.test.js`) — verified
  identical failure count on `main` before this change (same 55/4), so
  confirmed pre-existing and not introduced/worsened by this task.
  `unit/background/*` (the affected area): 78/78 passed.

## Pre-QA gate status

- [x] Build/tests run — task tests pass, full suite failures are pre-existing (documented above)
- [x] No uncommitted changes — committed to branch
- [x] Branch even with `origin/main` (0 ahead/0 behind at fetch time) — no merge needed
- [x] Pushed to remote
- [ ] Spec documents — reviewed: no system-specification/architecture doc describes this per-tab caching behavior at a level this fix would affect; no spec update needed (implementation-detail bug fix, not a contract/interface change)

## Process note (why this ticket needed a fresh start)

JIRA showed ASPS-750 as "In Progress" but there was no branch, no commits, no
PR, and no handoff file when this session resumed the task — a prior attempt
left no artifacts. Restarted from scratch on 2026-09-08.

## Unrelated infra issues found and filed separately during this task

While working over the Telegram CEO bridge, discovered and filed (not part of
this branch):
- **[ASPS-760](https://isaacmendelsonjira.atlassian.net/browse/ASPS-760)** — Telegram approval flow gives no visual confirmation after Approve/Deny tap.
- **[ASPS-761](https://isaacmendelsonjira.atlassian.net/browse/ASPS-761)** — `telegram-ceo` spawned duplicate concurrent `claude --resume` processes for the same session (observed live, causing a real file-write race on this very branch — stale process manually killed as immediate mitigation).

## Continuation point

Branch pushed. Next: open PR (reviewer = CEO), transition JIRA ASPS-750 to
**In Review** (transition 31), then orchestrator code review + mandatory
security gate before merge.
