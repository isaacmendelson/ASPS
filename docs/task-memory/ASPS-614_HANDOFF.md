# ASPS-614 — Correct Backend DI lifetimes and enable container validation

## Status

QA PASS — independent re-review completed; ready for isolated commit and Jira closure.

## QA remediation

- QA initially failed two Major findings: `appsettings.Development.json` loading had been commented out, and a public UserDomain test constructor retained scoped dependencies.
- **Red:** the QA review identified the behavior/lifetime regressions; the focused suite covered the affected service construction paths.
- **Green:** restored the original Development JSON configuration provider and added a composition test that loads an isolated Development override while validation stays enabled. Removed the public scoped-dependency constructor; tests now supply an `IServiceScopeFactory`, so the long-lived service retains no `DbContext` or repository.

## Acceptance checklist

- `ValidateScopes` and `ValidateOnBuild` are enabled at the Backend composition root.
- A singleton no longer captures the scoped device-alert repository.
- Production container builds under validation and rejects an injected captive dependency.
- Long-lived UserDomain and NetMQ paths resolve scoped data through a fresh scope.

## TDD evidence

- **Red:** the new production composition-root test initially failed: `Assert.ThrowsAny() Failure: No exception was thrown`, proving validation was disabled. After enabling it, the same production build exposed `DomainEventsContext`, `UserDomainManagerService`, and `NetMQMessageProcessor` lifetime failures.
- **Green:** `BackendServiceProviderValidationTests` now verifies both rejection of a probe singleton that captures a scoped dependency and successful production-container construction.
- **Refactor:** Alert persistence now resolves its repository only inside its operation scope; UserDomain uses `IServiceScopeFactory` for database/phishing access; NetMQ creates a scope per message. Legacy constructors remain only as test seams.

## Changed files

- `ASPSBackend14_J/ASPSBackend/Program.cs`
- `ASPSBackend14_J/Business/RealtimeAnalysis/AlertPersistenceActor.cs`
- `ASPSBackend14_J/Business/Messaging/RealTimeAlertListener.cs`
- `ASPSBackend14_J/Business/RealtimeAnalysis/UserDomain/UserDomainManagerService.cs`
- `ASPSBackend14_J/Business/RealtimeAnalysis/UserDomain/UDAnalysisManager.cs`
- `ASPSBackend14_J/Business/RealtimeAnalysis/UserDomain/UDUrlAnalyzer.cs`
- `ASPSBackend14_J/Business/Messaging/NetMQMessageProcessor.cs`
- `ASPSBackend14_J/ASPS.Tests/Composition/BackendServiceProviderValidationTests.cs`
- `ASPSBackend14_J/ASPS.Tests/Business/RealtimeAnalysis/UserDomainManagerServiceTests.cs`

## Verification

- `dotnet test ASPS.Tests/ASPS.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~BackendServiceProviderValidationTests|FullyQualifiedName~NetMQMessageProcessorTests|FullyQualifiedName~UserDomainManagerServiceTests|FullyQualifiedName~UDUrlAnalyzerTests"` — **53 passed, 0 failed, 0 skipped**.
- `dotnet build ASPSBackend.sln -c Debug --nologo --no-restore -v:minimal` — **0 errors**; 2 existing EF Core Relational version-conflict warnings (`MSB3277`).
- Independent QA re-review — **PASS**, with no Blocker, Major, Minor, or Nit findings.

## QA handoff

Review DI scopes/registrations, the production composition-root validation tests, and NetMQ per-message scope disposal. Preserve unrelated ASPS-609 Analyzer/Docker worktree changes.
