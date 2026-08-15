# Messaging Refactoring Specification — Task Handoff

## Task
**Name:** ASPS Messaging Refactoring Implementation Specification  
**Type:** Research / Documentation (NO code changes)  
**Status:** APPROVED — JIRA tasks created, ready for Phase 0  
**Date:** 2026-08-10 (approved), 2026-08-03 (spec drafted)

## Deliverable
[ASPS_Messaging_Refactoring_Implementation_Specification.md](../architecture/messaging/ASPS_Messaging_Refactoring_Implementation_Specification.md)

## Completed Work
1. 7 parallel Explore agents researched the entire messaging subsystem:
   - NetMQ infrastructure (7 sockets, 4 ports, threading model)
   - CQRS message flow (ICQRSClient, CQRSGateway dispatch, serialization, HMAC)
   - Alerts & notifications (RealTimeAlertListener, NotificationPublisher, outbox, SignalR)
   - Serialization & config (Newtonsoft.Json, appsettings, error handling, logging)
   - DI & startup (Program.cs lifecycle, service registration, domain events, outbox pruning)
   - Desktop agent protocol (pyzmq, CURVE, auth, reconnection, WebSocket bridge)
   - Existing tests (23 test files, xUnit, coverage map)

2. Full specification written with 16 sections covering:
   - Current architecture with Mermaid diagrams
   - All 21 command types and 50 query types (verified against actual code)
   - 6 notification types with topic patterns
   - Target architecture with transport abstraction interfaces
   - File-by-file change plan (new, modified, removed, unchanged)
   - 6-phase migration plan with dependencies
   - 17 JIRA tasks estimated at ~73 story points
   - Test plan building on 23 existing test files
   - 7 open questions requiring decision

## Key Finding
The CQRS gateway has **71 cases** (not ~32 as initially estimated) across 1,256 lines of boilerplate. 67 of 71 follow identical 6-line patterns. This strongly validates the registry-based dispatch approach.

## Constraints Preserved
- No production code modified
- No JIRA tasks created
- No Azure SDKs added
- No NetMQ removed
- No wire protocol changed
- No secrets in documentation

## JIRA Tasks Created

**Epic:** ASPS-675 — Messaging Refactoring

| Phase | Key | Summary |
|-------|-----|---------|
| 0 | ASPS-676 | Create transport abstraction interfaces |
| 0 | ASPS-677 | Create CqrsHandlerRegistry |
| 0 | ASPS-678 | Create MessagingServiceRegistration |
| 1 | ASPS-679 | Extract 21 command handlers |
| 1 | ASPS-680 | Extract 50 query handlers |
| 1 | ASPS-681 | Wire handler registry into dispatch |
| 2 | ASPS-682 | Extract NotificationPublisher behind INotificationEgress |
| 2 | ASPS-683 | Update OutboxNotificationPublisher |
| 3 | ASPS-684 | Split RealTimeAlertListener |
| 3 | ASPS-685 | IHostedService on NetMQAlertIngress |
| 4 | ASPS-686 | Extract CQRSGateway behind ICqrsTransport |
| 4 | ASPS-687 | Extract CQRSClient behind ICqrsClient |
| 5 | ASPS-688 | Convert all to IHostedService lifecycle |
| 5 | ASPS-689 | Add CancellationToken to listen loops |
| 6 | ASPS-690 | Remove legacy dispatch code |
| 6 | ASPS-691 | Update architecture documentation |
| — | ASPS-692 | Security review (cross-cutting) |

## Next Steps
1. Begin Phase 0: ASPS-676, ASPS-677, ASPS-678 (can run in parallel)
2. Decisions still needed on open questions OQ-1 through OQ-7 before Phase 1

## Implementation Progress

**Branch:** `asps-675-messaging-refactoring`

| Phase | Status | Commit(s) |
|---|---|---|
| 0 — Foundation | Done | `90829f9` |
| 1 — CQRS handler extraction | Done | `8cf77d7` |
| 2 — ASPS-682/683 Notification egress extraction | Done | `c606c80` |
| 3 — ASPS-684/685 Alert ingress extraction | Done | `c606c80` |
| 4 — ASPS-686/687 CQRS transport extraction | Done | `31282d0` |
| 5 — ASPS-688/689 Lifecycle migration + CancellationToken | Done (this session) | `7940c6c` |

