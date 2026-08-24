# ASPS Azure Deployment Guide

> Living document — updated as architecture decisions are made.
> JIRA Epic: ASPS-693 (Azure Deployment)

---

## Current Azure State

| Resource | Name | Region | Status |
|---|---|---|---|
| Resource Group | `rg-asps-dev` | North Europe | Created |
| Container Registry | `acraspsisaacdev.azurecr.io` | North Europe | Operational |
| Log Analytics | `log-asps-dev` | North Europe | Provisioned |
| Container Apps Env | `cae-asps-dev` | North Europe | Provisioned |
| MySQL Flexible Server | `mysql-asps-dev` | North Europe | Ready |
| PostgreSQL Flexible Server | `pg-asps-keycloak-dev` | North Europe | Ready (Keycloak only) |
| Key Vault | `kv-asps-dev` | North Europe | RBAC mode |
| Managed Identity | `id-asps-dev` | North Europe | KV Secrets User + AcrPull |
| Storage Account | `staspsdev` | North Europe | curve-keys file share |
| Backend Image | `asps-backend:latest` | ACR | CI/CD auto-tagged `YYYYMMDD-<sha7>` |
| WebApi Image | `asps-webapi:latest` | ACR | CI/CD auto-tagged `YYYYMMDD-<sha7>` |
| Angular Admin Image | `asps-angular-admin:latest` | ACR | CI/CD auto-tagged `YYYYMMDD-<sha7>` |

| Application Insights | `appi-asps-dev` | North Europe | Connected |
| Container App: Keycloak | `ca-keycloak-dev` | North Europe | **Running** |
| Container App: WebApi | `ca-webapi-dev` | North Europe | **Running** (standalone, sidecar removed ASPS-729) |
| Container App: Backend | `ca-backend-dev` | North Europe | **Running** (standalone, ASPS-727) |
| Container App: Angular Admin | `ca-angular-admin-dev` | North Europe | **Running** (ASPS-724) |

All services deployed, verified, and data migrated from local. CI/CD pipeline operational.

---

## Target Architecture — Standalone Backend + WebApi (ASPS-725/726/727/728/729)

> **History:** originally deployed as a sidecar (Backend + WebApi in one Container App,
> communicating via localhost) because Container Apps TCP ingress via **FQDN** does NOT
> forward ZMQ/CURVE (ZMTP wire protocol) — DNS resolves, TCP connects, but the CURVE
> handshake fails through the Envoy/HTTP-aware proxy path. ASPS-726 proved that **app-name
> addressing** (`tcp://ca-backend-dev:<port>`, not the app's public FQDN) between two
> Container Apps in the same environment forwards ZMQ/CURVE correctly. This unblocked
> splitting Backend into its own standalone Container App (ASPS-727), pointing WebApi's
> CQRS client and `/ws/agent` gateway (`AgentGatewayService`) at it via app-name TCP
> (ASPS-728), and removing the sidecar entirely (ASPS-729).

```
                    Internet
                       |
              Azure Container Apps Environment (cae-asps-dev, VNet-integrated)
                       |
         ┌─────────────┼─────────────┬─────────────────┐
         |                           |                 |
    ca-webapi-dev                ca-backend-dev    ca-keycloak-dev
    ┌──────────────────────┐     ┌────────────────┐  (auth endpoint)
    │ WebApi (main, :8080) │     │ CQRS  tcp://*:5556
    │  └→ ca-backend-dev:5556    │ NetMQ tcp://*:5555 (not exposed, AD-4)
    │  └→ ca-backend-dev:50001/  │ Alerts tcp://*:50001 (external)
    │       :50002 (/ws/agent)   │ Notif  tcp://*:50002 (external)
    └──────────────────────┘     └────────────────┘
                       |                 |            |
              ┌────────┼────────┬───────┴────┬────────┘
              |        |        |            |
         Azure MySQL  PG Flex  Key Vault  Azure Files
         Flex Server  (KC DB)  (secrets)  (CURVE keys, shared:
                                            webapi reads, backend r/w)
```

Platform: **Azure Container Apps** (not AKS) — simpler, cheaper for dev, automatic HTTPS, built-in scaling.

---

## Architecture Decisions

### AD-1: Python Analyzers — how to run in cloud

**Decision:** Two phases.

**Phase 1 (initial deployment):** Option B — multi-stage Docker image containing both .NET Backend + Python + Playwright + Chromium. Subprocess invocation works as-is, zero code changes. Image will be ~2GB+.

**Phase 2 (ASPS-708):** Option C — Analyzer as a separate Container App with HTTP API (FastAPI). Backend calls via HTTP instead of subprocess. Foundation already exists in codebase (`api.py`, `analyzer-client/`). Independent scaling, smaller images.

**Key files:**
- `Business/RealtimeAnalysis/UserDomain/AnalyzerV1ProcessClient.cs` — subprocess launcher
- `Business/RealtimeAnalysis/UserDomain/UDUrlAnalyzer.cs` — orchestrator
- `Analyzers/basic-url-analyzer/analyze.py` — Python entry point
- `Analyzers/basic-url-analyzer/v1_stdio.py` — stdin/stdout adapter
- `Analyzers/basic-url-analyzer/api.py` — existing FastAPI server (for Phase 2)

### AD-2: CURVE key loading

**Decision:** Option A — Azure Files mount. Zero code changes.

**How it works:**
- Azure Files share stores `curve-server-keys.json` (full keypair) and `curve-server-public-key.txt` (Z85 public key only)
- Backend Container App: volume mount at `/keys` (read-write). Config: `Security:KeysFilePath=/keys/curve-server-keys.json`
- WebApi Container App: volume mount at `/keys` (read-only). Config: `Security:ServerPublicKeyFilePath=/keys/curve-server-public-key.txt`
- On first Backend startup, `CurveKeyManager` auto-generates keys and writes to the share
- Keys persist across container restarts (Azure Files is persistent storage)
- Desktop agents receive the Z85 public key once (via installer or config) — no change from current behavior

**Cost:** < $0.01/month (500 bytes storage, handful of reads on startup).

**Azure Files config:**
- Storage Account: Standard LRS, Transaction Optimized
- Share name: `curve-keys`
- No minimum size, no flat fee, pay-as-you-go

### AD-3: Port 50001 (alert ingress)

**Decision:** Use Container Apps TCP ingress via `additionalPortMappings`. Works as TCP pass-through — ZeroMQ/CURVE traffic passes transparently.

> **Correction (ASPS-726, 2026-08-22):** the pass-through claim above is true for **external**
> ingress (device-facing, port 50001/50002 on `ca-backend-dev`) but does NOT hold for
> Container-App-to-Container-App traffic addressed by **FQDN** — that path goes through the
> same Envoy/HTTP-aware proxy and strips the ZMTP wire protocol. What does work,
> confirmed by direct test, is **app-name addressing** between two Container Apps in the
> same environment (`tcp://ca-backend-dev:5556`, not the FQDN) — this is how WebApi now
> reaches Backend post-ASPS-728/729, instead of localhost/sidecar networking.

**Requirements:**
1. Container Apps Environment MUST be deployed into a VNet (required for external TCP ingress)
2. TCP keepalive MUST be configured on both sides (code change ~4 lines per side):
   - Backend (NetMQ): `TcpKeepalive=true`, `TcpKeepaliveIdle=60`, `TcpKeepaliveInterval=30`
   - Desktop agent (pyzmq): `ZMQ_TCP_KEEPALIVE=1`, `ZMQ_TCP_KEEPALIVE_IDLE=60`, `ZMQ_TCP_KEEPALIVE_INTVL=30`
   - Reason: Container Apps load balancer silently drops TCP connections idle >4 minutes (no TCP RST sent)
3. Desktop agent reconnection logic must handle silent disconnects (not just explicit errors)
4. Expect periodic connection drops (~every 4 days) due to infrastructure maintenance

**Configuration:**
```yaml
additionalPortMappings:
  - targetPort: 50001
    exposedPort: 50001
    transport: tcp
    external: true       # desktop agents connect from internet
  - targetPort: 50002
    exposedPort: 50002
    transport: tcp
    external: false      # WebApi subscribes internally only
```

Main ingress remains HTTP (for health checks). TCP ports added via `additionalPortMappings`.

**Limitations:**
- Max 5 additional TCP ports per app (can request more via Azure support)
- TCP ports do not get HTTP features (CORS, session affinity) — not needed for ZeroMQ
- Scale-down events may disrupt active TCP connections

### AD-4: Port 5555 (security debt)

**Decision:** Option A — do not expose in cloud. Create separate JIRA task to deprecate.

**Problem:** Port 5555 (`NetMQMessageProcessor`) is a legacy CQRS channel running in parallel with port 5556.
It handles only 9 message types (User + Device CRUD) that are already available through port 5556.
Unlike 5556, port 5555 has **no encryption** (no CURVE, no HMAC) and binds to `tcp://*:5555` (all interfaces).
Only 2 legacy WebApi controllers use it (`UsersController` — marked "not called by any frontend", `UserDevicesController`).

**Options evaluated:**
- **A) Don't expose in cloud (chosen)** — no ingress rule for 5555 in Container App. Zero code changes.
  The Backend still listens internally, but nothing can reach it from outside the container.
  WebApi runs in a separate Container App, so it can't reach 5555 either.
