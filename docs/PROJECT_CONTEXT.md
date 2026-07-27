# ASPS Project Context

**Purpose:** Mandatory shared context for every ASPS task, chat, session, and agent.  
**Repository:** `C:\Jobs\ASPS\GitHub\Software`  
**Scope:** Product, architecture, specifications, knowledge retrieval, external resources, and task-memory discovery.  
**Last updated:** 2026-07-27

This is an entry point, not a duplicate specification. Follow its links to the current source of truth and verify implementation claims against the current working tree.

## 1. What ASPS Is

ASPS (Anti-Scam Protection System) is a distributed real-time protection platform intended to detect online scams, phishing, suspicious browsing journeys, and unauthorized remote-access activity before the user suffers harm.

Its primary users include elderly, immigrant, and technology-anxious adults. A surrounding protector model may involve family members, social workers, nonprofit organizations, or other trusted parties.

The product combines signals from browsing, URL analysis, device activity, remote-access sessions, user-level history, and intelligence sources. Product goals, users, differentiators, and business roadmap are maintained in [`../PRODUCT.md`](../PRODUCT.md).

## 2. Source-of-Truth Order

When sources disagree, use this order:

1. Current source code, configuration, migrations, and runtime behavior in the working tree.
2. Current build, test, and integration evidence.
3. [`system-specifications/ASPS_System_Specification.md`](system-specifications/ASPS_System_Specification.md).
4. Accepted ADRs and explicit user decisions.
5. Component specifications and data-flow documentation.
6. Product plans, unified requirements, roadmaps, historical designs, and task handoffs.
7. README files and historical task statements.

Ticket status, document status, class existence, or a passing unit test alone is not proof that a feature is reachable and working end to end.

## 3. System Components

| Component | Location | Primary role |
|---|---|---|
| Backend host | `ASPSBackend14_J/ASPSBackend/` | Runtime host, persistence startup, messaging, and service wiring |
| Business layer | `ASPSBackend14_J/Business/` | Domain logic, CQRS, repositories, analysis, events, and messaging |
| Shared contracts | `ASPSBackend14_J/Common/`, `ASPSBackend14_J/Interface/` | Entities, DTOs, enums, value objects, and repository contracts |
| Admin UI and REST | `ASPSBackend14_J/WebApi/` | Razor Pages, REST APIs, Keycloak/OIDC, and SignalR |
| Tests | `ASPSBackend14_J/ASPS.Tests/` | .NET unit and integration tests |
| Windows Desktop Agent | `apps/desktop/win/` | Local orchestration, Extension WebSocket, ZMQ client, remote-access and browser monitoring |
| Chrome Extension | `apps/extension/chrome/` | Browser telemetry, URL requests, policy actions, popup, and durable local state |
| Basic URL Analyzer | `Analyzers/basic-url-analyzer/` | Python URL-analysis service and scoring pipeline |
| Deployment | Repository root and component Dockerfiles | Docker Compose, images, ports, and service configuration |
| Mobile clients | Planned | Android and iOS implementations are not part of the current completed baseline |

Inspect all system components under `ASPSBackend14_J/`, `apps/`, `Analyzers/` when making system-wide implementation claims.

## 4. Stack and Interfaces

| Area | Technology |
|---|---|
| Backend | .NET 8, C#, EF Core, Pomelo MySQL |
| Admin UI | Razor Pages, vanilla JavaScript, Keycloak/OIDC |
| Messaging | NetMQ/ZeroMQ, including CURVE on selected paths |
| Desktop Agent | Python 3.11, pyzmq, asyncio, websockets |
| Browser Extension | Chrome Manifest V3, vanilla JavaScript |
| Analyzer | Python service |
| Database | MySQL 8 |

Important ports:

| Port | Purpose |
|---:|---|
| 50001 | Device-to-backend real-time alerts over NetMQ ROUTER |
| 50002 | Backend notification publisher over NetMQ PUB |
| 5555 | Internal NetMQ business endpoint |
| 5556 | WebApi-to-Backend CQRS gateway |
| 5001 / 5002 | WebApi HTTP / HTTPS |
| 3306 | MySQL |
| 8080–8484 | Candidate local WebSocket ports between Extension and Desktop Agent |
| 8180 | Local development Keycloak |

Do not assume a documented interface is secure, bound to localhost, or connected at runtime. Verify configuration and production wiring.

## 5. Specification Index

### System-level

- [`system-specifications/ASPS_System_Specification.md`](system-specifications/ASPS_System_Specification.md) — canonical system specification and implementation-status matrix.
- [`system-specifications/ASPS_Unified_System_Requirements_2026-07-15.md`](system-specifications/ASPS_Unified_System_Requirements_2026-07-15.md) — unified target requirements and unresolved conflicts; treat as requirements input, not proof of implementation.
- [`ASPS_DATA_FLOW.md`](ASPS_DATA_FLOW.md) — end-to-end flows and protocol narrative; validate against code.
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md) — broad technical architecture and component map.
- [`system-specifications/ASPS_System_Overview.md`](system-specifications/ASPS_System_Overview.md) — detailed system overview.

