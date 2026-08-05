---
name: gsd-workflow-selection
description: When to use GSD full workflow vs direct delegation — agreed with Isaac 2026-07-29
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 1eefbae1-9500-47b5-9605-6efa76ce9901
---

Use GSD full (research → plan → execute → verify) for new features, architecture changes, and ambiguous-scope work where planning adds clear value.

Use direct delegation + QA gate for well-defined bug fixes, remediation tasks with clear acceptance criteria (e.g., ASPS-607 code review remediation).

Use GSD plan-only for complex tasks where planning helps but execution is straightforward.

**Why:** Isaac agrees that GSD overhead isn't justified for tasks with clear scope/acceptance criteria (like the 21 ASPS-607 remediation items), but wants it for everything else going forward. The planning phase catches issues before implementation starts.

**How to apply:** After ASPS-607 is complete, default to GSD for all new work. For ASPS-607 remaining tasks, continue with direct delegation + QA gate. The rule is also codified in [[charter-gsd-section]] `.claude/team/CHARTER.md` so all agents inherit it.
