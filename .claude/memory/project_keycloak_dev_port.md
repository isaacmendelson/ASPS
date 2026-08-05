---
name: project-keycloak-dev-port
description: "Local development Keycloak runs on port 8180 (not the default 8080) — moved to avoid collision with the desktop agent's extension WebSocket port range"
metadata: 
  node_type: memory
  type: project
  originSessionId: 79421ab0-5013-4f83-811d-ef1680a5ec72
---

Local development Keycloak runs on **port 8180**, not the default 8080.

**Why:** The Python desktop agent's WebSocket bridge to the Chrome extension scans `EXTENSION_PORTS = [8080, 8181, 8282, 8383, 8484]` in [apps/desktop/win/src/config.py](c:/Jobs/ASPS/GitHub/Software/apps/desktop/win/src/config.py) and binds the first free one. When both Keycloak and the agent need to run for end-to-end dev (which is the common case), they collide on 8080. Keycloak was moved to 8180 — outside the agent's port range — on 2026-06-16.

**How to apply:** When starting Keycloak locally:

```powershell
& "C:\Users\Isaac\Keycloak\keycloak-26.6.3\bin\kc.bat" start-dev --http-port=8180
```

Authority URLs in both [WebApi/appsettings.Development.json](c:/Jobs/ASPS/GitHub/Software/ASPSBackend14_J/WebApi/appsettings.Development.json) and [ASPSBackend/appsettings.Development.json](c:/Jobs/ASPS/GitHub/Software/ASPSBackend14_J/ASPSBackend/appsettings.Development.json) point at `http://localhost:8180/realms/asps` (both files are gitignored, so the local edit doesn't propagate). Admin console: `http://localhost:8180/admin/`.

**CLAUDE.md `Ports / messaging` table still lists 8080–8484 for the extension WebSocket** — that hasn't been amended yet. When updating, add a row noting Keycloak (8180, OIDC, dev-only).

Related: [[project_curve_auth]] for the CURVE-related Keycloak setup; [[reference_python_clients]] for the desktop agent paths.
