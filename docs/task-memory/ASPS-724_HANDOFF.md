# ASPS-724 — Create Angular Admin Container App in Azure

**JIRA:** ASPS-724 (sub-task of ASPS-693 epic)
**Branch:** `asps-724-create-angular-admin-container-app`
**Status:** In Progress
**Last updated:** 2026-08-19

---

## Goal

Create the missing Azure Container App (`ca-angular-admin-dev`) to run the already-built
`asps-angular-admin` image, so the Angular admin dashboard is reachable over HTTPS in Azure.

## Pre-existing state found on branch creation

Working tree on `main` had uncommitted doc changes left over from ASPS-706 (CI/CD pipeline).
Committed separately as `9ec4b3f` ("ASPS-706 Catch up Azure docs...") before starting ASPS-724
work, to keep history clean. Not part of this task's scope — flagged here for visibility.

## Investigation findings

- **Runtime config mechanism:** `apps/admin/angular/docker-entrypoint.sh` writes
  `/usr/share/nginx/html/assets/runtime-config.json` from env vars `API_URL`, `KEYCLOAK_URL`,
  `KEYCLOAK_REALM`, `KEYCLOAK_CLIENT_ID` at container start (nginx `docker-entrypoint.d` hook).
  Angular's `RuntimeConfigService` fetches this file before bootstrap.
- **API calls are direct cross-origin**, not proxied through nginx. `ApiService.baseUrl` = `RuntimeConfigService.apiUrl`
  (an absolute URL). The `/api/` `proxy_pass` block in `nginx.conf` targets the Docker-Compose-only
  hostname `webapi:8080` and is unused in Azure (dead code for this deployment, left as-is — local
  compose still uses it... actually not used there either since apiUrl is absolute in compose too;
  out of scope to change, not touching `nginx.conf`).
- **CORS gap found:** `ca-webapi-dev` currently has **no** `Cors__AllowedOrigins__*` env var set,
  so `Program.cs` falls back to the default `http://localhost:4200` only. The Angular admin Container
  App origin must be added or the browser will block API calls. Must add
  `Cors__AllowedOrigins__0=https://ca-angular-admin-dev.<domain>` to `ca-webapi-dev`.
- **Keycloak client gap found:** Keycloak cloud realm `asps` has **no** `asps-angular-admin` client
  yet (confirmed via Admin REST API — empty result). Spec at
  `docs/specs/ANGULAR_ADMIN_ARCHITECTURE.md` section 3.6 explicitly assigns "Keycloak
  `asps-angular-admin` client creation + mappers" to the DevOps agent. Required: public client,
  Standard Flow only, PKCE S256, redirect URIs / web origins matching the new Container App URL,
  plus 3 protocol mappers (groups, realm roles, audience).
- **ACR image confirmed present:** `asps-angular-admin:latest` and `asps-angular-admin:20260819-0604ba5`
  in `acraspsisaacdev.azurecr.io` (already pushed by `build-push-angular` CI job).
- **ACR auth pattern (reused):** `ca-webapi-dev` pulls via user-assigned identity `id-asps-dev`
  (already has `AcrPull` role) — same pattern used for the new Container App, no new secrets needed.

## Plan

1. `az containerapp create` — `ca-angular-admin-dev`, external HTTPS ingress on port 80, image
   `asps-angular-admin:latest`, identity `id-asps-dev`, env vars for runtime config pointing at
   `ca-webapi-dev` and `ca-keycloak-dev`.
2. Add CORS origin for the new app to `ca-webapi-dev`.
3. Create Keycloak client `asps-angular-admin` in the cloud `asps` realm with mappers, per spec 3.6.
4. Verify: dashboard HTML loads, `runtime-config.json` has correct URLs, login redirects to Keycloak.
5. Update `deploy.yml` — add `deploy-angular` step (simple `az containerapp update --image`, no sidecar).
6. Update docs: `AZURE_DEPLOYMENT_GUIDE.md`, `AZURE_ARCHITECTURE.md`, `ASPS_Azure_Architecture.html`,
   `azure/troubleshooting.md`.
7. Pre-QA gate, handoff finalize.

## Completed work

1. **Container App created:** `ca-angular-admin-dev` in `cae-asps-dev`, external HTTPS ingress
   on port 80, image `acraspsisaacdev.azurecr.io/asps-angular-admin:20260819-nginxfix`
   (originally `:latest`, retagged after the nginx fix rebuild below). CPU 0.25 / Memory 0.5Gi,
   1 replica (min=max=1). Registry pull + container identity reuse `id-asps-dev`
   (already has `AcrPull` — same pattern as `ca-webapi-dev`).
2. **Bug found and fixed:** `apps/admin/angular/nginx.conf` had two `proxy_pass` blocks
   (`/api/`, `/notificationshub`) pointing at the Docker-Compose-only hostname `webapi`. nginx
   validates upstream hostnames at config-load time — unresolvable hostname → hard startup
   failure → `CrashLoopBackOff` in Azure (confirmed via `az containerapp logs show`). Verified
   the Angular app never uses these relative paths (`ApiService`/`SignalRService` always use
   the absolute `RuntimeConfigService.apiUrl`) — removed the dead blocks. Rebuilt image via
   `az acr build` (tag `20260819-nginxfix`), redeployed, replica went `Running`/`ready:true`,
   restartCount 0.
