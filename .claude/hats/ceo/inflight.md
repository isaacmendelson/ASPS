# In-Flight Initiatives

What's actively in progress. Updated frequently — at session start, mid-session as state changes, and end-of-session.

**Last updated:** 2026-06-29

---

## Currently active

### AI Operating System v1 (2026-06-28/29)
- ✅ Built `.claude/` org structure: `agents/` (12 role defs in 3 layers + 4 legacy), `workflows/` (5), `rules/` (4), `memory/`, `aiducation/` (principles/lessons/prompts/schemas/role-training/learning-engine), `architecture/` (AI-OS.md + ADR/). Commit `a45327e`.
- ✅ Roster: **Executive** = CEO (sole orchestrator) · **C-level** = vp-engineering / product / knowledge-manager · **Technical** = architect / backend / desktop-agent / browser-extension / analyzer-ai / qa / security / devops.
- ✅ Legacy agents retained by user decision: `cto`, `frontend` (owns Razor admin UI), `mobile`, `python`.
- 🔜 All role/workflow/rule files are TODO-stubbed templates — content to be filled as work demands.

### Knowledge Engine (vendored + MCP, 2026-06-28/29)
- ✅ Vendored into repo at `KnowledgeEngine/` (was `C:\AI\Projects\KnowledgeEngine`). Commit `e71fc8c`.
- ✅ Gitignored: `.venv/`, `db/`, `output/`, `.env`, `NIU/`. `requirements.txt` + `.env.example` committed.
- ✅ Runs **Sonnet 4.6** (`llm_provider.py`, max_tokens 1200). Indexes `docs/` + `.claude/` (env-driven config).
- ✅ CLI: `ke_cli.py ask "Q" --sources` (venv python only — never global `python`; chromadb 1.5.9 vs 1.0.20 mismatch panics).
- ✅ **MCP live**: `ke_mcp_server.py` registered in repo-root [`.mcp.json`](../../../.mcp.json) → tools `knowledge_ask` / `knowledge_search` available to all agents. Commit `7a5380b`. Doc: [`.claude/tools/knowledge-engine.md`](../../tools/knowledge-engine.md).
- 🔜 Optional: delete original `C:\AI\Projects\KnowledgeEngine` (new path verified working).

### System specification doc (gsd:quick 001, 2026-06-28)
- ✅ [`docs/system-specifications/ASPS_System_Specification.md`](../../../docs/system-specifications/ASPS_System_Specification.md) — 7 subsystems, cited from code + KE + JIRA. Commits `1699aa5`/`3709da5`/`f17340f`/`eb6a548`.
- ✅ Status: backend/admin-portal/desktop-agent/browser-extension/url-analyzer = **built**; mobile-agent + users-portal = **planned**.
- ✅ 4 `.docx` specs converted to `.md` under `docs/system-specifications/`; binaries archived in `spec-sources/`. Commit `84f7e44`.

### Hat-based memory system (legacy)
- ✅ CEO hat: 7 files complete (this folder). Superseded as the org model by AI OS v1 `.claude/agents/`, but CEO hat memory still load-bearing.
- ⏳ Other-role hats (`hats/<role>/`): not built. Note: `.claude/agents/<role>.md` (definitions) now exist for the new roster; hat *memory* folders do not.

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
- **2026-06-28/29** — Built AI Operating System v1 (`.claude/` org: agents/workflows/rules/memory/aiducation/architecture). Vendored KnowledgeEngine into the repo + wired its MCP server (now live: `knowledge_ask`/`knowledge_search`). Authored unified `ASPS_System_Specification.md` via gsd:quick 001. Commits `a45327e`, `84f7e44`, `e71fc8c`, `6b64e96`, `7a5380b`, `a7ad3d1`, `1699aa5`, `3709da5`, `f17340f`, `eb6a548`. 10 local commits, **not pushed**. CEO memory synced to reflect all of the above.