### Phase 2 (ASPS-682 + ASPS-683) — 2026-08-12

**ASPS-682** — Renamed `NotificationPublisher` → `NetMQNotificationEgress`, implementing
`INotificationEgress` (async wrappers delegating to the existing sync/virtual methods,
`Task.CompletedTask` since the NetMQ send is synchronous). Wire protocol (topics, JSON) unchanged.

**ASPS-683** — Wired `INotificationEgress` into DI and consumers:
- `OutboxNotificationPublisher` — constructor now depends on `INotificationEgress`; sync `_inner.PublishXxx(...)` calls replaced with `await _inner.PublishXxxAsync(...)`.
- `ReconnectSnapshotService` — constructor now depends on `INotificationEgress`; `PublishSnapshot` → `await PublishSnapshotAsync`.
- `Program.cs` — `NetMQNotificationEgress` registered as singleton; `INotificationEgress` registered to resolve the same singleton instance (single NetMQ PUB socket shared by both consumers).
- `NotificationPublisherActor` unchanged (depends on `OutboxNotificationPublisher`, not the egress type directly).

**Changed files:**
- `ASPSBackend14_J/Business/Messaging/NetMQNotificationEgress.cs` (renamed from `NotificationPublisher.cs`)
- `ASPSBackend14_J/Business/Messaging/OutboxNotificationPublisher.cs`
- `ASPSBackend14_J/Business/Messaging/ReconnectSnapshotService.cs`
- `ASPSBackend14_J/ASPSBackend/Program.cs`
- `ASPSBackend14_J/ASPS.Tests/Business/Messaging/NetMQNotificationEgressTests.cs` (renamed from `NotificationPublisherTests.cs`)
- `ASPSBackend14_J/ASPS.Tests/Business/Messaging/OutboxNotificationPublisherTests.cs`
- `ASPSBackend14_J/ASPS.Tests/Business/Messaging/ReconnectSnapshotServiceTests.cs`
- `ASPSBackend14_J/ASPS.Tests/Business/Messaging/NotificationPublisherActorTests.cs`

**Verification:**
- `dotnet build ASPSBackend.sln -c Debug --nologo` → 0 errors (299 pre-existing warnings, none new).
- `dotnet test ASPS.Tests/ASPS.Tests.csproj --nologo -v q` → Passed: 1657, Failed: 0, Skipped: 7, Total: 1664.

### Phase 5 (ASPS-688 + ASPS-689) — 2026-08-16

**Read first:** verified Phases 0–4 had already done most of the Phase 5 work —
`ICqrsTransport`/`NetMQCqrsTransport` (Phase 4) already implemented `IHostedService`
with a poll-with-timeout receive loop and was already wired via `AddHostedService` in
`Program.cs`. `IAlertIngress`/`ICqrsClient`/`INotificationEgress` already had full
`CancellationToken` parameters on every method (from Phases 2–4). Remaining gaps:

**ASPS-688 — IHostedService lifecycle:**
- `NetMQMessageProcessor` (internal CQRS channel, port 5555): was `IDisposable` only,
  manually `Start()`/`Stop()`-ed from `Program.Main()`. Converted to
  `IHostedService` (`StartAsync`/`StopAsync`); registered via
  `services.AddHostedService(sp => sp.GetRequiredService<NetMQMessageProcessor>())`.
- `NetMQAlertIngress` (real-time alert listener, port 50001): already implemented
  `IHostedService` (via `IAlertIngress`) but was never registered with
  `AddHostedService` — `Program.Main()` called `alertIngress.StartAsync(CancellationToken.None)`
  manually instead. Fixed: added `services.AddHostedService(sp => sp.GetRequiredService<NetMQAlertIngress>())`;
  removed the manual `StartAsync` call and the now-unused `alertIngress` variable
  from `Program.cs`. Startup order preserved — `SetReconnectSnapshotService` and
  `InitializeAnalysisManagersAsync` still run synchronously in `Main()` before
  `host.RunAsync()` starts all hosted services (`NetMQMessageProcessor`,
  `NetMQAlertIngress`, `NetMQCqrsTransport`, `SimulationRunner`, `OutboxPruningService`)
  in registration order.
