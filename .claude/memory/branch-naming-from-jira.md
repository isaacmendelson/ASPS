---
name: branch-naming-from-jira
description: How to name a git branch created for a JIRA task.
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 79421ab0-5013-4f83-811d-ef1680a5ec72
---

When opening a git branch for a JIRA task, the branch name is **always**: the JIRA task **number**, then `-`, then the JIRA task **title** — with every space in the title replaced by a hyphen `-`.

Example: JIRA task SCRUM-863 titled "Auto-Update Agent Desktop-Win to Latest Version" → branch `863-Auto-Update-Agent-Desktop-Win-to-Latest-Version`.

**Why:** every branch is tied to its JIRA task at a glance — consistent, greppable, traceable.

**How to apply:** take the numeric part of the JIRA key (e.g. SCRUM-863 → `863`) + the *current* task title; replace every ` ` with `-`; hyphens already in the title stay as-is. Verify the number against the actual JIRA task — if a number typed in passing doesn't match the task being worked on, surface the mismatch and confirm before creating the branch (don't trust a number on faith). See [[reference_jira]] for the JIRA connection.
