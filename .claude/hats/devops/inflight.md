# DevOps In-Flight Work

**Last updated:** 2026-08-02

## ASPS-626 — Reproducible build/test baselines

- All four components verified green on 2026-07-30:
  - .NET: 1470 passed, 0 failed, 7 skipped
  - Analyzer: 343 passed, 5 skipped, 0 failed
  - Desktop: 245 passed, 2 xfailed, 0 failed
  - Extension: 221 passed, 74 known-quarantined failures (7 files, expiry 2026-08-12)
- Baseline checker PASS — exact SHA-256 hashes match all quarantined groups.
- NuGet locked-mode restore PASS.
- SDK conflict resolved: global.json pins 9.0.308 (host); Dockerfiles pin SDK 8.0.423 (container).
- All changes committed to branch `ASPS-626-reproducible-build-test-baselines`.
- Ready for independent QA review. DevOps must remediate any QA FAIL before resubmission.

## ASPS-656 — Angular Admin Docker container (nginx)

- Branch: `asps-642-angular-admin-client` (worktree commit b7b3845)
- Dockerfile rewritten for `context: ./apps/admin/angular` (self-contained, local COPY paths)
- Node base pinned to `node:20-alpine`; nginx pinned to `nginx:1.27-alpine`
- Build output path corrected: `dist/angular/browser` (Angular 18 application builder)
- nginx.conf: added `/api/` proxy pass and `/notificationshub` SignalR WebSocket proxy to `webapi:8080`
- docker-compose: added `angular-admin` service on port 4201, `asps-network`
- `.dockerignore` created: excludes `node_modules`, `dist`, `.angular`, `.git`
- Status: committed to worktree branch, ready for merge to `asps-642-angular-admin-client`

## Docker stack stabilization

- Analyzer crash loop fixed: entrypoint now runs iptables as root, drops to
  UID 10001 via `setpriv` with `SETUID/SETGID/SETPCAP` caps (dropped after
  firewall init). tmpfs at `/run` for iptables lock file.
- Keycloak client secret mismatch resolved (runtime fix in Keycloak volume,
  not persisted to a realm-export).
- All 5 containers running stable: mysql (healthy), backend, webapi, keycloak
  (healthy), analyzer.

## Backlog

- Export Keycloak realm to a JSON file for reproducible setup.
- Add healthchecks to backend and webapi containers.
- Establish CI/CD pipeline (GitHub Actions).
- Set up container registry for image publishing.
- Add centralized logging.
- Cloud deployment planning.
