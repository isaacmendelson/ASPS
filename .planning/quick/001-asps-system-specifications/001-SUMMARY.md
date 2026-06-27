---
phase: quick-001
plan: "001"
subsystem: documentation
tags: [specification, architecture, all-subsystems, citation]

dependency-graph:
  requires: []
  provides:
    - unified-asps-system-specification
  affects:
    - all future phases (authoritative reference)

tech-stack:
  added: []
  patterns:
    - citation-backed specification (code path + KE + JIRA)
    - per-subsystem template (Status / Purpose / Components / Tech Stack / Interfaces / Data Flow / Security / Open Items)

key-files:
  created:
    - docs/system-specifications/ASPS_System_Specification.md
  modified: []

decisions:
  - id: D1
    decision: Mobile Agent status = planned (no code exists)
    rationale: No directory in repo; KE returns agents/mobile.md confirming "pre-implementation"; JIRA SCRUM-898/899 are To Do
    date: 2026-06-27
  - id: D2
    decision: Users Portal status = planned (design only)
    rationale: No directory in repo; KE confirms no end-user portal; LIMAT CustomerPortal is a separate project; design doc exists but no implementation
    date: 2026-06-27
  - id: D3
    decision: URL Analyzer is invoked as subprocess, not over HTTP
    rationale: docs/ASPS_DATA_FLOW.md §10 and Business/RealtimeAnalysis/UserDomain/UDUrlAnalyzer.cs confirm ProcessStartInfo pattern; FastAPI mode is standalone/testing only
    date: 2026-06-27

metrics:
  duration: ~32 minutes
  completed: 2026-06-27

---

# Quick 001: ASPS System Specifications — Summary

**One-liner:** Citation-backed unified spec covering all 7 ASPS subsystems (5 built, 2 planned) sourced from repo code, Knowledge Engine, and JIRA.

## What Was Done

Produced a single authoritative specification document at `docs/system-specifications/ASPS_System_Specification.md` covering:

1. **Backend (Host Service)** — built; .NET 8 + EF Core 7 + NetMQ CURVE; 51 EF migrations; full DI wiring verified in `Program.cs`
2. **Admin Portal + REST (WebApi)** — built; Razor Pages + SignalR + Keycloak OIDC; stateless (zero DB access); dev Keycloak at port 8180
3. **Desktop Agent** — built; Python 3.11 + pyzmq + websockets; CURVE auth; adaptive remote-access polling; Velopack update in progress (SCRUM-863)
4. **Mobile Agent** — planned; no directory; wire protocol fully spec'd in `docs/ASPS_DATA_FLOW.md §10`; JIRA SCRUM-898/899 confirm planned
5. **Browser Extension** — built; Chrome MV3 v0.0.1.4; vanilla JS; Shadow DOM anti-tamper overlay; friction mechanism
6. **Users Portal** — planned (design only); design artifacts exist in `docs/system-specifications/`; explicitly not the LIMAT CustomerPortal
7. **URL Analyzer** — built; Python FastAPI + scikit-learn + Playwright; invoked as subprocess by Backend with 30s timeout

## Task Commits

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1+2 | Gather facts + write unified spec | `1699aa5` | `docs/system-specifications/ASPS_System_Specification.md` |
| 3 | Self-check (no changes needed) | *(no commit — doc passed clean)* | — |

## Deviations from Plan

None — plan executed exactly as written. Tasks 1 and 2 were executed together (Task 1 produced in-context notes, Task 2 produced the output file) and committed as a single atomic commit.

## Self-Check: PASSED

- `docs/system-specifications/ASPS_System_Specification.md` — FOUND
- Commit `1699aa5` — FOUND
- 7 `## N.` section headers — confirmed (grep returned 7 matches)
- All 7 sections have full template (Status + 7 headings) — confirmed
- Mobile Agent: explicit `planned` status with 3 independent citations
- Users Portal: explicit `planned` status; LIMAT excluded in 2 places
- At-a-glance table: 7 rows, statuses match section statuses exactly
- All ports match `CLAUDE.md` exactly
