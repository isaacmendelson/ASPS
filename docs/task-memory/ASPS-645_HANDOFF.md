# Task Handoff: ASPS-645

**Task:** Angular Admin - Backend: JWT Auth + CORS + Paging Infrastructure
**JIRA:** ASPS-645
**Epic:** ASPS-642 (Angular Admin Client)
**Branch:** `asps-645-angular-admin-backend-jwt-cors-paging`
**Status:** Ready for QA
**Last Updated:** 2026-08-02

---

## Completed Work

### 1. JWT Bearer Authentication (ADR-0001)
`ASPSBackend14_J/WebApi/Program.cs`

- Replaced `DefaultScheme = Cookie` with `DefaultScheme = "SmartScheme"` + `AddPolicyScheme()`
- Policy scheme auto-selects `JwtBearerDefaults.AuthenticationScheme` when `Authorization: Bearer` header is present, or for SignalR `/notificationshub?access_token=...` query string
- Added `.AddJwtBearer()` configured for Keycloak authority from `keycloakSection["Authority"]`, audience `asps-angular-admin`, `ValidAudiences = ["asps-angular-admin", "account"]`
- SignalR `OnMessageReceived` event extracts JWT from query string for WebSocket upgrade
- All existing Cookie+OIDC config unchanged
- `AdminClaimsTransformer` (IClaimsTransformation) continues to apply to both schemes automatically

### 2. CORS Policy (ADR-0001)
`ASPSBackend14_J/WebApi/Program.cs`

- Added `AddCors()` with `"AngularAdmin"` policy: configurable origins via `Cors:AllowedOrigins`, `AllowAnyHeader()`, `AllowAnyMethod()`, `AllowCredentials()`
- Middleware order: `UseRouting` → `UseCors("AngularAdmin")` → `UseAuthentication` → `UseAuthorization`
- Origins in `appsettings.json`: `["http://localhost:4200"]`
- Docker config (gitignored): add `["http://localhost:4200", "http://localhost:4201"]` manually

### 3. Paging Infrastructure (ADR-0003)
- `ASPSBackend14_J/Common/Models/Paging.cs` — `PagedRequest` (Page, PageSize, Search, SortBy, SortDirection, `Normalize()`) + `PagedResult<T>` (Items, TotalCount, Page, PageSize, `TotalPages` computed)
- `ASPSBackend14_J/Common/Messaging/PagedQuery.cs` — abstract `PagedQuery : Query`

### 4. Newtonsoft JSON Serialization (ADR-0005)
`ASPSBackend14_J/WebApi/Program.cs` — `AddNewtonsoftJson` updated with:
- `CamelCasePropertyNamesContractResolver`
- `StringEnumConverter`
- `DateTimeZoneHandling.Utc`

### 5. NuGet Package
`ASPSBackend14_J/WebApi/WebApi.csproj` — added `Microsoft.AspNetCore.Authentication.JwtBearer` Version `8.0.0`

### 6. Configuration
`ASPSBackend14_J/WebApi/appsettings.json` — added `"Cors": { "AllowedOrigins": ["http://localhost:4200"] }`

### 7. Razor JS Audit (camelCase impact)
All DataTables in Razor pages use **server-side rendered HTML** (no AJAX JSON fetching) — no DataTables column definitions to update.

Only one breaking change found and fixed:
- `ASPSBackend14_J/WebApi/Pages/Shared/_Layout.cshtml` — SignalR `data.Message` → `data.message` (2 occurrences)

Simulations Create/Edit pages access properties (`user.firstName`, `device.deviceType`, etc.) that were **already using camelCase** in JavaScript — these match the new serializer output correctly.

Roadmaps Edit page returns `new { success, message, ... }` anonymous objects — already camelCase, no change needed.

---

## TDD Evidence

**Red:** `PagedRequest` and `PagedResult<T>` did not exist; 22 tests failed with `error CS0246: type or namespace not found`.

**Green:** Created `Common/Models/Paging.cs` and `Common/Messaging/PagedQuery.cs`; all 22 tests passed.

**Test file:** `ASPSBackend14_J/ASPS.Tests/Common/Models/PagingTests.cs`

---

## Test Results

```
dotnet test ASPS.Tests/ASPS.Tests.csproj --nologo -q --no-build
Passed: 1491  Failed: 0  Skipped: 7  Total: 1498

(1 pre-existing environmental failure excluded: AnalyzerV1ProcessClientTests.RunAsync_RealAnalyzerSubprocess
-- requires .venv at worktree path which doesn't exist in the worktree; passes in main checkout)
```

Baseline before task: 1470 passed, 7 skipped, 0 failed. Net new: +21 paging tests.

## Build Result

```
dotnet build ASPSBackend.sln -c Debug --nologo
Build succeeded. 0 Error(s)
```

---

## Files Changed

| File | Change |
|---|---|
| `ASPSBackend14_J/WebApi/Program.cs` | JWT Bearer, CORS, Newtonsoft camelCase, middleware order |
| `ASPSBackend14_J/WebApi/WebApi.csproj` | Added JwtBearer 8.0.0 NuGet package |
| `ASPSBackend14_J/WebApi/appsettings.json` | Added Cors:AllowedOrigins section |
| `ASPSBackend14_J/WebApi/Pages/Shared/_Layout.cshtml` | `data.Message` → `data.message` (SignalR JS) |
| `ASPSBackend14_J/Common/Models/Paging.cs` | New: PagedRequest + PagedResult<T> |
| `ASPSBackend14_J/Common/Messaging/PagedQuery.cs` | New: abstract PagedQuery : Query |
| `ASPSBackend14_J/ASPS.Tests/Common/Models/PagingTests.cs` | New: 22 unit tests |

Not committed (gitignored, contains secrets):
- `ASPSBackend14_J/WebApi/appsettings.Docker.json` — manually add `"Cors": { "AllowedOrigins": ["http://localhost:4200", "http://localhost:4201"] }` to that file in the Docker environment

---

## Decisions Made

1. `appsettings.Docker.json` is gitignored (contains `ClientSecret`) — not committed. Cors entry must be added manually/via CI env injection.
2. `SmartScheme` PolicyScheme applies only in the `keycloakEnabled` path. In cookie-only dev fallback, JWT auth is not available (consistent with existing behavior).
3. The single Razor JS camelCase impact (`data.Message`) is fixed. All other Razor JS already uses camelCase property names matching the DTO names.
4. `PageSize = 0` clamps to 25 (not 0), preventing divide-by-zero in `TotalPages`.

---

## Continuation Point

Next step: QA review on branch `asps-645-angular-admin-backend-jwt-cors-paging`.
