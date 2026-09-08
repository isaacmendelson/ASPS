# ADR-006 — Separate MASDP (platform) from SPS (product) into independent repos + JIRA

- Status: **Proposed** (decisions locked 2026-09-08; **execution scheduled after ASPS-763** — do not fork the platform mid-hardening)
- Date: 2026-09-08
- Decision owners: CEO (operator-ratified)
- Inputs: [MASDP-SPS Boundary Inventory](../MASDP-SPS-BOUNDARY-INVENTORY.md)
- Related: ASPS-763 (agent privilege separation — prerequisite for safe multi-tenant), ADR-005

## Context

The repo `C:\Jobs\ASPS\GitHub\Software` and the JIRA project `ASPS` currently hold **two distinct products** entangled together:

- **SPS** (scam-protection-system) — the anti-scam software **product** (the "what"): `ASPSBackend14_J/`, `apps/{extension,desktop,admin}/`, `Analyzers/`, its docs/specs/CI.
- **MASDP** (multi-agent-software-development-platform) — the AI **platform** that builds software (the "how"): the `.claude/` operating system, the Telegram CEO bot (`apps/telegram-ceo`), the Knowledge Engine, `deploy/vps`, `python_model_tools`.

MASDP bootstrapped **inside** its first project (SPS). The goal: extract a clean, reusable MASDP with its **own repo + JIRA**, designed **multi-tenant** ("software house") — it operates ON project repos. SPS becomes the **first** managed project. The boundary inventory classified ~28 SPS / ~16 MASDP / ~20 MIXED assets and identified coupling points C1–C10 (chief among them C1: the bot has a single hardcoded `WORKING_DIR` — no project selector).

## Decision

### Project-config contract
A managed project is a config record:
```
{ name, github_repo, jira_project, working_dir, domain_context, secrets_ref }
```
Principle: **MASDP carries the generic "how"; each project carries its own "what"** (repo, JIRA, domain context, secrets). **MASDP is also "tenant #0"** — it manages itself as a project (dogfooding).

### The nine locked decisions
| # | Topic | Decision |
|---|---|---|
| 1 | Registry | A `projects.json` in the MASDP repo; `secrets_ref` points to a per-project credential bundle held **outside every clone** on the bot host (generalizing the `SECRETS_DIR` pattern). |
| 2 | Stack-specialists | Live in the **project** (SPS ships `backend`/`desktop-agent`/`browser-extension`/`analyzer-ai` as a role-pack under its `domain_context`); MASDP core stays **stack-agnostic**. |
| 3 | `.claude/settings.json` | MASDP **enforces a single platform permission policy and ignores project `settings.json`** (safer; the bot already does `settingSources:[]` for this — coupling C7). |
| 4 | Knowledge Engine | **One KE process, N collections** — one collection per managed project **plus a `masdp_knowledge` collection for MASDP itself** (so agents developing the platform can query its own docs/ADRs). |
| 5 | Root `AIDucation/` | Stays with SPS for now; promote the onboarding framework into MASDP later if warranted. |
| 6 | `CHARTER.md` mission | Neutralize the "protect vulnerable users from scams" framing in the MASDP core charter (that is SPS's mission); MASDP's charter is about **building software well**. The mission travels with SPS. |
| 7 | git history | Start the **MASDP repo fresh** (clean initial commit); the mixed history **stays in the SPS repo**. No `filter-repo` split (error-prone; MASDP code is small and recent). |
| 8 | Secrets | **Rotate as part of the split** — the GitHub PAT, JIRA token, and `CQRS_SHARED_SECRET` are on disk; the split is the natural rotation point. |
| 9 | Stray/archival dirs | **Archive or drop** (`KnowledgeEngine/NIU/`, `deploy/vps;C/`, root `artifacts|reports|versions|obj`) — not carried into the clean core. |

### Extraction order (from the inventory §9)
1. Freeze the project-config contract + secrets/registry shape.
2. Extract the clean MASDP core (platform code + generic process docs).
3. Split `CLAUDE.md` → MASDP operating-charter + SPS project CLAUDE.md.
4. Split `.claude/` rules & agents → generic in MASDP; SPS stack-specialists + stack skills into the SPS role/skill pack.
5. Parameterize the Knowledge Engine (per-project collections + MASDP's own; per-project source paths + vector DB).
6. Make the Telegram bot multi-tenant — project registry + per-conversation project binding (break C1); generalize `deploy/vps` to clone N project repos with N secret bundles.
7. Register SPS as project #1.
8. Split shared docs last (task-memory, cloud Azure-vs-VPS, security-audits, ADRs) by subject.

## Consequences

Positive: a reusable, stack-agnostic MASDP that can run a "software house" of projects, each isolated by repo/JIRA/collection/secrets; SPS keeps its full history and becomes a clean tenant; the platform's own knowledge is queryable (tenant #0).

Costs/risks: content extraction from `CLAUDE.md` + `.claude/hats`/`agents` is large, hand-curated, and the biggest effort; the multi-tenant bot work (C1) depends on ASPS-763's sandbox (safe isolation across projects) — hence **execution is scheduled after ASPS-763**; secret rotation must be coordinated so SPS stays deployable; the chicken-and-egg (MASDP builds SPS) is handled by registering SPS as project #1 immediately so there is no development gap.

## Alternatives considered
- Keeping both in one repo/JIRA (status quo) — rejected: the entanglement already causes operational confusion (mixed epics/tickets).
- Per-project MASDP instances instead of multi-tenant — rejected: N copies drift and don't propagate platform improvements.
- Splitting git history via `filter-repo` — rejected (decision 7): error-prone for intertwined commits; fresh MASDP repo is cleaner.
