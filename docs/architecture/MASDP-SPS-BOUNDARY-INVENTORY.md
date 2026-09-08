# MASDP / SPS Boundary Inventory

**Purpose:** Classify every significant asset in this repo (`C:\Jobs\ASPS\GitHub\Software`) as belonging to **MASDP** (the multi-agent software-development platform — the "how") or **SPS** (the anti-scam software product — the "what"), or as **MIXED** (must be split), to plan separating the two products into independent repos + JIRA projects.

**Analysis only — nothing was moved, deleted, or refactored.**

**Date:** 2026-09-08
**Author:** analysis pass (uncommitted; for CEO review)

---

## Target model (recap)

MASDP becomes its **own repo + JIRA**, designed **multi-tenant** ("software house") — it operates ON project repos. A project is a config record:

```
{ name, github_repo, jira_project, working_dir, domain_context, secrets_ref }
```

SPS becomes the **first** such project. Principle: **MASDP carries the generic "how"; each project carries its own "what" (repo, JIRA, domain context, secrets).**

Per-project config parameters are referenced below as `→ github_repo`, `→ jira_project`, `→ working_dir`, `→ domain_context`, `→ secrets_ref`.

---

## Legend

| Class | Meaning |
|---|---|
| **MASDP** | Platform, reusable across any project. Moves to the MASDP repo. |
| **SPS** | Anti-scam product / its domain. Stays in the SPS project repo. |
| **MIXED** | Contains both; must be SPLIT. Split approach given inline. |

---

## 1. Root config & entry-point files

| Asset | Class | Notes / split |
|---|---|---|
| `CLAUDE.md` | **MIXED** | The single biggest MIXED item. Generic: the hat-system entry protocol, GSD working style, TDD rule, QA/security/review gates, workflow-selection, destructive-op confirmation, communication style. SPS-specific: the entire "Stack quick reference" (.NET/NetMQ/MySQL/Chrome/Python), "Ports / messaging" table, "Repo layout", "Build & migrations" (dotnet/EF/MySQL CLI), "Paths I never touch" (SPS DB dumps, appsettings). **Split:** generic half → MASDP `CLAUDE.md` (project-agnostic operating charter); SPS half → SPS repo's own `CLAUDE.md` (`→ domain_context`). MASDP's per-project loader reads the project's CLAUDE.md at session start. |
| `AGENTS.md` | **MASDP** | Redirect stub pointing Codex/Claude at `CLAUDE.md` + `.claude/` structure. Generic mechanism; the target CLAUDE.md is per-project. |
| `.mcp.json` | **MIXED** | Wires the `knowledge-engine` MCP server. Mechanism is generic (MASDP), but the two hardcoded absolute paths (`C:\Jobs\ASPS\GitHub\Software\KnowledgeEngine\...`) are SPS-box-specific. **Split:** MASDP ships the KE MCP wiring with a parameterized path (`→ working_dir`/`→ secrets_ref`); SPS project supplies the concrete path. |
| `global.json` | **SPS** | Pins .NET SDK 9.0.308. Pure .NET-build concern → SPS. |
| `.env` | **SPS** | `CQRS_SHARED_SECRET` — SPS backend runtime secret (`→ secrets_ref`). **Contains a live secret.** |
| `.env.example` | **SPS** | MySQL env template for the SPS stack. |
| `ACCESS_KEYS.env` | **MIXED** | **Contains live GitHub PAT + JIRA token.** The *file/mechanism* (agent credentials for GitHub + JIRA) is MASDP-generic, but the *values* are SPS's repo + JIRA project. **Split:** MASDP defines the `secrets_ref` contract (per-project GitHub/JIRA creds injected as env); each project (SPS first) supplies its own `ACCESS_KEYS.env` (`→ secrets_ref`, `→ github_repo`, `→ jira_project`). Gitignored today; keep out of both repos. |
| `.gitignore`, `.gitattributes`, `.dockerignore` | **MIXED** | Both repos need these; content is a union of MASDP rules (bot `.env`, `config.env`) and SPS rules (appsettings, DB dumps, `*.pfx`). **Split:** partition each ruleset to the repo it protects. |
| `README.md`, `README-HE.md` | **SPS** | Product README for the anti-scam system. |
| `PRODUCT.md` | **SPS** | SPS product vision / users / roadmap. Canonical SPS `→ domain_context`. |
| `ARCHITECTURE.md` | **SPS** | 73 KB SPS software-product architecture (backend/messaging/extension/agent). |
| `ASPS-243-COMPLETE.md`, `ASPS-318-COMPLETION-REPORT.md`, `ASPS-75-IMPLEMENTATION-SUMMARY.md`, `SCRUM-821-summary.md` | **SPS** | SPS task completion reports (root clutter; SPS history). |
| `docker-compose.yml`, `Dockerfile.backend`, `Dockerfile.webapi`, `Dockerfile.analyzer` | **SPS** | Compose + images for the SPS stack (backend/webapi/analyzer/MySQL). |
| `aspsbackend2db_20260130.sql`, `aspsbackend2db_20260728.sql` | **SPS** | SPS MySQL seed dumps (~60 MB / ~200 MB). SPS-only; CLAUDE.md flags them as never-touch. |

