# Agents — ASPS AI Operating System

Role charters for the ASPS multi-agent development environment.

The organization has three layers (see [../architecture/AI-OS.md](../architecture/AI-OS.md)):
**Executive** (CEO) → **C-level** (VP Engineering, Product, Knowledge Manager) →
**Technical** (architect, backend, desktop-agent, browser-extension, analyzer-ai, qa, security, devops).

Each file defines **one role** in the AI OS using a fixed skeleton:

- **Mission** — why this role exists, in one sentence.
- **Responsibilities** — what it owns.
- **Inputs** — what it consumes to do its work.
- **Outputs** — what it produces.
- **Constraints** — hard limits and non-negotiables.
- **Collaboration** — which roles it works with and how.
- **Definition of Done** — the bar for "complete".

## Roster (AI OS)

| File | Layer | Role |
|---|---|---|
| [ceo.md](ceo.md) | Executive | CEO — sole orchestrator |
| [vp-engineering.md](vp-engineering.md) | C-level | VP Engineering — technical execution owner |
| [product.md](product.md) | C-level | Product — problem & priorities |
| [knowledge-manager.md](knowledge-manager.md) | C-level | Knowledge Manager — AIducation / ADR / learning |
| [architect.md](architect.md) | Technical | Architect — cross-cutting design |
| [backend.md](backend.md) | Technical | Backend — .NET / EF / NetMQ / MySQL |
| [desktop-agent.md](desktop-agent.md) | Technical | Desktop Agent — Python (Windows) |
| [browser-extension.md](browser-extension.md) | Technical | Browser Extension — Chrome MV3 |
| [analyzer-ai.md](analyzer-ai.md) | Technical | Analyzer AI — analyzer microservices |
| [qa.md](qa.md) | Technical | QA — pre-merge verification gate |
| [security.md](security.md) | Technical | Security — threat review & posture |
| [devops.md](devops.md) | Technical | DevOps — build / release / environments |
| [tech-writer.md](tech-writer.md) | Technical | Tech Writer — specifications, ICDs, system documentation |

## Retained legacy agents

Kept alongside the OS roster by decision (not removed):

| File | Status |
|---|---|
| [cto.md](cto.md) | Superseded by `architect` — kept for continuity |
| [frontend.md](frontend.md) | **Currently owns the Razor Admin UI** (no OS-roster owner for it) |
| [mobile.md](mobile.md) | Deferred — Android/iOS not yet started |
| [python.md](python.md) | Split into `desktop-agent` + `analyzer-ai` — kept for continuity |

> TODO: Revisit legacy agents once the OS roster is exercised in real work.
> If `frontend` is ever retired, the Razor Admin UI needs a new owner first.
