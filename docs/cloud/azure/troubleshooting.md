# Azure Troubleshooting — ASPS

## MissingSubscriptionRegistration
Cause: required resource provider was not registered.

Resolution:
```powershell
az provider register --namespace Microsoft.ContainerRegistry
az provider show --namespace Microsoft.ContainerRegistry --query registrationState -o tsv
```

## Container Apps unavailable in Israel Central
Symptom: Azure rejected creation of a Container Apps managed environment in `israelcentral`.

Resolution: choose a region returned as eligible by Azure CLI.

## West Europe rejected new Log Analytics workspace
Symptom:
```text
RequestDisallowedByAzure
The selected region is currently not accepting new customers.
```

Resolution: move the deployment to `northeurope` and create the Log Analytics workspace explicitly.

## Multiline JMESPath query failed in PowerShell
Cause: PowerShell split the query incorrectly.

Resolution: prefer short single-line `--query` expressions or one provider check per command.

---

## CI/CD — OIDC federated credential mismatch for deploy job

**Symptom:**
```
AADSTS700213: No matching federated identity record found for presented assertion subject
'repo:isaacmendelson/ASPS:environment:dev'
```

**Cause:** The deploy job uses `environment: dev` in the workflow YAML, which changes the OIDC subject claim from `repo:...:ref:refs/heads/main` to `repo:...:environment:dev`. The Azure AD app only had federated credentials for `ref:refs/heads/main` and `pull_request`, not for `environment:dev`.

**Resolution:**
```bash
az ad app federated-credential create \
  --id <APP_OBJECT_ID> \
  --parameters "{
    \"name\": \"github-env-dev\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"repo:isaacmendelson/ASPS:environment:dev\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }"
```

**Key insight:** Each unique OIDC subject claim needs its own federated credential. GitHub Actions sends different subjects for: branch refs, pull requests, and named environments.

## CI/CD — workflow dispatch 403 with fine-grained PAT

**Symptom:**
```
HTTP 403: Resource not accessible by personal access token
```
when running `gh workflow run deploy.yml`.

**Cause:** Fine-grained PAT was missing the **Actions** repository permission.

**Resolution:** GitHub → Settings → Developer settings → Personal access tokens → Edit token → Add permissions → **Actions: Read and write** → Update.

## CI/CD — Nerdbank.GitVersioning shallow clone error

**Symptom:**
```
Shallow clone lacks the objects required to calculate version height
```

**Cause:** `actions/checkout@v4` defaults to `fetch-depth: 1` (shallow clone). Nerdbank.GitVersioning needs full git history to calculate version numbers.

**Resolution:** Add `fetch-depth: 0` to the checkout step:
```yaml
- uses: actions/checkout@v4
  with:
    fetch-depth: 0
```

## CI/CD — AnalyzerV1ProcessClientTests fails in CI

**Symptom:** Test `RunAsync_RealAnalyzerSubprocess_PreservesEchoAndReturnsStructuredError` fails because it requires a Python venv at `Analyzers/basic-url-analyzer/.venv/Scripts/python.exe`.

**Cause:** CI runners don't have the Python venv set up.

**Resolution:** Added early return when venv is absent:
```csharp
var python = Path.Combine(analyzerDirectory, ".venv", "Scripts", "python.exe");
if (!File.Exists(python))
    return; // Python venv not available (CI)
```

File: `ASPS.Tests/Business/UserDomain/AnalyzerV1ProcessClientTests.cs`

## Local dev — CURVE server public key empty

**Symptom:**
```
System.InvalidOperationException: CURVE server public key must be a 40-character Z85 value.
```

**Cause:** `C:\Users\Isaac\AppData\Local\ASPS\curve-server-public-key.txt` was empty (0 bytes). WebApi reads this file in client-only mode (`CurveClientOnly=true`) and expects a 40-character Z85 key.

**Resolution:** Extract the Z85 key from `curve-server-keys.json` (which Backend generates on first run) and write it to the txt file:
```powershell
$keys = Get-Content "$env:LOCALAPPDATA\ASPS\curve-server-keys.json" | ConvertFrom-Json
$keys.ServerPublicKeyZ85 | Set-Content "$env:LOCALAPPDATA\ASPS\curve-server-public-key.txt" -NoNewline
```

