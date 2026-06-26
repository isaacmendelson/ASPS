# Workflow: Feature Development

Idea → spec → design → build → verify → merge. The default path for new functionality.

## Trigger
A user/CEO-approved feature request.

## Roles Involved
CEO (orchestrate, approve) · Product (requirements) · VP Engineering (technical exec) ·
Architect (design) · implementer agent(s) · QA (gate) · Security (sensitive paths) ·
Knowledge Manager (capture learning).

## Stages
1. **Define** — Product writes user stories + verifiable acceptance criteria; CEO approves scope.
2. **Design** — VP Eng commissions the architect; significant decisions become ADRs.
3. **Build** — VP Eng assigns the implementer agent(s) per ownership boundary.
4. **Verify (QA gate)** — QA verifies against acceptance criteria → PASS/FAIL. Security reviews sensitive paths.
5. **Merge** — VP Eng merges only on QA PASS; CEO does the final review.
6. **Learn** — Knowledge Manager captures lessons/ADRs from the completed feature.

## Hand-offs
- Product → VP Eng: acceptance criteria.
- Architect → implementer: design + ADRs.
- Implementer → QA: change + hand-off summary.
- VP Eng → CEO: consolidated result. → Knowledge Manager: learning trigger.

## Gates
- **QA PASS** is mandatory before merge.
- **Security review** required when the change touches auth / crypto / secrets / deserialization.

## Definition of Done
- [ ] Acceptance criteria met and QA-verified (PASS).
- [ ] Security review passed where applicable.
- [ ] Significant decisions recorded as ADRs.
- [ ] Merged + final-reviewed by CEO.
- [ ] Learning captured by the Knowledge Manager.
