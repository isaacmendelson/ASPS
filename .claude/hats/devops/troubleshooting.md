# DevOps Troubleshooting

Common issues encountered during build, deploy, and operations. Each entry:
symptom, cause, fix. For Azure-specific issues with full diagnosis commands,
see also `docs/cloud/azure/troubleshooting.md`.

---

## VS IndexingService lock — `Permission denied: .vs/...vsidx`

**Symptom:** `az acr build` fails with:
```
ERROR: [Errno 13] Permission denied: 'ASPSBackend14_J\.vs\ASPSBackend\FileContentIndex\<guid>.vsidx'
```

**Cause:** `az acr build` tars the entire source directory before applying
`.dockerignore`. Visual Studio's `ServiceHub.IndexingService` holds a lock on
`.vs\` cache files. The lock causes a `PermissionError` during tar creation,
even though `.vs/` is in `.dockerignore`.

**Fix:** Build from a clean copy using `git archive` (only tracked files, no
locked files):
```powershell
git archive HEAD | tar -x -C $scratchDir/build-src
az acr build --registry acraspsisaacdev `
  --image <repo>:<tag> `
  --file $scratchDir/build-src/<Dockerfile-path> `
  $scratchDir/build-src/<context-path>
```

Alternative (more disruptive): kill the indexer first:
```powershell
Stop-Process -Name "ServiceHub.IndexingService*" -Force
```
Visual Studio will restart the indexer automatically within seconds.

**Caveat — `git archive` only includes tracked files.** `Dockerfile.backend`
`COPY`s `ASPSBackend14_J/ASPSBackend/appsettings.Docker.json`, which is
git-ignored (per `feedback_gitignored_appsettings_use_envvars.md`) but still
present on disk in a normal working copy. `git archive HEAD` silently drops
it, and the build fails at the `COPY` step with "file not found in build
context". If the `.vs` lock isn't actually blocking the build, prefer
building directly from the working directory (`.dockerignore` already
excludes `.vs/`, `bin/`, `obj/`) — only fall back to `git archive` when the
lock genuinely reproduces, and if so, temporarily copy the missing
gitignored file into the archived tree before building.

---

## ACR build context path — `Unable to find Dockerfile`

**Symptom:** `az acr build` cannot find the Dockerfile or source files.

**Cause:** Different components have different Dockerfile locations and build
contexts. Using the wrong combination produces errors.

**Correct paths:**

| Component | Dockerfile | Build context |
|---|---|---|
| Backend (CI/CD) | `ASPSBackend14_J/ASPSBackend/Dockerfile` | `.` (repo root) |
| Backend (local) | `Dockerfile.backend` | `.` (repo root) |
| WebApi (CI/CD) | `ASPSBackend14_J/WebApi/Dockerfile` | `ASPSBackend14_J/` |
| WebApi (local) | `Dockerfile.webapi` | `.` (repo root) |
| Angular Admin | `apps/admin/angular/Dockerfile` | `apps/admin/angular/` |
| Analyzer (local) | `Dockerfile.analyzer` | `.` (repo root) |

The `ASPSBackend14_J/Dockerfile` also exists but is the OLD Dockerfile for local
compose. Do not confuse it with the CI/CD Dockerfiles.

---

## Azure env var bug — `az containerapp update` drops plain env vars

**Symptom:** After running `az containerapp update --set-env-vars` or
`--replace-env-vars`, all plain (non-`secretRef`) environment variables are
silently wiped. Only `secretRef`-backed entries survive.

**Cause:** Bug in the `containerapp` CLI extension (v1.3.0b4 and earlier).
Reproduced independently twice. See `decisions.md` for full details.

**Fix:** Two proven-safe workarounds:

1. **YAML sed-patch** (image-only updates): Export YAML, `sed`-patch only the
   image tag string, re-apply. Do not restructure env/ingress via the CLI.
