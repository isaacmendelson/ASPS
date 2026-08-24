# DevOps In-Flight Work

**Last updated:** 2026-08-25

## Azure Container Apps — production deployment (ASPS-693)

- `ca-backend-dev` running revision 0000014, image `manual-20260825-v1env-fix`
- `ca-webapi-dev` running image `20260824-9230e0e` (WebSocket gateway active)
- `ca-keycloak-dev` running
- `ca-angular-admin-dev` running
- All apps deployed and stable in `cae-asps-dev` (North Europe)

## Recent deploys

| Date | App | Image tag | Changes |
|---|---|---|---|
| 2026-08-25 | ca-backend-dev | `manual-20260825-v1env-fix` | V1 envelope for all alert types + removed test code |
| 2026-08-24 | ca-webapi-dev | `20260824-9230e0e` | WebSocket gateway `/ws/agent` |
| 2026-08-24 | ca-backend-dev | `manual-20260824-1623` | Initial Azure deployment |

## ASPS-626 — Reproducible build/test baselines

- All four components verified green on 2026-07-30:
  - .NET: 1470 passed, 0 failed, 7 skipped
  - Analyzer: 343 passed, 5 skipped, 0 failed
  - Desktop: 245 passed, 2 xfailed, 0 failed
  - Extension: 221 passed, 74 known-quarantined failures (7 files, expiry 2026-08-12)
- Branch: `ASPS-626-reproducible-build-test-baselines`

## ASPS-656 — Angular Admin Docker container (nginx)

- Branch: `asps-642-angular-admin-client` (worktree commit b7b3845)
- Deployed to `ca-angular-admin-dev`

## Backlog

- CI/CD pipeline (GitHub Actions) — not yet built
- Application-level health check endpoints (`/healthz`)
- App Insights SDK integration for telemetry
- Secret rotation mechanism for CQRS shared secret
- Bicep/IaC for infrastructure reproducibility
- Keycloak realm export for reproducible setup
- Centralized logging in Azure