---

## 2. `.claude/` — the platform's operating system

Almost entirely **MASDP-generic**; the coupling is in *content* (SPS stack assumptions and the ASPS JIRA/GitHub references embedded in prompts/memory), not in structure.

| Asset | Class | Notes / split |
|---|---|---|
| `.claude/agents/*.md` (19 role defs) | **MIXED** | Generic: the executive/QA/security/architect/devops/tech-writer/product roles and the whole reporting structure. SPS-coupled: `backend.md` (.NET/EF/NetMQ), `desktop-agent.md` (Python/pyzmq), `browser-extension.md` (Chrome MV3), `analyzer-ai.md` (Analyzers/), `mobile.md`, `frontend.md`, `python.md`, `cto.md` — these embed the SPS stack. **Split:** MASDP keeps generic roles as the base team; **stack-specialist roles become a per-project "role pack"** the project registers (`→ domain_context`). SPS ships the .NET/Python/Chrome specialists. |
| `.claude/agents/README.md` | **MASDP** | Roster mechanism (roster content is per-project). |
| `.claude/hats/<role>/` (per-role memory) | **MIXED** | The hat *mechanism* (INDEX + identity/decisions/inflight/operating-principles) is MASDP. The *accumulated content* is saturated with SPS specifics — CEO hat `delegation.md`/`decisions.md`/`inflight.md`, `agent_routing_learning.md`, `user_profile.md`, and every technical hat's learnings reference ASPS tasks, the SPS stack, and the ASPS JIRA. **Split:** MASDP ships empty/stub hat scaffolding; SPS-accumulated memory travels with the SPS project (`→ domain_context`). This is the second-biggest split after CLAUDE.md — large and hand-curated. |
| `.claude/rules/task-workflow.md` | **MIXED** | Mostly MASDP-generic (branch-per-task, pre-QA gate, security gate, code review, merge flow). SPS-coupled specifics: **JIRA transition IDs 21/31/41**, the exact `dotnet test`/`pytest`/`jest` commands, spec-doc index paths under `docs/system-specifications/`. **Split:** generic workflow → MASDP; the JIRA transition map and per-stack test commands become per-project config (`→ jira_project`, `→ domain_context`). |
| `.claude/rules/review-standards.md` | **MIXED** | Severity scale + QA/code/security review guides are MASDP-generic. "ASPS-specific" security checklist (CURVE, device auth, WebSocket) is SPS. **Split:** generic guide → MASDP; the ASPS-specific check row → SPS domain security addendum. |
| `.claude/rules/security-rules.md` | **MIXED** | Generic: secrets hygiene, input-validation, finding format, review-vs-fix separation. SPS: CURVE/NetMQ crypto boundary, the "known security debt" list (ports 5555/5556, MySQL 3306, `ws://` extension↔agent). **Split:** generic secrets/review rules → MASDP; crypto + known-debt → SPS. |
| `.claude/rules/coding-standards.md` | **MIXED** | Universal + DRY section is MASDP; the "Per Stack" (.NET/Python/JS/EF) rows are SPS. **Split** along that seam. |
| `.claude/rules/agent-creation-protocol.md` | **MASDP** | How to create a new agent in the OS. Fully generic. |
| `.claude/rules/team-rules.md`, `rules/README.md` | **MASDP** | Generic team-operation rules (mostly TODO stubs). |
| `.claude/team/CHARTER.md` | **MASDP** | Team-wide ethics/thinking/conduct charter. Generic (verify no SPS mission-specific clauses on extraction — the "protect vulnerable users" framing may need neutralizing). |
| `.claude/workflows/*.md` (feature-development, bug-fix, architecture-review, knowledge-update, release) | **MASDP** | Generic development-process workflows. May carry SPS examples to genericize. |
| `.claude/skills/*.md` | **MIXED** | Generic: `adr.md`, `qa-gate.md`, `qa-report.md`, `scrum-design.md`, `threat-model.md`, `security-audit-now.md`. SPS-specific: `cqrs-handler.md`, `ef-migrate.md`, `mv3-content-script.md`, `netmq-endpoint.md`, `ng-feature.md`, `pyzmq-curve-client.md`, `razor-admin-page.md`, `velopack-publish.md` — these are SPS-stack recipes. **Split:** generic skills → MASDP; stack recipes → SPS project skill pack (`→ domain_context`). |
| `.claude/architecture/AI-OS.md` | **MASDP** | The platform's own org-architecture doc ("ASPS AI Operating System"). Rename/de-ASPS on extraction; explicitly separates itself from the product architecture. Core MASDP design doc. |
| `.claude/architecture/ADR/` (0000-template, README) | **MASDP** | Platform ADR scaffolding. |
| `.claude/architecture/README.md` | **MASDP** | Platform architecture index. |
| `.claude/aiducation/` (principles, lessons, prompts, schemas, role-training, learning-engine) | **MIXED** | The learning-system *structure* is MASDP-generic (Principle 7 / KE integration). Accumulated lessons/role-training reference SPS tasks. **Split:** framework → MASDP; SPS lessons → SPS `→ domain_context`. |
| `.claude/tools/knowledge-engine.md` | **MASDP** | Doc for the KE tool integration (KE itself is MASDP). |
| `.claude/settings.json` | **MIXED** | Permission allowlist (`Bash(*)`, Read/Edit/Write, Agent, session MCP). Generic mechanism, but this is the file the Telegram bot deliberately does **not** load (see coupling C7). **Split:** MASDP ships a baseline; each project may override. Note the `Bash(*)`/`Write` allow-all is a security consideration the bot already isolates against. |
| `.claude/settings.local.json` | **MIXED** | Local per-machine overrides. Same treatment; keep box-specific values out of the shared repo. |
| `.claude/memory/` | **MIXED** | Git-tracked project memory mirrored to auto-memory. Mechanism MASDP; content (ASPS architecture, CURVE auth, JIRA instance, security-audit cron, access-keys note, SCRUM-863 progress) is SPS. **Split:** mechanism → MASDP; content → SPS. |
| `.claude/scheduled_tasks.lock`, `.claude/worktrees/` | **MASDP** | Runtime/scratch of the platform tooling. |

