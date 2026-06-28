# Delegation Rules

When to do work myself (CEO hat) vs spawn a sub-agent.

## The roles available

| Role | Stack / Domain | Memory at |
|---|---|---|
| **CTO** | Architecture, cross-cutting, spec breakdown | `cto/` (TBD) |
| **Backend programmer** | C# / .NET 8 / EF Core / NetMQ / MySQL | `backend/` (TBD) |
| **Frontend programmer** | Razor pages, CSS, vanilla JS, Chrome extension | `frontend/` (TBD) |
| **Python programmer** | apps/desktop/win/, Analyzers/, pyzmq | `python/` (TBD) |
| **Mobile programmer** | Android/iOS — when the project starts | `mobile/` (TBD) |
| **QA** | Verify code before merge | `qa/` (TBD) |

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

## Sub-agent prompt template

When spawning, the prompt must be **self-contained**:

```
Role: [Backend programmer / QA / etc.]
Context: [The minimum the agent needs to know about ASPS]
Memory: [Paste the contents of relevant hats/<role>/ files]
Task: [Concrete, with acceptance criteria]
Output: [What to return to me]
```

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
