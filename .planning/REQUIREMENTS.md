# Requirements: ASPS v1.1 — Cleanup & Fix Communication

**Defined:** 2026-02-13
**Core Value:** Full score flow must work: URL -> analysis -> score displayed in Chrome Extension

## v1.1 Requirements

### Cleanup

- [ ] **CLEAN-01**: Delete all duplicate ZIP files from repository (apps.zip x3, basic-url-analyzer ZIPs x4)
- [ ] **CLEAN-02**: Delete all __pycache__ directories and .pyc files from repository
- [ ] **CLEAN-03**: Delete duplicate virtual environments (keep .venv, delete venv)
- [ ] **CLEAN-04**: Delete .NET build artifacts (bin/, obj/, .vs/, WebApi/publish/)
- [ ] **CLEAN-05**: Delete temp files and one-off scripts from root (nul, test_result.*, PowerShell scripts, update_emails.sql)
- [ ] **CLEAN-06**: Add comprehensive .gitignore to prevent future bloat

### Communication Bug Fixes

- [ ] **COMM-05**: scan_service.py _create_result() includes `url` field in all responses to Extension
- [ ] **COMM-06**: extension_handler.py handlers are properly async or called correctly from async context
- [ ] **COMM-07**: content.js message type constants match ScanService.js (page:info:request)
- [ ] **COMM-08**: notification_handler.py broadcast has retry logic and proper error handling (not silent failure)

### Verification

- [ ] **VERIFY-01**: End-to-end flow verified: Extension sends url_check -> Desktop forwards to Backend -> result returns to Extension with correct url field

## v1.0 Requirements (Completed)

All 14 requirements from v1.0 milestone completed. See git history for details.

## Out of Scope

| Feature | Reason |
|---------|--------|
| Port changes | Cannot change existing ports |
| Tech debt fixes | Not our responsibility |
| Password/security | Not relevant now |
| Directory restructuring | Only delete garbage, don't move things |
| New features | Fix existing first |
| UI changes | Keep current interface |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| CLEAN-01 | — | Pending |
| CLEAN-02 | — | Pending |
| CLEAN-03 | — | Pending |
| CLEAN-04 | — | Pending |
| CLEAN-05 | — | Pending |
| CLEAN-06 | — | Pending |
| COMM-05 | — | Pending |
| COMM-06 | — | Pending |
| COMM-07 | — | Pending |
| COMM-08 | — | Pending |
| VERIFY-01 | — | Pending |

**Coverage:**
- v1.1 requirements: 11 total
- Mapped to phases: 0
- Unmapped: 11

---
*Requirements defined: 2026-02-13*
*Last updated: 2026-02-13 after initial definition*
