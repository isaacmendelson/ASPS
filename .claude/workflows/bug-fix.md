# Workflow: Bug Fix

Reproduce → diagnose → fix → verify → close. Symptom suppression is not a fix.

## Trigger
A confirmed bug report (from a user, QA, security, or monitoring).

## Roles Involved
CEO (priority) · VP Engineering (assign) · implementer agent (owner of the affected area) ·
QA (gate) · Security (if security-relevant) · Knowledge Manager (lesson).

## Stages
1. **Reproduce** — establish a reliable repro; capture the failing behavior.
2. **Diagnose** — find the **root cause**, not just the symptom.
3. **Fix** — the owning implementer applies the minimal correct fix (no silent side-fixes).
4. **Verify (QA gate)** — QA confirms the bug is gone *and* nothing regressed → PASS.
5. **Close** — VP Eng merges on PASS; CEO confirms to the user.
6. **Learn** — Knowledge Manager records a lesson if the cause was systemic.

## Hand-offs
- Reporter → VP Eng: repro + impact.
- Implementer → QA: fix + the repro to re-test.
- VP Eng → Knowledge Manager: lesson trigger for systemic causes.

## Gates
- **QA PASS** confirming fix + no regression, before merge.
- Security review if the bug is a security finding.

## Definition of Done
- [ ] Root cause understood and documented (not just symptom suppressed).
- [ ] Fix verified against the original repro; no regression (QA PASS).
- [ ] Merged + user informed.
- [ ] Lesson captured if the cause was systemic.
