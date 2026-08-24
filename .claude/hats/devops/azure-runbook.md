# Azure Runbook

Step-by-step procedures for common Azure operations. Source of truth for full architecture
and env vars: `docs/cloud/AZURE_DEPLOYMENT_GUIDE.md` and `docs/cloud/AZURE_ARCHITECTURE.md`.

Verify commands and resource names against those documents before executing.

---

## Deploy Backend

**Image:** `asps-backend` | **Dockerfile:** `ASPSBackend14_J/ASPSBackend/Dockerfile` | **App:** `ca-backend-dev`

### 1. Build image

```bash
az acr build --registry acraspsisaacdev --resource-group rg-asps-dev \
  --image asps-backend:<tag> \
  --file ASPSBackend14_J/ASPSBackend/Dockerfile .
```

**Build context must be `.` (repo root)**, not `ASPSBackend14_J/` — the Dockerfile copies `Analyzers/` which is a sibling of `ASPSBackend14_J/`.

### 2. Known build issues

| Issue | Symptom | Workaround |
|---|---|---|
| VS indexer lock | `Permission denied` tarring `.vs/` files | Stop `ServiceHub.IndexingService` process, or use `git archive` to export a clean copy (see below) |
| Windows `UnicodeEncodeError` | `az acr build` crashes streaming non-ASCII log output | Add `--no-logs`, then poll with `az acr task list-runs --registry acraspsisaacdev --top 1 -o table` |

**`git archive` workaround for VS lock:**
```bash
mkdir -p <scratchpad>/asps-build-src
git archive HEAD | tar -x -C <scratchpad>/asps-build-src
cd <scratchpad>/asps-build-src
az acr build --registry acraspsisaacdev --image asps-backend:<tag> \
  --file ASPSBackend14_J/ASPSBackend/Dockerfile .
```

### 3. Deploy to Container App

```bash
az containerapp update \
  --name ca-backend-dev \
  --resource-group rg-asps-dev \
  --image acraspsisaacdev.azurecr.io/asps-backend:<tag>
```

### 4. Verify

- Check the output for the new revision name.
- Confirm `provisioningState: Succeeded` and `runningStatus: Running`.
- Check logs: `az containerapp logs show --name ca-backend-dev --resource-group rg-asps-dev --type console`

---

## Deploy WebApi

**Image:** `asps-webapi` | **Dockerfile:** `Dockerfile.webapi` | **App:** `ca-webapi-dev`

### 1. Build image

```bash
az acr build --registry acraspsisaacdev --resource-group rg-asps-dev \
  --image asps-webapi:<tag> \
  --file Dockerfile.webapi .
```

### 2. Deploy to Container App

```bash
az containerapp update \
  --name ca-webapi-dev \
  --resource-group rg-asps-dev \
  --image acraspsisaacdev.azurecr.io/asps-webapi:<tag>
```

### 3. Verify

Same pattern as Backend: check revision, `provisioningState`, `runningStatus`, and logs.

```bash
az containerapp logs show --name ca-webapi-dev --resource-group rg-asps-dev --type console
```

---

## Deploy Angular Admin

**Image:** `asps-angular-admin` | **App:** `ca-angular-admin-dev`

```bash
az acr build --registry acraspsisaacdev --resource-group rg-asps-dev \
  --image asps-angular-admin:<tag> \
  --file <angular-dockerfile-path> .

az containerapp update \
  --name ca-angular-admin-dev \
  --resource-group rg-asps-dev \
  --image acraspsisaacdev.azurecr.io/asps-angular-admin:<tag>
```

---

## Rollback

### Find previous revision

```bash
az containerapp revision list \
  --name <app-name> \
  --resource-group rg-asps-dev \
  -o table
```

### Route traffic to previous revision

```bash
az containerapp ingress traffic set \
  --name <app-name> \
  --resource-group rg-asps-dev \
  --revision-weight <previous-revision-name>=100
```

### Verify rollback

```bash
az containerapp show \
  --name <app-name> \
  --resource-group rg-asps-dev \
  --query "properties.latestReadyRevisionName" -o tsv
```

---

## Check Health

### Container App status

```bash
az containerapp show \
  --name <app-name> \
  --resource-group rg-asps-dev \
  --query "properties.runningStatus" -o json
```

### Container logs

```bash
az containerapp logs show \
  --name <app-name> \
  --resource-group rg-asps-dev \
  --type console
```

### List all app statuses

```bash
for app in ca-backend-dev ca-webapi-dev ca-keycloak-dev ca-angular-admin-dev; do
  echo "--- $app ---"
  az containerapp show --name $app --resource-group rg-asps-dev \
    --query "{name:name, status:properties.runningStatus, revision:properties.latestReadyRevisionName}" -o json
done
```

### MySQL connectivity

- Host: `mysql-asps-dev.mysql.database.azure.com`
- Admin user: `aspsadmin`
- Database: `aspsbackend2db`
- Requires `--ssl-mode=REQUIRED`

```bash
mysql -h mysql-asps-dev.mysql.database.azure.com -P 3306 -uaspsadmin -p --ssl-mode=REQUIRED aspsbackend2db
```

