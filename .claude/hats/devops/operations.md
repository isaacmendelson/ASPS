# DevOps Operations — Self-Service Runbook

Self-service operations the CEO can trigger by phrase. Each operation is
end-to-end: DevOps executes all steps, updates JIRA, and reports back.

**Related:** [azure-runbook.md](azure-runbook.md) has the raw technical
procedures, known CLI bugs, and env var inventory. This file adds the
CEO-facing structure: trigger phrases, post-conditions, and JIRA rules.

**Universal rules:**

- DevOps never deploys without an explicit request or approval.
- DevOps always updates JIRA after completing a deploy (per `feedback_jira_auto_sync.md`).
- DevOps always notifies the CEO when done (status, image tag, evidence).
- Use **PowerShell** for all Azure CLI commands — Git Bash mangles `/app/...`
  paths (per `feedback_azure_env_vars_powershell.md`).
- For the VS `.vsidx` lock issue during `az acr build`, use the `git archive`
  workaround documented in `troubleshooting.md`.

**Azure resource constants:**

| Resource | Value |
|---|---|
| ACR | `acraspsisaacdev` |
| Resource Group | `rg-asps-dev` |
| Backend Container App | `ca-backend-dev` |
| WebApi Container App | `ca-webapi-dev` |
| Angular Admin Container App | `ca-angular-admin-dev` |
| Keycloak Container App | `ca-keycloak-dev` |
| Backend image repo | `asps-backend` |
| WebApi image repo | `asps-webapi` |
| Angular Admin image repo | `asps-angular-admin` |

---

## Operation: Deploy Backend

**Trigger:** "deploy backend" or "deploy backend to Azure"

**Steps:**

1. Ensure working tree is clean or stash unrelated changes.
2. Build the image via ACR remote build. Use `git archive` to avoid VS lock
   issues (see `troubleshooting.md`).
   ```powershell
   $tag = "manual-$(Get-Date -Format 'yyyyMMdd')-<descriptor>"
   git archive HEAD | tar -x -C $scratchDir/build-src
   az acr build --registry acraspsisaacdev `
     --image asps-backend:$tag `
     --file $scratchDir/build-src/ASPSBackend14_J/ASPSBackend/Dockerfile `
     $scratchDir/build-src
   ```
   - CI/CD tag convention: `YYYYMMDD-<sha7>` (automatic).
   - Manual tag convention: `manual-YYYYMMDD-<descriptor>`.
   - Build context is the repo root (`.`), not `ASPSBackend14_J/`.
3. Deploy the image to `ca-backend-dev`:
   ```powershell
   az containerapp update `
     --name ca-backend-dev `
     --resource-group rg-asps-dev `
     --image acraspsisaacdev.azurecr.io/asps-backend:$tag
   ```
4. Wait 15 seconds, then verify the new revision is running:
   ```powershell
   az containerapp show --name ca-backend-dev --resource-group rg-asps-dev `
     --query "{latest: properties.latestRevisionName, ready: properties.latestReadyRevisionName, status: properties.provisioningState}" `
     -o table
   ```
   - Confirm `latestRevisionName` equals `latestReadyRevisionName`.
5. Update JIRA with a deployment comment (image tag, timestamp, revision name).
6. Notify CEO: deployed image tag, revision name, running status.

**Post-conditions:**

- `ca-backend-dev` runs the new image.
- `latestRevisionName == latestReadyRevisionName`.
- JIRA issue has a deployment comment.

**JIRA:** Add comment with image tag and deployment timestamp. If deploying for
a specific issue, transition per `task-workflow.md`.

---

## Operation: Deploy WebApi

**Trigger:** "deploy webapi" or "deploy webapi to Azure"

**Steps:**

1. Ensure working tree is clean or stash unrelated changes.
2. Build the image via ACR remote build:
   ```powershell
   $tag = "manual-$(Get-Date -Format 'yyyyMMdd')-<descriptor>"
   git archive HEAD | tar -x -C $scratchDir/build-src
   az acr build --registry acraspsisaacdev `
     --image asps-webapi:$tag `
     --file $scratchDir/build-src/ASPSBackend14_J/WebApi/Dockerfile `
     $scratchDir/build-src/ASPSBackend14_J
   ```
   - Build context is `ASPSBackend14_J/` (not repo root).
3. Deploy the image to `ca-webapi-dev`:
   ```powershell
   az containerapp update `
     --name ca-webapi-dev `
     --resource-group rg-asps-dev `
     --image acraspsisaacdev.azurecr.io/asps-webapi:$tag
   ```
4. Wait 15 seconds, then verify the new revision is running:
   ```powershell
   az containerapp show --name ca-webapi-dev --resource-group rg-asps-dev `
     --query "{latest: properties.latestRevisionName, ready: properties.latestReadyRevisionName, status: properties.provisioningState}" `
     -o table
   ```
