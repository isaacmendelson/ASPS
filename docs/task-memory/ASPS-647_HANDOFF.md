# ASPS-647 — Angular Admin Backend: Devices + Alerts + Analysis API

**Task:** ASPS-647  
**Branch:** `asps-642-angular-admin-client` (worktree `worktree-agent-a11d1e29e65730525`)  
**JIRA Status:** In Progress → ready for QA  
**Last updated:** 2026-08-02

---

## Completed work

All REST API endpoints implemented per `docs/specs/ANGULAR_ADMIN_ARCHITECTURE.md`.

### New files

| File | Purpose |
|---|---|
| `ASPSBackend14_J/Common/Models/ApiDtos.cs` | `DeviceDto`, `AlertDto`, `AnalysisResultDto` |
| `ASPSBackend14_J/WebApi/Controllers/DevicesApiController.cs` | GET /api/devices (paged), /{keyType}/{keyValue}, /uid/{uid}, /{keyType}/{keyValue}/alerts |
| `ASPSBackend14_J/WebApi/Controllers/AlertsApiController.cs` | GET /api/alerts (paged, time-range, severity), /{keyType}/{keyValue}, /{keyType}/{keyValue}/analysis |
| `ASPSBackend14_J/WebApi/Controllers/AnalysisResultsApiController.cs` | GET /api/analysis-results (paged), /{keyType}/{keyValue} |
| `ASPSBackend14_J/ASPS.Tests/Business/Handlers/AdminQueryHandlersPaged647Tests.cs` | 17 handler tests |
| `ASPSBackend14_J/ASPS.Tests/WebApi/Controllers/DevicesApiControllerTests.cs` | 8 controller tests |
| `ASPSBackend14_J/ASPS.Tests/WebApi/Controllers/AlertsApiControllerTests.cs` | 8 controller tests |
| `ASPSBackend14_J/ASPS.Tests/WebApi/Controllers/AnalysisResultsApiControllerTests.cs` | 6 controller tests |

### Modified files

| File | Change |
|---|---|
| `ASPSBackend14_J/Business/Queries/AdminQueries.cs` | 12 new query/result classes (paged + detail) |
| `ASPSBackend14_J/Business/Handlers/AdminQueryHandlers.cs` | 6 new `HandleAsync` overloads + 5 private mapping helpers |
| `ASPSBackend14_J/Business/Messaging/CQRSGateway.Queries.cs` | 6 new switch cases + 6 private handler methods |

---

## Test results

**Commit:** `8b57a5e`

```
dotnet test ASPSBackend.sln
Failed:  3 (pre-existing, unrelated to ASPS-647)
Passed:  1529
Skipped: 7
Total:   1537
```

Pre-existing failures (all pre-date this task):
- `AnalyzerV1ProcessClientTests.RunAsync_RealAnalyzerSubprocess_...` — Python venv missing in worktree
- `ReconnectSnapshotServiceTests.SendSnapshotAsync_EmptyOutbox_...` — pre-existing
- `ReconnectSnapshotServiceTests.SendSnapshotAsync_OutboxException_...` — pre-existing

ASPS-647 new tests: **38 pass, 0 fail** (verified with `--filter` targeting new test classes).

---

## Decisions

- `ICQRSClient.SendQueryAsync<T>` takes `Query` (base type) as parameter; Moq `.Callback<T>` must use `Query` not the subtype, then cast inside.
- `SetKeyField()` is `internal` on `Entity` — all tests use `KeyField = "..."` property initializer instead.
- `GetDeviceByKeyQueryResult.Device` is `UserDevice` type (not DTO) — `GetByKey` on devices returns the raw entity; controller maps it to `Ok(result.Device)` which serializes as-is. If the Angular client needs a DTO here, that is future work.
- `GetAlertDetailQuery` / `GetAnalysisResultDetailQuery` re-use the existing `GetAlertByKeyQuery` / `GetAnalysisResultByAlertKeyQuery` patterns for consistency but add full DTO mapping.

---

## Next step for QA

Run on branch `worktree-agent-a11d1e29e65730525` (or after merge to `asps-642-angular-admin-client`):

```bash
dotnet test ASPSBackend14_J/ASPS.Tests --filter "FullyQualifiedName~AdminQueryHandlersPaged647|FullyQualifiedName~DevicesApiController|FullyQualifiedName~AlertsApiController|FullyQualifiedName~AnalysisResultsApiController"
# Expected: Passed: 38, Failed: 0
```

Verify endpoints compile with correct routes, `[Authorize(Roles = "Admin")]` present on all controllers, `PagedRequest.Normalize()` called in every list action.
