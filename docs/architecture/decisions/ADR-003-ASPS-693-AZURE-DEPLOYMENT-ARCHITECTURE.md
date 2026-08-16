# ADR-003 — ASPS-693 Azure Deployment Architecture Decisions

- Status: Accepted
- Date: 2026-08-15
- Jira: ASPS-693 — `Azure Deployment`
- Decision owners: CEO + user (Isaac)

## Context

ASPS is moving from local Docker Compose to Azure Container Apps. The existing
infrastructure (resource group, ACR, Log Analytics, Container Apps Environment)
was provisioned in a prior session but no application services are deployed yet.

Seven architecture decisions were needed before deployment could begin. Each was
evaluated against the current codebase, Azure platform constraints, and the
principle of minimizing code changes for the initial deployment.

Deployment guide with full `az` CLI commands: `docs/cloud/AZURE_DEPLOYMENT_GUIDE.md`.

## Decisions

### AD-1: Python Analyzers — subprocess vs HTTP API

**Decision:** Option B (multi-stage Docker) for initial deployment; Option C (separate
Container App with HTTP API) as follow-up work (ASPS-708).

**Context:** Backend invokes Python Analyzers as subprocesses via `ProcessStartInfo`
(`AnalyzerV1ProcessClient.cs`). The subprocess model requires Python + Playwright +
Chromium (~400MB) in the same container as the .NET Backend.

**Rationale:** Multi-stage Docker image (~2GB) works with zero code changes — the
subprocess invocation is identical to local dev. Acceptable for dev environment.
Production-grade separation (ASPS-708) will extract the Analyzer into its own
Container App with a FastAPI HTTP endpoint, using an `AnalyzerV1HttpClient` that
implements the same interface. Foundation already exists: `api.py` (FastAPI server)
and `analyzer-client/` (HTTP client) are in the codebase.

### AD-2: CURVE key loading — Key Vault vs Azure Files vs env vars

**Decision:** Option A — Azure Files mount.

**Context:** `CurveKeyManager` reads CURVE keypair files from disk paths configured
in `appsettings.json` (`Security:KeysFilePath`, `Security:ServerPublicKeyFilePath`).
Keys are Z85-encoded text files (~80 bytes each).

**Rationale:** Azure Files share mounted into the container at the expected path
requires zero code changes to `CurveKeyManager`. The file-based loading stays
identical. Cost: < $0.01/month. Both Backend and WebApi containers mount the same
share. Key Vault was considered but would require code changes to load from secrets
instead of files.

### AD-3: Port 50001 — TCP ingress for desktop agent alerts

**Decision:** TCP ingress (Layer 4 pass-through) with TCP keepalive.

**Context:** Desktop agents connect to port 50001 via NetMQ ROUTER socket with CURVE
encryption. Container Apps supports TCP ingress but has a 4-minute idle timeout that
silently drops connections without RST/FIN.

**Rationale:** TCP ingress passes raw TCP — no TLS termination, no HTTP parsing.
CURVE encryption operates end-to-end unmodified. TCP keepalive settings prevent the
load balancer from dropping idle connections:
- `ZMQ_TCP_KEEPALIVE = 1`
- `ZMQ_TCP_KEEPALIVE_IDLE = 60` (seconds)
- `ZMQ_TCP_KEEPALIVE_INTVL = 30` (seconds)

Container Apps Environment must be in a VNet for TCP ingress. Desktop agents connect
to the Container App's FQDN on port 50001.

### AD-4: Port 5555 — security debt

**Decision:** Do not expose in cloud. Separate deprecation task (ASPS-717, low priority).

