# ASPS-693: Azure Deployment Architecture

**JIRA:** ASPS-693 (Epic)
**Status:** In Progress
**Last updated:** 2026-08-18

---

## Completed Steps

| Step | JIRA | Status | Notes |
|---|---|---|---|
| 1. MySQL Flexible Server | — | Done | `mysql-asps-dev`, Burstable B1ms, AllowAll firewall |
| 2. Key Vault + Identity | — | Done | `kv-asps-dev` (RBAC), `id-asps-dev` (KV Secrets User + AcrPull) |
| 3. Storage Account | — | Done | `staspsdev`, `curve-keys` file share |
| 4. WebApi Docker image | ASPS-698 | Done | `asps-webapi:0.1.0` in ACR |
| 5. Backend Docker image | ASPS-701 | Done | `asps-backend:0.2.0` in ACR (repo root context, includes Analyzers/) |
| 6. Keycloak Container App | ASPS-700 | Done | PostgreSQL backend (see below) |
| 7+8. WebApi + Backend | ASPS-701/702 | Done | **Sidecar architecture** — both in `ca-webapi-dev` (see below) |
| 9. Networking | ASPS-703 | Done | Sidecar localhost + CQRS bind fix |
| 10. Database Migration | ASPS-704 | Done | 29 migrations (24 EF + 5 manual) |
| 11. E2E Verification | — | Done | CQRS queries flowing WebApi→Backend→MySQL |
| 12. Monitoring | ASPS-705 | Done | Log Analytics, App Insights, 5 alerts |
| 13. CI/CD Pipeline | ASPS-706 | In Progress | Workflow created, secrets pending |

## Architecture — Sidecar Pattern

Container Apps TCP ingress does NOT forward ZMQ/CURVE protocol traffic between separate apps (Envoy proxy incompatibility). Solution: **Backend runs as a sidecar container in the WebApi Container App** — both share localhost networking.

```
ca-webapi-dev (Container App)
├── webapi (main container, HTTP ingress :8080)
│   └── CQRS Client → tcp://localhost:5556
└── backend (sidecar container)
    ├── CQRS Gateway on tcp://*:5556
    ├── NetMQ processor on tcp://*:5555
    ├── Alert listener on tcp://*:50001
    └── Notification publisher on tcp://*:50002
```

**`ca-backend-dev`** — standalone Backend Container App, deactivated (revision 0000005). Kept as rollback safety net; can be deleted once sidecar is stable.

## Step 6 Details — Keycloak

