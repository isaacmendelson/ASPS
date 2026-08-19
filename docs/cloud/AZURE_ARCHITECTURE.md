# ASPS Azure Architecture

> Current state of the Azure deployment. Visual diagram: [ASPS_Azure_Architecture.html](ASPS_Azure_Architecture.html).
> Deployment steps: [AZURE_DEPLOYMENT_GUIDE.md](AZURE_DEPLOYMENT_GUIDE.md).

**Region:** North Europe
**Platform:** Azure Container Apps (not AKS)
**JIRA Epic:** ASPS-693

---

## Architecture Overview — Sidecar Pattern

Backend runs as a **sidecar container** inside the WebApi Container App (`ca-webapi-dev`). Both share localhost networking — ZMQ/CURVE traffic flows directly without passing through Envoy proxy.

```
                    Internet
                       |
          Azure Container Apps Environment (cae-asps-dev)
                       |
    ┌──────────────────┼──────────────────────────────┐
    |                  |                              |
    |   ca-webapi-dev (sidecar)    ca-keycloak-dev    |   ca-angular-admin-dev
    |   ┌─────────────────────┐    (OIDC provider)    |   (nginx SPA)
    |   │ WebApi (main, :8080)│         |             |
    |   │  → localhost:5556   │         |             |
    |   │  → localhost:5555   │         |             |
    |   │  → localhost:50001  │         |             |
    |   │                     │         |             |
    |   │ Backend (sidecar)   │         |             |
    |   │  CQRS   tcp://*:5556         |             |
    |   │  NetMQ  tcp://*:5555│         |             |
    |   │  Alerts tcp://*:50001        |             |
    |   │  Notif  tcp://*:50002        |             |
    |   └─────────────────────┘         |             |
    └──────────────────┼──────────────────────────────┘
                       |
         ┌─────────────┼─────────────┬─────────────┐
         |             |             |             |
    Azure MySQL    PG Flex       Key Vault     Azure Files
    Flex Server    (KC DB)       (secrets)     (CURVE keys)
```

**Why sidecar?** Container Apps TCP ingress (Envoy proxy) does NOT forward ZMQ/CURVE (ZMTP wire protocol). DNS resolves, TCP connects, but the CURVE handshake fails through the proxy. Sidecar containers share localhost — no proxy, no problem.

---

## Container Apps

### ca-webapi-dev — WebApi + Backend (sidecar)

**URL:** `https://ca-webapi-dev.purplesand-dfb51ae4.northeurope.azurecontainerapps.io/`
**Ingress:** External HTTP on port 8080
**Status:** Running

| Container | Role | CPU | Memory | Image |
|---|---|---|---|---|
| `webapi` (main) | Admin UI + REST API + Keycloak SSO | 0.5 | 1 Gi | `asps-webapi:<tag>` |
| `backend` (sidecar) | CQRS gateway, alert listener, notifications, analyzer | 1.0 | 2 Gi | `asps-backend:<tag>` |

**Shared volume:** Azure Files `curvekeys` → `/keys/` (CURVE key files)

**Inter-container communication (localhost):**
- WebApi → Backend CQRS: `tcp://localhost:5556`
- WebApi → Backend NetMQ: `tcp://localhost:5555`
- WebApi → Backend Alerts: `tcp://localhost:50001`
- Backend → WebApi Notifications: `tcp://localhost:50002` (PUB/SUB)

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

### ca-backend-dev — Backend standalone (deactivated)

**Status:** Deactivated (revision 0000005)

Original standalone Backend Container App — replaced by sidecar pattern. Kept as rollback safety net. Can be deleted once sidecar is confirmed stable in production use.

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

All inter-service communication within `ca-webapi-dev` uses **localhost** (sidecar pattern). External access:

| Port | Protocol | Service | Exposure | Security |
|---|---|---|---|---|
| 8080 | HTTP | WebApi | Public HTTPS (ingress) | Keycloak SSO |
| 5556 | TCP | Backend CQRS | Internal (localhost) | CURVE + HMAC-SHA256 |
| 50001 | TCP | Backend Alerts | Not exposed (sidecar) | CURVE encryption |
| 50002 | TCP | Backend Notifications | Internal (localhost) | CURVE encryption |
| 5555 | TCP | Backend (legacy) | Not exposed | None — excluded |
| 8080 | HTTP | Keycloak | Public HTTPS (ingress) | Admin credentials |
| 80 | HTTP | Angular Admin (nginx) | Public HTTPS (ingress) | Keycloak OIDC (PKCE, browser-side) |
| 3306 | TCP | MySQL | Private | Username/password |

**Note:** Device-facing TCP ports (50001, 50002) are NOT externally reachable in sidecar mode. OK for dev — production will need direct TCP ingress or a separate Backend Container App.

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
  └─────┬─────┘          |
        |                |
     deploy          deploy-angular
  (sidecar YAML)    (simple --image)
```

**Deploy mechanism (WebApi + Backend):** Export Container App YAML → patch image tags with `sed` → re-apply with `az containerapp update --yaml`. Required because `--image` only updates the main container, not sidecars.

**Deploy mechanism (Angular Admin):** `ca-angular-admin-dev` is a single-container app — `az containerapp update --image` is sufficient, no YAML patching needed.

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
