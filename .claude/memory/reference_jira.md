---
name: jira-instance
description: ASPS JIRA on isaacmendelsonjira.atlassian.net — project ASPS, REST via direct URL, API token in ACCESS_KEYS.env
metadata:
  type: reference
---

## JIRA Instance

- **URL:** https://isaacmendelsonjira.atlassian.net
- **Project key:** `ASPS`
- **REST base:** `https://isaacmendelsonjira.atlassian.net/rest/api/3/`
- **Auth:** HTTP Basic — email + API token from `ACCESS_KEYS.env`

## Issue types

| ID | Name | Subtask? |
|---|---|---|
| 10009 | Epic | No |
| 10011 | Task | No |
| 10012 | Story | No |
| 10013 | Feature | No |
| 10014 | Bug | No |
| 10010 | Subtask | Yes |

## Notes

- The old on-prem instance (`http://187.124.10.197:8080/`) and old Cloud instance (`aspsjira.atlassian.net`) are no longer active. All work is on `isaacmendelsonjira.atlassian.net`.
- The token in `ACCESS_KEYS.env` works directly against the site URL — no need for the `api.atlassian.com/ex/jira/{cloudId}` gateway (that was only needed for scoped tokens on the old instance).
