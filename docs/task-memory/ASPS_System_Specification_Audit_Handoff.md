# ASPS System Specification Audit — Task Handoff

**Memory status:** Canonical handoff  
**Created:** 2026-07-27  
**Source task:** `019f8a0f-1166-7e60-80e9-7a71386ce18d`  
**Source task state:** Idle; final user message received without an assistant response  
**Repository:** `C:\Jobs\ASPS\GitHub\Software`  
**Primary artifact:** `docs/system-specifications/ASPS_System_Specification.md`

## Purpose

This document preserves the durable knowledge from the Codex task that audited the ASPS specifications against the implementation. It is a retrieval-oriented handoff, not a replacement for the specification, source code, test results, or Git history.

## Trust and Provenance

Use sources in this order:

1. Current source code and configuration in the working tree.
2. `docs/system-specifications/ASPS_System_Specification.md`.
3. Current test and build results.
4. This handoff.
5. Historical statements from the source task.

Statements under **Verified Current State** were checked against the repository on 2026-07-27. Statements under **Historical Audit Results** report what the source task concluded and may become stale as code changes.

The source task was read in full through Codex thread pagination. Tool chatter, intermediate hypotheses, superseded findings, and repeated progress updates were intentionally excluded.

## Original Objective

The task was asked to:

- Read the specifications under `docs/system-specifications/` and `docs/`.
- Inspect all system components under `ASPSBackend14_J/` and `apps/`.
- Include the URL Analyzer and deployment files needed for an end-to-end view.
- Produce an updated system specification.
- Mark every specification requirement as implemented, partially implemented, or not implemented.
- Identify capabilities present in code but missing from the specifications.
- Verify the final document through independent QA, citation checks, builds, and tests.

The audit treated the current working tree, including pre-existing uncommitted changes, as the implementation being assessed.

## Work Completed

The source task completed five phases:

1. Specification and code-source inventory.
2. Backend and WebApi audit.
3. Desktop Agent, Chrome Extension, URL Analyzer, and deployment audit.
4. Rewrite of the canonical system specification with implementation matrices and evidence.
5. Independent content QA, citation validation, build checks, and test runs.

Only `docs/system-specifications/ASPS_System_Specification.md` was intentionally edited by that task. No commit or staging operation was performed.

## Verified Current State

Verified on 2026-07-27:

- `docs/system-specifications/ASPS_System_Specification.md` exists and contains 894 lines.
- The functional-requirement matrix includes FR-001 through FR-022.
- The document includes a remediation section titled `Remaining Architecture Decisions and Remediation Queue`.
- The specification currently has no Git diff.
- Its current H1 title is `ASPS System Specification`.
- The source task's final user request was not applied: the requested document name/title is `ASPS System Specification Gaps`.
- The worktree contains other modified and untracked files. They are outside this handoff and must not be attributed to the specification-audit task without separate evidence.

## Historical Audit Results

The final source-task report classified the 22 functional requirements as:

| Implementation status | Count |
|---|---:|
| Fully implemented end to end | 0 |
| Partially implemented | 13 |
| Not implemented | 9 |

The canonical specification contains the requirement-by-requirement evidence and remains the authoritative location for the matrix.

### Major Implementation Gaps Recorded

- The User Risk Score implementation existed in code and tests, but required production dependency-injection wiring was incomplete.
- `TrackUrlAlert.Duration` was documented in seconds but decoded as milliseconds.
- Simulation paths wrote records directly to the database and bypassed normal analysis, events, and notification flows.
- Notification delivery lacked persistence, acknowledgement, retry, and restart recovery.
- Deleting a tracked domain was not propagated to devices.
- Several asynchronous operations were not awaited or observed.
- OTP interception, Black Screen, targeting, and the Protective Actions Matrix existed as disconnected or unreachable code.
- No active bridge connected the backend notification publisher on port 50002 to SignalR.
- Some REST paths returned a queued result without actually sending the notification.
- The `ProtectiveAction` contract was inconsistent between Backend and Desktop Agent: `SubjectKey` versus `Subject`.
- The deployment definition referenced an Analyzer path that did not exist in the expected build context.

### Documentation Corrections Recorded

The audit corrected or challenged these earlier specification claims:

- The system was not fully operational end to end.
- The active migration count was 27 rather than 51 at audit time.
- A described `NotificationSubscriber` component did not exist.
- The Analyzer could legitimately return a score of `0`.
- `/DebugClaims` and the SignalR Hub were more exposed than previously described.
- FR-017 had no production caller and could not be treated as partially reachable.
- Remote-access detection covered 10 tools rather than 11.
- `TrackUrlAlert`, `TabClosed`, `TabChanged`, and `SetTrackedDomains` required more accurate protocol documentation.

### Security Findings Recorded

The task recorded these security concerns for inclusion in the specification:

- A tracked OIDC secret existed; its value was not reproduced in the task.
- A fallback login path could grant Admin access for a non-empty username while ignoring the password when `ClientId` was absent.
- Some REST controllers and the SignalR Hub lacked consistent authorization.
- Roadmap export had a stored-XSS path.
- Internal CQRS/NetMQ endpoints were exposed without CURVE protection.
- Device tokens were stored in plaintext.

These are historical audit findings, not a substitute for a current security audit.

## Historical Verification Results

The source task reported:

- Independent content QA: PASS.
- Markdown tables: 22 valid.
- Code citations: 263 of 263 resolved successfully.
- FR-001 through FR-022 each appeared exactly once in the matrix.
- `git diff --check`: PASS.
- .NET build: no `CS####` compilation errors were found; output-copy failures occurred because running Visual Studio, Backend, or WebApi processes held DLL locks.
- EF Core package-version conflict warning: 7.0.2 versus 7.0.20.
- .NET tests using existing binaries: 1,351 passed, 39 failed, and 3 skipped.
- Chrome Extension Jest: 99 passed and 140 failed, primarily around ESM configuration and Chrome mocks.
- Docker was unavailable for runtime verification.
- Python and end-to-end tests involving MySQL, Keycloak, Chrome, and remote-access applications were not completed.

These counts describe the historical run and must be rerun before being presented as current results.

## Important Architectural Conclusions

- `User Layer` means backend user-level correlation and risk state, not a Users Portal.
- Risk-score direction and formulas were inconsistent across source documents.
- The implemented administration UI used .NET 8 and Razor Pages, while one source document described .NET 10 and Angular as a target.
- Ports 5555 and 5556 were bound more broadly than some documentation claimed.
- The system boundary includes `Analyzers/` and root deployment files in addition to `ASPSBackend14_J/` and `apps/`.
- Code-only or under-documented candidates included consent and risk profiles, tab lifecycle, remote-session forensics, browser-tab policy, device registration and token refresh, the MV3 durable queue, and the Roadmap SPA.

## Pending Work

1. Resolve the final naming request: change the specification title, filename, or both to `ASPS System Specification Gaps`. The source task did not clarify which form was intended.
2. Rebuild the Knowledge Engine index after this handoff is accepted.
3. Verify retrieval using queries that target the requirement counts, notification reliability gap, and final naming request.
4. Keep future conclusions synchronized with the canonical specification and current code.

## Retrieval Anchors

Useful semantic-search phrases:

- ASPS System Specification Gaps
- specification versus implementation audit
- FR-001 through FR-022 implementation matrix
- 13 partially implemented and 9 not implemented
- NotificationSubscriber SignalR bridge missing
- notification ACK retry persistence restart recovery
- TrackUrlAlert duration seconds milliseconds
- ProtectiveAction SubjectKey Subject contract mismatch
- ASPS code-only undocumented features
- ASPS specification audit historical test results

