---
name: azure-env-vars-powershell
description: Use PowerShell (not Git Bash) for az containerapp env vars with path values — MSYS2 mangles /app/... to C:/Program Files/Git/app/...
metadata:
  type: feedback
---

Always use PowerShell for `az containerapp update --set-env-vars` when values contain Linux paths (starting with `/`).

**Why:** Git Bash's MSYS2 layer converts any argument starting with `/` to a Windows path before the process receives it. So `/app/Analyzers` becomes `C:/Program Files/Git/app/Analyzers` inside the container. This is silent — the Azure API accepts the mangled value without error.

**How to apply:** When setting Container App env vars that contain Linux filesystem paths, either use PowerShell or set `MSYS_NO_PATHCONV=1` in Bash first. Always verify with `az containerapp exec --command "printenv VAR_NAME"` after setting. Related: [[azure-msys-path-conversion]] (existing troubleshooting entry for resource IDs).
