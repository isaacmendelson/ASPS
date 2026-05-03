# ASPS Security Audits

Automated CISO-level security audit reports.

## Schedule

Cron job runs daily at 05:00 local time (Israel). It launches 3 parallel sub-agents covering:
- Backend (auth, crypto, DB, deserialization)
- Client agents (browser extension, Python desktop, mobile spec)
- Configuration & secrets (appsettings, deps, git history)

## Output

- **Report file:** `YYYY-MM-DD.md` — full findings with severity, evidence, recommendations.
- **Flag file:** `NEEDS_ATTENTION.md` — written ONLY when new Critical/High findings appear since the previous audit. Deleted automatically when no new issues.

## Folder layout

```
docs/security-audits/
├── README.md                # This file
├── _prompts/                # Sub-agent prompt templates (do not delete)
│   ├── backend.md
│   ├── clients.md
│   └── config.md
├── 2026-05-03.md            # Daily reports
├── 2026-05-04.md
└── NEEDS_ATTENTION.md       # Created/deleted by each run based on findings
```

## Constraints

- Cron auto-expires every 7 days. The job self-renews on each run, but if Claude is offline for >7 days the cron is gone.
- Each run requires Claude REPL to be idle at 05:00.
- Each run consumes ~450K tokens (3 sub-agents × ~150K).

## Manual run

You can also trigger a manual audit by asking Claude:
> "Run the daily security audit now"
