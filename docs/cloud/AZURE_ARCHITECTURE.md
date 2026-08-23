# ASPS Azure Architecture

> Current state of the Azure deployment. Visual diagram: [ASPS_Azure_Architecture.html](ASPS_Azure_Architecture.html).
> Deployment steps: [AZURE_DEPLOYMENT_GUIDE.md](AZURE_DEPLOYMENT_GUIDE.md).

**Region:** North Europe
**Platform:** Azure Container Apps (not AKS)
**JIRA Epic:** ASPS-693

---

## Architecture Overview — Standalone Backend + WebApi (ASPS-725/729)

Backend runs as its **own standalone Container App** (`ca-backend-dev`), separate from WebApi
(`ca-webapi-dev`). They communicate over internal TCP ingress using **app-name addressing**
(`tcp://ca-backend-dev:<port>`), not localhost/sidecar and not FQDN. This replaced the earlier
sidecar pattern (ASPS-701/702) once ASPS-726 proved app-name addressing correctly forwards the
ZMQ/CURVE (ZMTP) wire protocol between two Container Apps in the same environment.

```
                    Internet
                       |
          Azure Container Apps Environment (cae-asps-dev, VNet-integrated)
                       |
    ┌──────────────────┼───────────────────┬──────────────────────┬───────────────────┐
    |                  |                   |                      |                   |
    | ca-webapi-dev     ca-backend-dev      ca-keycloak-dev        ca-angular-admin-dev |
    | ┌───────────────┐ ┌─────────────────┐ (OIDC provider)        (nginx SPA)          |
    | │ WebApi :8080  │ │ CQRS   tcp://*:5556                                          |
    | │  → ca-backend-│ │ NetMQ  tcp://*:5555 (not exposed, AD-4)                      |
    | │    dev:5556   │ │ Alerts tcp://*:50001 (external)                              |
    | │  → ca-backend-│ │ Notif  tcp://*:50002 (external)                              |
    | │    dev:50001/ │ └─────────────────┘                                            |
    | │    :50002     │                                                                |
    | └───────────────┘                                                                |
    └──────────────────┼───────────────────┴──────────────────────┴───────────────────┘
                       |
         ┌─────────────┼─────────────┬─────────────┐
         |             |             |             |
    Azure MySQL    PG Flex       Key Vault     Azure Files
    Flex Server    (KC DB)       (secrets)     (CURVE keys, shared by
                                                 both webapi and backend)
```

