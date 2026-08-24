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

## `az acr build` — Permission denied packing `.vs\...\.vsidx` (Windows)

**Symptom:**
```
WARNING: Packing source code into tar to upload...
ERROR: [Errno 13] Permission denied: 'ASPSBackend14_J\.vs\ASPSBackend\FileContentIndex\<guid>.vsidx'
```

**Cause:** `az acr build` tars the entire source directory tree for upload **before** it
applies `.dockerignore` filtering (confirmed: `.dockerignore` already excludes `.vs/`, but the
local packing step still tries to read every file first). Visual Studio locks its `.vs\`
cache/index files while running, causing a Windows file-lock `PermissionError` mid-tar — this
is unrelated to the actual Docker build context and happens even when `.vs/` is correctly
`.dockerignore`d.

**Resolution:** build from a clean, `.vs`-free copy of the tracked source instead of the live
working directory. `git archive` only exports tracked files (so gitignored dirs like `.vs/`,
`bin/`, `obj/` are never present), and never touches locked files:
```bash
git archive HEAD | tar -x -C /path/to/scratch/build-src
az acr build --registry <acr> --image <repo>:<tag> \
  --file /path/to/scratch/build-src/ASPSBackend14_J/WebApi/Dockerfile \
  /path/to/scratch/build-src/ASPSBackend14_J
```
Alternative (not used, more disruptive): close Visual Studio first so `.vs\` unlocks.

## Container Apps — env var change has no runtime effect (image predates the code that reads it)

**Symptom:** Set a new plain env var (e.g. `AgentGateway__BackendHost=ca-backend-dev`) on a
Container App, PATCH succeeds, revision goes `Healthy` — but the application log still shows
the **old** hardcoded/default behavior (e.g. `AgentGatewayService started — REQ endpoint
tcp://localhost:50001` instead of the new host).

