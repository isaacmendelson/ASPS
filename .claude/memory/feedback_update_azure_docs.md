---
name: feedback-update-azure-docs
description: Always update Azure deployment docs when Azure infrastructure or CI/CD changes
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 429ee8b3-85e1-42ff-b1b0-f7d623110a6e
  modified: 2026-08-19T18:34:30.845Z
---

Any change to Azure infrastructure, CI/CD pipeline, Container Apps, or cloud configuration must be reflected in the deployment documentation before the work is considered done.

**Documents to update:**
- `docs/cloud/AZURE_DEPLOYMENT_GUIDE.md` — step-by-step guide, current state table, architecture decisions
- `docs/cloud/AZURE_ARCHITECTURE.md` — architecture overview, resource inventory, networking
- `docs/cloud/ASPS_Azure_Architecture.html` — visual diagram
- `docs/cloud/azure/troubleshooting.md` — any new issues encountered and their solutions
- `docs/task-memory/<TASK>_HANDOFF.md` — task-specific status (separate from the guide)

**Why:** The user emphasized "הידע שאנחנו צוברים חייב להישמר" — knowledge we accumulate must be preserved. Documentation drift was caught when Step 13 (CI/CD) was done but the guide still said "IN PROGRESS" and showed the old pipeline structure.

**How to apply:** At the end of any Azure-related task, review all five documents above and update any that are stale. Don't confuse the handoff (task tracking) with the guide (permanent reference). Troubleshooting gets every new problem+solution pair.
