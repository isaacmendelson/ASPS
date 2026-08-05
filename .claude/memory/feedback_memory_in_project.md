---
name: feedback-memory-in-project
description: All memory files must be saved in the project repo (.claude/memory/) not just Claude's auto-memory path
metadata:
  type: feedback
  originSessionId: 429ee8b3-85e1-42ff-b1b0-f7d623110a6e
  modified: 2026-08-05T15:28:09.863Z
---

Memory files must be saved to `.claude/memory/` inside the project repo (`C:\Jobs\ASPS\GitHub\Software\.claude\memory\`), not only in Claude Code's auto-memory path (`C:\Users\Isaac\.claude\projects\...\memory\`).

**Why:** The auto-memory path is only accessible to Claude Code. Other AI agents working on the same project directory can't see it. Memory must be git-tracked and accessible to all agents.

**How to apply:**
- When saving a new memory, write to BOTH locations:
  1. `C:\Jobs\ASPS\GitHub\Software\.claude\memory\` (project — git-tracked, shared)
  2. `C:\Users\Isaac\.claude\projects\C--Jobs-ASPS-GitHub-Software\memory\` (auto-memory — Claude Code reads MEMORY.md on session start)
- Update MEMORY.md in both locations
- The project copy is the canonical source; the auto-memory copy mirrors it
