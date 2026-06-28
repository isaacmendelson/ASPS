---
name: security-audit-now
description: Run the daily ASPS security audit on-demand. Spawns 3 parallel general-purpose sub-agents using the existing prompt files and synthesizes a CISO-style Markdown report. Safe to invoke before releases or after major changes.
---

# /security-audit-now

Manual trigger for the daily security audit flow. The same code path that runs at 05:00 every day via cron, just invoked now instead of waiting. Use before a release or after a major change that you'd like reviewed independently before the next scheduled run.

## When to invoke
- User explicitly asks "run the security audit now" / "audit before we ship".
- A change just landed in a security-sensitive path (auth, crypto, secrets, deserialization, network endpoints) and the user wants confirmation before committing or releasing.
- Before merging to `main` for a significant release.

## When to NOT invoke
- Trivial changes the QA gate already covered.
- Right after the cron just ran (waste of tokens). Check the latest report date first.

## Cost awareness

Each run consumes ~450K tokens (3 parallel sub-agents × ~150K each). Surface this to the user before kicking off:

> "This will spawn 3 parallel general-purpose agents (~450K tokens, ~$5–15). Proceed? [y/N]"

If the answer isn't yes, don't run.

## Steps

### 1. Pre-flight

Check the existing audit cadence:

```bash
ls /c/Jobs/ASPS/GitHub/Software/docs/security-audits/*.md | tail -5
```

If the most recent dated report is from today, ask the user whether they want to re-run anyway (could be intentional after a fix).

Confirm the three prompt files still exist:

```bash
ls /c/Jobs/ASPS/GitHub/Software/docs/security-audits/_prompts/
# expect: backend.md  clients.md  config.md
```

If any is missing, **stop** — the audit can't run without them. Surface the gap.

### 2. Spawn the sub-agents — in parallel

Use the `Agent` tool with `subagent_type: "general-purpose"` for each of the three scopes. Send all three calls in one assistant message so they run concurrently. Each prompt should be the contents of the matching `_prompts/<scope>.md` file, plus instructions to return a structured Markdown findings list with severity (Critical / High / Medium / Low / Info) + evidence (file:line) + recommendation per finding.

Three scopes:
- **Backend** — `_prompts/backend.md` — auth, crypto, DB, deserialization, NetMQ.
- **Clients** — `_prompts/clients.md` — browser extension, Python desktop agent, mobile spec.
- **Config** — `_prompts/config.md` — appsettings, .env, dependencies, git history.

### 3. Synthesize

After all three agents finish, write a single report:

```
docs/security-audits/YYYY-MM-DD.md
```

Use today's date in Israel local time (the audit's convention). If a file with that date already exists from the cron run, append `-manual` (`2026-06-16-manual.md`).

Report structure:

```markdown
# ASPS Security Audit — YYYY-MM-DD (manual)

**Triggered by:** Isaac (manual via /security-audit-now)
**Reason:** <user-stated reason for the manual run>
**Scope:** Backend + Clients + Config

## Summary
- N Critical findings (X new since last audit)
- N High
- N Medium
- N Low

## Critical
### Finding — <one-line title>
- **Scope:** backend | clients | config
- **Evidence:** `file:line` + excerpt
- **Recommendation:** <one paragraph>

## High
... (same shape)

## Medium / Low / Info
... (same shape)

## New since previous audit (docs/security-audits/<prev-date>.md)
- <list>
```

### 4. Update NEEDS_ATTENTION

If any new Critical or High finding appeared that wasn't in the previous audit:

- Write `docs/security-audits/NEEDS_ATTENTION.md` summarizing the new findings.
- Surface a short summary in chat ("⚠️ 2 new High findings — see NEEDS_ATTENTION.md").

If no new Critical/High, delete `NEEDS_ATTENTION.md` if it exists, and reply in chat: "audit clean — N total findings, none new since <prev-date>."

### 5. Don't commit the report

The cron job commits its daily reports; manual runs should leave that decision to the user. Surface the new file path + a 5-line summary in chat and let the user decide whether to commit.

## Trust-but-verify

Per `operating_principles.md`: sub-agent findings describe what they *think* they found. For any Critical / High finding before relaying:

1. Open the cited `file:line`.
2. Confirm the code at that location actually shows the issue described.
3. If a finding doesn't match the cited evidence, downgrade severity and add a note: "Sub-agent claim not confirmed on re-read; needs follow-up."

This step is non-negotiable. The cron run does this implicitly; the manual run should too.

## Never

- Run without confirming the token cost with the user. Surprise $15 charges erode trust.
- Run the audit and commit the report in one step. The user owns the commit decision.
- Skip a sub-agent because you "think you know what it'll find". Parallel coverage is the whole point.
- Trust sub-agent findings without re-reading cited evidence for Critical/High items.

## Output convention

```
Run: manual (/security-audit-now)
Triggered by: Isaac
Reason: <user-stated>
Scope: Backend + Clients + Config
Sub-agents: 3 spawned, 3 returned (failures: 0)
Token cost: ~<N>K

Findings:
  Critical: N (X new)
  High:     N (X new)
  Medium:   N
  Low:      N

Report: docs/security-audits/YYYY-MM-DD[-manual].md
NEEDS_ATTENTION: created | unchanged | deleted

Trust-but-verify: <N> Critical/High items re-read against cited evidence
```
