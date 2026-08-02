# ASPS-642 Angular Admin Client — Epic Handoff

**Status:** DONE — merged to main  
**Branch:** `asps-642-angular-admin-client` (deleted)  
**Merge commit:** `0409caa`  
**Last updated:** 2026-08-02  

## JIRA Status

| Story | Title | Status |
|---|---|---|
| ASPS-642 | Angular Admin Client (Epic) | Done |
| ASPS-643 | Spec + Architecture | Done |
| ASPS-644 | Foundation + Hat System | Done |
| ASPS-645 | Backend: JWT Auth + CORS + Paging | Done |
| ASPS-646 | Backend: Dashboard + Users API | Done |
| ASPS-647 | Backend: Devices + Alerts + Analysis API | Done |
| ASPS-648 | Backend: Blacklists API | Done |
| ASPS-649 | Backend: Simulations + Roadmaps + System API | Done |
| ASPS-650 | Frontend: Scaffold + Auth + Layout | Done |
| ASPS-651 | Frontend: Shared Components | Done |
| ASPS-652 | Frontend: Dashboard + Users Pages | Done |
| ASPS-653 | Frontend: Devices + Alerts + Analysis Pages | Done |
| ASPS-654 | Frontend: Blacklists Pages | Done |
| ASPS-655 | Frontend: Simulations + System + Downloads | Done |
| ASPS-656 | DevOps: Docker Container | Done |

## Verification

- .NET build: 0 errors, 296 warnings (pre-existing)
- .NET tests: 1645 passed, 0 failed, 7 skipped
- Angular build: success (budget warning pre-existing)
- Angular tests: 303 passed, 0 failed
- Code review: PASS (backend, frontend, devops — 3 parallel reviews)
- QA: All stories PASS

## Code Review Findings (Minor — tracked for follow-up)

1. TrackedDomains 500-row page limit in notify endpoints (backend)
2. Missing Content-Security-Policy header in nginx.conf (devops)
3. API_URL default undocumented for non-dev deployments (devops)
4. 5 near-identical blacklist state services — DRY violation (frontend)
5. Dead devicesResult/alertsResult signals in user-detail (frontend)
6. RoadmapsApiController.Create returns StatusCode(201) instead of CreatedAtAction (backend)
7. SignalR nginx location missing X-Forwarded-Proto (devops)

## Completed

- Merged to main: `0409caa` (2026-08-02)
- Epic branch deleted (local + remote)
- ASPS-642 Epic transitioned to Done in JIRA
