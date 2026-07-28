# CEO Delegation and Ownership

## Current role roster

| Role | Ownership |
|---|---|
| `vp-engineering` | Coordinates technical execution and engineering gates; does not implement |
| `product` | Requirements, user stories, acceptance criteria, priorities |
| `knowledge-manager` | Organizational learning, ADRs, lessons, Knowledge Engine |
| `architect` | Cross-component design, specifications, ADRs; no production code |
| `backend` | .NET, C#, EF Core, NetMQ, MySQL, Backend/WebApi |
| `desktop-agent` | `apps/desktop/win` |
| `browser-extension` | `apps/extension/chrome` |
| `analyzer-ai` | Analyzer services under `Analyzers/` |
| `qa` | Independent pre-commit verification; reviews, does not fix |
| `security` | CISO threat review and security audits; reviews, does not fix |
| `devops` | Build, packaging, release, environments |

Legacy roles may exist, but prefer the current specialist roster. Use
`frontend` for Razor/admin UI work until ownership is formally moved.

## Routing rules

- One primary implementation owner per Jira task.
- Cross-component tasks receive an architect design phase, then explicit
  per-component file ownership.
- State in every implementation prompt that Agents share the worktree and must
  not revert concurrent edits.
- QA must be independent from implementation.
- Security review supplements QA; it does not replace it.
- The CEO remains available to the user and reserves a slot for QA when
  implementation tasks are close to readiness.
- Do not spawn when direct work is faster and safe for a trivial task.

## Spawn prompt minimum

Include:

- exact Jira ID and title;
- original requirement and acceptance criteria;
- relevant specifications and handoff;
- exact file/module ownership;
- dependencies and compatibility constraints;
- required unit tests and reporting format;
- QA/commit/Jira restrictions;
- model/effort/context selection from the adaptive routing method.

Read
[`agent-routing-learning.md`](agent-routing-learning.md)
and the linked complete method before every spawn.
