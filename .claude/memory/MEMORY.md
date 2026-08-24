# ASPS Project Memory Index

## ⭐ Hat-based working system (read first every session)
- **Project entry point:** `c:\Jobs\ASPS\GitHub\Software\CLAUDE.md` (auto-loaded by Claude Code)
- **CEO hat (my default):** `c:\Jobs\ASPS\GitHub\Software\.claude\hats\ceo\INDEX.md`
- **All hats:** `c:\Jobs\ASPS\GitHub\Software\.claude\hats\` (CEO built; CTO, Backend, Frontend, Python, QA — TBD)

## About Isaac's environment
- [Python interpreter on Windows](user_python_interpreter.md) — use `python` (not `python3`) for ASPS work; `python3` resolves to the Microsoft Store stub without project deps

## Topical references
- [ASPS Backend Architecture](project_architecture.md) — Project structure, ports, tech stack (.NET 8, MySQL, NetMQ)
- [CURVE Key Management & Auth](project_curve_auth.md) — CURVE encryption keys in appsettings.json, device auth flow
- [Local Keycloak port](project_keycloak_dev_port.md) — Dev Keycloak runs on 8180 (not 8080) to avoid collision with the agent's 8080–8484 WebSocket range
- [NetMQ CURVE API Reference](reference_netmq_curve.md) — Correct API usage for NetMQ 4.0.1.13 CURVE
- [Common Enums](reference_enums.md) — DeviceMonitoringStatus, DeviceType values (avoid wrong names)
- [Python Clients](reference_python_clients.md) — Desktop agent paths, pyzmq, auth.json
- [ASPS JIRA Instances](reference_jira.md) — Two parallel JIRA instances (old on-prem + new Atlassian Cloud); match tasks by title
- [Daily Security Audit Cron](reference_security_audit_cron.md) — 05:00 daily CISO audit; reports under `docs/security-audits/`; flag file `NEEDS_ATTENTION.md`
- [Access keys file](reference_access_keys.md) — `ACCESS_KEYS.env` in project root has GitHub + JIRA tokens; read before asking the user

## Active work
- [SCRUM-863 progress](scrum-863-progress.md) — auto-update agent: phases 1–6 coded, 7–8 + build-pipeline remain
- [SCRUM-863 Velopack decision](scrum-863-velopack-decision.md) — Option B: `Update.exe apply` CLI, must be Velopack-installed

## Lessons learned
- [Docker .sh line endings](feedback_docker_sh_line_endings.md) — Windows CRLF in .sh files causes exit 127 in Alpine; always use LF + .gitattributes
- [Docker deps from lockfile](feedback_docker_deps_from_lockfile.md) — Install Python deps from requirements.lock.txt, never cherry-pick; missing deps = silent analyzer failure
- [Azure env vars: use PowerShell](feedback_azure_env_vars_powershell.md) — Git Bash MSYS2 mangles `/app/...` path values to `C:/Program Files/Git/app/...`
- [Gitignored appsettings → use env vars](feedback_gitignored_appsettings_use_envvars.md) — appsettings.Docker.json absent from CI builds; use Container App env vars
- [SupportedSchemaMajors required](feedback_supported_schema_majors.md) — All auth messages need `SupportedSchemaMajors=[1]`; Azure rejects v0
- [Desktop config_override.py](feedback_config_override_check_first.md) — Check config_override.py first when debugging wrong desktop agent config

## Conventions
- [GSD workflow selection](feedback_gsd_workflow.md) — GSD full for new features; direct delegation for defined remediation; agreed 2026-07-29
- [Branch naming from JIRA](branch-naming-from-jira.md) — branch = JIRA number + `-` + task title, spaces→hyphens
- [CEO never codes](feedback_ceo_no_coding.md) — CEO orchestrates only; all code changes delegated to specialist agents, even one-line fixes
- [Continue between phases](feedback_continue_phases.md) — Don't wait for approval between phases when path is clear; keep moving
- [JIRA auto-sync on delegation](feedback_jira_auto_sync.md) — Create sub-tasks and set In Progress BEFORE agents start; don't mark Done prematurely
- [JIRA agent labels](feedback_jira_agent_labels.md) — Always set Labels with the handling agent name(s) when creating/updating issues
- [Parent JIRA status](feedback_parent_jira_status.md) — Update parent epic/story to In Progress when first child work begins
- [Memory in project repo](feedback_memory_in_project.md) — Save memories to `.claude/memory/` in the project, not just Claude's auto-memory path
- [Always create ADRs](feedback_always_create_adrs.md) — Create ADR in `docs/architecture/decisions/` whenever architecture decisions are made
- [No idle agents](feedback_no_idle_agents.md) — When agent finishes a task, immediately assign the next one
- [PR only after all sub-tasks done](feedback_pr_after_all_subtasks.md) — Don't create PR/merge while sub-tasks are still open
