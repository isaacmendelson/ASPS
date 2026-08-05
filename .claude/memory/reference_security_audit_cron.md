---
name: ASPS Daily Security Audit Cron
description: Schedule and conventions for the daily 05:00 CISO-level security audit run via CronCreate.
type: reference
originSessionId: 79421ab0-5013-4f83-811d-ef1680a5ec72
---
A cron job runs every day at **05:00 local time (Israel)** to perform a CISO-level security audit of the ASPS codebase.

**Scope:** Backend (.NET), WebApi, Browser extension, Python desktop agent, Mobile spec, Configuration & secrets, Git history, Dependencies.

**Mechanism:** The cron prompt instructs Claude to spawn 3 parallel `general-purpose` sub-agents using the prompts saved in:
- `c:\Jobs\ASPS\GitHub\Software\docs\security-audits\_prompts\backend.md`
- `c:\Jobs\ASPS\GitHub\Software\docs\security-audits\_prompts\clients.md`
- `c:\Jobs\ASPS\GitHub\Software\docs\security-audits\_prompts\config.md`

After the agents finish, Claude synthesizes a CISO-style Markdown report at `c:\Jobs\ASPS\GitHub\Software\docs\security-audits\YYYY-MM-DD.md`.

If any new Critical or High findings appeared since the previous audit, Claude also writes a flag file at `docs/security-audits/NEEDS_ATTENTION.md` and surfaces a short summary on screen. Otherwise the flag file is deleted (or left if it already showed clean) and the chat reply just says "audit clean".

**Constraints to remember:**
- CronCreate jobs auto-expire after **7 days**. The cron prompt should call CronCreate again on each run to renew (self-renewing pattern), or the user has to remind weekly.
- Cron only fires while Claude REPL is **idle** (not mid-query). If the machine is off at 05:00 or Claude isn't running, the run is skipped.
- Each run consumes ~450K tokens (3 sub-agents × ~150K). Daily cost ~$5–15.

**Manual trigger:** The user can ask "Run the daily security audit now" — re-use the same flow.

**First run committed:** 2026-05-03 baseline report.