- `ASView` and `ReconnectSnapshotService` were checked and are **not** NetMQ
  transport services (in-memory cache / reactive publisher respectively) — left
  unchanged, out of scope.
- `NetMQNotificationEgress` (PUB socket, port 50002) binds its socket in the
  constructor and has no `Start()`/`Stop()` calls in `Program.cs` to remove — this
  eager-construction + DI-disposal pattern was established in Phase 2 and is left
  as-is (no manual lifecycle calls existed to migrate).

**ASPS-689 — CancellationToken propagation:**
- `NetMQMessageProcessor.ProcessMessages` and `NetMQAlertIngress.ListenForAlerts`
  switched from blocking receive calls (`ReceiveFrameString()`,
  `ReceiveMultipartMessage()`, `ReceiveFrameBytes()`) to `TryReceive*` with a 500ms
  poll timeout, checking `cancellationToken.IsCancellationRequested` each iteration
  (mirrors the pattern `NetMQCqrsTransport` already used from Phase 4). Note: the
  `_isRunning` boolean flag was kept (not removed per spec's "ideal" step 5) because
  it is the actual mechanism `StopAsync` uses to signal shutdown — the
  `CancellationToken` passed to `IHostedService.StartAsync` by the generic host is
  the *startup* token, not linked to `StopAsync`'s shutdown token, so it alone
  cannot stop the loop. This matches the already-reviewed `NetMQCqrsTransport`
  convention from Phase 4 (kept for consistency, not modified).
- `NetMQNotificationEgress`'s six `Publish*Async` wrapper methods now call
  `ct.ThrowIfCancellationRequested()` before delegating to the synchronous publish
  methods.
- `ICqrsClient`/`NetMQCqrsClient` (Phase 4) already fully threaded `ct` through
  `Task.Run(..., ct)` with `ct.ThrowIfCancellationRequested()` inside — verified,
  no changes needed.
- `OutboxNotificationPublisher` (the outbox-wrapped caller of `INotificationEgress`)
  does **not** have `CancellationToken` parameters on its own public API — this was
  out of scope (task instructions scoped ASPS-689 to the four interfaces under
  `Business/Messaging/Abstractions/`, not every downstream wrapper; adding `ct` there
  would cascade into `NotificationPublisherActor` and other callers not covered by
  this task).

**Changed files:**
- `ASPSBackend14_J/Business/Messaging/NetMQMessageProcessor.cs` — `IHostedService`, poll-with-timeout loop
- `ASPSBackend14_J/Business/Messaging/NetMQAlertIngress.cs` — poll-with-timeout loop (`TryReceiveMultipartMessage`/`TryReceiveFrameBytes`)
- `ASPSBackend14_J/Business/Messaging/NetMQNotificationEgress.cs` — `ct.ThrowIfCancellationRequested()` in async wrappers
- `ASPSBackend14_J/ASPSBackend/Program.cs` — `AddHostedService` registrations; removed manual `Start()`/`StartAsync()` calls
- `ASPSBackend14_J/ASPS.Tests/Business/Messaging/NetMQMessageProcessorTests.cs` — `Start()`/`Stop()` → `StartAsync`/`StopAsync`; added `IHostedService` assignability test + cooperative-shutdown timing test
- `ASPSBackend14_J/ASPS.Tests/Business/Messaging/NetMQNotificationEgressTests.cs` — added 3 `CancellationToken` tests

**Verification:**
- `dotnet build ASPSBackend.sln -c Debug --nologo` → 0 errors (299 warnings, same baseline, none new).
- `dotnet test ASPS.Tests/ASPS.Tests.csproj --nologo -v q` → Passed: 1682, Failed: 0, Skipped: 7, Total: 1689 (+5 new tests vs. Phase 4 baseline of 1677 passed).
- Local `main` matches `origin/main` (`fa07566`) — no merge needed before QA.

**Commit:** `7940c6c` — `ASPS-675 Phase 5: Lifecycle migration + CancellationToken (ASPS-688, ASPS-689)`

**Not yet done:** push to remote, request QA review (per `.claude/rules/task-workflow.md` pre-QA gate — push + notify orchestrator still pending), Phase 6 cleanup (remove `CQRSGateway.Commands.cs`/`CQRSGateway.Queries.cs`, doc updates).
