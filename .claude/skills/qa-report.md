---
name: qa-report
description: Format a uniform QA report (PASS or FAIL) for the change just verified. The artifact attaches to PR / commit message / JIRA comment for an audit trail.
---

# /qa-report

Generates a uniform QA report after `/qa-gate` has run. The report is the durable artifact: it sits in the JIRA ticket, the PR description, and (for large changes) on disk. The format keeps the audit trail consistent across releases so a future reader can tell what was verified, by whom, and against what acceptance criteria.

## When to invoke
- Directly after `/qa-gate` produces a result.
- User says "format the QA report", "write QA notes for the ticket", "PR comment for QA".

## Ask first (most can be auto-filled from /qa-gate context)

1. **Result** — PASS or FAIL.
2. **Scope** — files changed (auto-fill from `git diff --stat HEAD`).
3. **Acceptance criteria** — restate from `/qa-gate` for the audit trail.
4. **Evidence** — what was actually checked (build output line, test count, manual smoke test result).
5. **Destination** — JIRA comment, PR description, on-disk doc, or all three? Default: paste-ready Markdown for JIRA / PR.

## Template — PASS

```markdown
# QA Report — <Branch>

**Result:** PASS
**Date:** YYYY-MM-DD
**Verifier:** Claude (qa agent) via Isaac
**Change:** SCRUM-NNN — <one-line summary>

## Scope

- N files modified, M created
  - `path/to/file1.cs`
  - `path/to/file2.cs`
  - ...
- Base commit: `<short-sha>` (`<branch> @ HEAD~N`)

## Acceptance criteria

1. ✅ <Criterion 1> — <how it was verified>
2. ✅ <Criterion 2> — <how it was verified>
3. ✅ <Criterion 3> — <how it was verified>

## Evidence

- **Build:** `dotnet build ASPSBackend.sln -c Debug --nologo` → 0 errors (`MSB3027` file lock on `ASPSBackend.dll` — compilation succeeded, only copy failed; backend was running).
- **Tests:** `dotnet test ASPS.Tests` → <N> passed, 0 failed.
- **Manual smoke:** <what was clicked / called / observed>.

## Notes

Anything QA noticed in passing that's worth a follow-up but didn't block PASS:
- <observation>
- <observation>

## Safe to commit
```

## Template — FAIL

```markdown
# QA Report — <Branch>

**Result:** FAIL
**Date:** YYYY-MM-DD
**Verifier:** Claude (qa agent) via Isaac
**Change:** SCRUM-NNN — <one-line summary>

## Scope

(same as PASS template)

## Acceptance criteria

1. ❌ <Criterion 1> — FAILED. <what failed>
2. ✅ <Criterion 2> — passed.
3. ⏸️ <Criterion 3> — not verified (blocked by failure on #1).

## Findings

### Finding 1 — <one-line title>
**Severity:** Critical | High | Medium | Low
**Evidence:** `<file:line>` or command output excerpt.
**Recommended fix:** <one sentence>.

### Finding 2 — <one-line title>
...

## Notes

Anything else QA noticed that isn't blocking but should be tracked.

## NOT safe to commit. Recommended next step: <fix and re-run /qa-gate>
```

## Severity definitions

For consistency in FAIL reports:

| Level | Meaning |
|---|---|
| **Critical** | Breaks the build, breaks an existing test, introduces a security vulnerability, or loses data. Must fix before commit. |
| **High** | Wrong behavior under a realistic scenario, missing required step (e.g. no CQRSGateway routing for a new handler), silently swallowed error. Must fix before commit. |
| **Medium** | Works but is fragile or violates a project convention (`operating_principles.md`). Should fix before commit; if deferred, file a follow-up. |
| **Low** | Style, naming, comments. Can defer; mention in Notes. |

## Output convention

When done, present the report in three forms (let the user pick):

1. **Markdown block** — paste-ready for JIRA / PR.
2. **One-line summary** — for chat: `QA PASS — SCRUM-NNN, <N> files, build clean, tests N/N. Safe to commit.`
3. **On-disk artifact** (optional) — for large changes: `docs/qa-reports/SCRUM-NNN-YYYY-MM-DD.md`.

If destination is JIRA / PR: just hand back the Markdown. If user asks for on-disk, write it; otherwise don't create the file.

## Never

- Mark PASS without checking the build output. "Build looked fine" is not evidence.
- Use vague severity ("might be a problem"). Pick a level or downgrade to Notes.
- Skip Acceptance criteria. If the criteria weren't restated from `/qa-gate`, the report has no anchor and the audit trail is broken.
- Report on a change that hasn't been verified. This is the *report* step — `/qa-gate` is the *verification* step. Don't conflate.