- **URL:** `https://ca-keycloak-dev.purplesand-dfb51ae4.northeurope.azurecontainerapps.io/`
- **Image:** `quay.io/keycloak/keycloak:26.0`
- **Database:** PostgreSQL Flexible Server `pg-asps-keycloak-dev` (NOT MySQL)
- **MySQL incompatibility:** Azure MySQL enforces `lower_case_table_names=1` (read-only). Keycloak 26.0 Liquibase changelogs have mixed-case table names that collide → "Table already exists" on first-start migration. Switched to PostgreSQL (Keycloak's recommended DB).
- **Startup probe:** `/health/started`, 30s delay, 50 failures × 10s = 530s window for Liquibase migrations
- **Liveness probe:** `/health/live`, 30s interval
- **OIDC verified:** `/realms/asps/.well-known/openid-configuration` returns valid endpoints
- **Realm:** `asps` created via REST API with brute force protection enabled
- **Client:** `asps-webapi` — confidential OIDC client, redirect to WebApi
- **Secrets:** `kc-db-password` (PostgreSQL), `kc-admin-password`, `kc-webapi-client-secret` — all in Key Vault

## Step 7+8 Details — WebApi + Backend (Sidecar)

**Container App:** `ca-webapi-dev` (revision `0000002`)
- **URL:** `https://ca-webapi-dev.purplesand-dfb51ae4.northeurope.azurecontainerapps.io/`
- **Ingress:** External HTTP on port 8080
- **Volume mount:** Azure Files `curvekeys` → `/keys/` (shared by both containers)

**WebApi container:**
- **Image:** `acraspsisaacdev.azurecr.io/asps-webapi:0.1.0`
- **Resources:** 0.5 CPU, 1 Gi
- **CQRS endpoint:** `tcp://localhost:5556` (sidecar)
- **NetMQ endpoints:** `tcp://localhost:5555`, `tcp://localhost:50001`
- **CURVE:** Client-only mode, reads server public key from shared volume
- **Keycloak:** Authority = `https://<keycloak-fqdn>/realms/asps`, client = `asps-webapi`

**Backend container (sidecar):**
- **Image:** `acraspsisaacdev.azurecr.io/asps-backend:0.2.0`
- **Resources:** 1.0 CPU, 2 Gi
- **CQRS Gateway:** `tcp://*:5556` with CURVE + HMAC authenticated envelopes
- **CURVE:** Server mode, keys from Azure Files share
- **Database:** MySQL `aspsbackend2db` via connection string secret
- **Services:** ASView, TokenStore, CQRS processor/Gateway, Alert listener, Notification publisher, SimulationRunner, OutboxPruning

**E2E verified:**
- `GetVersionQuery` → 83 chars response ✓
- `GetDashboardStatsQuery` → 169 chars response ✓
- SSO login page → Keycloak redirect ✓

## Azure Resources

| Resource | Name | Status |
|---|---|---|
| Resource Group | `rg-asps-dev` | Created |
| VNet | `vnet-asps-dev` (10.0.0.0/16) | Created |
| Container Apps Env | `cae-asps-dev` | Succeeded |
| Container Registry | `acraspsisaacdev` | Operational |
| MySQL Flexible Server | `mysql-asps-dev` | Ready, 29 migrations applied |
| PostgreSQL Flexible Server | `pg-asps-keycloak-dev` | Ready (Keycloak DB) |
| Key Vault | `kv-asps-dev` | RBAC mode, 7 secrets |
| Managed Identity | `id-asps-dev` | KV Secrets User + AcrPull |
| Storage Account | `staspsdev` | curve-keys file share |
| Application Insights | `appi-asps-dev` | Created |
| Log Analytics | `log-asps-dev` | Connected to Container Apps Env |
| **ca-keycloak-dev** | Container App | **Running** |
| **ca-webapi-dev** | Container App | **Running** (WebApi + Backend sidecar) |
| **ca-backend-dev** | Container App | **Deactivated** (replaced by sidecar) |

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

## Step 12 Details — Monitoring

- **Log Analytics:** `log-asps-dev` — container stdout/stderr flowing from Container Apps Environment
- **Application Insights:** `appi-asps-dev` — workspace-based, linked to Log Analytics
- **Action Group:** `ag-asps-dev` — email alerts to isaacmendelson@gmail.com
- **Alerts:**
  - `alert-backend-down` (Sev 1) — replicas < 1 for 5min
  - `alert-webapi-down` (Sev 1) — replicas < 1 for 5min
  - `alert-keycloak-down` (Sev 1) — replicas < 1 for 5min
  - `alert-backend-restarts` (Sev 2) — >3 restarts in 15min
  - `alert-webapi-restarts` (Sev 2) — >3 restarts in 15min

## Step 13 Details — CI/CD Pipeline

- **Workflow:** `.github/workflows/deploy.yml`
- **Auth:** Azure AD app `github-actions-asps` (OIDC federated credentials for main + PRs)
- **Roles:** Contributor on rg-asps-dev, AcrPush on ACR
- **Triggers:** Push to main (when Backend/WebApi code changes), manual dispatch
- **Pipeline:** detect-changes → build-test → build-push-{backend,webapi} → deploy-{backend,webapi}
- **PENDING:** GitHub secrets (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`) must be set via GitHub web UI. Environment `dev` must be created.
- **NOTE:** deploy-webapi step needs update for sidecar YAML deployment instead of simple image update

## Networking Fix — History

1. **CQRS Gateway bound to localhost** — `tcp://127.0.0.1:5556` default. Fixed with `CQRS__BindEndpoint=tcp://*:5556`.
2. **Container Apps TCP ingress incompatible with ZMQ/CURVE** — Envoy proxy doesn't forward ZMTP protocol. `additionalPortMappings` and even primary TCP port both fail. Fixed by **sidecar pattern** (localhost networking).

## Database — Manual Migrations

5 migrations had no `.Designer.cs` file (EF Core couldn't track them). Applied manually via SQL:

| Migration | What |
|---|---|
| `20260312134757_ConvertLargeVarcharsToText` | VARCHAR→TEXT for large columns |
| `20260314215700_AddScamInProgressFields` | Duration, FromUrl, ScamInProgressKey, TabId, Timezone |
| `20260326170400_AddSensitiveSitesTable` | SensitiveSites table |
| `20260326210000_AddBlacklistedPhoneNumbersAndBankWebsites` | BlacklistedPhoneNumbers + BankWebsites tables |
| `202604161203_AddWebsiteCategoryTable` | WebsiteCategories table |

All recorded in `__EFMigrationsHistory`. Total: 29 migrations.

## Known Issues

- Docker Desktop broken on dev machine (AF_UNIX socket crash-loop) — all builds use ACR cloud build (`az acr build --no-logs`)
- Key Vault reference integration from Container Apps failed — using direct secret values in container secrets
- MySQL 3306 exposed with AllowAll firewall (security debt)
- `appsettings.Docker.json` has `Python:AnalyzersFolderPath=/app/analyzer` (singular) vs Dockerfile's `/app/Analyzers` (needs alignment)
- DataProtection keys stored in ephemeral container storage (WebApi warning) — needs persistent volume or Azure Blob
- Application Insights SDK not yet integrated into .NET code (requires code change + rebuild)
- Sidecar pattern means device-facing TCP ports (50001, 50002) are NOT externally reachable — OK for dev, needs separate Backend app or different networking for production
- CI/CD deploy step needs update for sidecar YAML deployment (currently does simple `az containerapp update --image`)

## Continuation Point

**E2E verified working.** Dashboard loads, CQRS queries succeed, SSO flow works.

Remaining:
- Set GitHub Actions secrets via web UI (3 values: `AZURE_CLIENT_ID=e3acd155-ce1a-4257-8a41-fd8017e7e72a`, `AZURE_TENANT_ID=5d3a01c0-eccb-4b50-8798-609b92c89098`, `AZURE_SUBSCRIPTION_ID=d9f067ae-8b6e-42a9-a45f-4918c20f2bbb`)
- Create GitHub environment "dev"
- Update CI/CD pipeline deploy step for sidecar YAML deployment
- Delete `ca-backend-dev` once sidecar is confirmed stable
- Bicep IaC (future)
- Application Insights SDK integration (future code change)
- Production networking for device-facing ports (future)
