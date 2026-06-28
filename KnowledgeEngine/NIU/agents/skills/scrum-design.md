---
name: scrum-design
description: Produce a SCRUM-NNN design document in docs/ matching the SCRUM-904 template — context, data model, formula/algorithm, architecture, phasing, decisions. Use before implementation begins on a non-trivial ticket.
---

# /scrum-design

Generates a comprehensive design document at `docs/SCRUM-NNN-<short-slug>.md` before any implementation work begins on a non-trivial ticket. The reference template is [SCRUM-904-user-risk-score-design.md](c:/Jobs/ASPS/GitHub/Software/docs/SCRUM-904-user-risk-score-design.md).

## When to invoke
- A non-trivial JIRA ticket has been assigned and implementation is about to start.
- User says "design doc", "design for SCRUM-###", "let's design <feature> before coding".

## Don't write a design doc for
- Bug fixes. A commit message + tests are sufficient.
- Single-file changes. Inline comments + the PR description carry the context.
- Refactors that don't change behavior. ADRs are the better surface if there's a debatable choice.

A design doc is for **new subsystems**, **multi-component features**, and anything where someone joining the project six months later will need the *why*, not just the *what*.

## Ask first

1. **JIRA number** — `SCRUM-###` exact. This drives the filename and the cross-link.
2. **One-sentence goal** — what does this feature deliver to the user / system?
3. **Scope boundary** — what's out of scope for this ticket? (Most useful section to write first; prevents creeping requirements.)
4. **Has any design conversation happened?** If so, what's been decided so far? If not — propose a CTO-agent review before writing the doc.

If the user can't state the scope boundary, the ticket isn't ripe for design. Push back, propose breaking it down.

## Filename

`docs/SCRUM-NNN-<kebab-slug>.md` — slug is 2–4 words describing the feature. Examples:
- `docs/SCRUM-904-user-risk-score-design.md`
- `docs/ASPS-352-DESIGN.md` (older convention; new docs follow SCRUM-NNN format)

## Template — adapt sections to the ticket

Not every section is required. Skip sections that don't apply, but don't reorder — the structure helps readers scan.

```markdown
# SCRUM-NNN: <Feature Title>

**Status:** Draft | Approved | Implemented (link to commits) | Superseded
**Last updated:** YYYY-MM-DD
**Owner:** Isaac
**Related:** [JIRA SCRUM-NNN](<link>), [ADR NNNN](./adrs/NNNN-...), [parent design](./SCRUM-MMM-...)

---

## 0. TL;DR

A 3–5 sentence summary. Anyone reading just this section should know what we're building and why. Place at the top so reviewers don't have to read the whole doc to decide if they care.

## 1. What is <feature>?

The conceptual definition. **What is it, who is it for, what does it deliver?** Avoid implementation here — describe the *thing*, not the *code*.

## 2. The conceptual model

The mental model a developer needs before reading the rest. Diagrams, terminology, the units of the system. If the feature has a formula, state it abstractly here and derive it in §5.

## 3. Inputs / data model

What does the feature consume? Entities, signals, events, configuration. For each input, state:
- Source (DB table, NetMQ event, user input, …)
- Shape (entity + key fields)
- Cardinality (one, many, per-user, per-device, …)
- Consent / privacy constraint, if any.

## 3.5. Consent / privacy / configuration

If the feature touches user data with non-trivial privacy implications, give consent its own section. Borrow the consent-ladder pattern from SCRUM-904 if relevant. State default proposals + per-user override semantics.

## 4. Data-source landscape

A table of every backend source the feature reads from / writes to. Useful when a feature spans Backend + Python agent + extension + mobile.

| Source | Read / Write | Owner | Frequency |
|---|---|---|---|
| `UserRiskProfile` table | R/W | Backend | per-event + batch |
| Inbound SMS log | R | Mobile agent | per-message |
| ... | ... | ... | ... |

## 5. The algorithm / formula — layered

If the feature has computation, break it into layers (L1 per-event, L2 per-dimension, L3 axis, L4 final). Show the formula, the constants, and at least one worked numerical example per layer.

## 6. Why this approach — and what was rejected

Honest tradeoff section. If a non-trivial choice was made (e.g. logistic vs linear, structured object vs scalar, event-driven vs polling), explain the alternatives and why each lost. Promote to a separate ADR if the choice is load-bearing across other features.

## 7. Parameters / weights / thresholds — initial values

State all magic numbers up front:
- Initial value
- Source (literature, expert estimate, baseline measurement)
- Tuning plan (manual, A/B, auto-correction)

This section ages well — future tuning can be tracked against the baseline here.

## 8. Architectural placement

Concrete file paths, class names, project layout. Where does this code live?
- Common entities → `Common/Entities/`
- Repositories → `Interface/Repositories/` + `Business/Data/EF/Repositories/`
- Domain logic → `Business/<Subsystem>/`
- CQRS → `Business/Queries|Commands|Handlers/` + `Business/Messaging/CQRSGateway.cs`
- WebApi → `WebApi/Pages/<Folder>/`
- Migrations → `Business/Data/EF/Migrations/`

Diagram of the request/data flow if multi-component.

## 9. Practical phasing — what to build first

Numbered phases A → … with concrete deliverables per phase. Each phase should be independently mergeable (build green, no half-implementations).

Example:
- **Phase A** — Foundation: schema + repositories
- **Phase B** — Algorithm: per-event compute
- **Phase C** — Wiring: event-driven service + CQRS
- **Phase D** — Surface: admin Razor page
- **Phase E** — Tuning (deferred)

## 10. Decisions — resolved

Append-only log of decisions made during design.

> **2026-MM-DD** — Decided X over Y. Reason: …

Use this section instead of editing earlier sections silently. Future readers need to see what changed.

## 11. Open questions / follow-ups

Things explicitly deferred. Each item should name an owner or "TBD" + a trigger event.

## 12. Summary

Bullet list — what we're building, what we explicitly aren't, the success criterion.
```

## Verification

- **Filename matches** `SCRUM-NNN-<slug>.md`.
- **TL;DR is at the top** and can stand alone.
- **At least one rejected alternative** in §6. If there were none, the design wasn't worth documenting.
- **Phasing is real** — each phase is independently mergeable, not "Phase A: write the code; Phase B: test it".
- **No `<placeholder>` text left.**
- **Cross-links resolve** — JIRA, ADRs, related design docs.

## Output convention

```
Design doc: docs/SCRUM-NNN-<slug>.md
Status: Draft
Sections: <count of populated sections / total>
Phases: A, B, C, ... (mergeable independently: yes/no)
Open questions: <count>
Related: JIRA SCRUM-NNN, ADR ..., ...
```

## Never

- Bury the goal in section 4. Put it in TL;DR.
- Use vague phasing ("Phase 2: finish phase 1"). Each phase should produce something running.
- Skip the rejected-alternatives section. If you have nothing to put there, the design isn't ripe — push back.
- Document an implementation that already exists. Past-tense design docs aren't design — they're archaeology. Either write the doc *before* code, or write it up as an ADR if the decision is what mattered.
