---
name: gitignored-appsettings-use-envvars
description: appsettings.Docker.json is gitignored so it's absent from CI builds — use Azure Container App env vars for Docker-specific config
metadata:
  type: feedback
---

Gitignored `appsettings.*.json` files are NOT present in CI-built Docker images. Use Azure Container App env vars instead.

**Why:** GitHub Actions `checkout@v4` only checks out git-tracked files. `appsettings.Docker.json` is in `.gitignore` (line 140), so `dotnet publish` inside the Dockerfile never sees it. ASP.NET Core silently falls back to `appsettings.json` defaults — no error, just wrong config.

**How to apply:** For any Docker/container-specific config, set env vars on the Container App using ASP.NET Core's `__` convention (e.g., `Python__ExecutablePath=python3` maps to `Python:ExecutablePath`). Use PowerShell for path values (see [[azure-env-vars-powershell]]). Verify the env var is active: `az containerapp show --query "properties.template.containers[0].env"`.