**Context:** Port 5555 (`NetMQMessageProcessor`) is a legacy CQRS channel running in
parallel with port 5556. It handles only 9 message types (strict subset of 5556's 71).
No encryption (no CURVE, no HMAC), bound to `tcp://*:5555` (all interfaces). Only 2
legacy controllers use it — one marked "not called by any frontend."

**Rationale:** No ingress rule for port 5555 in the Container App. The Backend process
still listens internally, but nothing can reach it from outside the container. WebApi
in a separate Container App cannot reach it either. Code-level consolidation into port
5556 is tracked as ASPS-717 and overlaps with ASPS-675 Phase 6 (messaging refactoring
cleanup). Exposing unencrypted TCP to the internet was rejected outright.

### AD-5: Database migration strategy

**Decision:** Container Apps Job with `--migrate-only` CLI flag.

**Context:** EF Core migrations (Pomelo MySQL) must run before the app serves traffic.
Running `database.Migrate()` at startup risks concurrent execution with multiple replicas.

**Rationale:** A dedicated Container Apps Job uses the same Backend image with a
`--migrate-only` flag added to `Program.cs`. When the flag is present, the app resolves
`DbContext`, calls `database.Migrate()`, and exits. The CI/CD pipeline triggers the job
before deploying a new app version — if migration fails, deploy halts. Safe for any
replica count.

### AD-6: Keycloak — containerized vs Entra ID

**Decision:** Keycloak as Container App (same setup as local dev).

**Context:** ASPS uses Keycloak as OIDC provider for the admin panel SSO. Locally it
runs as a Docker container on port 8180 with MySQL backend.

**Rationale:** Zero code changes — same realm, same clients, same OIDC flow. Realm
export/import from local to cloud. Uses the existing Azure MySQL Flexible Server
(separate `keycloak` database). Entra ID is a viable future migration (OIDC is
standard — mainly config changes) but is a separate project.

### AD-7: Infrastructure as Code

**Decision:** ~~Defer.~~ **Revised (2026-08-16):** Bicep, phased approach.

**Context:** All infrastructure commands are documented in the deployment guide
(`docs/cloud/AZURE_DEPLOYMENT_GUIDE.md`). IaC (Bicep or Terraform) would make
environments reproducible automatically.

**Original rationale (2026-08-15):** Scope is small, only one environment — defer.

**Revised rationale (2026-08-16, per cloud architect review):** CLI first, Bicep
immediately after the deployment is working. The working deployment provides the best
context for writing IaC templates — converting known-good `az` CLI commands to Bicep,
not writing templates from scratch. This also builds real Azure IaC experience.

**Approach:** Phase 1 = `az` CLI → Phase 2 = convert to Bicep → Git-track → CI/CD.

## Summary Table

| # | Topic | Decision | Code changes | JIRA |
|---|---|---|---|---|
| AD-1 | Analyzers | Multi-stage Docker (now), HTTP API (future) | None (now) | ASPS-708 (future) |
| AD-2 | CURVE keys | Azure Files mount | None | ASPS-697 |
| AD-3 | Port 50001 | TCP ingress + keepalive | TCP keepalive settings | ASPS-701 |
| AD-4 | Port 5555 | Don't expose | None | ASPS-717 (deprecation) |
| AD-5 | DB migrations | Container Apps Job | `--migrate-only` flag | ASPS-704 |
| AD-6 | Keycloak | Container App | None | ASPS-700 |
| AD-7 | IaC | CLI first, then Bicep | Bicep templates (Phase 2) | ASPS-706 |

## Architect Review (2026-08-16)

Cloud architect (ChatGPT) reviewed all 7 decisions. Feedback incorporated:

1. **AD-7 revised** — IaC changed from "defer" to "CLI first, Bicep after"
2. **Deployment order revised** — networking validation (VNet) moved to Step 0, before
   any resource provisioning. External TCP ingress requires VNet on the Container Apps
   Environment.
3. **Integration tests added** — TCP keepalive must be tested empirically (idle >5 min,
   reconnection after Backend restart, alert loss scenarios), not assumed.
4. **Security backlog items documented:**
   - CURVE private keys in Azure Files → future move to Key Vault/HSM
   - MySQL username/password auth → future move to Managed Identity/Entra token

## Consequences

- Initial deployment requires **minimal code changes**: only TCP keepalive settings
  (AD-3) and the `--migrate-only` flag (AD-5).
- The multi-stage Docker image (AD-1) will be ~2GB — acceptable for dev, replaced by
  separate Analyzer Container App (ASPS-708) for production.
- Port 5555 is dead code in cloud context — tracked for removal (ASPS-717).
- **Networking validation is a prerequisite** — must verify VNet before provisioning
  any Azure services.
- Bicep templates will be written after the deployment is working (AD-7 Phase 2).
