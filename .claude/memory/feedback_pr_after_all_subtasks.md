---
name: feedback-pr-after-all-subtasks
description: PR/merge only after ALL sub-tasks of a story are Done — never while work remains open
metadata:
  type: feedback
---

Don't create a PR or merge to main until ALL sub-tasks of the story are completed.

**Why:** ASPS-718 was merged while ASPS-723 (E2E test) was still To Do. The PR should have waited until every sub-task was Done. A story isn't ready for merge while it still has open work.

**How to apply:** Before creating a PR, verify that every sub-task/child issue is Done. If any are still open, the story stays on its branch until they complete.