---

## 3. MASDP platform components (outside `.claude/`)

| Asset | Class | Notes / split |
|---|---|---|
| `apps/telegram-ceo/` (src, dist, package.json, README, tests) | **MIXED** (predominantly MASDP) | **This is the running MASDP platform** — the Telegram→Claude Agent SDK CEO bot with a full security model (path guard, deny-by-default approvals, read-only git allowlist, bubblewrap sandbox). The *code* is generic and multi-tenant-ready in spirit. SPS coupling is in **runtime config**: `WORKING_DIR`→the ASPS clone, GitHub/JIRA MCP creds→ASPS repo+project, `loadClaudeMd` reads the project CLAUDE.md. **Split:** move wholesale to MASDP. To become true multi-tenant it must accept a **project selector** (which `{working_dir, github_repo, jira_project, secrets_ref, domain_context}` to bind per conversation) instead of a single `WORKING_DIR`. Today it is single-tenant-hardcoded via env — the core coupling to break. |
| `deploy/vps/` (01-harden, 02-toolchain, 03-clone, 05-service, 06-sandbox, lib.sh, telegram-ceo.service, config.env.example, README) | **MIXED** (predominantly MASDP) | VPS provisioning for the bot host. Generic: OS hardening, toolchain, systemd unit, bubblewrap/AppArmor sandbox. SPS coupling: `REPO_URL=github.com/isaacmendelson/ASPS`, `CLONE_PATH=/home/aspsbot/ASPS`, `ACCESS_KEYS.env` mirrors SPS GitHub/JIRA, `03-clone.sh` builds `apps/telegram-ceo` from the SPS clone, and the toolchain installs .NET/Docker specifically for D4 "agent builds ASPS locally". **Split:** infra → MASDP; the concrete repo/clone/secret values → per-project provisioning input (`→ github_repo`, `→ working_dir`, `→ secrets_ref`). In multi-tenant MASDP the bot host clones N project repos, not one. |
| `deploy/vps;C/` | **MASDP** | Empty/near-empty stray dir (malformed name — likely accidental `vps;C:` artifact). MASDP side; candidate for deletion (do not delete now). |
| `KnowledgeEngine/` (src, scripts, db, documents, requirements.txt, tests) | **MIXED** | The RAG **engine** (`src/knowledge_engine/*`, `scripts/ke_mcp_server.py`, `ke_cli.py`) is MASDP-generic. Its **config and indexed corpus are SPS**: `config.py` hardcodes `ASPS_DOCS=C:\Jobs\ASPS\GitHub\Software\docs`, `ASPS_CLAUDE=...\.claude`, `COLLECTION_NAME="asps_knowledge"`; `db/` (Chroma vector store) holds embedded SPS docs; `documents/spec-sources` are SPS specs. **Split:** engine → MASDP; the `KNOWLEDGE_SOURCES` paths, collection name, and vector DB become **per-project** (`→ domain_context` = which docs to index, `→ working_dir` = where). Each project gets its own KE collection. Already env-overridable (`ASPS_DOCS`/`ASPS_CLAUDE`/`COLLECTION_NAME`) — good seam. |
| `KnowledgeEngine/NIU/` | **MASDP** | "Not In Use" — an older multi-agent workspace snapshot (agents/hats/skills/workspaces incl. brand/creative/design-system roles). Historical MASDP artifact; archive/prune, do not carry into clean core. |
| `python_model_tools/` (`_change_model.py`, `_check_codex_claude.py`, `_check_harnesses.py`, `_configure_codex_harness.py`, `_check_telegram_schema.py`, ...) | **MASDP** | Operator tooling for managing Claude/Codex harnesses, model selection, and the Telegram bot. Platform tooling, project-agnostic. |
| `.claude/architecture/AI-OS.md` | *(listed in §2)* | — |

