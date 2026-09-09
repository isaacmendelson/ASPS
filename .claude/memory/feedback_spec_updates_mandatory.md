---
name: feedback-spec-updates-mandatory
description: Every task must update affected specification/design documents as part of completion; undocumented features found during bug fixes must be added to specs
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 429ee8b3-85e1-42ff-b1b0-f7d623110a6e
  modified: 2026-08-24T22:46:06.907Z
---

Updating specification and design documents (ICD, SRS, system specs) is mandatory as part of completing every task — not a separate activity.

**Why:** Isaac explicitly required this as an integral part of task workflow. Spec drift was causing features to exist in code but not in documentation, making the specs unreliable.

**How to apply:**
- Every implementing agent checks affected spec docs before requesting QA (pre-QA gate item 9)
- If a bug fix touches a feature not yet in the specs, add the feature to the spec
- QA verifies spec consistency — missing update is a Minor finding
- Rule defined in `.claude/rules/task-workflow.md#specification-update-rule`
- Referenced in Definition of Done of: backend, desktop-agent, browser-extension, analyzer-ai, devops
- Referenced in QA responsibilities

Related: [[feedback_update_azure_docs]], [[feedback_always_create_adrs]]
