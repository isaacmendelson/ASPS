---
name: adr
description: Create an Architecture Decision Record (ADR) capturing context, decision, and consequences. Use when an architectural call needs to be defensible later and reproducible by others.
---

# /adr

Generates a numbered ADR in `docs/adrs/` capturing a load-bearing architectural decision. ADRs sit between design docs (which describe a system) and commit messages (which describe a change) — they answer "why this choice, not the alternative".

## When to invoke
- User makes an architectural choice that's hard to reverse (e.g., choosing Velopack Option B over A, choosing structured-JSON URS over scalar, choosing Razor over Angular for the admin UI).
- User says "ADR", "architecture decision", "document this decision", "write up why we chose X".
- A sub-agent (typically CTO hat) has finished evaluating options and wants the decision recorded.

## Don't write an ADR for
- Tactical choices (which method name, which exception to throw, formatting). Those go in code review.
- Decisions that are obviously the only viable path. ADRs capture *tradeoffs* — if there was no real alternative, the document has nothing to say.
- Things already covered by an existing design doc. Link to the design doc instead.

## Ask first

1. **What decision was made?** One sentence. ("We will use Velopack's `Update.exe apply` CLI directly, not the Python `UpdateManager`.")
2. **What alternatives were considered?** At least one. ("Option A: Python `UpdateManager` with feed; Option B: shell out to `Update.exe`.")
3. **Why this option won?** The reasons that mattered, not all reasons.
4. **What are the consequences?** Both costs and benefits. ADRs are evaluated honestly — include the downsides.
5. **Who decided + when?** Often the user, sometimes after a CTO sub-agent review.

If the user can't answer 1-4 succinctly, the decision isn't ripe — propose a CTO-agent review first to clarify, then come back.

## File layout

Path: `docs/adrs/NNNN-<short-slug>.md` — zero-padded sequence, kebab-case slug.

Pick the next number by listing `docs/adrs/` (`ls docs/adrs/`); if the directory doesn't exist yet, this is `0001-*`.

## Template

```markdown
# ADR NNNN: <Title>

- **Status:** Accepted | Superseded by [ADR NNNN](./NNNN-...) | Deprecated
- **Date:** YYYY-MM-DD
- **Deciders:** Isaac (+ CTO agent review on YYYY-MM-DD if applicable)
- **Related:** [SCRUM-###](https://jira-link), [design doc](../<file>.md), [ADR NNNN](./NNNN-...)

## Context

What forced the decision now. The constraints, the failed attempts, the deadline, the dependency. 2–4 paragraphs. State the *problem*, not the *solution*.

## Decision

The chosen path. **One paragraph.** Imperative voice: "We will use X to Y."

## Alternatives considered

For each: one paragraph saying what it was and why it didn't win. Be honest about what it *would* have done well — pretending the rejected option had no upside makes the ADR less trustworthy.

### Option A — <Name>
What it was. Pros. Cons. Why it lost.

### Option B — <Name>
What it was. Pros. Cons. Why it lost.

## Consequences

### Positive
- Concrete benefit.
- Concrete benefit.

### Negative
- Concrete cost or constraint we now have to live with.
- Concrete cost or constraint we now have to live with.

### Neutral / Open
- Things we don't yet know but will need to monitor.

## References

- Code: <file or commit>
- Docs: <design doc, JIRA, external link>
- Prior art / inspiration if any.
```

## Status lifecycle

- **Accepted** — the live decision. Most ADRs stay here forever.
- **Superseded by ADR NNNN** — a newer ADR replaces it. Update the old ADR's status; don't delete it.
- **Deprecated** — the decision no longer applies but no replacement was made (the system moved past the question). Rare.

Never delete an ADR. Even superseded ones explain history.

## Verification

- Filename matches `NNNN-<slug>.md` and increments correctly.
- All template sections are present (no `<placeholder>` text remaining).
- Each alternative actually has a real reason it lost.
- "Consequences → Negative" is not empty. If it is, you haven't thought hard enough about the decision — push back to the user.

## Output convention

```
ADR: docs/adrs/NNNN-<slug>.md
Status: Accepted
Decision: <one-sentence summary>
Alternatives recorded: <count>
Related: <links>
```

## Examples of decisions worth ADRs in this codebase

For reference — these are real choices that should have been ADRs if they weren't already:

- SCRUM-863 Velopack "Option B" (shell out to `Update.exe`, not Python `UpdateManager`).
- SCRUM-904 structured URS object (Score + Level + Confidence + AxisScores + …), rejecting scalar-only.
- SCRUM-904 logistic risk function with explicit k, θ — rejecting linear and rejecting per-user neural net.
- SCRUM-906 CWS + control-plane reuse, rejecting self-hosted `.crx` (Chrome MV3 forbids for consumers).
- Keeping ports 5555 / 5556 bound to `*:` (security debt) — explicitly deferred, should have an ADR explaining the deferral and the migration path.