**Cause:** the currently-deployed image was built from a commit **before** the code change
that added support for reading that config key. Setting the env var is inert — the binary in
that image doesn't have the corresponding `IOptions<T>` property or config-binding call at all,
so nothing reads it (no error, since ASP.NET Core config binding silently ignores unknown keys).
Encountered in ASPS-729: `ca-webapi-dev` was running `asps-webapi:20260819-0604ba5`
(commit `0604ba5`), which predates commit `4b2460e` (`AgentGatewayService` configurable
`BackendHost`, merged in PR #32/`9f834bb`).

**Diagnosis:** compare the deployed image's build commit against the commit that introduced the
config-reading code:
```bash
az containerapp show --name <app> --resource-group <rg> \
  --query "properties.template.containers[?name=='<container>'].image" -o tsv
# image tag convention: YYYYMMDD-<sha7> — check if <sha7> is an ancestor of the fix commit
git merge-base --is-ancestor <sha7> <fix-commit-sha> && echo "image predates the fix"
```

**Resolution:** rebuild and push a new image from a commit that includes the fix (see the
`git archive` workaround above for the `.vs` lock issue), then redeploy that image tag to the
Container App before relying on the new env var/config key.

## Messaging — "No mutually supported messaging schema major" on Azure DeviceLogin

**Symptom:** DeviceLogin page (WebApi) and desktop agent's `RequestToken` both fail with:
```
No mutually supported messaging schema major
```

**Cause:** Backend's `AlertProcessor.HandleRegisterDevice` (line 340-342) checks
`SupportedSchemaMajors` in the incoming message. A missing or absent field defaults to `[0]`.
Azure runs with `AcceptLegacyV0=false`, so v0-only messages are rejected. The WebApi
`DeviceLogin.cshtml.cs` and the desktop agent's `alert_builders.py` were not sending
`SupportedSchemaMajors` at all.

**Resolution:** add `SupportedSchemaMajors = [1]` (or `new[] { 1 }` in C#) to every
`RegisterDevice`, `RequestToken`, and `RefreshToken` message payload — both in WebApi and
desktop agent.

**Files changed:**
- `ASPSBackend14_J/WebApi/Pages/DeviceLogin.cshtml.cs` — added `SupportedSchemaMajors = new[] { 1 }`
- `apps/desktop/win/src/alert_builders.py` — added `"SupportedSchemaMajors": [1]` to
  `build_request_token_message` and `build_refresh_token_message`

**Key insight:** any new environment with `AcceptLegacyV0=false` (including Azure) will reject
messages without this field. Always include `SupportedSchemaMajors` in auth-related messages.

## Desktop agent — connects to localhost instead of Azure Backend

**Symptom:** desktop agent logs show `[ZMQ] Server: tcp://127.0.0.1:50001` and
`RequestToken response: DeviceNotRecognized` even though `config.py` has the Azure hostname
commented in.

**Cause:** `config.py` imports `from config_override import *` at the end (lines 414-415).
The gitignored `config_override.py` had `BACKEND_HOST = "127.0.0.1"`, which silently overwrites
the value in `config.py` at import time.

**Diagnosis:** the `-B` flag (skip .pyc) doesn't help — it's not a cache issue. Search for the
override mechanism:
```bash
grep -n "config_override\|from.*import \*" apps/desktop/win/src/config.py
```

**Resolution:** edit `config_override.py` to set the correct Azure hostname. The committed
`config.py` should keep `127.0.0.1` as the default (local dev). Environment-specific overrides
go in `config_override.py`, generated by `build_release.py --env <name>`.

**Key insight:** when debugging "wrong config" in the desktop agent, always check
`config_override.py` first — it has final say over all `config.py` values.

## Container Apps — `az containerapp update --set-env-vars` path mangling from Git Bash

**Symptom:** env var value `/app/Analyzers` appears in the container as
`C:/Program Files/Git/app/Analyzers`.

**Cause:** this is the same MSYS2 path conversion documented above (see "MSYS path conversion"),
but applied to env var *values*, not just resource IDs. Git Bash converts any argument starting
with `/` to a Windows path before the process receives it. The `--set-env-vars` syntax
`KEY=VALUE` doesn't protect the value from conversion.

**Resolution:** use **PowerShell** instead of Git Bash for `az containerapp update` commands
that set path-like env var values:
```powershell
az containerapp update --name <app> --resource-group <rg> `
  --set-env-vars "Python__AnalyzersFolderPath=/app/Analyzers"
```

Alternatively, `export MSYS_NO_PATHCONV=1` before the Bash command.

**Diagnosis:**
```bash
az containerapp exec --name <app> --resource-group <rg> --command "printenv Python__AnalyzersFolderPath"
```

## Docker — Python analyzer dependencies incomplete (silent failure)

**Symptom:** URL analysis returns only cached/whitelisted results. Analyzer invocations for
new URLs silently produce no output (the `Process` exits with an import error, but the Backend
swallows the exception in background dispatch).

**Cause:** the Backend Dockerfile installed only `playwright scikit-learn requests`, but the
`basic-url-analyzer` requires ~30 additional packages (`beautifulsoup4`, `lxml`, `httpx`,
`python-whois`, `validators`, `langdetect`, `duckduckgo_search`, `pydantic`, etc.). The
Python subprocess fails on the first missing import.

**Resolution:** install from the analyzer's lock file instead of cherry-picking packages:
```dockerfile
COPY Analyzers/ /app/Analyzers/
RUN python3 -m pip install --break-system-packages --no-deps \
    -r /app/Analyzers/basic-url-analyzer/requirements.lock.txt && \
    python3 -m playwright install chromium && \
    python3 -m playwright install-deps
```

**Key insight:** always install Python dependencies from the project's `requirements.lock.txt`,
never manually list individual packages in the Dockerfile. The lock file is the single source
of truth for the dependency set.

## Container Apps — appsettings.Docker.json is gitignored (not in CI builds)

**Symptom:** Backend runs with `ASPNETCORE_ENVIRONMENT=Docker` but doesn't pick up
Docker-specific configuration from `appsettings.Docker.json`.

**Cause:** `.gitignore` line 140 excludes `appsettings.Docker.json`. GitHub Actions
`actions/checkout@v4` only checks out tracked files, so the file is absent from the CI build
context. The Dockerfile's `dotnet publish` copies only files present in the build context —
no `appsettings.Docker.json` means ASP.NET Core falls back to `appsettings.json` defaults.

**Resolution:** use Azure Container App **env vars** for Docker-specific configuration instead
of relying on `appsettings.Docker.json`. ASP.NET Core's `__`-delimited env vars map to config
sections:
```powershell
az containerapp update --name <app> --resource-group <rg> `
  --set-env-vars "Python__ExecutablePath=python3" "Python__AnalyzersFolderPath=/app/Analyzers"
```

**Key insight:** any `appsettings.*.json` file that is gitignored will NOT be present in
CI-built Docker images. For Container Apps, env vars are the correct mechanism for
environment-specific config — they're visible in the Azure portal, versionable via IaC, and
don't require the file to be tracked in git.