5. Update JIRA with a deployment comment.
6. Notify CEO: deployed image tag, revision name, running status.

**Post-conditions:**

- `ca-webapi-dev` runs the new image.
- `latestRevisionName == latestReadyRevisionName`.
- JIRA issue has a deployment comment.

**JIRA:** Add comment with image tag and deployment timestamp.

---

## Operation: Rollback

**Trigger:** "rollback backend" or "rollback webapi"

**Steps:**

1. List recent revisions for the target app:
   ```powershell
   az containerapp revision list `
     --name <app-name> `
     --resource-group rg-asps-dev `
     --query "[].{name:name, active:properties.active, created:properties.createdTime, image:properties.template.containers[0].image}" `
     -o table
   ```
2. Identify the previous healthy revision (the one before the current
   `latestRevisionName`).
3. Activate the previous revision and deactivate the current one:
   ```powershell
   az containerapp revision activate --name <app-name> --resource-group rg-asps-dev --revision <previous-revision>
   az containerapp ingress traffic set --name <app-name> --resource-group rg-asps-dev --revision-weight <previous-revision>=100
   az containerapp revision deactivate --name <app-name> --resource-group rg-asps-dev --revision <current-revision>
   ```
4. Verify the rollback took effect:
   ```powershell
   az containerapp show --name <app-name> --resource-group rg-asps-dev `
     --query "{latest: properties.latestRevisionName, ready: properties.latestReadyRevisionName}" `
     -o table
   ```
5. Notify CEO: rolled back from revision X to revision Y, running status.

**Post-conditions:**

- The app runs the previous revision.
- The failed revision is deactivated.
- CEO is informed with before/after revision names.

**JIRA:** Add comment documenting the rollback (reason, from/to revisions, timestamp).

---

## Operation: Check Health

**Trigger:** "check azure status" or "is backend running?" or "health check"

**Steps:**

1. Show running status of all container apps:
   ```powershell
   $apps = @("ca-backend-dev", "ca-webapi-dev", "ca-angular-admin-dev", "ca-keycloak-dev")
   foreach ($app in $apps) {
     az containerapp show --name $app --resource-group rg-asps-dev `
       --query "{name:name, status:properties.provisioningState, latest:properties.latestRevisionName, ready:properties.latestReadyRevisionName, image:properties.template.containers[0].image}" `
       -o table
   }
   ```
2. For each app, check if `latestRevisionName == latestReadyRevisionName` (healthy)
   or not (unhealthy/deploying).
3. If any app is unhealthy, pull recent error logs:
   ```powershell
   az containerapp logs show --name <app-name> --resource-group rg-asps-dev --tail 30 --type console
   ```
4. Report a summary table to the CEO:
   ```
   | App | Status | Revision | Image Tag | Healthy |
   ```

**Post-conditions:**

- CEO receives a status table for all container apps.
- Any errors or unhealthy states are highlighted.

**JIRA:** No JIRA update unless an issue is discovered.

---

## Operation: Build Only

**Trigger:** "build backend image" or "build webapi image" (no deploy)

**Steps:**

1. Build and push to ACR using the same process as the deploy operations above,
   but skip the `az containerapp update` step.
2. After build, retrieve the image digest:
   ```powershell
   az acr repository show-manifests --name acraspsisaacdev `
     --repository <image-repo> `
     --query "[?tags[?contains(@, '<tag>')]].{tag:tags[0], digest:digest, created:createdTime}" `
     -o table
   ```
3. Report to CEO: image tag, digest, ACR repository, and timestamp.

**Post-conditions:**

- Image is in ACR with the specified tag.
- No container app was updated.
- CEO has the image tag and digest for later deployment.

**JIRA:** No JIRA update (image build only, no deployment).

---

## Operation: View Logs

**Trigger:** "show backend logs" or "check for errors" or "show webapi logs"

**Steps:**

1. Pull recent console logs:
   ```powershell
   az containerapp logs show `
     --name <app-name> `
     --resource-group rg-asps-dev `
     --tail 50 `
     --type console
   ```
2. Filter output for errors and warnings (look for `Error`, `Exception`,
   `Warning`, `FATAL`, `fail`).
3. If system logs are needed (container lifecycle events):
   ```powershell
   az containerapp logs show `
     --name <app-name> `
     --resource-group rg-asps-dev `
     --tail 30 `
     --type system
   ```
4. Report findings to CEO:
   - Total lines retrieved.
   - Count of errors/warnings found.
   - The actual error lines with context.
   - Assessment: healthy (no errors) or needs attention (with specifics).

**Post-conditions:**

- CEO receives a log summary with any errors highlighted.

**JIRA:** No JIRA update unless a new issue is discovered that warrants a ticket.
