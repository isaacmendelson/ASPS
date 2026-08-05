---
name: feedback-jira-agent-labels
description: Always set JIRA Labels field with the handling agent name(s) when creating or updating issues
metadata:
  type: feedback
  modified: 2026-08-05
---

When creating or updating JIRA issues, always set the Labels field with the agent(s) handling the work (e.g. `backend`, `frontend`, `ceo`, `qa`, `security`).

**Why:** Isaac expects JIRA to show which agent handled each task. He noticed ASPS-667 was missing the label after it was closed.

**How to apply:**
- When delegating work: set the label on the JIRA issue at the same time as transitioning to In Progress
- Parent issues get the orchestrator label (ceo) plus any agent labels
- Subtasks get their specific agent label
- Use the agent role name as the label (e.g. `backend`, `frontend`, `desktop-agent`, `qa`, `security`)

Related: [[feedback-jira-auto-sync]]
