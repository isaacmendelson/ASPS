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
| Backend Image | `asps-backend:0.1.0` | ACR | Pushed |

Application services are NOT yet deployed.

---

## Target Architecture

```
                    Internet
                       |
              Azure Container Apps Environment (cae-asps-dev)
                       |
         ┌─────────────┼─────────────┐
         |             |             |
    WebApi (public)  Backend     Keycloak
    HTTPS ingress    (internal)  (auth endpoint)
         |             |             |
         |       ┌─────┼─────┐      |
         |       |     |     |      |
         |     :5556 :50001 :50002  |
         |     CQRS  Alerts  Notif  |
         |     (int) (ext)   (int)  |
         |       |     |     |      |
         └───────┴─────┴─────┴──────┘
                       |
              ┌────────┼────────┐
              |        |        |
         Azure MySQL  Key Vault  Azure Files
         Flex Server  (secrets)  (CURVE keys)
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

**Decision:** Option A — Keycloak as Container App (same setup as local dev).

**Problem:** ASPS uses Keycloak as OIDC provider for the admin panel SSO. Locally it runs as a Docker
container on port 8180 with a MySQL-backed `keycloak` database. Need to decide how to run it in Azure.

**Options evaluated:**
- **A) Keycloak Container App (chosen)** — deploy the same Keycloak image as a Container App.
  Use the Azure MySQL Flexible Server (separate `keycloak` database). Export local realm and import to cloud.
  Zero code changes — same Authority URL, same client IDs, same OIDC flow.
- **B) Microsoft Entra ID** — replace Keycloak with Azure AD/Entra ID. Managed service, zero maintenance,
  MFA built-in. But requires code changes (claims mapping, token validation, auth flow adjustments).
  Better suited as a future migration after the deployment is stable.

**Implementation:**
1. Pin Keycloak image version (match local dev version)
2. Mirror image to ACR: `acraspsisaacdev.azurecr.io/keycloak:<version>`
3. Create `keycloak` database on Azure MySQL Flexible Server
4. Export local realm: `docker exec keycloak /opt/keycloak/bin/kc.sh export --realm asps --file /tmp/realm.json`
5. Deploy Container App:
   ```bash
   az containerapp create \
     --name ca-asps-keycloak \
     --resource-group rg-asps-dev \
     --environment cae-asps-dev \
     --image acraspsisaacdev.azurecr.io/keycloak:<version> \
     --target-port 8080 \
     --ingress external \
     --min-replicas 1 --max-replicas 1 \
     --cpu 0.5 --memory 1Gi \
     --env-vars \
       KC_DB=mysql \
       KC_DB_URL=secretref:kc-db-url \
       KC_DB_USERNAME=secretref:kc-db-user \
       KC_DB_PASSWORD=secretref:kc-db-pass \
       KEYCLOAK_ADMIN=secretref:kc-admin-user \
       KEYCLOAK_ADMIN_PASSWORD=secretref:kc-admin-pass \
       KC_HOSTNAME_STRICT=false
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

---

## Deployment Sequence

Order matters — each step depends on the previous ones.

### Step 0: Networking Validation — Checkpoint 0

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

### Step 1: Azure Database for MySQL Flexible Server (ASPS-695)

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

# Create databases
az mysql flexible-server db create --resource-group rg-asps-dev --server-name mysql-asps-dev --database-name aspsbackend2db
az mysql flexible-server db create --resource-group rg-asps-dev --server-name mysql-asps-dev --database-name keycloak
```

Connection string format for EF Core (Pomelo):
```
server=mysql-asps-dev.mysql.database.azure.com;port=3306;database=aspsbackend2db;user=aspsadmin;password=<PASSWORD>;SslMode=Required;
```

### Step 2: Azure Key Vault + Managed Identity (ASPS-696)

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

### Step 3: Azure Storage Account + Files Share (ASPS-697)

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

### Step 4: Create WebApi Dockerfile + push to ACR (ASPS-698)

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

### Step 5: Update Backend Docker image (ASPS-701)

The existing `asps-backend:0.1.0` needs updating to include Python + Playwright (AD-1 Phase 1).

```dockerfile
# Backend multi-stage Dockerfile with Python + Playwright
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish "ASPSBackend/ASPSBackend.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
# Install Python 3.11 + Playwright deps
RUN apt-get update && apt-get install -y python3 python3-pip python3-venv && \
    python3 -m pip install --break-system-packages playwright scikit-learn requests && \
    python3 -m playwright install chromium && \
    python3 -m playwright install-deps && \
    apt-get clean && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