---

## 4. SPS product components

| Asset | Class | Notes |
|---|---|---|
| `ASPSBackend14_J/` (.NET solution: ASPSBackend, WebApi, Business, Common, Interface, ASPS.Tests) | **SPS** | The anti-scam backend, admin UI, domain logic, entities, tests. Core SPS deliverable. |
| `apps/extension/chrome/` | **SPS** | Chrome MV3 anti-scam extension. |
| `apps/desktop/win/` | **SPS** | Python Windows desktop agent. |
| `apps/admin/angular/` | **SPS** | Angular admin UI (Azure-deployed). |
| `Analyzers/` | **SPS** | Python analyzer microservices (URL/scam detection). |
| `contracts/messaging/` | **SPS** | Messaging contract definitions (envelope/schemas) for the SPS components. |
| `scripts/` (`verify.ps1`, `generate_messaging_contracts.py`, `check_test_baseline.py`, `version_bump.py`, `test-baseline-exceptions.json`, `version_config*.json`, `requirements.txt`, `tests/`) | **SPS** | Build/verify/contract-gen tooling for the SPS stack + reproducible-baseline gate. |
| `tests/contracts/` | **SPS** | Cross-component messaging contract tests. |
| `spec-sources/` (.docx/.html originals, HE + EN) | **SPS** | Source specification documents for the SPS product. |
| `python_clients/python-client-with-notifications.py` | **SPS** | SPS test/reference client. |
| `Website/` (`asps-website-v2/`, `NIU/` with website zips) | **SPS** | SPS marketing website + archived variants. |
| `AIDucation/` (root — guides, onboarding, templates) | **MIXED** | Developer onboarding/training. The *concept* (dev education) could be MASDP, but current content is SPS-flavored: `architecture.md` onboarding, C# `test-template.cs`, SPS coding standards. Distinct from `.claude/aiducation/` (the agent learning system). **Split:** generic dev-onboarding structure → MASDP optional; SPS-specific guides/templates → SPS. Lowest-priority split; can stay with SPS wholesale initially. |