**Why not sidecar anymore?** The original sidecar pattern (both containers sharing localhost)
worked but coupled WebApi and Backend scaling/lifecycle together and made Backend redeploys
touch the WebApi revision. ASPS-726 confirmed app-name addressing (`ca-backend-dev:<port>`,
as opposed to the Container App's public FQDN) correctly forwards ZMQ/CURVE traffic — FQDN-based
ingress still does NOT work (Envoy/HTTP-aware proxy path strips ZMTP), but app-name resolution
within the same Container Apps Environment bypasses that proxy. This unblocked splitting Backend
into its own Container App (ASPS-727), pointing WebApi's CQRS client and `/ws/agent`
`AgentGatewayService` gateway at it (ASPS-728, ASPS-729), and removing the sidecar entirely
(ASPS-729).

---

## Container Apps

### ca-webapi-dev — WebApi (standalone)

**URL:** `https://ca-webapi-dev.purplesand-dfb51ae4.northeurope.azurecontainerapps.io/`
**Ingress:** External HTTP on port 8080
**Status:** Running — single container (sidecar removed ASPS-729)

| Container | Role | CPU | Memory | Image |
|---|---|---|---|---|
| `webapi` (only) | Admin UI + REST API + Keycloak SSO + `/ws/agent` gateway | 0.5 | 1 Gi | `asps-webapi:<tag>` |

**Volume:** Azure Files `curvekeys` → `/keys/` (read — `CurveKeyManager` in client mode loads
the CURVE server public key from `/keys/curve-server-public-key.txt`; kept even after the
sidecar removal because WebApi still needs this file, it just no longer writes/generates it).

**Inter-app communication (app-name TCP, internal ingress on `ca-backend-dev`):**
- WebApi → Backend CQRS: `tcp://ca-backend-dev:5556` (`CQRS__Endpoint`)
- WebApi → Backend NetMQ (legacy, AD-4): `tcp://ca-backend-dev:5555` (unused channel, kept for parity)
- WebApi `/ws/agent` gateway (`AgentGatewayService`) → Backend Alerts/Notifications:
  `tcp://ca-backend-dev:50001` / `:50002` (`AgentGateway__BackendHost=ca-backend-dev`)

### ca-backend-dev — Backend (standalone)

**Ingress:** Internal TCP, VNet-integrated environment (main port 50001, `additionalPortMappings`
for 50002/5556 — see AD-3 correction below)
**Status:** Running — active, standalone (ASPS-727)

| Container | Role | CPU | Memory | Image |
|---|---|---|---|---|
| `backend` (only) | CQRS gateway, alert listener, notifications, analyzer subprocess | 1.0 | 2 Gi | `asps-backend:<tag>` |

**Volume:** Azure Files `curvekeys` → `/keys/` (read-write — generates/owns the CURVE keypair)

**Exposure matrix:**

| Port | Role | External |
|---|---|---|
| 50001 | Alert listener (device → backend) | **true** |
| 50002 | Notification publisher (backend → WebApi) | **true** |
| 5556 | CQRS gateway (WebApi → backend) | **false** (internal only) |
| 5555 | Legacy NetMQ (AD-4) | not exposed at all |

> Ingress `external` is an all-or-nothing switch tied to whichever port is the *main*
> `ingress.targetPort` — an `additionalPortMappings` entry cannot be `external: true` while the
> main port's own `external` is `false`. Port 50001 was made the main ingress port for this
> reason; which port is "main" vs "additional" has no functional effect, only the per-port
> `external` flags matter.

### ca-keycloak-dev — Keycloak OIDC

**URL:** `https://ca-keycloak-dev.purplesand-dfb51ae4.northeurope.azurecontainerapps.io/`
**Image:** `quay.io/keycloak/keycloak:26.0`
**Database:** PostgreSQL Flexible Server (NOT MySQL — see AD-6 in Deployment Guide)
**CPU/Memory:** 1.0 / 2 Gi
**Status:** Running

- Realm: `asps`
- Clients: `asps-webapi` (confidential, OIDC), `asps-angular-admin` (public, PKCE S256)
- Startup probe: `/health/started`, 530s window (for Liquibase migrations)
- Liveness probe: `/health/live`, 30s interval

### ca-angular-admin-dev — Angular Admin

**URL:** `https://ca-angular-admin-dev.purplesand-dfb51ae4.northeurope.azurecontainerapps.io/`
**Ingress:** External HTTPS on port 80 (nginx)
**Image:** `asps-angular-admin:<tag>` (built and pushed to ACR by CI/CD, deployed by `deploy-angular` job)
**CPU/Memory:** 0.25 / 0.5 Gi
**Status:** Running (ASPS-724)

The Angular admin dashboard SPA, served by nginx. Runtime config (`apiUrl`, `keycloakUrl`,
`keycloakRealm`, `keycloakClientId`) is written to `/assets/runtime-config.json` from
container env vars (`API_URL`, `KEYCLOAK_URL`, `KEYCLOAK_REALM`, `KEYCLOAK_CLIENT_ID`) at
startup — same image works unmodified across dev/staging/prod. The SPA calls `ca-webapi-dev`
and `ca-keycloak-dev` directly (absolute URLs), not through nginx `proxy_pass` — CORS on
`ca-webapi-dev` allows this origin. Registry auth and identity reuse `id-asps-dev` (already
has `AcrPull`), same pattern as `ca-webapi-dev`. Also runs locally via `npm start` (port 4200)
or Docker Compose (nginx on 4201).

---

## Azure Managed Services

| Resource | Name | Purpose |
|---|---|---|
| Container Apps Env | `cae-asps-dev` | Hosts all Container Apps, VNet-integrated |
| Container Registry | `acraspsisaacdev.azurecr.io` | Docker images (Backend, WebApi, Angular) |
| MySQL Flexible Server | `mysql-asps-dev` | ASPS database (`aspsbackend2db`), Burstable B1ms |
| PostgreSQL Flexible Server | `pg-asps-keycloak-dev` | Keycloak database only |
| Key Vault | `kv-asps-dev` | RBAC mode, 7 secrets |
| Managed Identity | `id-asps-dev` | KV Secrets User + AcrPull |
| Storage Account | `staspsdev` | `curve-keys` Azure Files share |
| VNet | `vnet-asps-dev` (10.0.0.0/16) | Container Apps networking |
| Application Insights | `appi-asps-dev` | Monitoring (workspace-based) |
| Log Analytics | `log-asps-dev` | Container stdout/stderr logs |

---

## Key Vault Secrets

| Secret | Purpose |
|---|---|
| `mysql-admin-password` | MySQL root password |
| `mysql-connection-string` | Full MySQL connection string |
| `postgres-keycloak-password` | PostgreSQL password for Keycloak DB |
| `keycloak-admin-password` | Keycloak admin console password |
| `cqrs-shared-secret` | HMAC-SHA256 shared secret for Backend↔WebApi CQRS |
| `kc-webapi-client-secret` | Keycloak OIDC client secret for asps-webapi |
| `storage-account-key` | Azure Files storage account key |

---

## Networking

WebApi (`ca-webapi-dev`) and Backend (`ca-backend-dev`) are separate Container Apps, connected
via internal TCP ingress + **app-name addressing** (`tcp://ca-backend-dev:<port>`) — not
localhost, not FQDN. External access:

| Port | Protocol | Service | Exposure | Security |
|---|---|---|---|---|
| 8080 | HTTP | WebApi | Public HTTPS (ingress) | Keycloak SSO |
| 5556 | TCP | Backend CQRS | Internal only (app-name TCP ingress on `ca-backend-dev`) | CURVE + HMAC-SHA256 |
| 50001 | TCP | Backend Alerts | External (desktop agents) | CURVE encryption |
| 50002 | TCP | Backend Notifications | External (app-name addressed by WebApi's `/ws/agent` gateway) | CURVE encryption |
| 5555 | TCP | Backend (legacy) | Not exposed | None — excluded |
| 8080 | HTTP | Keycloak | Public HTTPS (ingress) | Admin credentials |
| 80 | HTTP | Angular Admin (nginx) | Public HTTPS (ingress) | Keycloak OIDC (PKCE, browser-side) |
| 3306 | TCP | MySQL | Private | Username/password |

**Device-facing TCP ports (50001, 50002) are now externally reachable** on `ca-backend-dev`
directly (ASPS-727) — no longer gated behind the WebApi sidecar. Desktop agents connect straight
to `ca-backend-dev`'s public TCP ingress.

---

## CI/CD Pipeline

**Workflow:** `.github/workflows/deploy.yml`
**Auth:** Azure AD OIDC (federated credentials, no stored passwords)

```
push to main (code changes)
       |
  detect-changes ──→ build-test
       |                  |
  ┌────┴──────┬──────────┐
  |           |          |
build-push  build-push  build-push
 backend     webapi      angular
  |           |          |
deploy-backend deploy-webapi deploy-angular
(simple --image) (simple --image) (simple --image)
```

**Deploy mechanism (all three apps, ASPS-730):** Each app is now single-container —
`az containerapp update --image` updates it directly, no YAML export/patch/apply needed. This
replaced the earlier sidecar YAML-patching pattern used for WebApi+Backend before ASPS-729
split Backend into its own Container App.

**Manual trigger:** `gh workflow run deploy.yml -f deploy_backend=true -f deploy_webapi=true`

---

## Monitoring

**Alerts** (email to isaacmendelson@gmail.com via `ag-asps-dev` action group):

| Alert | Severity | Condition |
|---|---|---|
| `alert-backend-down` | Critical | Replicas < 1 for 5min |
| `alert-webapi-down` | Critical | Replicas < 1 for 5min |
| `alert-keycloak-down` | Critical | Replicas < 1 for 5min |
| `alert-backend-restarts` | Error | >3 restarts in 15min |
| `alert-webapi-restarts` | Error | >3 restarts in 15min |

---

## Architecture Decisions

Detailed in [AZURE_DEPLOYMENT_GUIDE.md](AZURE_DEPLOYMENT_GUIDE.md#architecture-decisions) and [ADR-003](../architecture/decisions/ADR-003-ASPS-693-AZURE-DEPLOYMENT-ARCHITECTURE.md).

| ID | Topic | Decision |
|---|---|---|
| AD-1 | Python Analyzers | Multi-stage Docker (subprocess), HTTP API separation later (ASPS-708) |
| AD-2 | CURVE Key Loading | Azure Files mount at `/keys/` |
| AD-3 | Port 50001 Ingress | TCP pass-through + keepalive (requires VNet) |
| AD-4 | Port 5555 (Legacy) | Not exposed in cloud, deprecation tracked (ASPS-717) |
| AD-5 | Database Migrations | Container Apps Job with `--migrate-only` flag |
| AD-6 | Keycloak | Container App with PostgreSQL (MySQL incompatible) |
| AD-7 | Infrastructure as Code | CLI first → Bicep after deployment stabilizes |
| AD-8 | Angular Admin Deployment | Standalone Container App (not sidecar), reuse `id-asps-dev` for ACR pull, CORS + Keycloak public client added (ASPS-724) |
| AD-9 | Backend split from WebApi sidecar | Standalone `ca-backend-dev` + internal TCP ingress with **app-name addressing** (`ca-backend-dev:<port>`) — confirmed to forward ZMQ/CURVE where FQDN addressing does not (ASPS-725/726/727/728/729/730) |

---

## Security Backlog

| Item | Current | Target | Priority |
|---|---|---|---|
| CURVE keys in Azure Files | Private key in file share | Key Vault / HSM | Medium |
| MySQL auth | Username/password | Managed Identity / Entra token | Low |
| Port 5555 deprecation | Code exists, not exposed | Remove from codebase (ASPS-717) | Low |

---

## Related Documents

| Document | Purpose |
|---|---|
| [ASPS_Azure_Architecture.html](ASPS_Azure_Architecture.html) | Visual architecture diagram |
| [AZURE_DEPLOYMENT_GUIDE.md](AZURE_DEPLOYMENT_GUIDE.md) | Step-by-step deployment guide with CLI commands |
| [ADR-003](../architecture/decisions/ADR-003-ASPS-693-AZURE-DEPLOYMENT-ARCHITECTURE.md) | Architecture Decision Record |
| [ASPS_Cloud_Architecture_Proposal.md](ASPS_Cloud_Architecture_Proposal.md) | Original design proposal (pre-deployment) |
| [azure/troubleshooting.md](azure/troubleshooting.md) | Known issues and solutions |
