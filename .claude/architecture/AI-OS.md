# ASPS AI Operating System — Organization Architecture

How the AI organization is structured, who orchestrates whom, and why.

> Scope: this document describes the **organization of AI agents**. The architecture of
> the *software product* lives in `docs/ARCHITECTURE.md`. The two are separate.

## Hierarchy

```
                        ┌─────────────┐
   human user  ───────► │     CEO     │   executive orchestrator (only one)
                        └──────┬──────┘
            ┌──────────────────┼──────────────────┐
            ▼                  ▼                  ▼
     ┌────────────┐     ┌────────────┐     ┌──────────────────┐
     │  Product   │     │ VP Eng     │     │ Knowledge Manager│   C-level functions
     │ what & why │     │ how (exec) │     │ organizational   │
     └────────────┘     └─────┬──────┘     │ learning         │
                              │            └──────────────────┘
        ┌───────┬───────┬─────┼─────┬───────────┬────────┬────────┐
        ▼       ▼       ▼     ▼     ▼           ▼        ▼        ▼
   architect backend desktop browser analyzer  qa   security  devops
                      -agent  -ext   -ai
                              technical agents (single responsibility each)
```

## Layers

| Layer | Members | Owns |
|---|---|---|
| **Executive** | CEO | Orchestration, workflow choice, priorities, approval, final review. The *only* orchestrator. |
| **C-level** | VP Engineering · Product · Knowledge Manager | Technical execution · problem & priorities · organizational learning. |
| **Technical** | architect, backend, desktop-agent, browser-extension, analyzer-ai, qa, security, devops | Implementation, verification, security, delivery — one responsibility each. |

## Design decisions (and why)

1. **CEO is the only executive orchestrator** (Principle 3). A single point of intent prevents
   conflicting direction. The CEO never writes code — it decides, approves, and reviews.
2. **VP Engineering owns technical execution** (Principle 4). Inserting this layer between the CEO
   and the technical agents gives one accountable technical owner and removes coordination load
   from the CEO. *Improvement over the prior hat-based system, where the CEO delegated to every
   programmer directly.*
3. **Product and Knowledge Manager are C-level, not executive.** The brief grouped them under an
   "executive layer", but they are not orchestrators — they own a domain (problem / learning),
   not the org. Modeling them as C-level functions under the CEO keeps Principle 3 intact.
   *(This is a deliberate correction of the brief, not an oversight.)*
4. **Single responsibility per technical agent** (Principle 2). The prior "Python" role conflated the
   desktop agent and the analyzers; it is split into **desktop-agent** and **analyzer-ai** with an
   explicit boundary (see those charters). Likewise the prior "Frontend" role's extension scope
   becomes **browser-extension**.
5. **Knowledge is a first-class asset** (Principles 5–6). Every completed task is eligible to produce
   organizational learning, routed by the CEO/VP-Eng to the Knowledge Manager and stored as
   schema-backed AIducation content + ADRs. The **Knowledge Engine** under development becomes the
   learning backbone (Principle 7).

## What is preserved from the reference (hat-based CEO)

The orchestration philosophy of the existing CEO is kept intact:
GSD · Mode B (stop between phases) · mandatory QA gate before merge · trust-but-verify ·
no silent side-fixes · destructive-ops-confirm-first · self-contained sub-agent prompts ·
persistent-vs-one-shot agent lifecycles. See `.claude/hats/ceo/`.

## Open items

- **Knowledge Engine location** — TODO: link the in-development engine here once its path is known
  (Principle 7); reference it, do not duplicate.
- **DevOps presence** — the repo has no DevOps tooling yet; the agent is a forward-looking placeholder.

## Map to the filesystem

| Concern | Path |
|---|---|
| Agent charters | `.claude/agents/` |
| Workflows | `.claude/workflows/` |
| Rules | `.claude/rules/` |
| Organizational memory | `.claude/memory/` |
| Learning system | `.claude/aiducation/` |
| Architecture decisions | `.claude/architecture/ADR/` |