---

## 5. `docs/` — heavily SPS, with MASDP pockets

| Asset | Class | Notes / split |
|---|---|---|
| `docs/PROJECT_CONTEXT.md` | **SPS** | Mandatory SPS shared context (product, specs, source-of-truth order). The canonical SPS `→ domain_context` entry point. MASDP's per-project loader reads *the project's* equivalent. |
| `docs/ARCHITECTURE`, `ASPS_DATA_FLOW.md`, `docs/system-specifications/`, `docs/specs/`, `docs/architecture/messaging/`, `docs/architecture/WS-AGENT-PROTOCOL.md` | **SPS** | SPS system specs, ICDs, protocol/data-flow docs. |
| `docs/architecture/decisions/ADR-001..005` | **MIXED** | ADR-001 (message envelope), ADR-002 (desktop↔ext IPC), ADR-003 (Azure deploy), ADR-004 (WebSocket gateway) are **SPS**. **ADR-005 (ASPS-763 Agent Tool Execution Privilege Separation)** is **MASDP** — it documents the Telegram bot / Agent SDK sandbox (bubblewrap, `06-sandbox.sh`). **Split:** ADR-005 → MASDP ADR log; 001–004 → SPS. *(ADR-005 is the untracked file in the current working tree.)* |
| `docs/task-memory/*_HANDOFF.md` (35 files) | **MIXED** | Per-task handoffs. Most are SPS (ASPS-6xx/7xx feature work). **MASDP ones:** `VPS_TELEGRAM_MIGRATION_HANDOFF.md`, `ASPS-763_HANDOFF.md` (sandbox), and the process-meta handoffs (`ASPS_PROJECT_CONTEXT_AND_TASK_HANDOFF_SYSTEM_HANDOFF.md`, `ASPS_TOP_LEVEL_CODE_REVIEW_HANDOFF.md`). **Split:** route each by subject; the *task-memory mechanism* is MASDP, the entries follow their product. |
| `docs/cloud/` (AZURE_*, ASPS_Azure_*, VPS_TELEGRAM_HARDENING.md, VPS_TELEGRAM_RUNBOOK.md, azure/, handoffs/) | **MIXED** | Azure docs (deployment guide, architecture, inventory, HTML diagram, `azure/`) are **SPS** infra. `VPS_TELEGRAM_HARDENING.md` + `VPS_TELEGRAM_RUNBOOK.md` are **MASDP** (bot host). **Split** along that line. |
| `docs/security-audits/` | **MIXED** | `2026-05-03.md`, `ASPS-628-full-audit.md`, `README.md`, `_prompts/`, `NEEDS_ATTENTION.md` cover the **SPS** codebase (the daily-audit cron target). `2026-09-07-vps-telegram.md` audits the **MASDP** bot/VPS. **Split:** the daily-audit *mechanism* is MASDP (reusable per project); the SPS audit reports stay with SPS, the VPS-telegram audit → MASDP. |
| `docs/agent-operations/AGENT_MODEL_EFFORT_LEARNING_METHOD.md` | **MASDP** | How the platform tunes model/effort per agent. Generic. |
| `docs/asps-roadmap*.json`, `docs/SCRUM-904-*`, `docs/ASPS-337-ANALYSIS.md`, `docs/ASPS-352-DESIGN*.md`, `docs/BUILD_TEST_BASELINE.md`, `docs/*.html` (roadmap/progress presentations) | **SPS** | SPS roadmap/design/analysis artifacts. |
| `docs/code-reviews/` | **SPS** | SPS code-review records (verify on extraction). |

