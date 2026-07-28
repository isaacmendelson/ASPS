# Curated Load-Bearing Decisions

Verify each code-specific decision against current code before relying on it.
Source code and current specifications outrank memory when they conflict.

## Architecture

- ASPS uses the current three-layer Agent organization:
  CEO → coordination roles → specialist technical Agents.
- Backend runtime is .NET 8.
- Persistence uses MySQL through Pomelo/EF Core; EF Core upgrade work must
  respect existing migration history.
- Messaging uses NetMQ/pyzmq with CURVE where required.
- Admin UI uses Razor Pages and Keycloak-based SSO.
- Desktop Agent is Python and the browser extension is Chrome MV3.
- `Common.Models.Key` is a value object, not an EF navigation. Persist its
  value through explicit scalar fields; verify the current mapping before edits.
- `ProtectiveAction[]` persistence uses a JSON backing column; endpoint wire
  contracts still require explicit versioning and compatibility tests.
- Roadmap data uses a JSON blob model rather than normalized item/category
  tables unless a later ADR explicitly supersedes it.

## Process

- QA gate is mandatory for non-trivial code.
- The CEO/root owns final trust-but-verify, commits, and Jira completion.
- Model/effort/context decisions follow the adaptive routing learning method.
- Task-specific handoffs under `docs/task-memory/` are canonical for active
  task state.

## Security baselines

- Avoid polymorphic `TypeNameHandling.Auto` at trust boundaries; prefer
  `TypeNameHandling.None` and explicit allowlisted dispatch.
- Network management/message endpoints must be explicitly bound and
  authenticated; never assume localhost or Docker topology is an authorization
  boundary.
- Authentication, crypto, deserialization, SSRF, secrets, and permissions are
  security-sensitive paths requiring High-effort review and negative tests.
- Current security remediation status belongs in Jira and current audit/task
  handoffs, not in this durable decisions file.
