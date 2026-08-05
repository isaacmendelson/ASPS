---
name: feedback-jira-auto-sync
description: JIRA must be updated automatically when delegating work — create sub-tasks and set In Progress before agents start
metadata:
  type: feedback
  originSessionId: 429ee8b3-85e1-42ff-b1b0-f7d623110a6e
  modified: 2026-08-05T15:06:38.287Z
---

When delegating work to agents, JIRA must be updated **before** the agents start, not after they finish. This includes:

1. **Create sub-tasks** in JIRA under the parent issue for each agent/work-stream (backend, frontend, etc.)
2. **Set all tasks to In Progress** — the parent issue AND the sub-tasks
3. **Don't mark Done prematurely** — a task is Done only when the full requirement is met (compare to the existing working version, e.g., Razor)

**Why:** Isaac expects JIRA to reflect real-time work status. If agents are working, JIRA should show it. He noticed ASPS-660 was closed as Done when only a metadata popup was added, while the Razor version shows a full interactive roadmap.

**How to apply:**
- At the start of any delegated work: create JIRA sub-tasks, transition parent + sub-tasks to In Progress
- At agent completion: transition sub-task to Done (or In Review if QA needed)
- Only transition parent to Done when ALL sub-tasks pass and the feature matches acceptance criteria
- Use issue type "Subtask" (id: 10010) with `parent: { key: "ASPS-XXX" }`
- Transition IDs: 11=To Do, 21=In Progress, 31=In Review, 41=Done

Related: [[reference-jira]], [[reference-access-keys]]
