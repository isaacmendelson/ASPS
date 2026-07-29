# Delegation Rules

When to do work myself (CEO hat) vs spawn a sub-agent.

## The roles available

> **Roster updated 2026-06-28 (AI OS v1).** Definitions live in `.claude/agents/<role>.md`.
> Org model: CEO (me) → **vp-engineering** (owns technical execution) → technical agents.
> For multi-step technical work, prefer routing through **vp-engineering** rather than spawning programmers directly.

| Role | Stack / Domain | Definition |
|---|---|---|
| **vp-engineering** | Owns/coordinates all technical execution; runs the engineering gates | `agents/vp-engineering.md` |
| **product** | Requirements, user stories, acceptance criteria, priorities | `agents/product.md` |
| **knowledge-manager** | AIducation, ADRs, lessons, organizational memory, Knowledge Engine | `agents/knowledge-manager.md` |
| **architect** | Architecture, cross-cutting design, spec breakdown, ADRs (replaces CTO) | `agents/architect.md` |
| **backend** | C# / .NET 8 / EF Core / NetMQ / MySQL | `agents/backend.md` |
| **desktop-agent** | `apps/desktop/win/` — Python desktop agent | `agents/desktop-agent.md` |
| **browser-extension** | `apps/extension/chrome/` — Chrome MV3 | `agents/browser-extension.md` |
| **analyzer-ai** | `Analyzers/` — detection/scoring microservices | `agents/analyzer-ai.md` |
| **qa** | Verify code before merge (mandatory gate) | `agents/qa.md` |
| **security** | Threat review, audits; reports, does not fix | `agents/security.md` |
| **devops** | Build / release / environments (forward-looking) | `agents/devops.md` |

**Legacy agents retained** (still spawnable): `cto` (→ use architect), `frontend` (**owns the Razor admin UI** — no new-roster owner for it), `mobile` (Android/iOS, not started), `python` (→ split into desktop-agent + analyzer-ai).

**Knowledge lookups:** any agent can query the Knowledge Engine via MCP tools `knowledge_ask` / `knowledge_search` (project knowledge, agent defs, workflows, rules, ADRs, lessons).

## Routing matrix

| Task type | Direct (me) | Sub-agent | Notes |
|---|---|---|---|
| Read a file, search the repo | ✅ | ❌ | Always do directly |
| Trivial code edit (typo, comment, log) | ✅ | ❌ | Skip QA |
| User Q&A about codebase | ✅ | ❌ | Use Read/Grep myself |
| Run dotnet build / test / migration | ✅ | ❌ | Bash directly |
| Create a cron / schedule | ✅ | ❌ | CronCreate directly |
| Architecture decision / spec | ❌ | **CTO** | Heavy reasoning, isolated context |
| New EF entity + migration + repo + handler | ❌ | **Backend** | After CTO design |
| Update Razor page + CSS + JS | ❌ | **Frontend** | |
| Modify desktop agent / analyzer | ❌ | **Python** | |
| Pre-merge review of non-trivial code | ❌ | **QA** | **MANDATORY** |
| Security audit | ❌ | **3 parallel general-purpose** | Existing pattern |
| Deep research with many file reads | ❌ | **Explore / general-purpose** | Protects my context |
| Cross-stack feature (backend + frontend) | ❌ | **CTO breaks down → spawns programmers in parallel** | |

## When NOT to delegate (override the matrix)

- The user explicitly says "תעשה את זה ישר" / "do it directly"
- The task is small enough that spawning costs more than executing
- Time-sensitive: user is waiting and a sub-agent adds 5+ minutes
- The user is debugging interactively and wants me responsive

## Spawn prompt minimum

Every implementation prompt must include:

- exact Jira ID and title;
- original requirement and acceptance criteria;
- relevant specifications and handoff;
- exact file/module ownership;
- dependencies and compatibility constraints;
- required unit tests and reporting format;
- QA/commit/Jira restrictions (agent must not commit or close Jira);
- model/effort/context selection from the adaptive routing method
  (read [`agent_routing_learning.md`](agent_routing_learning.md) and the linked
  complete method before every spawn);
- statement that Agents share the worktree and must not revert concurrent edits.

The agent doesn't see our chat history. Don't say "as we discussed" — restate.

## Persistent vs one-shot

- **CTO + QA:** persistent across the session via `SendMessage`. Spawn at session start (or first need).
- **Backend / Frontend / Python:** spawn lazily per task. May be persistent within a multi-task session, or one-shot per task — judgment call.
- **Security audit / research agents:** one-shot, run in background.

## When QA agent's context is full

A persistent QA agent's context grows with every review. After ~10 reviews, spawn a fresh QA — pass it the regression-watch + checklist memory plus a 1-paragraph summary of the previous QA's verdicts.

## Hat-mode shortcut

If a task is borderline (non-trivial but small, e.g., adding a single field), I can:
1. Wear the role's hat in my own context (read its memory)
2. Do the work
3. **Still send to QA agent** for the merge gate

This is the cheapest path that still keeps the QA gate.
