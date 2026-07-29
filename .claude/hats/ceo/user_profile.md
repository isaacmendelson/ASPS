# User Profile — Isaac

Captured from sessions. Append-only as I learn more.

## Identity
- **Name:** Isaac (יצחק)
- **Email:** isaacmendelson@gmail.com
- **Role:** Founder / CEO of ASPS (Anti-Scam Protection System)
- **Time zone:** Israel (UTC+2 winter / UTC+3 summer)
- **Language:** Hebrew primary; comfortable in technical English

## Background & expertise
- Deep technical hands-on developer
- Strong .NET / C# / EF Core / SQL
- Comfortable with Python, Docker, networking concepts
- Has worked with NetMQ, Keycloak, OAuth, JWT
- Reads error stacks carefully and notices issues quickly
- Quickly spots subtle issues in code (e.g., spotted `scamInProgressKey = scamInProgressKey;` typo)

## Communication preferences
- **Hebrew, terse.** Often single sentences ("מאשר.", "כן.", "תמשיך.")
- Doesn't tolerate preambles, "אני אעשה...", or status-narration
- Uses imperatives directly: "תקרא X", "תיצור Y", "תריץ Z"
- Approves with "מאשר" / "כן" / "תמשיך"
- Sometimes types fast and abbreviates (e.g., "bfui" = "בפועל" mistyped on English layout)
- When asking conceptual questions, expects a real answer with tradeoffs — not deflection

## Decision style
- Mode B explicitly requested: **stop after each phase to wait for user approval**
- Gives clear directives, expects me to execute
- When unsure, names approaches and asks me to recommend
- Takes my recommendations seriously when I cite tradeoffs
- Catches over-promises and under-deliveries

## Known projects / tools / accounts
- **Repo:** `c:\Jobs\ASPS\GitHub\Software\` (Windows host)
- **GitHub:** `github.com/yehudaz136/asps` (or similar — owner `yehudaz136`)
- **JIRA OLD (legacy on-prem):** http://187.124.10.197:8080/ — username `isaac`
- **JIRA NEW (Atlassian Cloud):** https://aspsjira.atlassian.net — same email
- **Keycloak:** https://auth.asps.io
- **Production domain:** https://asps.io (Cloudflare Access protected — I don't have access)
- **Admin domain:** https://admin.asps.io

## Tone do's
- Direct: "התיקון:", "הבעיה:", "מצאתי:"
- Tables and bullets > paragraphs
- Markdown links `[file](path#L42)` for code refs (NOT bare backticks)
- Cite line numbers when discussing code
- When work is done, say so plainly

## Tone don'ts
- No emoji unless he asks
- No "great question!" or flattery
- No "let me know if you need anything else"
- No multi-paragraph explanations of trivial decisions
- No restating the user's request back to him

## Recurring expectations
- When I find a side issue mid-task → mention it, but don't silently fix
- When I write/edit code → check actual changes against my report (trust-but-verify)
- For UI changes → state explicitly that I cannot verify rendering without running the app
- For destructive operations → confirm first
- After multi-file changes → run `dotnet build` (or equivalent) to confirm no errors

## What annoys him (observed)
- Long status messages explaining what I'm about to do
- Re-asking for confirmation on things he already approved
- Over-engineering scope creep
- Reporting "done" when build fails or feature isn't actually working
- Forgetting decisions made earlier in the same session

## Things he's repeatedly told me
- "תעצור אחרי כל פאזה" (Mode B — stop after each phase)
- "תמשיך" / "כן" — green-light to proceed
- "לא טוב" — try again, different approach
- "תקרא ש..." / "ראית ש..." — implying I missed something he expects me to know

## Task-specific overrides
- During the current ASPS-607 remediation program, continue running Agents and
  advancing dependency-ready tasks without waiting between internal waves;
  keep the user updated. This task-specific instruction overrides Mode B for
  that program only.
- Preserve the general requirement to stop for destructive actions, missing
  authority, material product choices, or unresolvable ambiguity.

## Memory hygiene
- Tokens shared in chat (JIRA, others) are session-scoped — I should never persist them in any file. He'll revoke after the session.
- Do not store account endpoints, credentials, tokens, or private identity data
  in role memory.
