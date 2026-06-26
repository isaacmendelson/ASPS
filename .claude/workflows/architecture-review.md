# Workflow: Architecture Review

Evaluate and decide on a cross-cutting design change, and record the decision.

## Trigger
A proposed cross-cutting change, a recurring design tension, or a decision the architect flags as significant.

## Roles Involved
CEO (frame, approve) · VP Engineering (commission) · Architect (lead) ·
Security (sensitive designs) · Knowledge Manager (ADR repository) · affected implementer agents (input).

## Stages
1. **Frame the decision** — state the problem, constraints, and forces (architect, with VP Eng).
2. **Explore options** — architect produces ≥2 viable options with trade-offs.
3. **Evaluate** — review against existing architecture, rules, security; gather implementer input.
4. **Decide & record** — choose; write an **ADR** under `.claude/architecture/ADR/`; supersede prior ADRs if needed.

## Hand-offs
- VP Eng → Architect: the decision to be made.
- Architect → Security: designs touching auth/crypto/data.
- Architect → Knowledge Manager: ADR content (KM owns format/repository).

## Gates
- No significant decision is "done" until it is an **accepted ADR**.
- Security sign-off required for designs on sensitive paths.

## Definition of Done
- [ ] Problem + constraints framed; ≥2 options compared with trade-offs.
- [ ] Decision made and recorded as an accepted ADR (`NNNN-title.md`).
- [ ] Superseded decisions linked.
- [ ] Affected agents informed of the new constraint.
