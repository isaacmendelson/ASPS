# Roadmap: ASPS v1.1 — Cleanup & Fix Communication

**Milestone:** v1.1 Cleanup & Fix Communication
**Created:** 2026-02-13
**Depth:** comprehensive
**Phases:** 2 (continuing from v1.0's Phase 5)

## Overview

This milestone cleans repository bloat and fixes critical communication bugs blocking the end-to-end score flow. Phase 6 removes ~1.8GB of garbage files. Phase 7 fixes 4 specific bugs found in code review that prevent Extension from receiving/matching scan results.

## Phases

### Phase 6: Repository Cleanup

**Goal:** Repository contains only necessary files with proper .gitignore protection

**Dependencies:** None (can start immediately)

**Plans:** 1 plan

Plans:
- [x] 06-01-PLAN.md — Delete duplicate ZIPs, build artifacts, Python caches, duplicate venvs, temp files; create comprehensive .gitignore

**Requirements:**
- CLEAN-01: Delete all duplicate ZIP files from repository (apps.zip x3, basic-url-analyzer ZIPs x4)
- CLEAN-02: Delete all __pycache__ directories and .pyc files from repository
- CLEAN-03: Delete duplicate virtual environments (keep .venv, delete venv)
- CLEAN-04: Delete .NET build artifacts (bin/, obj/, .vs/, WebApi/publish/)
- CLEAN-05: Delete temp files and one-off scripts from root (nul, test_result.*, PowerShell scripts, update_emails.sql)
- CLEAN-06: Add comprehensive .gitignore to prevent future bloat

**Success Criteria:**
1. Repository size reduced by at least 1.5GB (verified with git du)
2. All duplicate ZIPs, virtual environments, and build artifacts deleted
3. Comprehensive .gitignore in place covering Python, .NET, Node.js, and IDE artifacts
4. No __pycache__ or .pyc files remain in working tree
5. Clean git status shows only intentional untracked files

**Status:** Complete (2026-02-13) — 2.06GB freed, 6/6 must-haves verified

---

### Phase 7: Fix Communication Bugs

**Goal:** Extension receives scan results with correct url field and can match them to pending requests

**Dependencies:** None (independent of cleanup)

**Plans:** 2 plans

Plans:
- [ ] 07-01-PLAN.md — Fix missing url field in scan_service.py responses + offload url_check to thread pool executor
- [ ] 07-02-PLAN.md — Align content.js message type constant with MessageTypes.js + add broadcast retry to notification_handler.py + end-to-end verification

**Requirements:**
- COMM-05: scan_service.py _create_result() includes `url` field in all responses to Extension
- COMM-06: extension_handler.py handlers are properly async or called correctly from async context
- COMM-07: content.js message type constants match ScanService.js (page:info:request)
- COMM-08: notification_handler.py broadcast has retry logic and proper error handling (not silent failure)
- VERIFY-01: End-to-end flow verified: Extension sends url_check -> Desktop forwards to Backend -> result returns to Extension with correct url field

**Success Criteria:**
1. scan_service.py response includes url field in all code paths (success, error, cached)
2. extension_handler.py handlers execute without blocking asyncio event loop
3. content.js message listener responds to 'page:info:request' from ScanService.js
4. notification_handler.py logs broadcast failures and retries at least once before giving up
5. End-to-end test passes: Extension url_check -> Backend analysis -> result with correct url returns to Extension UI

**Status:** Planned

---

## Progress

| Phase | Status | Requirements | Success Criteria | Notes |
|-------|--------|--------------|------------------|-------|
| 6 - Repository Cleanup | **Complete** | 6 | 5 | 2.06GB freed (2026-02-13) |
| 7 - Fix Communication Bugs | Planned | 5 | 5 | 2 plans, includes E2E verification |

**Total Requirements:** 11/11 mapped (100% coverage)

**Phase Completion:**
- v1.0: Phases 1-5 complete
- v1.1: Phase 6 complete, Phase 7 planned

---

## Dependency Graph

```
Phase 6 (Cleanup) ──┐
                    ├──> Both independent, can run in parallel
Phase 7 (Comm Bugs) ┘
```

Both phases are independent. Cleanup is pure file deletion. Communication fixes are targeted code changes. They can be executed in any order or in parallel.

---

## Notes

**Starting phase number:** 6 (continuing from v1.0's 5 phases)

**Lean structure rationale:**
- Cleanup is naturally one atomic operation (file deletion + .gitignore)
- Communication fixes are 4 related bugs + verification, grouped for efficiency
- No artificial splitting needed — both phases deliver coherent capabilities

**Verification strategy:**
- Phase 6: Verify with git du, git status, manual inspection
- Phase 7: End-to-end test with fresh URL (avoid Extension cache masking)

---

*Last updated: 2026-02-13 after phase 7 planning*
