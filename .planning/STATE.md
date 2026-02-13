# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-13)

**Core value:** Full score flow must work: URL -> analysis -> score displayed in Chrome Extension
**Current focus:** Milestone v1.1 — Cleanup & Fix Communication

## Current Position

Phase: Phase 7 - Fix Communication Bugs (IN PROGRESS)
Plan: 1/2 complete
Status: 07-01 complete, ready for 07-02
Last activity: 2026-02-13 — 07-01 Desktop Python Bugfixes executed

Progress: [████████░░] 1.5/2 phases (75%)

## Performance Metrics

**Previous Milestone (v1.0):**
- Total plans completed: 10
- Total execution time: ~32 min
- All 5 phases complete

**Current Milestone (v1.1):**
- Plans completed: 2
- Phases complete: 1.5/2
- Coverage: 11/11 requirements mapped (100%)

| Phase | Plan | Duration | Tasks | Files |
|-------|------|----------|-------|-------|
| 06    | 01   | 4min     | 3     | 1     |
| 07    | 01   | 3min     | 2     | 2     |

## Accumulated Context

### Decisions

- [v1.0 Roadmap]: Repair proceeds link-by-link (diagnose -> bridge fix -> end-to-end -> security -> reliability)
- [v1.0 Phase 2]: asyncio.run() replaced with run_coroutine_threadsafe() (PRIMARY BUG FIX)
- [v1.0 Phase 3]: RegisterDevice/RequestToken two-step flow
- [v1.0 Phase 4]: CurveMQ re-enabled with ephemeral keypairs
- [v1.0 Phase 5]: Lazy Pirate retry, PendingResults store, SW keepalive
- [v1.1]: Focus on garbage cleanup + communication bug fixes only
- [v1.1]: No port changes, no tech debt, no password fixes, no restructuring
- [v1.1 Roadmap]: 2 phases (Cleanup, Communication Bugs) — both independent, can run in parallel
- [v1.1 Phase 6]: Preserved aspsbackend2db_20260130.sql (intentional DB dump, not bloat)
- [v1.1 Phase 6]: No *.sql in .gitignore -- database dumps may be intentional
- [v1.1 Phase 6]: No git history rewriting needed -- all bloat was untracked files
- [v1.1 Phase 7]: url passed as first positional param to _create_result/_create_error for consistency
- [v1.1 Phase 7]: Only url_check handler wrapped in executor -- other handlers are pure dict ops with no I/O

### Code Review Findings (2026-02-13)

Bugs preventing end-to-end flow:
1. ~~Missing `url` field in scan_service.py responses → Extension can't match results~~ FIXED (07-01, COMM-05)
2. ~~Async/sync mismatch in extension_handler.py → blocks event loop~~ FIXED (07-01, COMM-06)
3. Message type mismatch in content.js → page info not collected
4. Silent broadcast failure in notification_handler.py → results lost

### Pending Todos

- Runtime verification of full pipeline (requires starting Backend + Desktop App)
- ~~Delete ~1.8GB bloat from repository~~ DONE (2.06GB freed in 06-01)

### Blockers/Concerns

- [Research]: Extension cache (1-hour TTL) can mask broken pipeline during testing — use fresh URLs
- [Phase 3]: Default email user@example.com must match active Backend user
- [Phase 4]: CURVE mismatch symptoms are silent timeouts, not error messages

## Session Continuity

Last session: 2026-02-13
Stopped at: Completed 07-01-PLAN.md (Desktop Python Bugfixes)
Next step: Execute 07-02-PLAN.md (Extension & Notification Bugfixes)
Resume file: .planning/phases/07-fix-communication-bugs/07-01-SUMMARY.md
