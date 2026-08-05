---
name: feedback-continue-phases
description: Continue to next phase without waiting for approval between phases
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 439c5f36-492e-4ea3-b085-789c60b08571
---

When phases complete successfully and the next phase is clear, continue immediately without waiting for "מאשר" / "תמשיך".

**Why:** User explicitly said "למה לא המשכת? תתחיל את השלב הבא". Waiting wastes time when the path forward is obvious.

**How to apply:** After a phase completes with QA PASS and no blockers, launch the next phase immediately. Still pause for: architecture decisions, ambiguous scope, failures, or when user approval is genuinely needed. Update JIRA status transitions as you go.
