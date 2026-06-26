# Workflow: Knowledge Update

Turn a completed task, decision, or incident into structured, reusable organizational knowledge.

> Principle 6: **every completed task is eligible to produce organizational learning.**

## Trigger
A completed feature/bug-fix/release, a decision made, an incident, or a recurring question.

## Roles Involved
Knowledge Manager (owner) · CEO / VP Engineering (learning triggers) · Architect (ADRs) · any agent (source).

## Stages
1. **Capture** — KM gathers what happened, the decision, and the outcome from the source agent.
2. **Classify** — route to the right AIducation category: principle · lesson · prompt · role-training · ADR.
3. **Structure** — encode it against the matching schema in `.claude/aiducation/schemas/`.
4. **Curate** — de-duplicate against existing knowledge; supersede stale entries.
5. **Publish & feed back** — store under `.claude/aiducation/`; make it retrievable / update role training.

## Hand-offs
- CEO/VP Eng → KM: the learning trigger (what completed, what was decided).
- Architect → KM: decision content for ADRs.

## Gates
- Entry must be **schema-conformant** before publishing.
- Decisions affecting architecture must land as an **ADR**.

## Definition of Done
- [ ] Learning captured and classified into the correct category.
- [ ] Schema-conformant; duplicates removed, stale entries superseded.
- [ ] Stored under `.claude/aiducation/` and made retrievable.
- [ ] Relevant agents' role training updated if affected.