- **B) Consolidate with 5556** — migrate the 2 controllers to `ICQRSClient` (port 5556), delete
  `NetMQMessageProcessor` + `NetMQClientService`. Already planned in ASPS-675 Phase 6.
- **C) Expose as-is** — rejected. Unencrypted TCP on public internet is unacceptable.

**Action items:**
- Container App config: no port 5555 in ingress rules
- Separate JIRA task (non-urgent): deprecate port 5555 in Backend code (consolidate into 5556)
- ASPS-675 Phase 6 already covers the code-level consolidation

### AD-5: Database migration strategy

**Decision:** Option B — Container Apps Job with `--migrate-only` flag.

**Problem:** EF Core migrations must run before the app starts serving traffic. With multiple replicas,
running `database.Migrate()` at startup risks concurrent migration execution (deadlocks, partial applies).

**Options evaluated:**
- **A) Startup migration** — `database.Migrate()` in `Program.cs`. Simple but dangerous with multi-replica.
- **B) Container Apps Job (chosen)** — dedicated job runs migrations once before app deployment.
  Uses the same Backend image with a `--migrate-only` CLI flag. Job runs, applies migrations, exits.
  CI/CD triggers the job before deploying the new app version. If migration fails, deploy stops.
- **C) Manual** — run `dotnet ef` from local machine. Not scalable, not part of CI/CD.

**Implementation:**
1. Add `--migrate-only` flag to `ASPSBackend/Program.cs`:
   - When flag is present: resolve `DbContext`, call `database.Migrate()`, exit with code 0
   - When flag is absent: normal startup (current behavior)
2. Create Container Apps Job:
   ```bash
   az containerapp job create \
     --name job-asps-migrate \
     --resource-group rg-asps-dev \
     --environment cae-asps-dev \
     --image acraspsisaacdev.azurecr.io/asps-backend:latest \
     --trigger-type Manual \
     --command "dotnet" "ASPSBackend.dll" "--migrate-only" \
     --env-vars "ConnectionStrings__DefaultConnection=secretref:mysql-connstring"
   ```
3. CI/CD pipeline: trigger migration job → wait for success → deploy new app version

**Rollback:** if a migration fails, the job exits non-zero, deploy halts, DB stays at previous state.
Manual rollback: `dotnet ef database update <PreviousMigration>` from a connected machine.

### AD-6: Keycloak approach

**Decision:** Option A — Keycloak as Container App with **PostgreSQL** backend (not MySQL).

**Problem:** ASPS uses Keycloak as OIDC provider for the admin panel SSO. Locally it runs as a Docker
container on port 8180 with a MySQL-backed `keycloak` database. Need to decide how to run it in Azure.

**MySQL incompatibility:** Azure MySQL Flexible Server enforces `lower_case_table_names=1` (read-only,
immutable). Keycloak 26.0's Liquibase changelogs create tables with mixed-case names that collide when
MySQL lowercases them, causing "Table already exists" errors during first-start migration. PostgreSQL is
Keycloak's recommended database and avoids this issue entirely.

**Options evaluated:**
- **A) Keycloak Container App (chosen)** — deploy the same Keycloak image as a Container App.
  Use a dedicated Azure PostgreSQL Flexible Server for the `keycloak` database. Export local realm and import to cloud.
  Zero code changes — same Authority URL, same client IDs, same OIDC flow.
- **B) Microsoft Entra ID** — replace Keycloak with Azure AD/Entra ID. Managed service, zero maintenance,
  MFA built-in. But requires code changes (claims mapping, token validation, auth flow adjustments).
  Better suited as a future migration after the deployment is stable.

**Implementation:**
1. Pin Keycloak image version (match local dev version)
2. Mirror image to ACR: `acraspsisaacdev.azurecr.io/keycloak:<version>`
3. Create `keycloak` database on Azure PostgreSQL Flexible Server (NOT MySQL — see AD-6)
4. Export local realm: `docker exec keycloak /opt/keycloak/bin/kc.sh export --realm asps --file /tmp/realm.json`
5. Deploy Container App:
   ```bash
   az containerapp create \
     --name ca-keycloak-dev \
     --resource-group rg-asps-dev \
     --environment cae-asps-dev \
     --image quay.io/keycloak/keycloak:26.0 \
     --target-port 8080 \
     --ingress external \
     --min-replicas 1 --max-replicas 1 \
     --cpu 1.0 --memory 2Gi \
     --env-vars \
       KC_DB=postgres \
       KC_DB_URL=jdbc:postgresql://pg-asps-keycloak-dev.postgres.database.azure.com:5432/keycloak \
       KC_DB_USERNAME=kcadmin \
       KC_DB_PASSWORD=secretref:kc-db-pass \
       KEYCLOAK_ADMIN=admin \
       KEYCLOAK_ADMIN_PASSWORD=secretref:kc-admin-pass \
       KC_HOSTNAME_STRICT=false \
       KC_HTTP_ENABLED=true \
       KC_PROXY_HEADERS=xforwarded
   ```
6. Import realm to cloud Keycloak
7. Update Backend + WebApi `Authority` URLs to point to cloud Keycloak FQDN
8. Update Keycloak client redirect URIs to cloud WebApi URL

**Future option:** migrate to Entra ID when deployment is stable. OIDC is standard — mainly config changes
(Authority, ClientId, ClientSecret) + claims mapping adjustments.

### AD-7: Infrastructure as Code

**Decision:** ~~Option C — defer.~~ **Revised:** Option A — Bicep, phased approach.

**Original decision (2026-08-15):** Defer IaC until staging/prod environments are needed.

**Revised decision (2026-08-16, per cloud architect review):** CLI first, Bicep immediately after.

**Problem:** Infrastructure was created manually via `az` CLI. IaC makes environments reproducible and
version-controlled, but adds upfront investment.

**Options evaluated:**
- **A) Bicep (chosen)** — Azure-native IaC. No state file, always up-to-date with Azure features.
- **B) Terraform** — multi-cloud IaC. Larger community, but needs state backend and Azure provider
  sometimes lags behind new features.
- **C) Defer** — originally chosen, revised after architect review.

**Phased approach:**
1. **Phase 1:** Deploy using `az` CLI (documented in this guide). Get ASPS running in Azure.
   Understand every resource hands-on.
