---
name: ASPS JIRA Instances
description: Two JIRA instances tracking ASPS work — the legacy on-prem one and the new Atlassian-cloud one. Tasks are mirrored across both and matched by title.
type: reference
originSessionId: 79421ab0-5013-4f83-811d-ef1680a5ec72
---
ASPS uses **two parallel JIRA instances** during a migration period:

- **Old (legacy, on-prem)**: http://187.124.10.197:8080/
- **New (Atlassian Cloud)**: https://aspsjira.atlassian.net

Tasks have been migrated by title; statuses can drift between the two. When the user says "update statuses in new JIRA from old JIRA", they mean: for each task in the new instance, find the matching title in the old instance and copy its status. Match key is the **summary/title** (not key/ID — those differ).

Tokens are session-scoped: the user shared API tokens in chat, but they should be revoked after each session. Don't store tokens in memory or any committed file.

When checking JIRA tasks vs codebase implementation status, use this two-instance lookup. Recent project keys we've seen: `ASPS-*` (e.g., ASPS-282, ASPS-297, ASPS-318, ASPS-337, ASPS-352) and `SCRUM-*` (e.g., SCRUM-820, SCRUM-821, SCRUM-822).

## New JIRA (Atlassian Cloud) — REST connection details (non-secret)

Verified working 2026-05-22 against `aspsjira.atlassian.net`.

- **cloudId:** `e1a5acf4-fe07-49ff-b3cb-a8e43d5b2de4` (public — `GET https://aspsjira.atlassian.net/_edge/tenant_info` returns it unauthenticated).
- **Auth:** HTTP Basic — `email:api_token`. Email = `isaacmendelson@gmail.com` (Atlassian account "isaac", accountId `557058:94ddd429-c61d-43aa-9aec-9d470e5a34c1`).
- **Project:** `SCRUM` ("asps") is the only project on this instance.

**Gotcha — scoped vs classic API tokens:**
The user creates **scoped** API tokens ("Create API token with scopes" — they show a "View scopes" link). Scoped tokens return **401** against the direct site URL `https://aspsjira.atlassian.net/rest/api/3/...`. They authenticate **only** through the gateway:
`https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/...`
So the working base URL is:
`https://api.atlassian.com/ex/jira/e1a5acf4-fe07-49ff-b3cb-a8e43d5b2de4/rest/api/3/`
Don't burn time re-diagnosing 401s — if a JIRA token 401s on the site URL, switch to the `api.atlassian.com/ex/jira/{cloudId}` gateway first.