---

## 6. `.github/` — CI

| Asset | Class | Notes / split |
|---|---|---|
| `.github/workflows/deploy.yml` | **SPS** | Builds/deploys SPS backend/webapi/angular to Azure ACR + Container Apps. Hardcodes `acraspsisaacdev`, `rg-asps-dev`, `ca-*-dev`. Pure SPS CI/CD (`→ github_repo` of SPS). |
| `.github/workflows/reproducible-baseline.yml` | **SPS** | Runs `scripts/verify.ps1` — SPS .NET/Python/Node build+test baseline. |
| `.github/workflows/messaging-contracts.yml` | **SPS** | SPS messaging-contract drift gate across backend/desktop/extension/analyzer. |
| `.github/dependabot.yml` | **MIXED** | Dependency updates. If it covers `apps/telegram-ceo` (npm) it spans both; the .NET/Python/extension ecosystems are SPS. **Split:** the telegram-ceo npm ecosystem → MASDP repo's dependabot; the rest → SPS. |

**Note:** there is currently **no MASDP CI** (no bot lint/test/build workflow in `.github/`). The MASDP repo will need its own (e.g. `vitest` for telegram-ceo, shellcheck for `deploy/vps`, KE pytest). All three existing workflows are SPS.

---

## 7. Miscellaneous / low-signal

| Asset | Class | Notes |
|---|---|---|
| `artifacts/`, `reports/`, `versions/`, `obj/`, `.vs/`, `.qa-artifacts/`, `.planning/` | **SPS** (build/scratch) | Build outputs, IDE state, QA artifacts of the SPS solution. Not carried into clean cores; classify with SPS if kept. |
| `_inbox/` | **N/A** | Gitignored user staging area (CLAUDE.md: never touch). Neither product. |
| `.codex/` | **MASDP** | Codex harness config — platform tooling (pairs with `python_model_tools`). |
| `AIDucation/` | *(see §4)* | MIXED, leaning SPS. |

---

## 8. Dependency / risk notes — the coupling points to break

Where MASDP-generic assets currently **depend on** SPS-specific things. These are the joints the separation must sever.