COPY Analyzers/ /app/Analyzers/
EXPOSE 5556 50001 50002
ENTRYPOINT ["dotnet", "ASPSBackend.dll"]
```

```bash
docker build -f ASPSBackend/Dockerfile -t acraspsisaacdev.azurecr.io/asps-backend:0.2.0 .
docker push acraspsisaacdev.azurecr.io/asps-backend:0.2.0
```

### Step 6: Deploy Keycloak (ASPS-700)

```bash
az containerapp create \
  --resource-group rg-asps-dev \
  --name ca-keycloak-dev \
  --environment cae-asps-dev \
  --image quay.io/keycloak/keycloak:26.0 \
  --target-port 8080 \
  --ingress external \
  --min-replicas 1 --max-replicas 1 \
  --cpu 0.5 --memory 1Gi \
  --command "start" \
  --env-vars \
    KC_DB=mysql \
    KC_DB_URL=jdbc:mysql://mysql-asps-dev.mysql.database.azure.com:3306/keycloak \
    KC_DB_USERNAME=aspsadmin \
    KC_HOSTNAME_STRICT=false \
    KC_HTTP_ENABLED=true \
  --secrets \
    kc-db-password=keyvaultref:https://kv-asps-dev.vault.azure.net/secrets/mysql-admin-password,identityref:/subscriptions/<SUB>/resourceGroups/rg-asps-dev/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-asps-dev \
    kc-admin-password=keyvaultref:https://kv-asps-dev.vault.azure.net/secrets/keycloak-admin-password,identityref:/subscriptions/<SUB>/resourceGroups/rg-asps-dev/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-asps-dev
```

### Step 7: Deploy Backend Container App (ASPS-701)

```bash
az containerapp create \
  --resource-group rg-asps-dev \
  --name ca-backend-dev \
  --environment cae-asps-dev \
  --image acraspsisaacdev.azurecr.io/asps-backend:0.2.0 \
  --target-port 5556 \
  --transport tcp \
  --ingress internal \
  --min-replicas 1 --max-replicas 1 \
  --cpu 1 --memory 2Gi \
  --user-assigned <IDENTITY_ID> \
  --registry-server acraspsisaacdev.azurecr.io \
  --registry-identity <IDENTITY_ID> \
  --env-vars \
    Python__AnalyzersFolderPath=/app/Analyzers \
    Python__ExecutablePath=python3 \
    Security__CurveEnabled=true \
    Security__KeysFilePath=/keys/curve-server-keys.json \
    Security__ServerPublicKeyFilePath=/keys/curve-server-public-key.txt \
    CQRS__BindEndpoint=tcp://*:5556
```

Volume mount for CURVE keys (Azure Files):
```bash
# Add storage to Container Apps Environment
az containerapp env storage set \
  --resource-group rg-asps-dev \
  --name cae-asps-dev \
  --storage-name curvekeys \
  --azure-file-account-name staspsdev \
  --azure-file-account-key <STORAGE_KEY> \
  --azure-file-share-name curve-keys \
  --access-mode ReadWrite

# Update container app with volume mount
# (requires YAML manifest or ARM/Bicep template for volume mounts)
```

### Step 8: Deploy WebApi Container App (ASPS-702)

```bash
az containerapp create \
  --resource-group rg-asps-dev \
  --name ca-webapi-dev \
  --environment cae-asps-dev \
  --image acraspsisaacdev.azurecr.io/asps-webapi:0.1.0 \
  --target-port 5001 \
  --ingress external \
  --min-replicas 1 --max-replicas 1 \
  --cpu 0.5 --memory 1Gi \
  --user-assigned <IDENTITY_ID> \
  --registry-server acraspsisaacdev.azurecr.io \
  --registry-identity <IDENTITY_ID> \
  --env-vars \
    CQRS__Endpoint=tcp://ca-backend-dev:5556 \
    Security__CurveEnabled=true \
    Security__CurveClientOnly=true \
    Security__ServerPublicKeyFilePath=/keys/curve-server-public-key.txt \
    Keycloak__Authority=https://ca-keycloak-dev.<ENV_DOMAIN>/realms/asps
```

Volume mount for CURVE keys (read-only):
```bash
az containerapp env storage set \
  --resource-group rg-asps-dev \
  --name cae-asps-dev \
  --storage-name curvekeysro \
  --azure-file-account-name staspsdev \
  --azure-file-account-key <STORAGE_KEY> \
  --azure-file-share-name curve-keys \
  --access-mode ReadOnly
```

### Step 9: Configure Networking (ASPS-703)

> VNet was validated/established in Step 0. This step configures service-level networking.

Internal service discovery (Container Apps Environment auto-provides DNS):
- Backend: `ca-backend-dev` resolves internally
- Keycloak: `ca-keycloak-dev` resolves internally

External endpoints:
- WebApi: `https://ca-webapi-dev.<ENV_DOMAIN>` (HTTPS, public)
- Backend port 50001: TCP ingress via `additionalPortMappings` (desktop agent alerts)
- Keycloak: `https://ca-keycloak-dev.<ENV_DOMAIN>` (HTTPS, auth endpoints)