3. **CORS fixed:** `ca-webapi-dev` had no `Cors__AllowedOrigins__*` set. Added
   `Cors__AllowedOrigins__0=https://ca-angular-admin-dev.purplesand-dfb51ae4.northeurope.azurecontainerapps.io`
   to the `webapi` container only (backend sidecar untouched). New revision `ca-webapi-dev--0000004`
   healthy (both `webapi` and `backend` containers `ready:true`).
4. **Keycloak client created:** `asps-angular-admin` in the cloud `asps` realm — public client,
   Standard Flow only, Direct Access Grants off, PKCE S256, redirect URIs / web origins / root
   URL set to the Angular admin Container App URL (plus `localhost:4200`/`:4201` for local dev).
   3 protocol mappers added: `groups` (Group Membership), `realm roles` (User Realm Role →
   `realm_access.roles`), `audience` (Audience Resolve, includes `asps-angular-admin`).
5. **CI/CD updated:** `.github/workflows/deploy.yml` — new `deploy-angular` job, runs after
   `build-push-angular` succeeds, `az containerapp update --image` (no sidecar YAML patching
   needed — single container app). Added `ANGULAR_APP: ca-angular-admin-dev` to top-level `env`.
6. **Docs updated:** `AZURE_DEPLOYMENT_GUIDE.md` (state table + new Step 14 with full command,
   bug writeup, verification), `AZURE_ARCHITECTURE.md` (Container Apps section, CI/CD diagram,
   networking table, AD-8 entry), `ASPS_Azure_Architecture.html` (removed "planned" badge/dashed
   styling), `azure/troubleshooting.md` (3 new entries: nginx upstream crash, MSYS path
   conversion gotcha, `az acr build` Unicode log-streaming crash).

## Verification results

| Check | Result |
|---|---|
| `dotnet build ASPSBackend.sln -c Debug` | 0 errors, 299 pre-existing warnings (unrelated) |
| `deploy.yml` YAML parses (`yaml.safe_load`) | OK, `deploy-angular` job present, `needs: [build-push-angular]` |
| `GET https://ca-angular-admin-dev.../` | 200 |
| `GET .../assets/runtime-config.json` | Correct `apiUrl`/`keycloakUrl`/`keycloakRealm`/`keycloakClientId` |
| Container replica health | `ready:true`, `restartCount:0`, `runningState:Running` |
| `ca-webapi-dev` replica health (both containers) after CORS update | `ready:true` for `webapi` and `backend` |
| CORS preflight (`OPTIONS` + `Origin: <angular-url>`) against `ca-webapi-dev` | `204`, `access-control-allow-origin` echoes the Angular origin |
| Keycloak OIDC discovery (`/realms/asps/.well-known/openid-configuration`) | Resolves, issuer matches |
| Keycloak client `asps-angular-admin` exists with 3 mappers | Confirmed via Admin REST API (`201` on create + all 3 mappers) |

**Not verified (needs a human/browser session):** full interactive login flow through Keycloak
(PKCE redirect → token → dashboard render). All server-side pieces (CORS, OIDC discovery, client
config, runtime-config.json) are confirmed correct; only the browser-side click-through wasn't
exercised in this session.

## Decisions

- Standalone Container App (not sidecar) for Angular admin — it has no localhost-coupled
  dependency like Backend↔WebApi, ordinary `--image` update suffices (AD-8 in
  `AZURE_ARCHITECTURE.md`).
- Removed the dead nginx `proxy_pass` blocks rather than making them resolve dynamically
  (e.g. `resolver` + variable) — they're unused in the current architecture (SPA always calls
  absolute URLs), so removal is simpler and avoids maintaining unused proxy config.
- Reused `id-asps-dev` managed identity for ACR pull instead of creating a new identity —
  consistent with existing `ca-webapi-dev` pattern, no new secrets.

## Pre-existing issue found (out of scope, flagged not fixed)

`.claude/rules/task-workflow.md` had an uncommitted working-tree change when I checked status
before committing (JIRA transition-21 wording + new "Agent labels on JIRA issues" section) that
I did not make. Left uncommitted / excluded from this branch's commits — not part of ASPS-724.
Flagging for the orchestrator/CEO to commit separately if intended.

## Changed files

- `.github/workflows/deploy.yml`
- `apps/admin/angular/nginx.conf`
- `docs/cloud/ASPS_Azure_Architecture.html`
- `docs/cloud/AZURE_ARCHITECTURE.md`
- `docs/cloud/AZURE_DEPLOYMENT_GUIDE.md`
- `docs/cloud/azure/troubleshooting.md`
- `docs/task-memory/ASPS-724_HANDOFF.md` (this file)

**Azure resources created/modified (not files, but part of "changed"):**
- Created: Container App `ca-angular-admin-dev` (rg-asps-dev)
- Created: ACR image `asps-angular-admin:20260819-nginxfix` (+ `:latest` retag)
- Modified: `ca-webapi-dev` env var `Cors__AllowedOrigins__0` (new revision `--0000004`)
- Created: Keycloak client `asps-angular-admin` + 3 protocol mappers (realm `asps`)

## JIRA

- ASPS-724 transitioned To Do → In Progress (transition 21).
- Label `devops` added.
- Not yet transitioned to In Review — pending pre-QA gate completion (push to remote) and PR.

## Uncompleted work / continuation point

1. Pre-QA gate: commit staged changes, merge latest `main`, re-verify build, push branch to
   remote, then notify orchestrator "ready for QA" per `task-workflow.md`.
2. Manual browser verification of the full Keycloak login → dashboard flow (optional but
   recommended before marking AC #6 fully done).
3. Orchestrator/CEO to decide on the `.claude/rules/task-workflow.md` uncommitted change found
   on branch creation (not part of this task).
