# ASPS-718: WebSocket Gateway for Desktop Agent Cloud Connectivity

**JIRA:** ASPS-718 (Story under ASPS-693 Epic)
**Status:** In Progress — code merged to main via PR #29, ASPS-723 (E2E test) still To Do
**Branch:** `asps-718-websocket-gateway-for-desktop-agent-cloud-connectivity`
**Commit:** `f37e5cf`
**Last updated:** 2026-08-19

---

## Problem

Desktop agents communicate with Backend via ZMQ on ports 50001 (REQ/REP alerts) and 50002 (PUB/SUB notifications) with CURVE encryption. In the Azure sidecar architecture, these ports are not externally reachable:
- Sidecar containers have no individual ingress
- Container Apps TCP ingress (Envoy) does not forward ZMQ ZMTP wire protocol

## Solution

WebSocket gateway in WebApi that bridges WS connections (through HTTP ingress) to internal ZMQ sockets on localhost. Desktop agent gets a new WS transport that's selected via `TRANSPORT_MODE` config.

## Sub-tasks

| JIRA | Task | Agent | Status |
|---|---|---|---|
| ASPS-719 | ADR: WebSocket Gateway architecture decision | architect | Done |
| ASPS-720 | Backend: WebSocket Gateway hosted service | backend | Done (JIRA Done 2026-08-19) |
| ASPS-721 | Desktop Agent: WebSocket transport layer | desktop-agent | Done (JIRA Done 2026-08-19) |
| ASPS-722 | Message protocol: WS-ZMQ frame mapping spec | architect | Done |
| ASPS-723 | E2E test: Desktop agent alert via WebSocket | backend → qa | In Progress (JIRA In Progress 2026-08-19) |

## Architecture

```
Desktop Agent
  │ WebSocket (wss://ca-webapi-dev.../ws/agent)
  │ TLS terminated by Container Apps ingress
  ▼
WebApi Container (ASP.NET middleware)
  │ WebSocket Gateway
  │ Authenticates agent (device token)
  │ Forwards payloads verbatim (no field renaming)
  │
  ├──► ZMQ REQ → Backend AlertIngress (localhost:50001)
  └──► ZMQ SUB ← Backend NotificationEgress (localhost:50002)
       forwards notifications to agent via WS
```

## Implementation Summary

### ASPS-720: Backend WebSocket Gateway (C# / .NET 8)

New files in `ASPSBackend14_J/WebApi/`:
- `Services/AgentProtocol.cs` — Frame types, error codes, close codes, connection state enum, data types
- `Services/AgentFrameParser.cs` — Inbound frame parsing with `DateParseHandling.None` (prevents Newtonsoft date mutation)
- `Services/AgentFrameBuilder.cs` — Outbound frame construction (response, notification, error, ping)
- `Services/IAgentBackendGateway.cs` — Abstraction over ZMQ bridge for testability
- `Services/AgentConnection.cs` — Per-connection auth state machine, request/subscribe handling
- `Services/AgentGatewayService.cs` — Singleton IHostedService: ZMQ REQ per request (localhost:50001, CURVE), ZMQ SUB per subscription (localhost:50002, CURVE), per-IP connection counter
- `Middleware/AgentWebSocketMiddleware.cs` — HTTP upgrade, subprotocol validation, receive loop, idle timeouts; placed BEFORE UseAuthentication in pipeline

Modified files:
- `WebApi/Program.cs` — Added `AgentGatewayService` (singleton + hosted service), `UseWebSockets`, middleware registration
- `WebApi/appsettings.json` — Added `AgentGateway` config section

Tests: `ASPS.Tests/WebApi/AgentGatewayTests.cs` — 37 unit tests

**Test results:** 1712 passed, 7 skipped, 0 failed

### ASPS-721: Desktop Agent WebSocket Transport (Python 3.11)

New files in `apps/desktop/win/src/`:
- `ws_client.py` — Full WS transport client (665 lines): background thread with asyncio event loop, persistent connection, reconnect-with-backoff (1s→30s + jitter), request/response correlation via `concurrent.futures.Future`, auth state machine, auto-subscribe on auth + reconnect. Drop-in replacement for ZMQClient + NotificationClient.
- `alert_builders.py` — Extracted shared payload builders from zmq_client.py (DRY: both transports use identical payloads)
- `config_azure.py` — Azure environment override (TRANSPORT_MODE="ws", WS_URL)
- `tests/test_ws_client.py` — 51 tests covering frame builders, backoff timing, pending requests, dispatch, auth state, lifecycle, payload parity

Modified files:
- `config.py` — Added TRANSPORT_MODE, WS_URL config vars; CURVE key loader returns "" in WS mode (no SystemExit); key resolved AFTER config_override import
- `core/container.py` — Transport selection: shared WSClient when TRANSPORT_MODE=="ws", original ZMQ clients otherwise
- `zmq_client.py` — Refactored to use alert_builders.py (DRY extraction)
- `auth_manager.py` — CURVE key application skipped in WS mode
- `tests/test_curve_bootstrap.py` — Added WS transport tolerance tests

**Test results:** WS client: 51 passed. CURVE bootstrap: 25 passed.

### Design documents (ASPS-719, ASPS-722)

- `docs/architecture/decisions/ADR-004-ASPS-718-WEBSOCKET-GATEWAY.md` — Full ADR: 3 options evaluated, decision rationale, security analysis, threat model, backward compatibility
- `docs/architecture/WS-AGENT-PROTOCOL.md` — Complete wire protocol spec: frame types, auth handshake, correlation, subscription, heartbeat, reconnection, error codes

## Key Design Decisions Made

1. **Gateway in WebApi** (not Backend) — WebApi has HTTP ingress, already has ZMQ clients, CurveKeyManager in DI
2. **Middleware before UseAuthentication** — Agents use application-layer auth (device tokens), not Keycloak
3. **Single WSClient** for both request/response and notifications — replaces ZMQ REQ + ZMQ SUB
4. **Payloads forwarded verbatim** — No field renaming, no casing changes; `DateParseHandling.None` prevents Newtonsoft mutation
5. **`alert_builders.py` extracted** — DRY: both transports produce byte-identical payloads
6. **TRANSPORT_MODE config** — "zmq" (default, local) or "ws" (Azure); selected via config_override.py at build time

## Completed Steps

- [x] CEO code review of both implementations — verified
- [x] System specification docs updated (ASPS_System_Specification.md, ASPS_DATA_FLOW.md, DESKTOP_AGENT_FEATURES.md)
- [x] All changes committed to branch `asps-718-websocket-gateway-for-desktop-agent-cloud-connectivity` (commit `f37e5cf`)
- [x] Branch pushed to origin
- [x] JIRA: ASPS-720, ASPS-721 → Done (2026-08-19)

## Continuation Point

PR #29 merged 2026-08-19. ASPS-718 In Progress (ASPS-723 still open).

### ASPS-723: E2E Test — Current Status

**QA assessment (2026-08-19):** Testability blocker found.
- `AgentWebSocketMiddleware.cs:50` resolves concrete sealed `AgentGatewayService` instead of `IAgentBackendGateway` interface
- This prevents mock injection for E2E tests of the success path (auth + alert forwarding + notifications)
- 8 of 12 E2E scenarios can be tested without changes (HTTP rejection, unauthenticated errors, frame validation)
- 4 scenarios blocked: auth round-trip, alert forwarding, notification delivery

**Fix in progress:** Backend agent refactoring middleware to resolve `IAgentBackendGateway` + connection limiter interface instead of concrete class. After fix completes → QA agent writes full E2E test suite.
