---
name: reference-access-keys
description: Location of ACCESS_KEYS.env with GitHub and JIRA tokens — check here before asking the user
metadata: 
  node_type: memory
  type: reference
  originSessionId: 429ee8b3-85e1-42ff-b1b0-f7d623110a6e
  modified: 2026-08-05T13:40:39.558Z
---

External service credentials are stored in `ACCESS_KEYS.env` in the project root (`C:\Jobs\ASPS\GitHub\Software\ACCESS_KEYS.env`).

**Why:** The user has told us multiple times where this file is. Stop asking.

**How to apply:** At the start of any task that needs GitHub or JIRA access, read this file. Never copy its values into docs, handoffs, logs, commits, or responses. The file is gitignored.

Contains: GitHub token, JIRA base URL + email + API token.
