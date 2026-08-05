# Messaging Refactoring Specification — Task Handoff

## Task
**Name:** ASPS Messaging Refactoring Implementation Specification  
**Type:** Research / Documentation (NO code changes)  
**Status:** DRAFT COMPLETE — Awaiting Isaac's review and approval  
**Date:** 2026-08-03

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

## Next Steps
1. Isaac reviews the specification
2. Decisions needed on 7 open questions (OQ-1 through OQ-7)
3. On approval: create JIRA tasks, begin Phase 0 (foundation)
