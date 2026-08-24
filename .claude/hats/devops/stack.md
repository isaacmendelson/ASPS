# DevOps Stack and Tooling

Verify paths and versions against actual files before relying on this list.

## Docker

| File | Purpose |
|---|---|
| `docker-compose.yml` | Full local stack: mysql, backend, webapi, keycloak, analyzer |
| `Dockerfile.backend` | .NET 8 runtime + thin Python IPC client for analyzer |
| `Dockerfile.webapi` | .NET 8 WebApi (Razor Pages + Keycloak OIDC) |
| `Dockerfile.analyzer` | Playwright/Python analyzer with iptables egress firewall |

### Container inventory

| Container | Image base | Ports | Notes |
|---|---|---|---|
| `asps-mysql` | `mysql:8.0` | 3307→3306 | Init from `aspsbackend2db_*.sql` |
| `asps-backend` | `dotnet/runtime:8.0` + python3 | 5555, 50001, 50002 | NetMQ CURVE endpoints |
| `asps-webapi` | `dotnet/aspnet:8.0` | 8080 | Keycloak OIDC, Razor Pages |
| `asps-keycloak` | `keycloak:26.7.0` | 8081→8080 | Dev SSO, no realm import yet |
| `asps-analyzer` | `playwright/python:v1.61.0` | — (UDS only) | read_only, egress-filtered |

### Volumes

- `mysql_data` — persistent DB
- `curve_keys` / `curve_public_keys` — CURVE encryption keys
- `keycloak_data` — Keycloak realm + config
- `analyzer_socket` — Unix domain socket IPC between backend↔analyzer

### Networks

- `asps-network` — bridge for backend/webapi/mysql/keycloak
- `analyzer-egress` — isolated bridge for analyzer (ICC disabled)

## Build commands

```bash
# .NET solution
cd ASPSBackend14_J && dotnet build ASPSBackend.sln -c Debug --nologo

# Docker full stack
docker compose up -d --build

# Single container rebuild
docker compose up -d --build <service>

# Python desktop agent
cd apps/desktop/win && python -m pytest
```

## Environment variables

| Variable | Required by | Notes |
|---|---|---|
| `CQRS_SHARED_SECRET` | backend, webapi | Must be set in shell or `.env` before `docker compose up` |
| `ASPNETCORE_ENVIRONMENT` | webapi | Set to `Docker` in compose |
| `DOTNET_ENVIRONMENT` | backend | Set to `Docker` in compose |

## Azure Container Apps

> Source of truth: `docs/cloud/AZURE_ARCHITECTURE.md` and `docs/cloud/AZURE_DEPLOYMENT_GUIDE.md`.
> This section is a quick-reference summary; verify against those documents for full detail.

### Resources (dev environment)

| Resource | Name | Purpose |
|---|---|---|
| Resource Group | `rg-asps-dev` | All dev resources |
| Container Registry | `acraspsisaacdev.azurecr.io` | Docker images (Backend, WebApi, Angular Admin) |
| Container App Environment | `cae-asps-dev` | VNet-integrated hosting for all Container Apps |
| Container App | `ca-backend-dev` | Standalone Backend (CQRS, alerts, notifications, analyzer) |
| Container App | `ca-webapi-dev` | Standalone WebApi (Razor Pages, REST, Keycloak SSO, `/ws/agent` gateway) |
| Container App | `ca-keycloak-dev` | Keycloak OIDC provider |
| Container App | `ca-angular-admin-dev` | Angular admin dashboard SPA (nginx) |
| MySQL Flexible Server | `mysql-asps-dev.mysql.database.azure.com` | ASPS application database (`aspsbackend2db`) |
| PostgreSQL Flexible Server | `pg-asps-keycloak-dev` | Keycloak database only (MySQL incompatible with Keycloak — AD-6) |
| Key Vault | `kv-asps-dev` | RBAC mode, 7 secrets (DB passwords, CQRS secret, Keycloak creds, storage key) |
| Managed Identity | `id-asps-dev` | KV Secrets User + AcrPull — used by all Container Apps |
| Storage Account | `staspsdev` | Azure Files share `curve-keys` for CURVE encryption keys |
| VNet | `vnet-asps-dev` (10.0.0.0/16) | Container Apps networking, required for external TCP ingress |
| Application Insights | `appi-asps-dev` | Monitoring (workspace-based) |
| Log Analytics | `log-asps-dev` | Container stdout/stderr logs |

### Current images (as of 2026-08-25)

| App | Image | Tag |
|---|---|---|
| Backend | `asps-backend` | `manual-20260825-v1env-fix` |
| WebApi | `asps-webapi` | `20260824-9230e0e` |

### Architecture

Backend and WebApi run as **separate standalone Container Apps**. They communicate via internal TCP
ingress using **app-name addressing** (`tcp://ca-backend-dev:<port>`), not localhost or FQDN.
This replaced the earlier sidecar pattern (ASPS-729). FQDN-based ingress does NOT work for
ZMQ/CURVE traffic (Envoy proxy strips the ZMTP wire protocol), but app-name resolution within the
same Container Apps Environment bypasses that proxy.

### Transport modes

- **Device-to-backend (desktop agents):** NetMQ CURVE over external TCP (ports 50001/50002 on `ca-backend-dev`)
- **WebApi-to-backend (CQRS):** NetMQ CURVE + HMAC-SHA256 over internal TCP (port 5556 via app-name addressing)
- **Browser/WS clients:** WebSocket via WebApi gateway (`/ws/agent` on `ca-webapi-dev`), TLS replaces CURVE for WebSocket transport
- **Admin UI:** HTTPS (Keycloak SSO)

### Region

North Europe (Israel Central and West Europe were unavailable during initial provisioning).

## Current gaps (backlog)

- No realm-export for Keycloak — realm lives only in volume.
- No health checks on backend or webapi containers (Azure alerts exist for replica count/restarts but no application-level health endpoints).
- No centralized logging integration in Azure (Application Insights provisioned but SDK not integrated into .NET apps).
- `CQRS_SHARED_SECRET` has no rotation mechanism.
- Bicep IaC not yet written (AD-7: CLI first, Bicep after deployment stabilizes).
- `dev` GitHub environment not hardened (no required reviewers, no branch restriction on deploy).
