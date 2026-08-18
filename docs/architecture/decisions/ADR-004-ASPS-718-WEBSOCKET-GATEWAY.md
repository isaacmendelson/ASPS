# ADR-004 — ASPS-718 WebSocket Gateway for Device Communication

- Status: Proposed
- Date: 2026-08-19
- Jira: ASPS-718 — `WebSocket Gateway for Desktop Agent Cloud Connectivity`
- Decision owners: Architect
- Supersedes: ADR-003 AD-3 (TCP ingress for port 50001)
- Depends on: ADR-003 (sidecar architecture), ADR-001 (MessageEnvelopeV1)

## Context

Desktop agents communicate with the Backend via ZMQ on two channels:

| Channel | Port | Pattern | Purpose |
|---|---|---|---|
| Alert ingress | 50001 | ROUTER/REQ | Device alerts, token lifecycle, registration |
| Notification egress | 50002 | PUB/SUB | Analysis results, ImmediateDanger, tracked domains, policies |

Both channels use CURVE encryption (Z85-encoded keypair). The Backend binds
these sockets; agents connect as ZMQ clients.

ADR-003 AD-3 decided to use Container Apps TCP ingress (Layer 4 pass-through)
for port 50001. During ASPS-693 deployment, this was found to **not work**:
Container Apps TCP ingress routes through Envoy, which does not forward the
ZMQ ZMTP wire protocol. DNS resolves, TCP connects, but the CURVE handshake
fails through the proxy (`docs/cloud/AZURE_DEPLOYMENT_GUIDE.md`, Lessons
Learned). The Backend was moved to a sidecar container (ADR-003 revised
architecture), which solved internal WebApi-to-Backend communication via
localhost but left desktop agents with no way to reach the Backend from the
internet.

**Problem:** desktop agents cannot connect to the Azure-hosted Backend because
the sidecar has no external ingress and Container Apps Envoy does not forward
ZMTP.

## Options evaluated

### Option A: Azure Load Balancer + separate Backend Container App

Move Backend back to its own Container App with an Azure Load Balancer for
Layer 4 TCP pass-through (bypassing Envoy).

| Pro | Con |
|---|---|
| No code changes | Breaks sidecar architecture — CQRS needs localhost |
| Direct TCP pass-through | Azure LB cost (~$18/month base + data) |
| | TCP pass-through still might not work with CURVE (unverified) |
| | Separate Container App complicates deployment |
| | No path for mobile clients (mobile SDKs lack ZMQ) |

### Option B: WebSocket Gateway (selected)

Add a WebSocket endpoint to WebApi that bridges WS connections from desktop
agents to the Backend's ZMQ sockets on localhost. Container Apps HTTP ingress
handles WebSocket natively (Envoy supports WS upgrade). TLS replaces CURVE
for transport encryption.

| Pro | Con |
|---|---|
| Zero Azure infra cost — uses existing HTTP ingress | New code: WS gateway + agent transport layer |
| Works with Container Apps out of the box | Two transport implementations to maintain during migration |
| Bidirectional on a single connection | |
| Future-proof: mobile (Kotlin/Swift) can use WS natively | |
| TLS managed by Container Apps — no key distribution | |
| Preserves sidecar architecture (CQRS stays on localhost) | |

### Option C: VPN / Tailscale

Desktop agents join the Azure VNet via VPN or Tailscale, reaching the Backend
directly.

| Pro | Con |
|---|---|
| Transparent — no protocol changes | Requires VPN software on every protected device |
| | ASPS protects elderly scam victims — VPN install complexity is unacceptable |
| | Per-device licensing cost for managed VPN |
| | Does not solve mobile connectivity |

## Decision

**Option B: WebSocket Gateway.**

The WS gateway lives in the WebApi process as ASP.NET Core middleware,
accepting connections at `/ws/agent`. It bridges to the Backend's ZMQ sockets
on localhost within the same sidecar pod:

```
Desktop Agent
  | wss://ca-webapi-dev.../ws/agent
  | TLS terminated by Container Apps ingress
  v
WebApi Container (ASP.NET middleware)
  | WebSocket Gateway
  |
  |--- ZMQ REQ --> Backend ROUTER (localhost:50001)
  |                  forwards alert/token messages, returns responses
  |
  +--- ZMQ SUB <-- Backend PUB (localhost:50002)
                     subscribes to device topic, forwards notifications to WS
```

### Gateway placement rationale

The gateway is in WebApi (not Backend) because:

1. **WebApi has HTTP ingress** — the sidecar Backend has none.
2. **Precedent** — WebApi already communicates with Backend via ZMQ on
   localhost: `NetMQClientService` (port 5555, deprecated) and
   `NetMQCqrsClient` (port 5556, CQRS). Adding ZMQ REQ/SUB clients for
   ports 50001/50002 follows the same pattern.
3. **Zero Backend changes** — the Backend's `NetMQAlertIngress` (ROUTER) and
   `NetMQNotificationEgress` (PUB) continue to operate unchanged. The WS
   gateway is just another ZMQ client on localhost.
