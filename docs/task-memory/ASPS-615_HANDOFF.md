# ASPS-615 Handoff

## Task

- Jira: `ASPS-615`
- Exact title: `[CODE REVIEW] Enforce WebApi and SignalR authentication and authorization`
- Status: `QA PASS` — ready for isolated commit and Jira closure.
- Owner scope: `ASPSBackend14_J/WebApi`, relevant tests, this handoff.
- No commit and no Jira mutation were performed.

## Source of truth reviewed

- `docs/code-reviews/ASPS_TOP_LEVEL_CODE_REVIEW_2026-07-28.md`
- `docs/task-memory/ASPS_TOP_LEVEL_CODE_REVIEW_HANDOFF.md`
- `docs/system-specifications/ASPS_System_Specification.md`
- `docs/system-specifications/ASPS_System_Overview.md`
- `docs/security-audits/2026-05-03.md`
- Current `WebApi/Program.cs`, all REST controllers, and
  `WebApi/Hubs/NotificationsHub.cs`.

The mandated `.Codex/team/CHARTER.md` and
`.Codex/rules/coding-standards.md` paths do not exist in this working tree.
The available backend role definition at `.codex/agents/backend.toml` was read.

## Acceptance/security checklist

- [x] Anonymous REST requests are rejected by default.
- [x] Authenticated non-admin REST requests are forbidden.
- [x] Authenticated admin REST requests preserve the existing authorized flow.
- [x] Anonymous SignalR negotiate requests are rejected.
- [x] Missing, partial, invalid, or failed device credentials fail closed.
- [x] Valid device credentials are checked through the server-side CQRS token
  validator before authorization succeeds.
- [x] Device UID, user membership, and allowed SignalR group are emitted as
  trusted server-side claims.
- [x] Hub subscribe/unsubscribe cannot cross device/group boundaries.
- [x] Admin cookie/OIDC connections remain authorized to connect to the hub.
- [x] API/hub failures return `401`/`403`, not an OIDC HTML redirect.

## TDD evidence

### Red

Command:

```powershell
dotnet test ASPS.Tests/ASPS.Tests.csproj --no-restore --filter "FullyQualifiedName~WebApi.Security" --logger "console;verbosity=minimal"
```

Initial behavioral result: `0 passed, 7 failed`.

- Anonymous and cross-device group tests demonstrated that the old hub joined
  caller-selected groups.
- Anonymous `/api/version` and hub negotiate returned `200`.
- CQRS-backed policy-handler slice then failed compilation because the handler
  and claim contract did not yet exist.

After removing test-harness-only EventLog/host side effects, the stable
pre-change behavioral baseline was `1 passed, 6 failed`: own-device subscription
was the only passing security behavior.

### Green

```powershell
dotnet test ASPS.Tests/ASPS.Tests.csproj --no-restore --filter "FullyQualifiedName~WebApi.Security" --logger "console;verbosity=minimal"
```

Result: `14 passed, 0 failed, 0 skipped`.

```powershell
dotnet test ASPS.Tests/ASPS.Tests.csproj --no-build --filter "FullyQualifiedName~ASPS.Tests.WebApi" --logger "console;verbosity=minimal"
```

Result: `128 passed, 0 failed, 3 skipped`.
The three pre-existing `NetMQClientServiceTests` require a running Backend and
are marked skipped in source.

```powershell
dotnet test ASPS.Tests/ASPS.Tests.csproj --no-restore --filter "FullyQualifiedName~TrackUrlAlertFlowIntegrationTests" --logger "console;verbosity=minimal"
```

Result: `24 passed, 0 failed, 0 skipped`.

```powershell
dotnet build WebApi/WebApi.csproj -c Debug --no-restore --nologo
```

Result: build succeeded, `0 errors`, `1 MSB3277` warning for the pre-existing
EF Core Relational `7.0.2`/`7.0.20` version conflict.

```powershell
git diff --check
```

Result: exit code `0`; line-ending notices only.

## Implementation

- Added `AdminPolicy` as the application fallback policy so unannotated REST
  endpoints require authenticated admins by default.
- Added explicit `NotificationsHubPolicy`.
- Added an authorization handler that permits:
  - an authenticated `Admin`; or
  - a device whose `deviceUid` and token pass
    `ValidateDeviceTokenQuery` through the trusted Backend CQRS path.
- Valid device authorization creates `asps_device_uid`,
  `asps_user_key_field`, and `asps_notification_group` claims.
- Hub group operations require an exact trusted group claim; arbitrary client
  input alone can no longer create membership.
- Added a middleware result handler so unauthorized API/hub calls return
  protocol-correct `401`/`403` without contacting OIDC discovery.
- Refactored device validation out of `OnConnectedAsync` and into the
  authorization boundary, which runs before the hub connection is accepted.
- Updated the existing alert integration factory with an authenticated admin
  test principal and isolated external hosted/CQRS dependencies.

## File manifest

Production:

- `ASPSBackend14_J/WebApi/Program.cs`
- `ASPSBackend14_J/WebApi/Hubs/NotificationsHub.cs`
- `ASPSBackend14_J/WebApi/Security/ApiAuthorizationMiddlewareResultHandler.cs`
- `ASPSBackend14_J/WebApi/Security/ApiCookieAuthentication.cs`
- `ASPSBackend14_J/WebApi/Security/HubClaimTypes.cs`
- `ASPSBackend14_J/WebApi/Security/NotificationsHubAuthorizationHandler.cs`

Tests:

- `ASPSBackend14_J/ASPS.Tests/WebApi/Security/WebApiAuthorizationTests.cs`
- `ASPSBackend14_J/ASPS.Tests/WebApi/Security/NotificationsHubAuthorizationTests.cs`
- `ASPSBackend14_J/ASPS.Tests/WebApi/Security/NotificationsHubPolicyHandlerTests.cs`
- `ASPSBackend14_J/ASPS.Tests/IntegrationTests/TestWebApplicationFactory.cs`

Task memory:

- `docs/task-memory/ASPS-615_HANDOFF.md`

Explicitly excluded: pre-existing
`ASPSBackend14_J/WebApi/appsettings.Docker.json` changes belong to ASPS-609 and
were not modified for ASPS-615.

## QA focus

1. Verify every REST controller is covered by the fallback `AdminPolicy`.
2. Verify `/notificationshub/negotiate` returns `401` without credentials and
   does not redirect to Keycloak.
3. Verify admin cookie/OIDC principal can connect.
4. Verify valid device credentials produce only the device's own group claim.
5. Verify invalid/partial credentials and Backend validation errors fail closed.
6. Verify subscribe and unsubscribe reject another device's group.
7. Confirm the ASPS-615 diff excludes `appsettings.Docker.json` and all unrelated
   dirty-tree changes.

## Independent QA

- Verdict: **PASS**, with no Blocker, Major, Minor, or Nit findings.
- Security tests: **14 passed**.
- WebApi tests: **128 passed**, with 3 pre-existing NetMQ integration skips.
- TrackUrl regressions: **24 passed**.
- WebApi build: **0 errors**, one existing EF Core `MSB3277` warning.
- Scoped diff check: **PASS**.
- Live Keycloak and Backend CQRS were unavailable; fail-closed behavior was
  independently verified through TestServer and CQRS mocks.