| # | Coupling | Where | Break by |
|---|---|---|---|
| **C1** | **Single hardcoded working dir.** The Telegram bot binds one `WORKING_DIR` = the ASPS clone; there is no project selector. | `apps/telegram-ceo/src/*` (context/agent/security), `deploy/vps/config.env.example` (`CLONE_PATH=/home/aspsbot/ASPS`), `03-clone.sh` | Introduce a **project registry** (`{name, github_repo, jira_project, working_dir, domain_context, secrets_ref}`); bind per conversation/session. `→ working_dir`. |
| **C2** | **Hardcoded GitHub repo.** `isaacmendelson/ASPS` appears in `ACCESS_KEYS.env`, `deploy/vps/config.env.example` (`REPO_URL`), `03-clone.sh`, telegram-ceo README (GitHub MCP). | multiple | Repo becomes a per-project field. `→ github_repo`. |
| **C3** | **Hardcoded JIRA project + transitions.** JIRA project key `ASPS`/`SCRUM`, base URL `isaacmendelsonjira.atlassian.net`, transition IDs 21/31/41, agent-label rules. | `ACCESS_KEYS.env`, `.claude/rules/task-workflow.md`, `.claude/hats/*`, telegram-ceo mcp-atlassian wiring | JIRA becomes per-project; transition map is project config. `→ jira_project`. |
| **C4** | **KE indexes SPS docs by absolute path.** `config.py` defaults to `C:\Jobs\ASPS\GitHub\Software\docs` + `.claude`, collection `asps_knowledge`; `db/` holds SPS embeddings. `.mcp.json` hardcodes the KE venv/script path. | `KnowledgeEngine/src/knowledge_engine/config.py`, `.mcp.json` | Per-project KE collection + source paths (already env-overridable). `→ domain_context`, `→ working_dir`. |
| **C5** | **SPS stack baked into generic prompts.** Agent defs, skills, CLAUDE.md, coding/security rules assume .NET/EF/NetMQ/CURVE/MySQL/Chrome/Python. | `.claude/agents/*`, `.claude/skills/*`, `CLAUDE.md`, `.claude/rules/{coding-standards,security-rules,review-standards}.md` | Extract stack-specialists + stack recipes into a **per-project role/skill pack**; MASDP core keeps only process-generic roles. `→ domain_context`. |
| **C6** | **SPS mission language in the charter/learning layer.** "protect vulnerable users from scams" in CHARTER/hat identity; SPS lessons in aiducation. | `.claude/team/CHARTER.md`, `.claude/hats/*/identity.md`, `.claude/aiducation/*` | Neutralize mission-specific framing in MASDP core; SPS mission travels with SPS `→ domain_context`. |
| **C7** | **Bot deliberately bypasses repo `.claude/settings.json`.** telegram-ceo uses `settingSources: []` because SPS repo's `settings.json` pre-authorizes `Bash(*)`/`Write`. This is a *security* coupling: MASDP's permission model must not inherit an arbitrary project's allow-list. | `apps/telegram-ceo/src/agent.ts`, `.claude/settings.json` | Keep MASDP's `canUseTool` the sole authority; per-project settings are data the platform reads cautiously, never auto-honored. |
| **C8** | **Secrets mixed in one `ACCESS_KEYS.env`.** One file holds the (only) project's GitHub+JIRA creds; the bot mirrors it into systemd `EnvironmentFile=`. | `ACCESS_KEYS.env`, `deploy/vps/{03-clone,05-service}.sh`, `telegram-ceo.service` | Per-project `secrets_ref` (a pointer to that project's cred bundle); MASDP host holds N bundles outside every clone (the `SECRETS_DIR` pattern already generalizes). `→ secrets_ref`. |
| **C9** | **VPS toolchain installs the SPS stack.** `02-toolchain.sh` installs .NET 8 + Docker specifically so "the agent builds ASPS locally" (D4). | `deploy/vps/02-toolchain.sh` | Toolchain becomes per-project capability (a project declares what its build needs); MASDP core host installs only Node + Claude CLI + git + bwrap. `→ domain_context`. |
| **C10** | **CI is entirely SPS.** All three workflows build/test/deploy SPS; none test MASDP. | `.github/workflows/*` | SPS workflows stay in SPS repo; author fresh MASDP CI in the new repo. |

---

## 9. Separation strategy

### Recommended order of extraction

1. **Freeze the contract.** Lock the project-config record `{name, github_repo, jira_project, working_dir, domain_context, secrets_ref}` and the secrets/registry shape first — everything else maps onto it.
2. **Extract the clean MASDP core** (no SPS content): platform code + generic process docs. See "clean core" below.
3. **Split `CLAUDE.md`** into MASDP operating-charter + SPS project CLAUDE.md (biggest single item; unblocks the per-project loader).
4. **Split the `.claude/` rules & agents** — MASDP keeps generic roles/rules; carve SPS stack-specialists + stack skills into the SPS role/skill pack.
5. **Parameterize KnowledgeEngine** — per-project collection + source paths + its own vector DB (env seams already exist).
6. **Make the Telegram bot multi-tenant** — replace the single `WORKING_DIR` with the project registry + per-conversation project binding (C1); generalize `deploy/vps` provisioning to clone N project repos with N secret bundles.
7. **Register SPS as project #1** — SPS repo carries `ASPSBackend14_J/`, `apps/{extension,desktop,admin}/`, `Analyzers/`, `contracts/`, `scripts/`, `spec-sources/`, `Website/`, its `docs/`, its `.github/workflows`, its CLAUDE.md + domain context + role pack.
8. **Split shared docs last** — task-memory, cloud (Azure vs VPS), security-audits, ADRs by subject (§5).

### What a clean/empty MASDP core contains

