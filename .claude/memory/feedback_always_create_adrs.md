---
name: feedback-always-create-adrs
description: "Always create ADR documents when architecture decisions are made — don't wait for user to ask"
metadata: 
  type: feedback
---

Always create an ADR (Architecture Decision Record) in `docs/architecture/decisions/` when architecture decisions are made during a session.

**Why:** Architecture decisions are important organizational knowledge that must be captured immediately, not retroactively. The user expects ADRs to be created proactively as part of the decision-making process.

**How to apply:** After an architecture decision is agreed upon, create an ADR file following the existing format (`ADR-NNN-JIRA-ID-TITLE.md`) in `docs/architecture/decisions/`. Don't wait for the user to ask — treat it as a standard step in the decision workflow. Number sequentially from the last existing ADR. See [[feedback_ceo_no_coding]] for delegation context.