2. **Phase 2:** Convert the working deployment to Bicep. Redeploy / reproduce the environment.
   Git-track the templates. Integrate into CI/CD.

**Rationale for revision:** The deployment itself is the best context for writing Bicep — converting
working CLI commands to templates, not writing templates from scratch. This also provides real IaC
experience with a production-like system.

### AD-9: Backend split from WebApi sidecar

**Decision:** Standalone `ca-backend-dev` Container App + internal TCP ingress with app-name
addressing, replacing the WebApi+Backend sidecar pattern.

**Problem:** The sidecar pattern (AD-3's original rationale) coupled WebApi and Backend
scaling/lifecycle — redeploying Backend required touching the WebApi Container App's YAML,
and the two components couldn't scale independently.

**Options evaluated:**
- **A) Keep sidecar** — rejected, doesn't unblock independent scaling/deploys.
- **B) Standalone Backend + FQDN ingress (chosen: rejected on testing)** — the original
  assumption was that Container Apps TCP ingress cannot forward ZMQ/CURVE at all (any
  addressing mode). ASPS-726 re-tested this specifically with **app-name** addressing
  (as opposed to the FQDN addressing implicitly assumed in the original AD-3 test) and found
  it **does** work.
- **C) Standalone Backend + app-name TCP addressing (chosen)** — `ca-backend-dev` becomes a
  standalone Container App with internal TCP ingress (5556) + external TCP ingress
  (50001/50002, AD-3). WebApi connects via `tcp://ca-backend-dev:<port>` (app name, resolved
  within the Container Apps Environment's internal DNS), not FQDN and not localhost.

**Implementation (ASPS-725 through ASPS-730):**
1. ASPS-726 — practical CURVE test proving app-name addressing works (see AD-3 correction above)
2. ASPS-727 — reactivate `ca-backend-dev` as standalone Container App (own CPU/memory, own
   CURVE key volume mount in read-write mode)
3. ASPS-728 — point WebApi's CQRS client (`CQRS__Endpoint`) at `tcp://ca-backend-dev:5556`
   (sidecar `backend` container kept running as a safety net during the transition)
4. ASPS-729 — found and fixed a real gap: `AgentGatewayService` (the `/ws/agent` WebSocket
   gateway bridging browser/other WebSocket clients to Backend's ROUTER/PUB sockets) hardcoded
   `localhost` for ports 50001/50002 with no configurable host — this would have silently
   broken (no exception, ZMQ connect is lazy) once the sidecar was removed. Fixed by adding
   `AgentGatewayOptions.BackendHost` (default `"localhost"`, configurable via
   `AgentGateway__BackendHost`). Set to `ca-backend-dev` on `ca-webapi-dev`, verified via
   startup logs (`AgentGatewayService started — REQ endpoint tcp://ca-backend-dev:50001,
   SUB endpoint tcp://ca-backend-dev:50002`), then removed the Backend sidecar container from
   `ca-webapi-dev` entirely. WebApi resources reduced back to just its own 0.5 CPU / 1 Gi
   (Backend's 1.0 CPU / 2 Gi allocation freed). The Azure Files `curve-keys` volume mount was
   **kept** on WebApi — `CurveKeyManager` in client mode still reads the CURVE server public
   key from `/keys/curve-server-public-key.txt` on that same mount.
5. ASPS-730 — simplified CI/CD: `ca-backend-dev` and `ca-webapi-dev` are now both
   single-container apps, so `az containerapp update --image` deploys each directly (same
   pattern as `deploy-angular`) — no more YAML export/`sed`-patch/re-apply for a sidecar.

**Rollback:** the previous sidecar YAML (WebApi + Backend containers together) is preserved in
git history (this file, pre-ASPS-729 revision) and in `ca-webapi-dev`'s revision history
(`ca-webapi-dev--0000005`, sidecar still present) if a rollback is ever needed.

### AD-10: Messaging protocol version enforcement (v0 legacy vs. v1 envelope)

**Decision:** the Azure Backend runs with `Messaging:AcceptLegacyV0=false` — every client
message reaching `ca-backend-dev` MUST use the v1 envelope format (a `schemaVersion` field
present in the payload). There is no legacy fallback in the cloud environment.

**How enforcement works:** `AlertProcessor.RouteMessageAsync()` inspects the incoming message
for a `schemaVersion` field:
- Present → routed through the v1 handling path.
- Absent → routed to `ProcessLegacyAlertAsync()`, which **rejects** the message outright when
  `Messaging:AcceptLegacyV0=false` (the Azure setting). Locally, `AcceptLegacyV0=true` still
  allows old clients through during migration.

**Current status of alert types (2026-08-25):**
All alert types are **v1-compliant** (ASPS-732):
- `UrlAlert` — wraps in `url_scan.request` v1 envelope (commit `d51fb80`).
- `TrackUrlAlert` — wraps in `track_url.request` v1 envelope.
- `TabClosedAlert` — wraps in `tab_closed.request` v1 envelope.
- `TabChangedAlert` — wraps in `tab_changed.request` v1 envelope.
- `RemoteAccessAlert` — wraps in `remote_access.request` v1 envelope.

**Implication for any new client/agent code:** do not add new message types or alert paths
without a v1 envelope (`schemaVersion` + type-specific wrapper, e.g. `url_scan.request`) —
they will silently fail against the Azure Backend even though they may work against a local
Backend running with `AcceptLegacyV0=true`.

---

## Deployment Sequence

Order matters — each step depends on the previous ones.

### Step 0: Networking Validation — Checkpoint 0 — DONE

> **Gate:** Must pass before any resource provisioning.

Validate that the existing Container Apps Environment (`cae-asps-dev`) supports external TCP ingress,
which requires a custom VNet. If the environment was created without VNet integration, it may need to
be recreated.

```bash
# Check if cae-asps-dev has VNet integration
az containerapp env show \
  --resource-group rg-asps-dev \
  --name cae-asps-dev \
  --query "vnetConfiguration" -o json

# If vnetConfiguration is null/empty, the environment needs VNet:
# 1. Create a VNet + subnet
az network vnet create \
  --resource-group rg-asps-dev \
  --name vnet-asps-dev \
  --location northeurope \
  --address-prefix 10.0.0.0/16

az network vnet subnet create \
  --resource-group rg-asps-dev \
  --vnet-name vnet-asps-dev \
  --name snet-cae \
  --address-prefix 10.0.0.0/23

# 2. Recreate Container Apps Environment with VNet
SUBNET_ID=$(az network vnet subnet show --resource-group rg-asps-dev --vnet-name vnet-asps-dev --name snet-cae --query id -o tsv)
az containerapp env create \
  --resource-group rg-asps-dev \
  --name cae-asps-dev \
  --location northeurope \
  --logs-workspace-id <LOG_ANALYTICS_WORKSPACE_ID> \
  --infrastructure-subnet-resource-id $SUBNET_ID
```

**What to verify:**
- `vnetConfiguration` is populated with subnet ID
- External TCP ingress is supported (requires VNet)
- Private connectivity to MySQL is possible via VNet integration / private endpoint

### Step 1: Azure Database for MySQL Flexible Server (ASPS-695) — DONE

```bash
# Create MySQL Flexible Server
az mysql flexible-server create \
  --resource-group rg-asps-dev \
  --name mysql-asps-dev \
  --location northeurope \
  --admin-user aspsadmin \
  --admin-password <FROM_KEY_VAULT> \
  --sku-name Standard_B1ms \
  --storage-size 32 \
  --version 8.0.21 \
  --public-access None

# Create database (Keycloak uses PostgreSQL — see Step 6)
az mysql flexible-server db create --resource-group rg-asps-dev --server-name mysql-asps-dev --database-name aspsbackend2db
```

Connection string format for EF Core (Pomelo):
```
server=mysql-asps-dev.mysql.database.azure.com;port=3306;database=aspsbackend2db;user=aspsadmin;password=<PASSWORD>;SslMode=Required;
```

### Step 2: Azure Key Vault + Managed Identity (ASPS-696) — DONE

```bash
# Create Key Vault
az keyvault create \
  --resource-group rg-asps-dev \
  --name kv-asps-dev \
  --location northeurope

# Create User-assigned Managed Identity
az identity create \
  --resource-group rg-asps-dev \
  --name id-asps-dev \
  --location northeurope

# Grant Managed Identity access to Key Vault secrets
IDENTITY_PRINCIPAL=$(az identity show --resource-group rg-asps-dev --name id-asps-dev --query principalId -o tsv)
az keyvault set-policy \
  --name kv-asps-dev \
  --object-id $IDENTITY_PRINCIPAL \
  --secret-permissions get list

# Grant Managed Identity ACR pull
IDENTITY_ID=$(az identity show --resource-group rg-asps-dev --name id-asps-dev --query id -o tsv)
ACR_ID=$(az acr show --name acraspsisaacdev --query id -o tsv)
az role assignment create --assignee $IDENTITY_PRINCIPAL --role AcrPull --scope $ACR_ID
```

Secrets to store:
```bash
az keyvault secret set --vault-name kv-asps-dev --name mysql-admin-password --value "<PASSWORD>"
az keyvault secret set --vault-name kv-asps-dev --name mysql-connection-string --value "<FULL_CONN_STRING>"
az keyvault secret set --vault-name kv-asps-dev --name cqrs-shared-secret --value "<SECRET>"
az keyvault secret set --vault-name kv-asps-dev --name keycloak-admin-password --value "<PASSWORD>"
az keyvault secret set --vault-name kv-asps-dev --name keycloak-client-secret --value "<SECRET>"
```

### Step 3: Azure Storage Account + Files Share (ASPS-697) — DONE

```bash
# Create Storage Account
az storage account create \
  --resource-group rg-asps-dev \
  --name staspsdev \
  --location northeurope \
  --sku Standard_LRS \
  --kind StorageV2

# Create file share for CURVE keys
az storage share create \
  --account-name staspsdev \
  --name curve-keys

# Get storage account key (needed for Container Apps volume mount)
az storage account keys list --resource-group rg-asps-dev --account-name staspsdev --query "[0].value" -o tsv
```

### Step 4: Create WebApi Dockerfile + push to ACR (ASPS-698) — DONE

```dockerfile
# WebApi Dockerfile (ASPSBackend14_J/WebApi/Dockerfile)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["WebApi/WebApi.csproj", "WebApi/"]
COPY ["Business/Business.csproj", "Business/"]
COPY ["Common/Common.csproj", "Common/"]
COPY ["Interface/Interface.csproj", "Interface/"]
RUN dotnet restore "WebApi/WebApi.csproj"
COPY . .
RUN dotnet publish "WebApi/WebApi.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5001 5002
ENTRYPOINT ["dotnet", "WebApi.dll"]
```

```bash
cd ASPSBackend14_J
docker build -f WebApi/Dockerfile -t acraspsisaacdev.azurecr.io/asps-webapi:0.1.0 .
az acr login --name acraspsisaacdev
docker push acraspsisaacdev.azurecr.io/asps-webapi:0.1.0
```

### Step 5: Update Backend Docker image (ASPS-701) — DONE

The existing `asps-backend:0.1.0` needs updating to include Python + Playwright (AD-1 Phase 1).

> **Current Dockerfile (2026-08-24):** the snippet below reflects the actual, up-to-date
> `ASPSBackend14_J/ASPSBackend/Dockerfile`. Key points vs. the original version deployed here:
> Python deps are installed from `requirements.lock.txt` (not cherry-picked packages), and
> `Analyzers/` is copied **before** the `pip install` step so the lock file is available to it.
> `--no-deps` is passed to `pip install` — the lock file already pins the full transitive
> dependency set, so `pip` should not resolve anything on its own (see the Python 3.11
> compatibility note right after the build commands below for why version pinning matters here).

```dockerfile
# -------------------------
# Build stage
# -------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy project files first for better Docker layer caching
COPY ["ASPSBackend14_J/ASPSBackend/ASPSBackend.csproj", "ASPSBackend14_J/ASPSBackend/"]
COPY ["ASPSBackend14_J/Business/Business.csproj", "ASPSBackend14_J/Business/"]
COPY ["ASPSBackend14_J/Common/Common.csproj", "ASPSBackend14_J/Common/"]
COPY ["ASPSBackend14_J/Interface/Interface.csproj", "ASPSBackend14_J/Interface/"]

RUN dotnet restore "ASPSBackend14_J/ASPSBackend/ASPSBackend.csproj"

# Copy the full solution source
COPY ASPSBackend14_J/ ASPSBackend14_J/

WORKDIR "/src/ASPSBackend14_J/ASPSBackend"

RUN dotnet publish "ASPSBackend.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# -------------------------
# Runtime stage
# -------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Install Python 3.11 + Playwright so the Backend host can invoke the
# Analyzers/ microservices via subprocess (AD-1 Phase 1).
RUN apt-get update && apt-get install -y python3 python3-pip python3-venv && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/publish .

# Python analyzer microservices (invoked via subprocess, not HTTP — Phase 2/ASPS-708 will split this out)
COPY Analyzers/ /app/Analyzers/

# Install analyzer Python dependencies from the lock file
RUN python3 -m pip install --break-system-packages --no-deps \
    -r /app/Analyzers/basic-url-analyzer/requirements.lock.txt && \
    python3 -m playwright install chromium && \
    python3 -m playwright install-deps

ENV ASPNETCORE_ENVIRONMENT=Docker

# NetMQ ports — 5556 CQRS gateway (WebApi <-> Backend), 50001 real-time
# alert listener, 50002 notification publisher. No HTTP port: this is a
# backend host service, not a web app.
EXPOSE 5556 50001 50002

ENTRYPOINT ["dotnet", "ASPSBackend.dll"]
```

> **Build context:** unlike `WebApi/Dockerfile`, this Dockerfile needs the repo **root** as
> build context (not `ASPSBackend14_J/`), because it copies the Python `Analyzers/` directory,
> which lives at `<repo-root>/Analyzers/` — a sibling of `ASPSBackend14_J/`, not a child of it.

```bash
# Build from the repo root:
az acr build --registry acraspsisaacdev \
  -t asps-backend:0.2.0 \
  -f ASPSBackend14_J/ASPSBackend/Dockerfile .

# local docker equivalent:
docker build -f ASPSBackend14_J/ASPSBackend/Dockerfile -t acraspsisaacdev.azurecr.io/asps-backend:0.2.0 .
docker push acraspsisaacdev.azurecr.io/asps-backend:0.2.0
```

> **Python 3.11 compatibility note (2026-08-24):** the runtime base image
> (`mcr.microsoft.com/dotnet/aspnet:8.0`, Debian bookworm) ships **Python 3.11** via
> `apt-get install python3`. `requirements.lock.txt` must therefore be generated/pinned
> against Python 3.11-compatible package versions — it is not safe to lock against whatever
> versions a developer's local (newer) Python resolves. Specifically: `numpy >= 2.5` requires
> Python >= 3.12, and `scipy >= 1.18` requires Python >= 3.12. Pin `numpy` to the `2.4.x` line
> and `scipy` to the `1.17.x` line (or lower) in `requirements.lock.txt`, or the `pip install`
> step in the Dockerfile above will fail to find a compatible wheel/build for Python 3.11.

### Step 6: Deploy Keycloak (ASPS-700) — DONE

> **Note:** Uses PostgreSQL (not MySQL) due to Azure MySQL `lower_case_table_names=1` incompatibility.
> First-start Liquibase migration (148 changesets) needs ~2-5 min — startup probe configured with 530s window.

```bash
# 1. Create PostgreSQL Flexible Server (if not exists)
az postgres flexible-server create \
  --resource-group rg-asps-dev --name pg-asps-keycloak-dev \
  --location northeurope --admin-user kcadmin --admin-password "<FROM_KV>" \
  --sku-name Standard_B1ms --tier Burstable --storage-size 32 --version 16 \
  --public-access 0.0.0.0 --yes

# 2. Create keycloak database
az postgres flexible-server db create \
  --resource-group rg-asps-dev --server-name pg-asps-keycloak-dev --database-name keycloak

# 3. Deploy Container App with custom probes (YAML — see kc-create.yaml)
az containerapp create --resource-group rg-asps-dev --name ca-keycloak-dev \
  --yaml kc-create.yaml
```

Key container settings:
- Image: `quay.io/keycloak/keycloak:26.0`
- Command: `/opt/keycloak/bin/kc.sh start-dev`
- CPU/Memory: 1.0 / 2Gi (needed for migration)
- `KC_HEALTH_ENABLED=true` — **required**, or `/health/*` 404s (see ASPS-735 fix below)
- Startup probe: `/health/started` on port **9000** (management interface), 30s delay, 50 failures × 10s = 530s window
- Liveness probe: `/health/live` on port **9000** (management interface), 30s interval, 3 failures

> **ASPS-735 fix (2026-08-25):** the original deployment configured the probes with the
> correct paths but pointed them at port 8080 and never set `KC_HEALTH_ENABLED=true`. Keycloak
> 26 serves `/health/*` on a separate **management interface** (port 9000 by default,
> confirmed in container logs: `Management interface listening on http://0.0.0.0:9000`), which
> is only active once the health subsystem is enabled. Every request to `/health/started` on
> 8080 returned 404, so the startup probe's `failureThreshold` of 50 was exhausted after ~530s
> and Container Apps killed the container — a loop of 810+ restarts (CrashLoopBackOff). Fixed by
> adding `KC_HEALTH_ENABLED=true` (and explicit `KC_HTTP_MANAGEMENT_PORT=9000`) and repointing
> both probes to port 9000 via ARM PATCH (env var changes are not safe via
> `az containerapp update` — see the CLI bug note above). See AD-11 in
> `AZURE_ARCHITECTURE.md`.

### Steps 7+8: Deploy WebApi + Backend as Sidecar (ASPS-701/702) — HISTORICAL, superseded by ASPS-725/729 (see Step 16)

> **Superseded (2026-08-23, ASPS-729):** the sidecar pattern documented in this section was
> removed. Backend now runs as its own standalone Container App (`ca-backend-dev`) and WebApi
> connects to it over internal TCP via app-name addressing. See **Step 16** below for the
> current architecture and the reasoning (AD-9). This section is kept for historical record
> and rollback reference only — the YAML below no longer reflects the running configuration.

**Status (at the time): Running** | URL: `https://ca-webapi-dev.purplesand-dfb51ae4.northeurope.azurecontainerapps.io/`
**Revision:** `0000002` (WebApi main + Backend sidecar)

> **Architecture change:** Originally deployed as separate Container Apps. TCP ingress
> (Envoy proxy) failed to forward ZMQ/CURVE protocol. Switched to sidecar pattern —
> both containers in `ca-webapi-dev`, communicate via localhost. Standalone `ca-backend-dev`
> deactivated (kept as rollback safety net).

Deploy via YAML (`az containerapp update --yaml`):
```yaml
properties:
  configuration:
    ingress:
      external: true
      targetPort: 8080
      transport: Http
    secrets:
    - name: db-connection-string
      value: <FROM_KEY_VAULT>
    - name: cqrs-shared-secret
      value: <FROM_KEY_VAULT>
    - name: kc-client-secret
      value: <FROM_KEY_VAULT>
  template:
    containers:
    - name: webapi
      image: acraspsisaacdev.azurecr.io/asps-webapi:0.1.0
      env:
      - name: ConnectionStrings__DefaultConnection
        secretRef: db-connection-string
      - name: CQRS__Endpoint
        value: tcp://localhost:5556
      - name: CQRS__SharedSecret
        secretRef: cqrs-shared-secret
      - name: CQRS__ClientId
        value: asps-webapi
      - name: NetMQ__BusinessEndpoint
        value: tcp://localhost:5555
      - name: NetMQ__AlertListenerEndpoint
        value: tcp://localhost:50001
      - name: Security__CurveEnabled
        value: "true"
      - name: Security__CurveClientOnly
        value: "true"
      - name: Security__ServerPublicKeyFilePath
        value: /keys/curve-server-public-key.txt
      - name: Keycloak__Authority
        value: https://ca-keycloak-dev.purplesand-dfb51ae4.northeurope.azurecontainerapps.io/realms/asps
      - name: Keycloak__ClientId
        value: asps-webapi
      - name: Keycloak__ClientSecret
        secretRef: kc-client-secret
      - name: Keycloak__RequireHttpsMetadata
        value: "true"
      - name: ASPNETCORE_ENVIRONMENT
        value: Docker
      resources:
        cpu: 0.5
        memory: 1Gi
      volumeMounts:
      - volumeName: curve-keys
        mountPath: /keys
    - name: backend
      image: acraspsisaacdev.azurecr.io/asps-backend:0.2.0
      env:
      - name: ConnectionStrings__DefaultConnection
        secretRef: db-connection-string
      - name: CQRS__BindEndpoint
        value: tcp://*:5556
      - name: CQRS__SharedSecret
        secretRef: cqrs-shared-secret
      - name: Security__CurveEnabled
        value: "true"
      - name: Security__KeysFilePath
        value: /keys/curve-server-keys.json
      - name: Security__ServerPublicKeyFilePath
        value: /keys/curve-server-public-key.txt
      - name: NetMQ__BusinessEndpoint
        value: tcp://*:5555
      - name: NetMQ__RealTimeListenerPort
        value: "50001"
      - name: NetMQ__NotificationPublisherPort
        value: "50002"
      - name: Python__ExecutablePath
        value: python3
      - name: Python__AnalyzersFolderPath
        value: /app/Analyzers
      - name: ASPNETCORE_ENVIRONMENT
        value: Docker
      resources:
        cpu: 1.0
        memory: 2Gi
      volumeMounts:
      - volumeName: curve-keys
        mountPath: /keys
    volumes:
    - name: curve-keys
      storageType: AzureFile
      storageName: curvekeys
    scale:
      minReplicas: 1
      maxReplicas: 1
```

**Keycloak OIDC setup** (via REST API):
1. Realm `asps` created with brute force protection
2. Client `asps-webapi` — confidential, standard flow, redirect to WebApi URL
3. User `isaac` (ID `5b631279-4ef0-4a78-898c-1c8451cc295e`) — synced from local Keycloak via partial-import
4. SSO flow verified: Login → Keycloak → Dashboard

**E2E verified:** `GetVersionQuery` ✓, `GetDashboardStatsQuery` ✓, SSO login ✓

### Step 9: Networking (ASPS-703) — HISTORICAL, superseded by ASPS-725/729 (see Step 16)

> At the time: sidecar pattern — all inter-service communication via `localhost`. No TCP
> ingress needed for Backend ports. **This has since been replaced** — see Step 16 for the
> current app-name TCP addressing approach.

- WebApi → Backend CQRS: `tcp://localhost:5556`
- WebApi → Backend NetMQ: `tcp://localhost:5555`
- WebApi → Backend Alerts: `tcp://localhost:50001`
- Keycloak: `https://ca-keycloak-dev.purplesand-dfb51ae4.northeurope.azurecontainerapps.io` (external HTTPS ingress)

**Note:** Device-facing TCP ports (50001, 50002) were NOT externally reachable in sidecar mode. Post-ASPS-727, they are directly exposed on standalone `ca-backend-dev`.

### Step 10: Database Migration (ASPS-704) — DONE (manual)

29 migrations total: 24 applied via `dotnet ef database update`, 5 applied manually via SQL
(migrations without `.Designer.cs` files that EF Core couldn't track).

**Data migration:** Local MySQL → Azure MySQL via `mysqldump` (CMD, not PowerShell to avoid BOM encoding issues):
```bash
# Export (from CMD, not PowerShell!)
"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqldump.exe" -h 127.0.0.1 -P 3306 -uroot -p<PASSWORD> --no-create-info --complete-insert --single-transaction --set-gtid-purged=OFF --column-statistics=0 --ignore-table=ASPSBackend2DB.__efmigrationshistory ASPSBackend2DB > C:\Tmp\asps-data.sql

# Import (from CMD)
"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe" -h mysql-asps-dev.mysql.database.azure.com -P 3306 -uaspsadmin -p --ssl-mode=REQUIRED --force aspsbackend2db < C:\Tmp\asps-data.sql
```

For future migrations, use Container Apps Job with `--migrate-only` flag (see AD-5).

```bash
az containerapp job create \
  --name job-asps-migrate \
  --resource-group rg-asps-dev \
  --environment cae-asps-dev \
  --image acraspsisaacdev.azurecr.io/asps-backend:latest \
  --trigger-type Manual \
  --command "dotnet" "ASPSBackend.dll" "--migrate-only" \
  --env-vars "ConnectionStrings__DefaultConnection=secretref:mysql-connstring"

# Verify job has same network connectivity to MySQL and Managed Identity for Key Vault secrets
```

### Step 11: End-to-End Tests — DONE

Before monitoring/CI/CD, verify the system works:

1. **WebApi** — HTTPS accessible, admin panel loads, Keycloak SSO login works
2. **Backend CQRS** — WebApi can send commands/queries via port 5556
3. **Keycloak** — OIDC discovery endpoint responds, token issuance works
4. **TCP ingress (port 50001)** — desktop agent connects with CURVE encryption
5. **TCP keepalive test** — idle connection >5 minutes, then send message, verify connection survives
6. **Backend restart test** — kill/restart Backend revision, verify desktop agent reconnects, no lost alerts
7. **Notifications (port 50002)** — WebApi SignalR hub receives PUB/SUB notifications
8. **Analyzer** — URL analysis works end-to-end via subprocess in multi-stage image

### Step 12: Monitoring (ASPS-705) — DONE

**Log Analytics:** Container Apps Environment → `log-asps-dev` workspace (stdout/stderr).

**Application Insights:** `appi-asps-dev` (workspace-based, linked to Log Analytics). SDK integration into .NET apps is a future code change.

**Alerts** (email to isaacmendelson@gmail.com via `ag-asps-dev` action group):

| Alert | Severity | Condition | Window |
|---|---|---|---|
| `alert-backend-down` | 1 (Critical) | Replicas < 1 | 5min |
| `alert-webapi-down` | 1 (Critical) | Replicas < 1 | 5min |
| `alert-keycloak-down` | 1 (Critical) | Replicas < 1 | 5min |
| `alert-backend-restarts` | 2 (Error) | RestartCount > 3 | 15min |
| `alert-webapi-restarts` | 2 (Error) | RestartCount > 3 | 15min |

### Step 13: CI/CD (ASPS-706) — DONE

**Workflow:** `.github/workflows/deploy.yml`

**Pipeline stages (current, post-ASPS-730):**
1. `detect-changes` — dorny/paths-filter to determine which images need rebuild
2. `build-test` — `dotnet build` + `dotnet test` on ubuntu-latest (full clone for Nerdbank.GitVersioning)
3. `build-push-backend` — ACR cloud build (`az acr build`), tag = `YYYYMMDD-<sha7>` + `latest`
   (see **Known Issues — ACR build on Windows** in Lessons Learned for two local-build gotchas
   that don't affect the Linux-hosted CI runner but do affect ad-hoc builds from a Windows dev
   machine)
4. `build-push-webapi` — ACR cloud build, same tagging
5. `build-push-angular` — ACR cloud build for Angular admin
6. `deploy-webapi` — `az containerapp update --image` against `ca-webapi-dev` (single container)
7. `deploy-backend` — `az containerapp update --image` against `ca-backend-dev` (single container)
8. `deploy-angular` — `az containerapp update --image` against `ca-angular-admin-dev` (single container)

> **Historical (pre-ASPS-730):** WebApi and Backend used to be deployed together via a single
> `deploy` job that exported the `ca-webapi-dev` sidecar YAML, patched both image tags with
> `sed`, and re-applied the whole YAML — because `az containerapp update --image` only updates
> the container(s) you target and the sidecar had two containers sharing one Container App.
> ASPS-729 removed the sidecar (Backend is now its own Container App), so ASPS-730 replaced
> that single `deploy` job with two independent `deploy-webapi` / `deploy-backend` jobs, each a
> plain `--image` update — same simple pattern `deploy-angular` already used.

**Auth:** Azure AD OIDC (no stored credentials):
- App: `github-actions-asps` (`e3acd155-ce1a-4257-8a41-fd8017e7e72a`)
- Federated credentials:
  - `github-actions-main` — subject: `repo:isaacmendelson/ASPS:ref:refs/heads/main` (build jobs)
  - `github-actions-pr` — subject: `repo:isaacmendelson/ASPS:pull_request` (PR builds)
  - `github-env-dev` — subject: `repo:isaacmendelson/ASPS:environment:dev` (deploy job)
- Roles: Contributor on rg-asps-dev, AcrPush on ACR

**Triggers:**
- **Automatic:** push to `main` with changes in `ASPSBackend14_J/`, `Analyzers/`, `apps/admin/angular/`, or `.github/workflows/deploy.yml`
- **Manual:** `gh workflow run deploy.yml -f deploy_backend=true -f deploy_webapi=true` (requires `Actions` permission on GitHub PAT)

**Setup completed (2026-08-19):**
1. GitHub repo → Settings → Secrets → `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` ✓
2. GitHub repo → Settings → Environments → `dev` environment created ✓
3. Pipeline green — build+test passing ✓
4. GitHub PAT updated with Actions permission for workflow dispatch ✓
5. Azure AD federated credential `github-env-dev` added for deploy job ✓

**TODO — `dev` environment hardening:**
- Required reviewers — אישור לפני deploy
- Branch restriction — רק main יכול לעשות deploy

**Angular admin:** `build-push-angular` builds and pushes the image to ACR; `deploy-angular`
(added ASPS-724) then runs `az containerapp update --image` against `ca-angular-admin-dev` —
simple single-container update, no sidecar YAML patching needed.

### Step 14: Angular Admin Container App (ASPS-724) — DONE

**Status: Running** | URL: `https://ca-angular-admin-dev.purplesand-dfb51ae4.northeurope.azurecontainerapps.io/`

```bash
az containerapp create \
  --name ca-angular-admin-dev \
  --resource-group rg-asps-dev \
  --environment cae-asps-dev \
  --image acraspsisaacdev.azurecr.io/asps-angular-admin:latest \
  --target-port 80 \
  --ingress external \
  --registry-server acraspsisaacdev.azurecr.io \
  --registry-identity <id-asps-dev resource ID> \
  --user-assigned <id-asps-dev resource ID> \
  --min-replicas 1 --max-replicas 1 \
  --cpu 0.25 --memory 0.5Gi \
  --env-vars \
    API_URL=https://ca-webapi-dev.purplesand-dfb51ae4.northeurope.azurecontainerapps.io \
    KEYCLOAK_URL=https://ca-keycloak-dev.purplesand-dfb51ae4.northeurope.azurecontainerapps.io \
    KEYCLOAK_REALM=asps \
    KEYCLOAK_CLIENT_ID=asps-angular-admin
```

**Registry auth:** reuses the `id-asps-dev` managed identity (already has `AcrPull`) — same
pattern as `ca-webapi-dev`, no new secrets.

**Bug found + fixed during deployment:** `nginx.conf` had two `proxy_pass` blocks targeting the
Docker-Compose-only hostname `webapi` (`/api/` and `/notificationshub`). nginx resolves upstream
hostnames at config-load time; when `webapi` doesn't resolve (any environment other than the
Compose network), nginx refuses to start → `CrashLoopBackOff`. The Angular app never actually
calls those relative paths — `ApiService`/`SignalRService` always use the absolute
`RuntimeConfigService.apiUrl` — so the blocks were dead code. Removed them from
`apps/admin/angular/nginx.conf`; image rebuilt (`asps-angular-admin:20260819-nginxfix`) and
redeployed. See [troubleshooting.md](azure/troubleshooting.md).

**CORS:** `ca-webapi-dev` had no `Cors__AllowedOrigins__*` set (fell back to
`http://localhost:4200` only). Added:
```bash
az containerapp update --name ca-webapi-dev --resource-group rg-asps-dev \
  --container-name webapi \
  --set-env-vars "Cors__AllowedOrigins__0=https://ca-angular-admin-dev.purplesand-dfb51ae4.northeurope.azurecontainerapps.io"
```
Verified via CORS preflight (`OPTIONS` with `Origin` header) → `access-control-allow-origin` echoes
the Angular admin origin.

**Keycloak client:** `asps-angular-admin` created in the `asps` realm (previously missing —
confirmed via `GET /admin/realms/asps/clients?clientId=asps-angular-admin` returning `[]`).
Per `docs/specs/ANGULAR_ADMIN_ARCHITECTURE.md` §3.6: public client, Standard Flow only, PKCE
S256, redirect URIs / web origins / root URL set to the Container App URL (plus `localhost:4200`
and `:4201` for local dev). Three protocol mappers added: `groups` (Group Membership →
`groups` claim), `realm roles` (User Realm Role → `realm_access.roles`), `audience`
(Audience Resolve → includes `asps-angular-admin` in token audience).

**Verified:**
- `GET /` → 200, SPA HTML served
- `GET /assets/runtime-config.json` → correct `apiUrl`/`keycloakUrl`/`keycloakRealm`/`keycloakClientId`
- CORS preflight from the Angular admin origin against `ca-webapi-dev` → allowed
- Keycloak OIDC discovery (`/realms/asps/.well-known/openid-configuration`) → resolves, issuer matches

### Step 15: Bicep — Infrastructure as Code (AD-7)

Convert the working deployment to Bicep templates:

1. Codify each resource (VNet, MySQL, Key Vault, Identity, Storage, Container Apps, Job)
2. Parameterize environment-specific values (names, SKUs, secrets references)
3. Verify: `az deployment group create` reproduces the dev environment
4. Git-track under `infra/` or `deploy/bicep/`
5. Integrate into CI/CD pipeline (Step 13)

### Step 16: Split Backend into standalone Container App (ASPS-725/726/727/728/729/730) — DONE

**Status: Running** | `ca-webapi-dev` (WebApi only) + `ca-backend-dev` (Backend only, active)

See **AD-9** for the full rationale. Summary of the executed change:

1. **ASPS-726 (test):** confirmed app-name TCP addressing (`tcp://ca-backend-dev:5556`, not
   FQDN) forwards the ZMQ/CURVE handshake correctly between two Container Apps.
2. **ASPS-727:** `ca-backend-dev` reactivated as a standalone Container App — same image as
   the sidecar, own CPU/memory (1.0/2Gi), CURVE keys volume mounted read-write. Ingress:
   50001 (main, external), 50002 (`additionalPortMappings`, external), 5556
   (`additionalPortMappings`, internal). Port 5555 not mapped at all (AD-4).
3. **ASPS-728:** `ca-webapi-dev`'s `webapi` container env vars updated —
   `CQRS__Endpoint=tcp://ca-backend-dev:5556` (the sidecar `backend` container was kept
   running as a safety net during the transition).
4. **ASPS-729:**
   - Added `AgentGateway__BackendHost=ca-backend-dev` to `webapi`'s env (requires an image
     built from a commit including PR #32 / commit `4b2460e` — the AgentGatewayService
     configurable-host fix; the image running at the time predated that fix and the env var
     was inert until redeployed with a newer image).
   - Verified via container logs: `AgentGatewayService started — REQ endpoint
     tcp://ca-backend-dev:50001, SUB endpoint tcp://ca-backend-dev:50002`.
   - Removed the `backend` sidecar container from `ca-webapi-dev` entirely (ARM PATCH,
     `properties.template.containers` reduced to just `webapi`).
   - `ca-webapi-dev` resources back down to just WebApi's own 0.5 CPU / 1 Gi (Backend's
     1.0 CPU / 2 Gi freed).
   - Azure Files `curve-keys` volume **kept** on `ca-webapi-dev` — `CurveKeyManager` client
     mode still reads `/keys/curve-server-public-key.txt` from it.
   - Verified again after removal: revision `Healthy`/`RunningAtMaxScale`, clean startup logs
     (no connection errors), `GET /` → 302 (Keycloak redirect, proves the app serves
     requests), Keycloak OIDC discovery → 200, standalone `ca-backend-dev` still
     `Healthy`/`RunningAtMaxScale`.
5. **ASPS-730:** `.github/workflows/deploy.yml` — replaced the single sidecar `deploy` job
   (YAML export → `sed` patch → re-apply) with two independent jobs, `deploy-webapi` and
   `deploy-backend`, each a plain `az containerapp update --image` (same pattern as
   `deploy-angular`). Added `BACKEND_APP: ca-backend-dev` to the workflow's top-level `env`.

**Az CLI note:** used the `az rest --method patch` workaround (see
[`.claude/hats/devops/decisions.md`](../../.claude/hats/devops/decisions.md)) for both the
env-var addition and the container-removal PATCH — `az containerapp update
--set-env-vars`/`--yaml` silently drops plain env var values on this CLI extension version.
The plain `--image`-only updates (new WebApi image, and the CI/CD jobs) do not hit this bug
and used the normal CLI.

---

## Ports Reference

| Port | Protocol | Service | Exposure | Notes |
|---|---|---|---|---|
| 8080 | HTTP | WebApi | Public (HTTPS via ingress) | Admin panel + REST API |
| 5556 | TCP | Backend CQRS | Internal only (app-name TCP ingress on `ca-backend-dev`) | WebApi → Backend commands/queries |
| 50001 | TCP | Backend Alerts | External | Desktop agents → Backend (CURVE) |
| 50002 | TCP | Backend Notifications | External (app-name addressed by WebApi's `/ws/agent` gateway) | Backend → WebApi (PUB/SUB) |
| 5555 | TCP | Backend (legacy) | NOT exposed | Security debt — exclude from deployment (AD-4) |
| 8080 | HTTP | Keycloak | Public (HTTPS via ingress) | OIDC login/token endpoints |
| 3306 | TCP | MySQL | Private endpoint | Backend + Keycloak only |

---

## Environment Variables Reference

### Backend (ca-backend-dev, standalone Container App, post-ASPS-727)

| Variable | Value | Source |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | MySQL connection string | Key Vault secret |
| `CQRS__SharedSecret` | HMAC shared secret | Key Vault secret |
| `CQRS__BindEndpoint` | `tcp://*:5556` | Static |
| `Security__CurveEnabled` | `true` | Static |
| `Security__KeysFilePath` | `/keys/curve-server-keys.json` | Static (Azure Files mount, read-write) |
| `Security__ServerPublicKeyFilePath` | `/keys/curve-server-public-key.txt` | Static (Azure Files mount) |
| `Python__AnalyzersFolderPath` | `/app/Analyzers` | Static |
| `Python__ExecutablePath` | `python3` | Static |
| `NetMQ__BusinessEndpoint` | `tcp://*:5555` | Static (legacy, AD-4, not exposed) |
| `NetMQ__RealTimeListenerPort` | `50001` | Static |
| `NetMQ__NotificationPublisherPort` | `50002` | Static |

### WebApi (ca-webapi-dev, standalone Container App, post-ASPS-729 — sidecar removed)

| Variable | Value | Source |
|---|---|---|
| `CQRS__Endpoint` | `tcp://ca-backend-dev:5556` | Static (app-name TCP, not localhost) |
| `CQRS__SharedSecret` | HMAC shared secret | Key Vault secret |
| `AgentGateway__BackendHost` | `ca-backend-dev` | Static (ASPS-729 — `/ws/agent` gateway target host) |
| `Security__CurveEnabled` | `true` | Static |
| `Security__CurveClientOnly` | `true` | Static |
| `Security__ServerPublicKeyFilePath` | `/keys/curve-server-public-key.txt` | Static (Azure Files mount, read-only use — kept after sidecar removal) |
| `Keycloak__Authority` | `https://ca-keycloak-dev.<DOMAIN>/realms/asps` | Static |
| `Keycloak__ClientId` | `asps-webapi` | Static |
| `Keycloak__ClientSecret` | Keycloak client secret | Key Vault secret |

### Keycloak (ca-keycloak-dev)

| Variable | Value | Source |
|---|---|---|
| `KC_DB` | `postgres` | Static |
| `KC_DB_URL` | `jdbc:postgresql://pg-asps-keycloak-dev.postgres.database.azure.com:5432/keycloak` | Static |
| `KC_DB_USERNAME` | `kcadmin` | Static |
| `KC_DB_PASSWORD` | PostgreSQL password | Key Vault (`postgres-keycloak-password`) |
| `KC_HOSTNAME_STRICT` | `false` | Static |
| `KC_HTTP_ENABLED` | `true` | Static |
| `KC_PROXY_HEADERS` | `xforwarded` | Static |
| `KEYCLOAK_ADMIN` | `admin` | Static |
| `KEYCLOAK_ADMIN_PASSWORD` | Admin password | Key Vault (`keycloak-admin-password`) |
| `KC_HEALTH_ENABLED` | `true` | Static — required for `/health/*` probes to respond (ASPS-735) |
| `KC_HTTP_MANAGEMENT_PORT` | `9000` | Static — explicit; matches Keycloak 26 default management interface port |

---

## Security Backlog (post-MVP)

Items accepted for MVP but flagged for future improvement:

| Item | Current State | Target State | Priority |
|---|---|---|---|
| CURVE keys in Azure Files | Private key in file share (read-write mount) | Key Vault / HSM-backed key management | Medium |
| MySQL auth | Username/password | Managed Identity / Entra token auth | Low |
| Port 5555 deprecation | Not exposed but code still exists | Remove from codebase (ASPS-717) | Low |

---

## Lessons Learned

### From Session 1-2
- **Israel Central** region: Container Apps Environment creation was not available.
- **West Europe**: Log Analytics auto-creation failed (region not accepting new customers).
- **North Europe**: Successfully selected for all resources.
- New Azure subscriptions may require explicit resource provider registration.

### From Architecture Review (2026-08-16, ChatGPT cloud architect)
- **Networking must come first** — VNet validation before any resource provisioning. External TCP ingress
  requires VNet integration on the Container Apps Environment.
- **Don't defer IaC** — deploy with CLI first, convert to Bicep immediately after. The working deployment
  is the best context for writing IaC templates.
- **Integration test TCP keepalive** — don't rely on assumptions about Azure load balancer behavior.
  Test idle >5 minutes, reconnection after Backend restart, alert loss scenarios.
- **Container Apps Job needs same network/identity** — verify migration job has MySQL connectivity and
  Managed Identity access to Key Vault secrets.
- **Key Vault references over plain secrets** — use `secretref:` and `keyvaultref:` in Container Apps
  config, not plain secret values passed through CI/CD.

### From Deployment (2026-08-17/18)
- **Container Apps TCP ingress does NOT forward ZMQ/CURVE** — Envoy proxy strips ZMTP wire protocol.
  DNS resolves, TCP connects, but CURVE handshake fails. Solution: sidecar pattern (localhost networking).
- **Sidecar pattern works** — both containers in same Container App share localhost. CQRS via
  `tcp://localhost:5556` bypasses the proxy entirely.
- **EF Core migrations without `.Designer.cs` are invisible** — `dotnet ef database update` silently
  skips them. Must apply manually via SQL. Check `__EFMigrationsHistory` vs migration files.
- **PowerShell `Out-File` adds BOM** — corrupts MySQL dump files. Always use CMD for mysqldump/mysql.
- **PowerShell `<` redirection not supported** — use CMD for piping files into mysql.
- **Local Keycloak port conflict** — `wslrelay` can grab the same port on IPv6 `[::1]`, causing
  browser to hit the wrong service. Use a different port or access via `127.0.0.1`.
- **Keycloak partial-import preserves user IDs** — `POST /admin/realms/{realm}/partialImport` respects
  the `id` field, unlike `POST .../users` which ignores it. Use for syncing users between environments.
- **Azure MySQL `lower_case_table_names=1` is immutable** — breaks Keycloak Liquibase migrations.
  Use PostgreSQL for Keycloak (its recommended DB anyway).

### Known Issues — ACR build on Windows (2026-08-24)

- **`az acr build` crashes with `UnicodeEncodeError` on Windows** — when the build output
  contains non-ASCII characters, the Windows `az` CLI (charmap/cp1252 console encoding) throws
  `UnicodeEncodeError` while streaming logs, even though the remote ACR build itself may be
  succeeding. **Workaround:** run with `--no-logs` to skip the local log stream, then poll
  status with `az acr task list-runs --registry <registry> --top 1`, and if you need the raw
  log content, download it directly via the REST API (`listLogSasUrl`) rather than through the
  CLI's log streamer:
  ```bash
  az acr build --registry acraspsisaacdev -t asps-backend:<tag> \
    -f ASPSBackend14_J/ASPSBackend/Dockerfile . --no-logs

  az acr task list-runs --registry acraspsisaacdev --top 1 -o table

  # Get a SAS URL for the raw log and download it directly (bypasses the CLI's console encoding)
  az acr run show --registry acraspsisaacdev --run-id <RUN_ID> --query "id" -o tsv
  az rest --method post \
    --uri "https://management.azure.com/subscriptions/<SUB>/resourceGroups/rg-asps-dev/providers/Microsoft.ContainerRegistry/registries/acraspsisaacdev/listLogSasUrl?api-version=2019-06-01-preview" \
    --body "{\"runId\": \"<RUN_ID>\"}"
  ```
- **Visual Studio `.vs/` lock files break `az acr build`'s source tar packing** — when building
  from the repo root on a machine where Visual Studio has the solution open, `az acr build`
  fails with `Permission denied` while tarring up the build context, because `.vs/` contains
  files VS holds open/locked. **Workaround:** export a clean copy of the tracked source with
  `git archive` and build from that instead of the working tree directly:
  ```bash
  mkdir -p /tmp/asps-build-src
  git archive HEAD | tar -x -C /tmp/asps-build-src
  cd /tmp/asps-build-src
  az acr build --registry acraspsisaacdev -t asps-backend:<tag> \
    -f ASPSBackend14_J/ASPSBackend/Dockerfile .
  ```
  (On this machine, use the scratchpad directory instead of `/tmp` — see session scratchpad
  path.) `git archive` only exports committed, tracked files, so this also guarantees the
  build context matches what's actually in git, with no stray local/untracked files leaking
  into the image.
