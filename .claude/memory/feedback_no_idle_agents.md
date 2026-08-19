---
name: feedback-no-idle-agents
description: Never leave agents idle — proactively assign the next task when one completes
metadata: 
  type: feedback
---

When a sub-agent completes a task, immediately assign it the next task in the backlog. Don't wait for the user to ask.

**Why:** The user expects the CEO to manage agents like a real manager — when someone finishes their work, you give them the next assignment. Leaving agents idle while there's pending work is wasted time. The user had to ask "why didn't you start Phase 5?" after Phase 4 completed, which should never happen.

**How to apply:** After any agent reports task completion, check the backlog (JIRA, handoff file, or conversation context) for the next task in sequence and delegate immediately. This applies to all agent types — backend, frontend, QA, security, etc. See [[feedback_continue_phases]] for the related rule about not waiting between phases.