2. **ARM REST API** (env var or config changes):
   ```powershell
   az rest --method patch `
     --url "https://management.azure.com/subscriptions/<sub>/resourceGroups/rg-asps-dev/providers/Microsoft.App/containerApps/<app>?api-version=2025-07-01" `
     --body @patch.json
   ```
   - Fetch real secret values first via
     `az containerapp secret list --show-values` (ARM PATCH does not
     auto-preserve secrets).
   - Keep secret values in ephemeral scratch files only; never log or commit.
   - Strip unsupported fields from the body: `targetPortHttpScheme`,
     `revisionTransitionThreshold`, `targetLabel`, `imageType`,
     `customMetricsSettings`.
   - Poll `properties.provisioningState` to `Succeeded` before issuing
     another PATCH (`409 ContainerAppOperationInProgress` otherwise).

---

## Git Bash path mangling — `/app/...` becomes `C:/Program Files/Git/app/...`

**Symptom:** Azure env var values or resource IDs containing `/` are corrupted.
For example, `/app/Analyzers` appears in the container as
`C:/Program Files/Git/app/Analyzers`.

**Cause:** Git Bash / MSYS2 auto-converts arguments that start with `/` into
Windows paths before passing them to the process.

**Fix:** Always use **PowerShell** for Azure CLI commands, especially when
setting env vars with path values or passing resource IDs. Never use Git Bash
for these operations.

If Git Bash must be used:
```bash
export MSYS_NO_PATHCONV=1
```

---

## Docker .sh line endings — `exit 127` in Alpine

**Symptom:** Docker container exits immediately with code 127 when running a
`.sh` entrypoint script. Error:
```
/bin/sh: ./entrypoint.sh: not found
```

**Cause:** Windows CRLF (`\r\n`) line endings in `.sh` files. Alpine's `/bin/sh`
cannot parse them.

**Fix:** Use `.gitattributes` to force LF for all `.sh` files:
```gitattributes
*.sh text eol=lf
```

For an existing file, convert immediately:
```bash
sed -i 's/\r$//' entrypoint.sh
```

---

## Python deps from lockfile — missing analyzer dependencies

**Symptom:** URL analysis returns only cached/whitelisted results. Analyzer
invocations for new URLs silently produce no output.

**Cause:** The Dockerfile installed only a few hand-picked Python packages
instead of the full dependency set. The Python subprocess fails on the first
missing import, but the backend swallows the exception.

**Fix:** Always install from `requirements.lock.txt`, never cherry-pick:
```dockerfile
COPY Analyzers/ /app/Analyzers/
RUN python3 -m pip install --break-system-packages --no-deps \
    -r /app/Analyzers/basic-url-analyzer/requirements.lock.txt
```

The lock file is the single source of truth for the dependency set.

---

## Container App revision not ready — `latestReadyRevisionName` != `latestRevisionName`

**Symptom:** After deploying a new image, `az containerapp show` returns a
`latestRevisionName` that differs from `latestReadyRevisionName`. The new
revision may show `Provisioning` or `Failed` state.

**Cause:** New revisions take a few seconds to pull the image, start the
container, and pass health checks. A failed pull or crash loop will leave
the revision in a non-ready state permanently.

**Fix:**

1. Wait 15-30 seconds and re-check.
2. Poll until they match or the provisioning state settles:
   ```powershell
   az containerapp show --name <app> --resource-group rg-asps-dev `
     --query "{latest: properties.latestRevisionName, ready: properties.latestReadyRevisionName, state: properties.provisioningState}" `
     -o table
   ```
3. If stuck, check replica status and logs:
   ```powershell
   az containerapp replica list --name <app> --resource-group rg-asps-dev `
     --query "[0].properties.containers[].{name:name, ready:ready, state:runningState, details:runningStateDetails}"
   az containerapp logs show --name <app> --resource-group rg-asps-dev --tail 30 --type console
   ```
4. If the revision is in `CrashLoopBackOff`, fix the root cause and redeploy.
   The previous healthy revision continues to serve traffic until the new one
   is ready (default Container Apps behavior).

---

## MSB3027/MSB3021 build warnings — file lock during DLL copy

**Symptom:** Build output includes warnings like:
```
warning MSB3027: Could not copy "..." to "...". Beginning retry 1 in 1000ms.
warning MSB3021: Unable to copy file "..." to "...". The process cannot access the file because it is being used by another process.
```

**Cause:** A running process (the backend service, a test runner, or Visual
Studio) holds a lock on the output DLL. The compiler succeeded; only the
post-build copy step failed.

**Fix:**

- These are **not real build errors**. Compilation succeeded.
- Look for `error CS####` lines to identify actual compilation failures.
- To eliminate the warnings: stop the process holding the DLL, then rebuild.
- In CI this does not occur (no running processes hold locks).

