# Hats — Role-Based Memory for ASPS

Per-role accumulated memory (insights, conventions, learnings, decisions).
Agent definitions live in `.claude/agents/<role>.md`; this directory holds
what each role *learns over time*.

## Organization

```
User (the boss — Isaac)
  ↓
CEO (default session — orchestrates, never writes production code)
  ↓
├── vp-engineering (coordinates all technical execution)
├── product (requirements, acceptance criteria, priorities)
├── knowledge-manager (ADRs, lessons, Knowledge Engine)
  ↓
├── architect (cross-cutting design, specs, ADRs)
├── backend (.NET 8, EF Core, NetMQ, MySQL)
├── desktop-agent (Python desktop agent)
├── browser-extension (Chrome MV3)
├── analyzer-ai (Python analyzer microservices)
├── devops (Docker, CI/CD, build, release)
├── qa (pre-merge verification gate)
├── security (threat review, audits)
  ↓ legacy (still spawnable)
├── frontend (Razor admin UI — no new-roster owner)
├── mobile (Android/iOS — not started)
```

## Directory layout

```
.claude/hats/
├── README.md                    ← this file
├── ceo/                         ← full (9 topic files)
├── devops/                      ← full (5 topic files, imported from .codex)
├── vp-engineering/              ← INDEX stub, grows with use
├── product/                     ← INDEX stub
├── knowledge-manager/           ← INDEX stub
├── architect/                   ← INDEX stub
├── backend/                     ← INDEX stub
├── desktop-agent/               ← INDEX stub
├── browser-extension/           ← INDEX stub
├── analyzer-ai/                 ← INDEX stub
├── qa/                          ← INDEX stub
├── security/                    ← INDEX stub
├── frontend/                    ← INDEX stub (legacy)
└── mobile/                      ← INDEX stub (legacy)
```

## How hats work

| Mode | When | Cost |
|---|---|---|
| **Hat-mode (in-context)** | Trivial work, quick switches | Cheap — read the hat's memory, change style |
| **Sub-agent (real Agent)** | Non-trivial, parallel, or fresh-eyes review | Expensive — isolated context |

Default: hat-mode. Escalate to sub-agent when the task is non-trivial, parallel
work helps, or for the mandatory pre-merge QA review.

## Memory file conventions

- **Short** — 50–100 lines per file. Split past ~200.
- **Concrete** — cite file paths, line numbers, dates, ticket IDs.
- **Action-oriented** — "do X when Y", not "we generally try to..."
- **Markdown** — headings, bullets, tables. No paragraphs > 3 lines.

## Updating memory

When a session reveals durable learning, add it to the right hat directory:

- Insight about Isaac → `ceo/user_profile.md` or `ceo/communication.md`
- Load-bearing decision → `ceo/decisions.md` or `<role>/decisions.md`
- New initiative → `ceo/inflight.md`
- Stack-specific gotcha → `<role>/...` (e.g., `backend/ef-gotchas.md`)
- Verification recipe → `qa/...`
- Docker/infra learning → `devops/...`
