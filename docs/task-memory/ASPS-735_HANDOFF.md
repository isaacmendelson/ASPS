# ASPS-735 — Fix Keycloak CrashLoopBackOff due to misconfigured startup probe

**JIRA:** ASPS-735 | **Status:** In Review (transitioned 2026-08-25) | **Labels:** devops
**Branch:** `asps-735-fix-keycloak-crashloopbackoff` | **PR:** https://github.com/isaacmendelson/ASPS/pull/37
**Agent:** devops

## Problem

`ca-keycloak-dev` was in CrashLoopBackOff (810+ restarts, replica `ready: false`). Keycloak
started fine internally but the startup probe never succeeded, so Container Apps killed the
container roughly every ~8.5 minutes (the 530s startup probe window).

## Root cause

The startup/liveness probes were already configured with the *correct paths* (`/health/started`,
`/health/live`) but targeted **port 8080** (the main app port), and `KC_HEALTH_ENABLED` was never
set. Keycloak 26 only serves `/health/*` (and `/metrics`) on a separate **management interface**,
port **9000** by default, and only once the health subsystem is explicitly enabled. Every probe
request to `8080/health/started` returned 404, exhausting `failureThreshold: 50 × periodSeconds:
10` (530s) on every cycle — an infinite restart loop.

Confirmed via startup log after the fix:
```
Keycloak 26.0.8 on JVM ... Listening on: http://0.0.0.0:8080. Management interface listening on http://0.0.0.0:9000.
```

## Fix

Config-only change, no image rebuild (still `quay.io/keycloak/keycloak:26.0`). Applied via **ARM
PATCH** (`az rest --method patch` against `Microsoft.App/containerApps/ca-keycloak-dev`) — adding
an env var is unsafe via `az containerapp update` on this CLI extension version (known bug, see
`.claude/hats/devops/decisions.md`).

- Added `KC_HEALTH_ENABLED=true` env var.
- Added `KC_HTTP_MANAGEMENT_PORT=9000` env var (explicit; matches the Keycloak 26 default).
- Repointed both probes' `httpGet.port` from `8080` to `9000` (paths unchanged).
- Preserved existing secrets (`kc-db-password`, `kc-admin-password`), ingress, and all other
  config exactly as before (full `properties.configuration` + `properties.template` body per
  the documented ARM PATCH pattern).

## Verification

| Check | Before | After |
|---|---|---|
| Revision health | `ca-keycloak-dev--0000001` Unhealthy | `ca-keycloak-dev--0000002` Healthy |
| Replica ready | `false` | `true` |
| Restart count | 811 | 0 |
| Probe port | 8080 (404) | 9000 (200) |

Commands used:
```powershell
az containerapp revision list --name ca-keycloak-dev --resource-group rg-asps-dev -o table
az containerapp replica list --name ca-keycloak-dev --resource-group rg-asps-dev -o json
az containerapp logs show --name ca-keycloak-dev --resource-group rg-asps-dev --type console --tail 40
```

## Files changed

- `docs/cloud/AZURE_ARCHITECTURE.md` — AD-11 entry, `ca-keycloak-dev` probe/env description
- `docs/cloud/AZURE_DEPLOYMENT_GUIDE.md` — Step 6 (Deploy Keycloak) probe/env update + fix note, env var reference table
- `docs/cloud/azure/troubleshooting.md` — new troubleshooting entry
- `.claude/hats/devops/decisions.md` — new decision entry
- `.claude/hats/devops/inflight.md` — ASPS-735 status entry

No application code changed (infra config only — within DevOps scope per role charter).

## Completed

- [x] Root cause identified (probe port + missing `KC_HEALTH_ENABLED`)
- [x] Fix applied via ARM PATCH
- [x] Verified: revision Healthy, replica ready, restarts 0, management interface confirmed in logs
- [x] Azure docs updated (deployment guide, architecture AD-11, troubleshooting)
- [x] DevOps hat memory updated (decisions, inflight)
- [x] Branch created, committed, pushed
- [x] PR #37 opened
- [x] JIRA comment added with fix summary + PR link
- [x] JIRA transitioned to In Review (transition 31)

## Remaining / continuation point

- Awaiting CEO code review of PR #37 → merge to `main` → JIRA transition to Done (transition 41).
- No further DevOps action needed unless code review requests changes.