- **Platform code:** `apps/telegram-ceo/` (multi-tenant), `deploy/vps/` (generic provisioning), `KnowledgeEngine/` engine (no corpus, no `db/`), `python_model_tools/`, `.codex/`.
- **Operating system:** `.claude/` with **generic** agents (CEO/VP-eng/product/architect/QA/security/devops/tech-writer/knowledge-manager), generic `rules/` (task-workflow, review-standards minus ASPS rows, agent-creation-protocol, coding-standards universal half, security-rules generic half), `team/CHARTER.md` (de-missioned), generic `skills/` (adr, qa-gate, qa-report, scrum-design, threat-model, security-audit-now), `workflows/`, `aiducation/` framework (no SPS lessons), `architecture/AI-OS.md` (de-ASPS'd).
- **Docs:** `agent-operations/`, ADR-005, VPS runbook/hardening, the reusable daily-audit mechanism, the generic MASDP `CLAUDE.md`.
- **Its own CI** (to be written) + a **project registry** with **zero** or one (SPS) entry.
- **Empty hat memory, empty KE collection, no secrets, no SPS stack, no ASPS/JIRA constants.**

### Open questions for the operator

1. **Registry location & format** — where does the project registry live (a MASDP repo file? a DB? per-host `config.env`)? Where do the N `secrets_ref` bundles physically sit on the bot host?
2. **Stack-specialist roles** — do they live in the **project** repo (SPS ships `backend.md` etc.) or in MASDP as loadable "stack packs" a project references? (Recommend: in the project's `domain_context`, so MASDP core stays stack-agnostic.)
3. **`.claude/settings.json` per project** — does each project ship its own permission allow-list that MASDP reads, or does MASDP enforce a single platform policy and ignore project settings entirely (safer; see C7)?
4. **KE multi-tenant** — one KE process with N collections, or one KE instance per project? Affects `.mcp.json` wiring and the MCP server.
5. **AIDucation (root)** — keep as SPS dev-docs, or promote the onboarding framework into MASDP and leave only SPS content behind?
6. **`CHARTER.md` mission** — how much of the "protect vulnerable users" ethos is genuinely MASDP-universal vs SPS-specific and should move to SPS?
7. **History** — split git history (filter-repo per product) or start the MASDP repo fresh and archive the mixed history under SPS?
8. **Secrets already committed/present** — `.env`, `ACCESS_KEYS.env` hold live values; plan rotation as part of the split (both currently gitignored, but the values exist on disk).
9. **NIU / stray dirs** — `KnowledgeEngine/NIU/`, `deploy/vps;C/`, root `artifacts|reports|versions|obj` — archive or drop during the split rather than carry forward?

---

## Summary counts

| Class | Approx. count (significant assets) | Heaviest items |
|---|---|---|
| **SPS** | ~28 | `ASPSBackend14_J/`, `apps/{extension,desktop,admin}/`, `Analyzers/`, all 3 CI workflows, `docs/` specs, DB dumps |
| **MASDP** | ~16 | `apps/telegram-ceo/`, `deploy/vps/`, KE engine, `.claude/` scaffolding, `python_model_tools/`, `AI-OS.md` |
| **MIXED (must split)** | ~20 | **`CLAUDE.md`**, **`.claude/hats/*`**, **`.claude/agents/*`**, **`.claude/rules/*`**, **`.claude/skills/*`**, **KnowledgeEngine (engine vs corpus)**, **`apps/telegram-ceo` runtime config**, **`deploy/vps` values**, `docs/{cloud,security-audits,task-memory,architecture/decisions}` |

**Top coupling points:** C1 (single hardcoded `WORKING_DIR` — no project selector), C4 (KE indexes SPS docs by absolute path), C5 (SPS stack baked into generic agent/skill/rule prompts), C2/C3/C8 (ASPS repo + JIRA + secrets hardcoded as the only project).

**Biggest MIXED items:** `CLAUDE.md` (generic charter fused with the SPS stack/ports/build reference) and `.claude/hats/*` + `.claude/agents/*` (generic role framework saturated with hand-curated SPS memory and stack specialists) — these two carry the most split effort. `KnowledgeEngine` and the Telegram bot are structurally clean but config-coupled — the cheapest high-value wins.
