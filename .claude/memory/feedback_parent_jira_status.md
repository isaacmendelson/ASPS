---
name: feedback-parent-jira-status
description: "Update parent JIRA epic/story to In Progress when first child issue work begins, not when user reminds"
metadata: 
  type: feedback
---

Update the parent JIRA issue to In Progress automatically when work begins on any child issue — don't wait for the user to ask.

**Why:** ASPS-675 stayed in To Do while Phases 0-3 were already completed. The user had to explicitly request the status update, which should have been automatic.

**How to apply:** When delegating or starting work on a sub-task/child issue, check the parent's status first. If it's still To Do, transition it to In Progress before starting the child work. This applies to epics, stories with sub-tasks, and any parent-child JIRA hierarchy.

Related: [[feedback-jira-auto-sync]], [[branch-naming-from-jira]]
