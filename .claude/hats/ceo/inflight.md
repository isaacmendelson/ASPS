# In-Flight Initiatives

What's actively in progress. Updated frequently — at session start, mid-session as state changes, and end-of-session.

**Last updated:** 2026-06-17

---

## Currently active

### Hat-based memory system (this session)
- ✅ CEO hat: 7 files complete (this folder)
- ⏳ CTO, Backend, Frontend, Python, QA hats: not yet built
- 🔜 Next: build QA hat (highest priority per user's earlier comment)
- 🔜 Then: build Backend, Frontend, Python, CTO as we encounter the need

### Daily security audit cron
- ✅ Cron created 2026-05-03 (job ID `e54696bf`, fires daily at 05:03 local)
- ⚠️ Session-only — dies if Claude restarts. Self-rearm logic is in the prompt but only fires if Claude is running at 05:03.
- 📁 Reports go to `docs/security-audits/YYYY-MM-DD.md`
- 🚩 Flag file: `docs/security-audits/NEEDS_ATTENTION.md` (currently exists — 5 Critical findings)

### Security findings — open
From `docs/security-audits/2026-05-03.md` — 5 Critical, 15 High. None fixed yet.
- 🔴 Rotate MySQL root password, both Keycloak ClientSecrets, JIRA `isaac` password
- 🔴 Remove hardcoded admin allow-list in `AdminClaimsTransformer.cs` + `Program.cs:132-134`
- 🔴 Hard-gate dev-mode admin login in `Login.cshtml.cs:50-92`
- 🔴 Replace `TypeNameHandling.Auto` with `None` (or strict binder) in CQRS plane
- 🔴 Bind ZMQ ports 5555/5556 to `tcp://127.0.0.1:`
- 🟠 + 15 High items (see `NEEDS_ATTENTION.md`)

### Recently shipped (last 7 days)
- 2026-04-28: Roadmap admin Razor page + SPA + viewer export — feature complete
- 2026-05-01: ARCHITECTURE.md + ASPS_DATA_FLOW.md updates (drift fixes + Roadmap + Mobile sections)
- 2026-05-01: PRODUCT.md created
- 2026-05-01: JIRA sync — 251 status updates from OLD to NEW; reporter+assignee mapped
- 2026-05-02: ImmediateDangers table created via 2 migrations (`AddImmediateDangersTable`, `AddedColumnsProtectiveActionsAndScamInProgressToImmediateDangerTable`)
- 2026-05-02: `ImmediateDangerPersistanceActor` created + wired in DI; lazy-resolution pattern to break DI cycle
- 2026-05-03: Bug fix — `Key.ToString()` overflow in `DeviceKey` varchar(36)
- 2026-06-16: 14 project-local skills shipped under `.claude/skills/` (commit `f53435c`)
- 2026-06-16: SCRUM-904 Phase-1 MVP complete (Steps A–D, commits `bc5561c`–`b70e927`)
- 2026-06-17: CLAUDE.md `Ports / messaging` table updated — added 8180 (Keycloak) + annotated 8080–8484 agent scan range

### Open code threads
- `ImmediateDangerPersistanceActor` — currently publishes `ImmediateDangerAdded` to per-user UDAnalysis + global handlers via lazy publisher. Tested in passing alert; full integration test not run yet.
- ASView's `HandleImmediateDangerAdded` is empty stub — needs implementation if ASView should cache ImmediateDangers in-memory.

### Pending user decisions
- Daily security audit: full vs lightweight daily + weekly full? Currently full daily.
- Whether to set up Windows Task Scheduler as durable backup for cron (Claude-cron is session-only).
- Insights zip from other project — extracted to `_inbox/`, hat-system pattern adopted; rest still in inbox awaiting triage.

---

## Cleanup needed

- `_inbox/workspaces.zip` + extracted folder — once we've harvested everything useful, can be deleted (or archived elsewhere).
- `aspsbackend2db_20260130.sql` (12k+ lines) — flagged in security audit; check if real PII before keeping or replace with sanitized fixture.

---

## Update log

- **2026-05-03** — File created. CEO hat memory system bootstrapped. 5 Critical security findings remain open. Hat folders for other roles not yet built.
- **2026-06-16** — Added CLAUDE.md ports-table amendment as a 30-second follow-up (Keycloak moved to 8180 today). 14 project-local skills shipped under `.claude/skills/` (commit `f53435c`). SCRUM-904 Phase-1 MVP complete (commits `bc5561c`–`b70e927`).