4. **CurveKeyManager is already in WebApi DI** — the gateway uses
   `ApplyClientCurve()` for localhost ZMQ connections, exactly as a desktop
   agent would.

### Future optimization

A second-generation architecture could add Kestrel to the Backend sidecar
(listening on an internal port) with a `WebSocketAlertIngress` implementing
`IAlertIngress`. WebApi would reverse-proxy `/ws/agent` to the Backend's
internal WS port. This eliminates the localhost ZMQ hop but requires Backend
changes and YARP/reverse-proxy configuration. Deferred — the gateway-via-ZMQ
approach works with zero Backend changes and can be replaced transparently
because the WS protocol (see `docs/architecture/WS-AGENT-PROTOCOL.md`) is
transport-agnostic.

## Security analysis

### Transport encryption

| Transport | Encryption | Key management |
|---|---|---|
| ZMQ (current) | CURVE (NaCl) | Server keypair in Azure Files, ephemeral client keys |
| WebSocket (new) | TLS 1.2+ | Container Apps managed certificate (auto-renewal) |

TLS replaces CURVE for the WS path. CURVE remains for agents connecting via
ZMQ directly (local dev, on-premise). The agent selects transport from
configuration, not from the message format.

### Authentication

The application-level auth flow is unchanged:

1. Agent sends `RequestToken` (or `RefreshToken`) message over WS.
2. Backend validates device identity and returns a token.
3. Subsequent alert messages include the token.
4. Backend's `TokenStore.ValidateToken()` rejects invalid/expired tokens.

The WS gateway validates the device token at connection level (after the first
successful `RequestToken` response) and rejects subsequent messages from
unauthenticated connections. This prevents unauthenticated agents from
consuming gateway resources.

### No HMAC envelope needed

The CQRS channel (port 5556) uses HMAC message signing because it carries
admin commands. The alert channel carries device-initiated alerts validated
by device tokens. The WS connection is TLS-encrypted and device-token-
authenticated — no additional HMAC envelope is needed.

### Threat model

| Threat | Control |
|---|---|
| Man-in-the-middle on WS | TLS (Container Apps managed certificate) |
| Unauthenticated agent sends alerts | Device token validation at gateway and Backend |
| Token theft | Tokens are short-lived (24h), refreshable, per-device |
| Replay attack | `messageId` deduplication in `MessageDeduplicator` (15min window, 100K capacity) |
| DoS via WS connections | Rate limiting at gateway, connection limits, idle timeout |
| Message tampering | TLS integrity; Backend validates all fields on receipt |

## Backward compatibility

- **Local development:** agents continue to use ZMQ directly (localhost:50001,
  localhost:50002). No change to the local dev workflow.
- **Agent transport selection:** the agent reads transport type from
  configuration (`transport: "zmq"` or `transport: "ws"`). Message payloads
  are identical on both transports.
- **Backend is unaware of transport:** `AlertProcessor` receives the same JSON
  regardless of whether it arrived via ZMQ or via the WS gateway's ZMQ
  forwarding.

## Protocol specification

The WebSocket wire protocol is defined in a companion document:
`docs/architecture/WS-AGENT-PROTOCOL.md` (ASPS-722).

## Implementation ownership

| Order | Owner | Deliverable | JIRA | Depends on |
|---:|---|---|---|---|
| 1 | architect | ADR + protocol specification | ASPS-719, ASPS-722 | — |
| 2 | backend | WS gateway hosted service in WebApi | ASPS-720 | ASPS-719, ASPS-722 |
| 3 | desktop-agent | WS transport layer in Python agent | ASPS-721 | ASPS-722 |
| 4 | qa | E2E test: alert via WS | ASPS-723 | ASPS-720, ASPS-721 |

Steps 2 and 3 can run in parallel after the protocol spec is frozen — each
developer implements against the spec independently.

## Consequences

- **Positive:** desktop agents can reach the Azure-hosted Backend through
  standard HTTPS/WSS. No special Azure networking (VPN, Load Balancer, VNet
  TCP ingress) required. Mobile clients (future) can use the same WS
  endpoint. Container Apps managed TLS eliminates CURVE key distribution for
  cloud-connected agents.
- **Cost:** new gateway component (~200-400 LOC in WebApi), new WS transport
  in the Python agent (~150-250 LOC), two transport paths to maintain during
  the migration period.
- **Risk:** gateway introduces a forwarding hop (WS -> ZMQ on localhost).
  Mitigate with connection pooling and async forwarding. Latency impact is
  negligible for the alert use case (analysis takes seconds, not
  milliseconds).
- **Risk:** notification delivery over WS may lose messages if the WS
  connection drops. Mitigate with the existing reconnect-snapshot mechanism
  (ASPS-620: `ReconnectSnapshotService` replays pending outbox entries on
  `RequestToken`).
- **ADR-003 AD-3 is superseded:** TCP ingress for port 50001 is replaced by
  the WS gateway. The TCP keepalive settings from AD-3 are no longer
  relevant for cloud-connected agents (but remain valid for local ZMQ).
