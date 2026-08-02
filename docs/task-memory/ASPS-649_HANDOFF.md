# ASPS-649 — Angular Admin: Backend: Simulations + Roadmaps + System API

**JIRA:** ASPS-649  
**Branch:** `asps-642-angular-admin-client`  
**Status:** Implementation complete — ready for QA  
**Last updated:** 2026-08-02

---

## What was built

### 1. SimulationsApiController (extended)
`ASPSBackend14_J/WebApi/Controllers/SimulationsApiController.cs`

Added CRUD + run endpoints to the existing controller:
- `GET /api/simulations` — paged list (PagedRequest, normalized)
- `GET /api/simulations/{keyField}` — single simulation by KeyField
- `POST /api/simulations` — create (CreateSimulationRequest DTO)
- `PUT /api/simulations/{keyField}` — update (UpdateSimulationRequest DTO)
- `DELETE /api/simulations/{keyField}` — soft delete
- `POST /api/simulations/{keyField}/run` — execute simulation (RunSimulationRequest DTO)

Preserved all existing autocomplete helpers (users, devices, user devices). Added `[Authorize(Roles = "Admin")]`.

### 2. RoadmapsApiController (new)
`ASPSBackend14_J/WebApi/Controllers/RoadmapsApiController.cs`

- `GET /api/roadmaps` — paged list with `includeArchived` query param (default false)
- `POST /api/roadmaps` — create (name + optional description; empty data blob)
- `POST /api/roadmaps/{id}/archive` — archive roadmap

### 3. SystemController (extended)
`ASPSBackend14_J/WebApi/Controllers/SystemController.cs`

- Added `ICQRSClient` + `ILogger<SystemController>` constructor injection
- `POST /api/system/reinitialize-asview` — sends `ReInitializeASViewCommand` via CQRS

### 4. ASPS-649 Queries
`ASPSBackend14_J/Business/Queries/AdminQueries.cs` — added section at end:
- `GetAllSimulationsPagedQuery` (PagedQuery)
- `SimulationDto` + `GetAllSimulationsPagedQueryResult`
- `GetSimulationByKeyFieldQuery` + `GetSimulationByKeyFieldQueryResult`
- `GetAllRoadmapsPagedQuery` (PagedQuery, IncludeArchived)
- `RoadmapDto` + `GetAllRoadmapsPagedQueryResult`

### 5. ASPS649QueryHandlers (new)
`ASPSBackend14_J/Business/Handlers/ASPS649QueryHandlers.cs`

Handles:
- `GetAllSimulationsPagedQuery` — filters deleted, search by name/desc, sort, paginate, deserializes steps
- `GetSimulationByKeyFieldQuery` — by Key("Simulation", keyField), excludes deleted
- `GetAllRoadmapsPagedQuery` — delegates to IRoadmapRepository.ListAsync(includeArchived), search, sort, paginate

### 6. CQRSGateway.Queries.cs (extended)
`ASPSBackend14_J/Business/Messaging/CQRSGateway.Queries.cs`

Added dispatch cases and handler methods for the three new query types. Handler methods delegate to `ASPS649QueryHandlers`.

### 7. Program.cs (DI registration)
`ASPSBackend14_J/ASPSBackend/Program.cs`

Added: `services.AddScoped<ASPS649QueryHandlers>(); // ASPS-649`

### 8. SystemControllerTests.cs (updated)
`ASPSBackend14_J/ASPS.Tests/WebApi/Controllers/SystemControllerTests.cs`

Updated constructor to inject mocks (the no-arg ctor was removed when SystemController gained dependencies). Added 3 new tests for `ReinitializeAsView`.

---

## New test files

| File | Tests |
|---|---|
| `ASPS.Tests/WebApi/Controllers/ASPS649_SimulationsApiControllerTests.cs` | 14 tests |
| `ASPS.Tests/WebApi/Controllers/ASPS649_RoadmapsApiControllerTests.cs` | 12 tests |
| `ASPS.Tests/Business/Handlers/ASPS649_AdminQueryHandlersTests.cs` | 15 tests |

---

## Build & test evidence

```
dotnet build ASPSBackend.sln -c Debug --nologo
→ Build succeeded. 26 Warning(s). 0 Error(s).

dotnet test ASPS.Tests/ASPS.Tests.csproj --nologo -v q
→ Failed: 1 (pre-existing — Python venv missing), Passed: 1573, Skipped: 7, Total: 1581
```

Pre-existing failure: `RunAsync_RealAnalyzerSubprocess_PreservesEchoAndReturnsStructuredError`  
Cause: Python venv not installed in worktree (`Analyzers/basic-url-analyzer/.venv`). Confirmed failing before this task's changes.

---

## Commit

`355ae6f` — ASPS-649 Angular Admin - Backend: Simulations + Roadmaps + System API

---

## Key architectural decisions

- **No new Commands needed** — all write operations (Create, Update, Delete, Run simulation; Create, Archive roadmap) reuse existing command classes already wired in CQRSGateway.Commands.cs.
- **ASPS649QueryHandlers is a standalone class** (not merged into AdminQueryHandlers) — avoids growing AdminQueryHandlers further; follows the per-domain handler pattern (see RoadmapQueryHandlers, SimulationQueryHandlers).
- **SimulationStepsJson deserialized in handler** — consistent with SimulationQueryHandlers pattern.
- **User?.Identity?.Name** used with null-conditional — required for unit-test safety (no HttpContext in unit tests).

---

## Continuation

Ready for QA review. No migrations needed (no schema changes).
