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