---

## Image Tagging Strategy

| Source | Tag format | Example |
|---|---|---|
| Manual builds | `manual-YYYYMMDD-<descriptor>` | `manual-20260825-v1env-fix` |
| CI builds | `YYYYMMDD-<short-sha>` | `20260824-9230e0e` |
| CI latest | `latest` | Always points to most recent CI build |

CI pipeline (`deploy.yml`) auto-tags with both `YYYYMMDD-<sha7>` and `latest`.

---

## Known Azure CLI Bugs

### `az containerapp update` silently drops env vars

**CLI extension version:** `1.3.0b4` (latest available as of 2026-08-22).

**Problem:** `--yaml`, `--replace-env-vars`, and `--set-env-vars` silently drop every plain
(non-`secretRef`) env var value on write. Only `secretRef`-backed entries survive.

**Safe patterns:**

| Operation | Method |
|---|---|
| Image-only update | `az containerapp update --image` (safe, does not touch env vars) |
| Env var changes, ingress changes, container add/remove | `az rest --method patch` with ARM API (see below) |
| Image tag change in CI/CD | Export YAML, `sed`-patch only the image tag, re-apply (safe, does not restructure env) |

**ARM PATCH workaround:**
```bash
az rest --method patch \
  --url "https://management.azure.com/subscriptions/<SUB>/resourceGroups/rg-asps-dev/providers/Microsoft.App/containerApps/<app-name>?api-version=2025-07-01" \
  --body @patch.json
```

**Important when using ARM PATCH:**
- Fetch real secret values first: `az containerapp secret list --name <app> --resource-group rg-asps-dev --show-values`
- ARM PATCH does not auto-preserve secrets (unlike the CLI).
- Keep secret values in ephemeral scratch files only, never logged or committed.
- Strip these fields from `show` output before PATCHing (rejected as unknown): `targetPortHttpScheme`, `revisionTransitionThreshold`, `targetLabel`, `imageType`, `customMetricsSettings`.
- Poll `properties.provisioningState` before issuing a new PATCH (`409 ContainerAppOperationInProgress` otherwise).

### TCP ingress `external` is all-or-nothing

The `external` flag on Container Apps TCP ingress applies to whichever port is the *main*
`ingress.targetPort`. An `additionalPortMappings` entry cannot be `external: true` while the
main port is `external: false`. To get mixed exposure, make one of the external ports the main
ingress port and put the internal one in `additionalPortMappings` with `external: false`.

---

## Environment Variables

Variable names only. No secret values. See `docs/cloud/AZURE_DEPLOYMENT_GUIDE.md` for the
complete reference with value patterns.

### ca-backend-dev

| Variable | Type |
|---|---|
| `ConnectionStrings__DefaultConnection` | Secret (Key Vault) |
| `CQRS__SharedSecret` | Secret (Key Vault) |
| `CQRS__BindEndpoint` | Static |
| `Security__CurveEnabled` | Static |
| `Security__KeysFilePath` | Static |
| `Security__ServerPublicKeyFilePath` | Static |
| `Python__AnalyzersFolderPath` | Static |
| `Python__ExecutablePath` | Static |
| `NetMQ__BusinessEndpoint` | Static |
| `NetMQ__RealTimeListenerPort` | Static |
| `NetMQ__NotificationPublisherPort` | Static |
| `Messaging__AcceptLegacyV0` | Static (`false` in cloud) |

### ca-webapi-dev

| Variable | Type |
|---|---|
| `CQRS__Endpoint` | Static |
| `CQRS__SharedSecret` | Secret (Key Vault) |
| `CQRS__ClientId` | Static |
| `AgentGateway__BackendHost` | Static |
| `Security__CurveEnabled` | Static |
| `Security__CurveClientOnly` | Static |
| `Security__ServerPublicKeyFilePath` | Static |
| `Keycloak__Authority` | Static |
| `Keycloak__ClientId` | Static |
| `Keycloak__ClientSecret` | Secret (Key Vault) |
| `Keycloak__RequireHttpsMetadata` | Static |
| `Cors__AllowedOrigins__0` | Static |
| `ASPNETCORE_ENVIRONMENT` | Static |
| `NetMQ__BusinessEndpoint` | Static |
| `NetMQ__AlertListenerEndpoint` | Static |

### ca-keycloak-dev

| Variable | Type |
|---|---|
| `KC_DB` | Static |
| `KC_DB_URL` | Static |
| `KC_DB_USERNAME` | Static |
| `KC_DB_PASSWORD` | Secret (Key Vault) |
| `KC_HOSTNAME_STRICT` | Static |
| `KC_HTTP_ENABLED` | Static |
| `KC_PROXY_HEADERS` | Static |
| `KEYCLOAK_ADMIN` | Static |
| `KEYCLOAK_ADMIN_PASSWORD` | Secret (Key Vault) |

### ca-angular-admin-dev

| Variable | Type |
|---|---|
| `API_URL` | Static |
| `KEYCLOAK_URL` | Static |
| `KEYCLOAK_REALM` | Static |
| `KEYCLOAK_CLIENT_ID` | Static |