**Root cause:** Backend generates both files, but if Backend didn't run (or crashed before writing), the txt file may be empty. Always start Backend before WebApi in local dev.

## Local dev — Keycloak not running

**Symptom:** WebApi throws `SocketException` connecting to `localhost:8081` on startup.

**Cause:** Keycloak runs in Docker (`docker compose up -d keycloak`). If Docker isn't running or the container stopped, WebApi can't reach the OIDC provider.

**Resolution:**
```bash
cd C:\Jobs\ASPS\GitHub\Software
docker compose up -d keycloak
```

**Note:** Must run from the project directory (where `docker-compose.yml` lives). Running from another directory gives "no configuration file provided".

## Container Apps — nginx CrashLoopBackOff, "host not found in upstream"

**Symptom:**
```
2026/08/19 18:53:31 [emerg] 1#1: host not found in upstream "webapi" in /etc/nginx/conf.d/default.conf:18
nginx: [emerg] host not found in upstream "webapi" in /etc/nginx/conf.d/default.conf:18
```
Container replica stuck in `CrashLoopBackOff`, `runningState: Waiting`.

**Cause:** `apps/admin/angular/nginx.conf` had `proxy_pass http://webapi:8080/...` blocks
targeting the Docker-Compose-only service hostname `webapi`. nginx resolves upstream
hostnames **at config-load time** (no `resolver` + variable indirection was used), so if the
hostname doesn't resolve via DNS — true for any environment other than the Compose network,
including Azure Container Apps — nginx refuses to start at all.

**Resolution:** confirmed the Angular app never calls those relative paths (`ApiService` and
`SignalRService` always use the absolute `RuntimeConfigService.apiUrl`), so the blocks were
dead code. Removed both `proxy_pass` location blocks from `nginx.conf`. Rebuilt and pushed the
image (`az acr build`), then `az containerapp update --image <new-tag>`.

**Diagnosis commands:**
```bash
az containerapp replica list --name <app> --resource-group <rg> \
  --query "[0].properties.containers[].{name:name,ready:ready,state:runningState,details:runningStateDetails}"
az containerapp logs show --name <app> --resource-group <rg> --container <container> --tail 60
```

## `az` CLI — resource ID rejected despite looking correct (MSYS path conversion)

**Symptom:**
```
ERROR: --registry-identity must be an identity resource ID or 'system' or 'system-environment'
```
even though the resource ID string looks correct and validates fine when tested directly against
`azure.mgmt.core.tools.is_valid_resource_id`.

**Cause:** Git Bash / MSYS2 on Windows auto-converts arguments that look like POSIX absolute paths
(anything starting with `/`) into Windows paths before they reach the process — so
`/subscriptions/<id>/resourceGroups/...` silently gets mangled before `az` ever sees it.

**Resolution:** set `export MSYS_NO_PATHCONV=1` before running `az` commands that pass Azure
resource IDs as arguments (`--registry-identity`, `--user-assigned`, `--scope`, etc.) from Git Bash.

## `az acr build` — UnicodeEncodeError while streaming build logs (Windows console)

**Symptom:**
```
UnicodeEncodeError: 'charmap' codec can't encode character '❯' in position 108
```
CLI crashes while streaming remote build logs (e.g. Angular CLI's `❯` banner character), even
though the ACR Task itself keeps running and can succeed.

**Cause:** Windows console codepage (cp1252) can't render some UTF-8 characters emitted by the
build (npm/ng banners). This is a local log-streaming display failure, not a build failure.

**Resolution:** the remote ACR Task run is unaffected — poll it independently instead of relying
on the crashed CLI output:
```bash
az acr task list-runs --registry <registry> --top 1 --query "[0].status" -o tsv
```
Setting `export PYTHONIOENCODING=utf-8` before the command reduces (but doesn't fully eliminate)
the crash risk.

## Local dev — Docker containers using stale images

**Symptom:** After `docker compose build`, containers still run old code.

**Cause:** `docker compose up -d` reuses existing containers if they're already running.

**Resolution:**
```bash
docker compose up -d --force-recreate
```
