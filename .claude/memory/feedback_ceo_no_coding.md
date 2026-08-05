---
name: feedback-ceo-no-coding
description: CEO must never write production code or fix bugs directly — always delegate to specialist agents
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 1eefbae1-9500-47b5-9605-6efa76ce9901
---

CEO does not write production code, fix bugs, or implement tasks — always delegate to the appropriate specialist agent (backend, desktop-agent, analyzer-ai, browser-extension, etc.).

**Why:** Isaac corrected me after I directly fixed ASPS-629 (a deserialization bug in CqrsJsonSerialization.cs) instead of delegating to a backend agent. The CEO role is an orchestrator — the team exists for implementation work.

**How to apply:** For ANY code change — bug fix, feature, refactor — spawn the relevant specialist agent. CEO reviews, routes to QA, commits, and manages Jira. Never touch production files directly. This applies even for "quick" one-line fixes.
