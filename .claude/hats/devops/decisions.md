# DevOps Decisions

Verify each decision against current files before relying on it.

## Docker architecture

- Analyzer runs as isolated sidecar container communicating with backend via
  Unix domain socket (`/run/asps-analyzer/analyzer.sock`). Backend contains
  only a thin Python IPC client (`Analyzers/analyzer-client/analyze.py`);
  Playwright/Chromium and hostile content stay in the analyzer container.
- Analyzer container starts as root for iptables egress firewall setup, then
  drops to non-root (UID 10001) via `setpriv` before running uvicorn.
  Capabilities `NET_ADMIN`, `SETUID`, `SETGID`, `SETPCAP` are granted at
  start and dropped after firewall init.
- Analyzer egress network (`analyzer-egress`) has ICC disabled — analyzer
  cannot reach other containers or private networks. Only public internet
  egress is allowed.
- Keycloak realm config lives in a Docker volume (`keycloak_data`), not in a
  realm-export file. This is a known reproducibility gap.

## Ports

- MySQL exposed on 3307 (not 3306) to avoid host collision.
- Keycloak on 8081 (not 8080) to avoid collision with WebApi and desktop
  agent's WebSocket range.
- WebApi on 8080 (HTTP only in dev; HTTPS termination is a future concern).

## Environment

- `CQRS_SHARED_SECRET` is the only mandatory external env var for compose.
  It uses `${VAR:?msg}` syntax to fail fast if missing.