### Component and feature-level

- [`system-specifications/DESKTOP_AGENT_FEATURES.md`](system-specifications/DESKTOP_AGENT_FEATURES.md) — Desktop Agent features and interfaces.
- [`SCRUM-904-user-risk-score-design.md`](SCRUM-904-user-risk-score-design.md) — user risk-score and consent design history.
- [`ASPS-337-ANALYSIS.md`](ASPS-337-ANALYSIS.md) and [`ASPS-352-DESIGN.md`](ASPS-352-DESIGN.md) — historical feature analysis and design.
- `docs/system-specifications/` — additional source requirements and historical specifications in English and Hebrew.
- `docs/security-audits/` — security evidence and findings, not functional specifications.

### Known documentation work

- A dedicated specifications document for `Analyzers/basic-url-analyzer/` is still required.
- Task-specific open documentation work is recorded in the matching file under `docs/task-memory/`.

## 6. Knowledge Engine

The repository includes a local RAG implementation under `KnowledgeEngine/`.

It indexes:

- `docs/`
- `.claude/`

Primary access is through MCP:

- `knowledge_search` — semantic retrieval of source chunks.
- `knowledge_ask` — synthesized answer based on retrieved sources.

Detailed operating instructions are in [`.claude/tools/knowledge-engine.md`](../.claude/tools/knowledge-engine.md). MCP configuration is stored in `.mcp.json` and `.codex/config.toml`.

Rules:

- Use the Knowledge Engine for discovery, organizational memory, specifications, ADRs, workflows, and prior findings.
- For current implementation claims, retrieve first and then verify against code and tests.
- The index is static. Rebuild it after accepted changes under `docs/` or `.claude/`.
- When using the CLI, use `KnowledgeEngine/.venv/Scripts/python.exe`, never the global Python installation.
- Do not treat retrieval similarity or an old indexed statement as proof of current behavior.

## 7. External Resources and Connected Tools

ASPS work may involve Jira, GitHub, email, calendars, document stores, or other connected resources. Availability and authentication vary by session.

Every agent must:

- Discover which connectors, plugins, MCP tools, or authenticated CLIs are available in the current session.
- When Jira, GitHub, or another external service requires credentials, load them from the repository-local `../ACCESS_KEYS.env` file when it exists. This file is local-only and excluded from Git.
- Store credentials for additional external services only in `../ACCESS_KEYS.env`, using clear service-prefixed environment-variable names. Never copy their values into documentation, task handoffs, logs, commits, or responses.
- Never claim Jira, GitHub, or another service was checked unless the corresponding tool actually returned data.
- Prefer read-only retrieval when the user asks for analysis, status, or review.
- Obtain the required confirmation before sending messages, changing tickets, merging, pushing, deleting, or performing another consequential external action.
- Preserve ticket keys, URLs, repository identifiers, and source attribution when importing external information.
- Store no passwords, access tokens, private keys, cookies, or connection strings in this document or in task handoffs.
- If a required connector is unavailable, state the limitation and use repository evidence where possible; do not invent remote state.

Jira/design status is context, not proof of implementation. GitHub remote state is distinct from the local working tree and must be verified separately.

## 8. Task Memory and Cross-Session Continuity

Task handoffs live under:

`docs/task-memory/<TASK_NAME>_HANDOFF.md`

At the start of a task:

1. Identify the current task name and identifier when available.
2. Read its matching handoff.
3. If the match is ambiguous, list candidates and ask the user instead of guessing.
4. Read handoffs from other tasks only when they are relevant to the current task.
5. Use the Knowledge Engine to locate related decisions and prior findings.

At the end of every significant phase and before a planned session ending:

- Update the task's canonical handoff in place.
- Record completed work, changed files, verification results, decisions, uncompleted work, and the exact continuation point.
- If the task was renamed, rename the handoff and update its internal metadata and references in the same phase.

Do not load every historical handoff automatically. Use targeted retrieval to avoid stale or irrelevant context.

## 9. Working Rules

- Preserve pre-existing and unrelated working-tree changes.
- Treat code as the current implementation truth and specifications as intent plus audited status.
- Follow phased execution: complete one significant phase, report, and wait for approval before the next phase.
- Non-trivial code changes require independent QA before commit.
- Do not silently fix unrelated issues.
- Confirm destructive operations and consequential external changes.
- Never expose secrets found in configuration, history, logs, or tool output.
- Use Hebrew by default when the user writes in Hebrew; retain English technical terms where clearer.

## 10. New-Agent Startup Checklist

1. Read repository-root `AGENTS.md`.
2. Read this file completely.
3. Read the matching task handoff under `docs/task-memory/`.
4. Query the Knowledge Engine for task-specific prior knowledge.
5. Inspect the current Git status and relevant files without modifying unrelated work.
6. Verify which external resources are actually available.
7. Continue from the recorded handoff point or ask for identification if the task is ambiguous.
