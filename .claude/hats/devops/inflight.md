# DevOps In-Flight Work

**Last updated:** 2026-07-29

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