### Step 10: Database Migration (ASPS-704)

Container Apps Job with `--migrate-only` flag (see AD-5).

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

### Step 11: End-to-End Tests

Before monitoring/CI/CD, verify the system works:

1. **WebApi** — HTTPS accessible, admin panel loads, Keycloak SSO login works
2. **Backend CQRS** — WebApi can send commands/queries via port 5556
3. **Keycloak** — OIDC discovery endpoint responds, token issuance works
4. **TCP ingress (port 50001)** — desktop agent connects with CURVE encryption
5. **TCP keepalive test** — idle connection >5 minutes, then send message, verify connection survives
6. **Backend restart test** — kill/restart Backend revision, verify desktop agent reconnects, no lost alerts
7. **Notifications (port 50002)** — WebApi SignalR hub receives PUB/SUB notifications
8. **Analyzer** — URL analysis works end-to-end via subprocess in multi-stage image

### Step 12: Monitoring (ASPS-705)

TBD — Application Insights setup.

### Step 13: CI/CD (ASPS-706)

TBD — GitHub Actions workflows.

### Step 14: Bicep — Infrastructure as Code (AD-7)

Convert the working deployment to Bicep templates:

1. Codify each resource (VNet, MySQL, Key Vault, Identity, Storage, Container Apps, Job)
2. Parameterize environment-specific values (names, SKUs, secrets references)
3. Verify: `az deployment group create` reproduces the dev environment
4. Git-track under `infra/` or `deploy/bicep/`
5. Integrate into CI/CD pipeline (Step 13)

---

## Ports Reference

| Port | Protocol | Service | Exposure | Notes |
|---|---|---|---|---|
| 5001 | HTTP | WebApi | Public (HTTPS via ingress) | Admin panel + REST API |
| 5556 | TCP | Backend CQRS | Internal only | WebApi → Backend commands/queries |
| 50001 | TCP | Backend Alerts | External (TBD) | Desktop agents → Backend (CURVE) |
| 50002 | TCP | Backend Notifications | Internal only | Backend → WebApi (PUB/SUB) |
| 5555 | TCP | Backend (legacy) | NOT exposed | Security debt — exclude from deployment |
| 8080 | HTTP | Keycloak | Public (HTTPS via ingress) | OIDC login/token endpoints |
| 3306 | TCP | MySQL | Private endpoint | Backend + Keycloak only |

---

## Environment Variables Reference

### Backend (ca-backend-dev)

| Variable | Value | Source |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | MySQL connection string | Key Vault secret |
| `CQRS__SharedSecret` | HMAC shared secret | Key Vault secret |
| `CQRS__BindEndpoint` | `tcp://*:5556` | Static |
| `Security__CurveEnabled` | `true` | Static |
| `Security__KeysFilePath` | `/keys/curve-server-keys.json` | Static (Azure Files mount) |
| `Security__ServerPublicKeyFilePath` | `/keys/curve-server-public-key.txt` | Static (Azure Files mount) |
| `Python__AnalyzersFolderPath` | `/app/Analyzers` | Static |
| `Python__ExecutablePath` | `python3` | Static |
| `Keycloak__Authority` | `https://ca-keycloak-dev.<DOMAIN>/realms/asps` | Static |

### WebApi (ca-webapi-dev)

| Variable | Value | Source |
|---|---|---|
| `CQRS__Endpoint` | `tcp://ca-backend-dev:5556` | Static (service discovery) |
| `CQRS__SharedSecret` | HMAC shared secret | Key Vault secret |
| `Security__CurveEnabled` | `true` | Static |
| `Security__CurveClientOnly` | `true` | Static |
| `Security__ServerPublicKeyFilePath` | `/keys/curve-server-public-key.txt` | Static (Azure Files mount) |
| `Keycloak__Authority` | `https://ca-keycloak-dev.<DOMAIN>/realms/asps` | Static |
| `Keycloak__ClientId` | `asps-webapi` | Static |
| `Keycloak__ClientSecret` | Keycloak client secret | Key Vault secret |

### Keycloak (ca-keycloak-dev)

| Variable | Value | Source |
|---|---|---|
| `KC_DB` | `mysql` | Static |
| `KC_DB_URL` | `jdbc:mysql://mysql-asps-dev.mysql.database.azure.com:3306/keycloak` | Static |
| `KC_DB_USERNAME` | `aspsadmin` | Static |
| `KC_DB_PASSWORD` | MySQL password | Key Vault secret |
| `KEYCLOAK_ADMIN` | `admin` | Static |
| `KEYCLOAK_ADMIN_PASSWORD` | Admin password | Key Vault secret |

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