---

## Container App env var has no effect — image predates the code

**Symptom:** Setting a new env var on a Container App succeeds (PATCH returns
OK, revision healthy), but the application ignores the value entirely.

**Cause:** The currently deployed image was built from a commit before the code
that reads that config key was added. The binary simply does not contain the
`IOptions<T>` property or config-binding call. ASP.NET Core silently ignores
unknown config keys.

**Fix:**

1. Compare the deployed image's build commit against the commit that introduced
   the config change:
   ```powershell
   az containerapp show --name <app> --resource-group rg-asps-dev `
     --query "properties.template.containers[0].image" -o tsv
   # Image tag: YYYYMMDD-<sha7> — check if sha7 predates the fix
   git merge-base --is-ancestor <sha7> <fix-commit>; echo $LASTEXITCODE
   # 0 = image predates the fix, need to rebuild
   ```
2. Rebuild and push a new image from a commit that includes the fix.
3. Redeploy the new image, then set the env var.

---

## ACR build log crash — `UnicodeEncodeError` on Windows

**Symptom:** `az acr build` crashes mid-stream with:
```
UnicodeEncodeError: 'charmap' codec can't encode character ... in position ...
```

**Cause:** Windows console codepage (cp1252) cannot render certain UTF-8
characters in the build output (Angular/npm banners with special characters).
The ACR Task itself keeps running successfully.

**Fix:** The remote build is unaffected. Poll the run status instead:
```powershell
az acr task list-runs --registry acraspsisaacdev --top 1 --query "[0].status" -o tsv
```

To reduce crash risk, set before the command:
```powershell
$env:PYTHONIOENCODING = "utf-8"
```

---

## nginx CrashLoopBackOff — "host not found in upstream"

**Symptom:** Container replica stuck in `CrashLoopBackOff`. Logs show:
```
host not found in upstream "webapi" in /etc/nginx/conf.d/default.conf
```

**Cause:** `nginx.conf` contained `proxy_pass` blocks targeting Docker
Compose-only service hostnames (e.g., `webapi`). nginx resolves upstream
hostnames at config-load time. If the hostname does not resolve via DNS
(true outside Compose), nginx refuses to start.

**Fix:** Remove dead `proxy_pass` location blocks that reference Compose-only
hostnames. The Angular app uses absolute URLs from `RuntimeConfigService.apiUrl`,
not relative paths through nginx proxy.

**Diagnosis:**
```powershell
az containerapp replica list --name <app> --resource-group rg-asps-dev `
  --query "[0].properties.containers[].{name:name, ready:ready, state:runningState, details:runningStateDetails}"
az containerapp logs show --name <app> --resource-group rg-asps-dev --tail 60 --type console
```

---

## SupportedSchemaMajors missing — "No mutually supported messaging schema major"

**Symptom:** DeviceLogin and desktop agent auth requests fail with:
```
No mutually supported messaging schema major
```

**Cause:** Backend checks `SupportedSchemaMajors` in auth messages. A missing
field defaults to `[0]`. Azure runs with `AcceptLegacyV0=false`, so v0-only
messages are rejected.

**Fix:** Include `SupportedSchemaMajors = [1]` (C#: `new[] { 1 }`) in every
`RegisterDevice`, `RequestToken`, and `RefreshToken` message payload.

---

## Desktop agent connects to localhost instead of Azure

**Symptom:** Agent logs show `[ZMQ] Server: tcp://127.0.0.1:50001` despite
`config.py` having the Azure hostname.

**Cause:** `config.py` imports `from config_override import *` at the bottom.
The gitignored `config_override.py` has `BACKEND_HOST = "127.0.0.1"`, which
silently overwrites the `config.py` value.

**Fix:** Edit `config_override.py` to set the correct Azure hostname. Always
check `config_override.py` first when debugging wrong desktop agent config.
