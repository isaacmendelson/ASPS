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

## Current gaps (backlog)

- No CI/CD pipeline exists yet.
- No container registry — images are local only.
- No realm-export for Keycloak — realm lives only in volume.
- No health checks on backend or webapi containers.
- No centralized logging or observability.
- `CQRS_SHARED_SECRET` has no rotation mechanism.
