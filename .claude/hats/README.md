# Hats — Role-Based Memory & Working Modes for ASPS

This folder defines the **roles** Claude can adopt while working on ASPS, and the **per-role memory** (insights, conventions, learnings, regression watches) accumulated over sessions.

## The hierarchy

```
User (the boss — Isaac)
  ↓ talks to
CEO (Claude in default mode) ← entry point for every conversation
  ↓ delegates technical work to
CTO (sub-agent — architecture, cross-cutting decisions)
  ↓ breaks down + assigns to
├── Backend programmer  (C#/.NET, EF, NetMQ, MySQL)
├── Frontend programmer (Razor pages, CSS, JS, browser extension UI)
├── Python programmer   (desktop agent, analyzers)
└── Mobile programmer   (Android/iOS — when those start)
                            ↓ all merges go through
QA (sub-agent — gate on every non-trivial commit)
```

## How "wearing a hat" works

Two modes:

| Mode | When | Cost | What it gives |
|---|---|---|---|
| **Hat-mode (in-context)** | Trivial work, quick switches | Cheap | I change my style/checklist, read the hat's memory before starting |
| **Sub-agent (real Agent)** | Non-trivial work, parallel work, fresh-eyes review | Expensive (~150K tokens) | Isolated context, no shared bias, can run in parallel |

**Default:** start in hat-mode. Escalate to sub-agent when (a) the task is non-trivial, (b) parallel work helps, or (c) before any merge for QA review.

## Folder layout

```
.claude/hats/
├── README.md                 ← this file
├── ceo/                      ← CEO mode (default)
│   ├── INDEX.md              ← read first at session start
│   ├── identity.md
│   ├── user_profile.md
│   ├── communication.md
│   ├── operating_principles.md
│   ├── delegation.md
│   ├── decisions.md
│   └── inflight.md
├── cto/                      ← (built later)
├── backend/                  ← (built later)
├── frontend/                 ← (built later)
├── python/                   ← (built later)
└── qa/                       ← (built later — most important after CEO)
```

## Conventions for memory files

- **Short** — 50-100 lines per file. If a file grows past ~200 lines, split it.
- **Concrete** — cite file paths, line numbers, dates, ticket IDs.
- **Append-only-ish** — when something is wrong, mark it stale instead of deleting (history matters).
- **Markdown** — headings, bullets, tables. No prose paragraphs longer than 3 lines.
- **Action-oriented** — "do X when Y", not "we generally try to..."

## Updating memory

Whenever a session reveals a learning that future-me would benefit from, append to the right hat's memory:

- General insight about how to work with Isaac → `ceo/user_profile.md` or `ceo/communication.md`
- Decision that's now load-bearing → `ceo/decisions.md`
- New initiative → `ceo/inflight.md`
- Code pattern / gotcha in C# / EF → `backend/...`
- Code pattern in Razor / CSS / JS → `frontend/...`
- Code pattern in Python → `python/...`
- Architecture decision → `cto/...`
- Verification recipe / regression to watch → `qa/...`

The line `MEMORY.md → hats/<role>/INDEX.md` is the chain that gets me here at session start.
