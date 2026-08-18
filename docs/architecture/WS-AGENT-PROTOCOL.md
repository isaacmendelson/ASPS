# WebSocket Agent Protocol Specification

> **Status:** Proposed
> **Date:** 2026-08-19
> **JIRA:** ASPS-722
> **ADR:** ADR-004 (ASPS-718)
> **Audience:** Backend developer (.NET), Desktop-agent developer (Python)

---

## 1. Overview

This document specifies the wire protocol for WebSocket communication between
ASPS desktop agents and the Backend, bridged through the WebSocket gateway in
WebApi. It is a **transport-layer** specification — message payloads are
identical to those carried over ZMQ today. The gateway is a bridge, not a new
application layer.

### Design principles

1. **Same payloads, different transport.** The JSON bodies sent by the agent
   and returned by the Backend are unchanged. The WS protocol wraps them in a
   thin frame envelope for routing and correlation.
2. **JSON text frames only.** No binary framing, no compression extensions.
3. **Preserve MessageEnvelopeV1 where applicable.** V1-capable messages use
   the existing envelope; legacy messages use the existing flat JSON.
4. **Transport selected by configuration, not by message format.** The agent
   reads `transport: "zmq"` or `transport: "ws"` from config. The Backend's
   `AlertProcessor` is transport-agnostic.

### Scope

| In scope | Out of scope |
|---|---|
| WS endpoint, handshake, auth | Desktop-Extension local IPC (ADR-002) |
| Request/response framing | Admin UI notifications (SignalR `/notificationshub`) |
| Notification push framing | CQRS channel (port 5556) |
| Heartbeat, reconnection | Mobile client (uses same protocol — no additional spec) |
| Error handling | Backend internal changes |

---

## 2. WebSocket Endpoint

### Connection URL

```
wss://{host}/ws/agent
```

- **Path:** `/ws/agent`
- **Protocol:** `wss://` (TLS terminated by Container Apps or reverse proxy)
- **Local dev:** `ws://localhost:5001/ws/agent` (or HTTPS port 5002)

### Upgrade handshake

Standard HTTP/1.1 WebSocket upgrade per RFC 6455.

| Header | Value | Required |
|---|---|---|
| `Upgrade` | `websocket` | yes |
| `Sec-WebSocket-Protocol` | `asps-agent-v1` | yes |
| `Sec-WebSocket-Version` | `13` | yes |

The server MUST respond with `Sec-WebSocket-Protocol: asps-agent-v1` in the
101 response. If the subprotocol is missing or unsupported, the server rejects
the upgrade with 400.

### Connection limits

| Parameter | Value |
|---|---|
| Max concurrent connections per IP | 10 |
| Max frame size | 256 KiB |
| Unauthenticated idle timeout | 30 seconds |
| Authenticated idle timeout | 5 minutes (reset by any message or pong) |

---

## 3. Message Framing

All messages are UTF-8 JSON text frames. Each frame is a JSON object with a
`frame` discriminator field.

### 3.1 Client-to-server frames

#### `request` — agent sends an alert or token message

```json
{
  "frame": "request",
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "payload": { ... }
}
```

| Field | Type | Description |
|---|---|---|
| `frame` | string | Always `"request"` |
| `id` | string (UUID v4) | Correlation ID for matching the response. Unique per request. |
| `payload` | object | The alert/token JSON — identical to what `ZMQClient.send_alert()` sends today. |

The `payload` contains the same JSON the agent currently sends over ZMQ:

- `RequestToken` messages: `{"MessageType":"RequestToken","DeviceUid":"...","Email":"..."}`
- `RefreshToken` messages: `{"MessageType":"RefreshToken","DeviceUid":"...","Token":"..."}`
- `RegisterDevice` messages: `{"MessageType":"RegisterDevice",...}`
- `NotificationAck` messages: `{"MessageType":"NotificationAck","DeviceUid":"...","MessageId":"..."}`
- Alert messages: `{"AlertType":"UrlAlert","DeviceInfo":{...},...}`
- V1 envelope messages: `{"schemaVersion":"1.0","messageType":"url_scan.request",...}`

The gateway does not parse or transform `payload`. It forwards the JSON
verbatim to Backend's ROUTER socket on localhost:50001 and returns the
response.

#### `subscribe` — agent subscribes to notifications

```json
{
  "frame": "subscribe",
  "deviceUid": "PC-JOHN-001"
}
```

| Field | Type | Description |
|---|---|---|
| `frame` | string | Always `"subscribe"` |
| `deviceUid` | string | The device UID to subscribe to. Must match the authenticated device. |

The gateway subscribes to `device:{deviceUid}` on Backend's PUB socket
(localhost:50002) and forwards notifications to this WS connection. Only one
subscription per connection is permitted; a second `subscribe` replaces the
first.

**Security:** the gateway validates that `deviceUid` matches the device that
authenticated on this connection (via `RequestToken`). A mismatch results in
an `error` frame with code `auth.device_mismatch`.

#### `pong` — application-level keepalive response

```json
{
  "frame": "pong",
  "ts": "2026-08-19T10:15:30.123Z"
}
```

Optional. The agent MAY respond to `ping` frames with an application-level
`pong`. WebSocket protocol-level ping/pong (opcode 0x9/0xA) is the primary
keepalive mechanism; this application-level pong is supplementary.

### 3.2 Server-to-client frames

#### `response` — Backend response to a request

```json
{
  "frame": "response",
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "payload": { ... }
}
```

| Field | Type | Description |
|---|---|---|
| `frame` | string | Always `"response"` |
| `id` | string (UUID v4) | Echoes the `id` from the `request` frame. |
| `payload` | object | The response JSON from Backend — identical to what `ZMQClient.send_alert()` receives today. |

#### `notification` — Backend pushes a notification

```json
{
  "frame": "notification",
  "topic": "device:PC-JOHN-001",
  "payload": { ... }
}
```

| Field | Type | Description |
|---|---|---|
| `frame` | string | Always `"notification"` |
| `topic` | string | The PUB/SUB topic (e.g., `"device:PC-JOHN-001"`). |
| `payload` | object | The notification JSON — identical to the second frame of the ZMQ PUB message. |

#### `ping` — server keepalive probe

```json
{
  "frame": "ping",
  "ts": "2026-08-19T10:15:30.123Z"
}
```

Sent by the gateway every 60 seconds. The agent SHOULD respond with a `pong`
frame. The gateway also sends WebSocket protocol-level pings; the agent's WS
library handles those automatically.

#### `error` — connection-level or request-level error

```json
{
  "frame": "error",
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "code": "gateway.timeout",
  "message": "Backend did not respond within 10 seconds"
}
```

| Field | Type | Description |
|---|---|---|
| `frame` | string | Always `"error"` |
| `id` | string or null | Echoes the `id` from the `request` frame, or `null` for connection-level errors. |
| `code` | string | Machine-readable error code (see section 9). |
| `message` | string | Human-readable description, safe for logs. |

---

## 4. Authentication Handshake

Authentication uses the existing `RequestToken` / `RefreshToken` flow carried
inside `request` frames. There is no separate WS-level authentication
handshake.

### 4.1 Connection lifecycle

```
Agent                          Gateway                        Backend (ZMQ)
  |                              |                              |
  |-- WS upgrade (/ws/agent) --> |                              |
  |<- 101 Switching Protocols -- |                              |
  |                              |                              |
  |  Connection is UNAUTHENTICATED. Only RequestToken,          |
  |  RefreshToken, and RegisterDevice payloads are accepted.    |
  |                              |                              |
  |-- request {RequestToken} --> |-- ZMQ REQ (localhost:50001) ->|
  |                              |<- ZMQ REP {TokenCreated} ----|
  |<- response {TokenCreated} --|                              |
  |                              |                              |
  |  Connection is now AUTHENTICATED for this deviceUid.        |
  |  Token stored in gateway's connection state.                |
  |                              |                              |
  |-- subscribe {deviceUid} --> |-- ZMQ SUB device:{uid} ------>|
  |                              |  (subscribes on PUB socket)  |
  |                              |                              |
  |-- request {UrlAlert} -----> |-- ZMQ REQ (localhost:50001) ->|
  |                              |<- ZMQ REP {accepted} --------|
  |<- response {accepted} -----|                              |
  |                              |                              |
  |                              |<- ZMQ PUB [topic|json] ------|
  |<- notification {result} ---|                              |
  |                              |                              |
```

### 4.2 Gateway authentication state machine

```
UNAUTHENTICATED
  |
  | request with MessageType in {RequestToken, RefreshToken, RegisterDevice}
  | AND Backend responds with status in {TokenCreated, TokenRefreshed, Registered}
  v
AUTHENTICATED (deviceUid, token stored)
  |
  | Token expired -> Backend responds {status: "TokenExpired"}
  v
UNAUTHENTICATED (agent must re-authenticate)
```

**Rules:**

1. In `UNAUTHENTICATED` state, the gateway accepts only `request` frames
   whose `payload` has `"MessageType"` equal to `"RequestToken"`,
   `"RefreshToken"`, or `"RegisterDevice"`. All other `request` frames
   receive an `error` frame with code `auth.not_authenticated`.
2. A `subscribe` frame in `UNAUTHENTICATED` state receives an `error` frame
   with code `auth.not_authenticated`.
3. After successful authentication, the gateway stores the `deviceUid` and
   `token` from the response. The connection is `AUTHENTICATED`.
4. For authenticated `request` frames, the gateway validates that the
   `payload` contains a `Token` field matching the stored token (or that
   the `payload.DeviceInfo.DeviceUid` matches the authenticated device).
   Mismatches receive an `error` frame with code `auth.device_mismatch`.
5. If Backend responds with `status: "InvalidToken"` or
   `status: "TokenExpired"`, the gateway transitions back to
   `UNAUTHENTICATED` and forwards the response.

### 4.3 Token in payload vs. connection state

The agent continues to include `"Token"` in alert payloads (unchanged from
the ZMQ flow). The Backend validates the token in `AlertProcessor`. The
gateway's connection-level auth state is an additional layer of defense — it
prevents unauthenticated connections from forwarding arbitrary payloads to the
Backend.

---

## 5. Request/Response Correlation

The WS protocol is asynchronous (unlike ZMQ REQ/REP which is synchronous).
The `id` field provides request-response correlation.

### Rules

1. The agent generates a unique UUID v4 `id` for each `request` frame.
2. The gateway echoes the `id` in the corresponding `response` or `error`
   frame.
3. The agent uses `id` to match responses to pending requests.
4. Multiple requests may be in-flight simultaneously (the gateway can forward
   them concurrently to Backend via separate ZMQ REQ sockets or a queue).
5. If the gateway does not receive a Backend response within 10 seconds, it
   returns an `error` frame with `code: "gateway.timeout"`.
6. The `id` is a transport-level correlation ID. It is distinct from
   `MessageEnvelopeV1.messageId` / `correlationId` / `requestId`, which are
   application-level identifiers.

### Timeout behavior

| Scenario | Gateway behavior |
|---|---|
| Backend responds within 10s | Forward response as `response` frame |
| Backend does not respond within 10s | Return `error` frame with `gateway.timeout` |
| ZMQ connection to Backend lost | Return `error` frame with `gateway.backend_unavailable` |

---

## 6. Notification Subscription

### Subscription flow

1. Agent sends a `subscribe` frame after authenticating.
2. Gateway validates that `deviceUid` matches the authenticated device.
3. Gateway creates a ZMQ SUB socket connected to `tcp://localhost:50002`,
   subscribes to topic `device:{deviceUid}`.
4. Gateway reads 2-frame PUB messages `[topic_bytes | json_bytes]` and
   forwards them as `notification` frames to the agent.
5. Only one subscription per WS connection. A second `subscribe` frame
   replaces the previous subscription (closes the old SUB socket, opens a
   new one).

### Fan-out

Some notification types (e.g., `SetTrackedDomains`) are published to
multiple device topics. Each WS-connected agent receives the notification
on its own `device:{deviceUid}` topic — the fan-out happens on the Backend's
PUB socket, not in the WS gateway.

### Subscription lifetime

The ZMQ SUB socket is created when the agent sends `subscribe` and destroyed
when the WS connection closes. There is no persistent subscription across
reconnections — the agent must re-subscribe after reconnecting.

---

## 7. Heartbeat and Keepalive

### WebSocket protocol-level ping/pong

The gateway sends WebSocket ping frames (opcode 0x9) every 30 seconds. The
agent's WebSocket library responds with pong frames automatically.

If the gateway does not receive a pong within 10 seconds, it considers the
connection dead and closes it.

### Application-level ping/pong

The gateway sends a `{"frame":"ping","ts":"..."}` text frame every 60 seconds.
The agent MAY respond with `{"frame":"pong","ts":"..."}`. This is informational
(for latency monitoring) and not required for connection liveness.

### Container Apps idle timeout

Azure Container Apps has a 4-minute idle timeout on HTTP connections. The
30-second WebSocket ping interval ensures the connection is never idle for
more than 30 seconds, well within the timeout.

---

## 8. Reconnection Protocol

### When the WS connection drops

The agent detects a dropped connection via:
- WebSocket `onclose` event
- Failed `send()` call
- Missing pong after ping timeout

### Reconnection procedure

1. **Backoff.** Wait with exponential backoff: 1s, 2s, 4s, 8s, 16s, 30s max.
   Add random jitter (0-1s).
2. **Connect.** Open a new WS connection to `wss://{host}/ws/agent`.
3. **Authenticate.** Send `RequestToken` (or `RefreshToken` if the token is
   not yet expired).
4. **Subscribe.** Send `subscribe` with `deviceUid`.
5. **Resume.** Continue sending alerts as normal.

### Missed notification catch-up

The Backend has a reconnect-snapshot mechanism (ASPS-620,
`ReconnectSnapshotService`). When the agent sends `RequestToken` and the
Backend responds with `TokenCreated`, the Backend triggers
`SendSnapshotAsync(deviceUid)`, which:

1. Replays pending outbox entries (un-ACKed notifications) on the
   `device:{deviceUid}` PUB topic.
2. Sends a snapshot header followed by each pending notification.

The WS gateway forwards these as `notification` frames. The agent processes
them and sends `NotificationAck` messages (as `request` frames) to mark them
as delivered.

**No additional reconnection protocol is needed.** The existing outbox +
snapshot mechanism handles missed notifications transparently.

### Connection state is not preserved across reconnections

Each WS connection starts fresh:
- `UNAUTHENTICATED` state
- No active subscription
- No pending requests

The agent must authenticate and subscribe on every new connection.

---

## 9. Error Handling

### Error codes

| Code | Level | Description |
|---|---|---|
| `auth.not_authenticated` | connection | Request rejected — connection not authenticated |
| `auth.device_mismatch` | connection | deviceUid does not match authenticated device |
| `gateway.timeout` | request | Backend did not respond within 10 seconds |
| `gateway.backend_unavailable` | connection | Gateway cannot reach Backend on localhost |
| `gateway.invalid_frame` | connection | Received frame is not valid JSON or missing `frame` field |
| `gateway.frame_too_large` | connection | Frame exceeds 256 KiB limit |
| `gateway.rate_limited` | connection | Too many requests — rate limit exceeded |
| `gateway.unknown_frame_type` | connection | `frame` field has an unrecognized value |
| `gateway.subscribe_failed` | connection | Failed to create SUB socket for notifications |

### Connection-level vs request-level errors

- **Connection-level errors** (`id` is `null`): the gateway MAY close the WS
  connection after sending the error frame. Close codes:
  - 4001: authentication required
  - 4002: device mismatch
  - 4003: backend unavailable
  - 4008: rate limited
  - 4009: invalid frame
- **Request-level errors** (`id` echoes the request): the connection remains
  open. The agent should retry or handle the error.

### Backend-level errors

Backend error responses (e.g., `{"success":false,"message":"..."}` or
`{"status":"InvalidToken","message":"..."}`) are forwarded verbatim in the
`response` frame's `payload`. The gateway does not interpret or transform
them. The agent handles them the same way it handles ZMQ responses today.

---

## 10. Backward Compatibility

### Transport selection

The agent selects transport from configuration:

```python
# config.py
TRANSPORT_MODE = "zmq"  # or "ws"
WS_URL = "wss://ca-webapi-dev.azurecontainerapps.io/ws/agent"
```

- `transport: "zmq"` — current behavior (ZMQ REQ/SUB with CURVE).
- `transport: "ws"` — new WS transport.

### Message payloads are identical

The `payload` inside WS frames is byte-for-byte identical to the JSON sent
over ZMQ. No field renaming, no casing changes, no structural transformation.

---

## 11. Complete Message Type Reference

All message types carried over the alert ingress channel (port 50001). The WS
protocol carries them unchanged in `request.payload`.

### Client-to-server (alert ingress)

| Discriminator | Typical payload fields |
|---|---|
| `MessageType: "RequestToken"` | `DeviceUid`, `Email` |
| `MessageType: "RefreshToken"` | `DeviceUid`, `Token`, `Timestamp` |
| `MessageType: "RegisterDevice"` | `DeviceUid`, `Email`, `DeviceType`, `OperatingSystem`, `MAC`, `SupportedSchemaMajors` |
| `MessageType: "NotificationAck"` | `DeviceUid`, `Token`, `MessageId` |
| `AlertType: "UrlAlert"` | `DeviceInfo`, `Token`, `Url`, `Trackers`, `IFrameDomains`, `UserAgent`, `TabId` |
| `AlertType: "TrackUrlAlert"` | `DeviceInfo`, `Token`, `Url`, `FromUrl`, `Duration`, `ScamInProgressKey`, `TabId` |
| `AlertType: "RemoteAccessAlert"` | `DeviceInfo`, `Token`, `RemoteAccessApp`, `ConnectionUrl`, `Direction`, etc. |
| `AlertType: "TabClosedAlert"` | `DeviceInfo`, `Token`, `TabId`, `Url` |
| `AlertType: "TabChangedAlert"` | `DeviceInfo`, `Token`, `TabId`, `Url`, `IsSensitiveWebsite`, `IsLoggedIn` |
| `schemaVersion: "1.0"` | V1 envelope with `messageType: "url_scan.request"` |

### Server-to-client (notification egress)

| Notification `Type` | Payload (`Data`) |
|---|---|
| `AnalysisResult` | Risk assessment, indicators, protective actions |
| `ImmediateDangerNotification` | Scam detected event |
| `ImmediateDangerEndedNotification` | Danger resolved event |
| `SetTrackedDomainsNotification` | `TrackedDomains`, `UserKeyField`, `Reason` |
| `SetBrowserTabsPolicyNotification` | `DeviceUid`, `Mode`, `ValidUntil` |
| (raw JSON) | Snapshot payload from `ReconnectSnapshotService` |
| V1 envelope | `messageType: "url_scan.result"` with `outcome` |

---

## 12. Gateway Implementation Notes

Architectural guidance for the Backend developer.

### 12.1 ASP.NET Core middleware structure

```
WebApi/
  Middleware/
    AgentWebSocketMiddleware.cs    -- HTTP upgrade, connection lifecycle
  Services/
    AgentGatewayService.cs         -- ZMQ forwarding, SUB management
    AgentConnection.cs             -- per-connection state (auth, subscription)
```

The middleware:
1. Intercepts requests to `/ws/agent`.
2. Validates `Sec-WebSocket-Protocol: asps-agent-v1`.
3. Accepts the WebSocket upgrade.
4. Enters a receive loop, dispatching frames to `AgentGatewayService`.

### 12.2 ZMQ forwarding strategy

For request/response forwarding to Backend ROUTER (localhost:50001):

- **Option A (simple):** Create a new ZMQ REQ socket per request. Close after
  response. Matches the Python agent's current pattern. Adequate for expected
  load (tens of agents, not thousands).
- **Option B (pooled):** Maintain a pool of ZMQ DEALER sockets. More efficient
  for high concurrency. Defer until load requires it.

For notification subscription from Backend PUB (localhost:50002):

- One ZMQ SUB socket per authenticated WS connection.
- Subscribe to `device:{deviceUid}`.
- Background task reads from SUB socket and writes `notification` frames to
  the WS connection.
- The SUB socket is disposed when the WS connection closes.

### 12.3 CURVE on localhost

The Backend's ROUTER (50001) and PUB (50002) sockets have CURVE enabled.
The gateway must use `CurveKeyManager.ApplyClientCurve(socket)` when
connecting. This generates ephemeral client keys and sets the server public
key, exactly as a remote desktop agent would. On localhost, CURVE adds no
security value but is required because the Backend socket enforces it.

### 12.4 Registration in Program.cs

```csharp
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});
app.UseMiddleware<AgentWebSocketMiddleware>();
```

---

## 13. Agent Implementation Notes

Architectural guidance for the Desktop-agent developer.

### 13.1 Transport abstraction

The agent should implement a `WSTransportClient` alongside the existing
`ZMQClient` and `NotificationClient`, selected by configuration:

```python
class BackendTransport(ABC):
    @abstractmethod
    async def connect(self) -> bool: ...

    @abstractmethod
    async def send_alert(self, alert: dict) -> Optional[dict]: ...

    @abstractmethod
    async def subscribe_notifications(self, device_uid: str,
                                       callback: Callable) -> None: ...

    @abstractmethod
    async def close(self) -> None: ...
```

### 13.2 Configuration

```python
# config_azure.py
TRANSPORT_MODE = "ws"
WS_URL = "wss://ca-webapi-dev.purplesand-dfb51ae4.northeurope.azurecontainerapps.io/ws/agent"
```

---

## 14. Security Considerations

| Concern | Mitigation |
|---|---|
| Transport encryption | TLS (Container Apps managed cert) replaces CURVE |
| Authentication | Device token validated at gateway (connection level) and Backend (message level) |
| Authorization | Gateway enforces `deviceUid` matches authenticated device |
| Token exposure | Token in `payload` is TLS-encrypted in transit; not logged by gateway |
| Rate limiting | Gateway enforces per-IP connection limits and per-connection request rate |
| Frame injection | Subprotocol validation, JSON-only text frames, frame size limits |
| Replay | `MessageDeduplicator` in Backend (15min window, messageId-based) |
| Connection exhaustion | Max 10 connections per IP; unauthenticated timeout 30s |

The gateway MUST NOT log token values, payload content (may contain URLs and
PII), or device-identifying information beyond `deviceUid`.

---

## 15. Key Source Files

### Backend (.NET)

| File | Relevance |
|---|---|
| `Business/Messaging/AlertProcessor.cs` | Routes all alert/token messages — transport-agnostic |
| `Business/Messaging/NetMQAlertIngress.cs` | ROUTER socket on 50001 — gateway connects here |
| `Business/Messaging/NetMQNotificationEgress.cs` | PUB socket on 50002 — gateway subscribes here |
| `Business/Services/CurveKeyManager.cs` | CURVE key management, `ApplyClientCurve()` |
| `Common/Generated/Messaging/V1/MessageEnvelope.cs` | V1 envelope types |
| `WebApi/Services/NetMQClientService.cs` | Existing ZMQ client in WebApi (pattern reference) |

### Desktop agent (Python)

| File | Relevance |
|---|---|
| `apps/desktop/win/src/zmq_client.py` | ZMQ REQ client — all alert/token send methods |
| `apps/desktop/win/src/notification_client.py` | ZMQ SUB client — notification subscription |
| `apps/desktop/win/src/auth_manager.py` | Token lifecycle (RequestToken/RefreshToken) |

---

## Appendix A: Frame Examples

### A.1 Authentication

```json
{"frame":"request","id":"aaa-111","payload":{"MessageType":"RequestToken","DeviceUid":"PC-JOHN-001","Email":"john@example.com"}}
```
```json
{"frame":"response","id":"aaa-111","payload":{"status":"TokenCreated","token":"eyJ...","expiration":"2026-08-20T10:15:30.000Z","deviceUid":"PC-JOHN-001"}}
```

### A.2 Subscribe

```json
{"frame":"subscribe","deviceUid":"PC-JOHN-001"}
```

### A.3 URL Alert

```json
{"frame":"request","id":"bbb-222","payload":{"AlertId":"4b2a...","AlertType":"UrlAlert","DeviceInfo":{"DeviceUid":"PC-JOHN-001","DeviceType":1,"OperatingSystem":1},"Timestamp":"2026-08-19T10:15:30Z","Priority":1,"Token":"eyJ...","Url":"https://suspicious.example.com","Trackers":[],"IFrameDomains":[],"UserAgent":"Mozilla/5.0","TabId":"384"}}
```
```json
{"frame":"response","id":"bbb-222","payload":{"success":true,"message":"Alert accepted","alertType":"UrlAlert","deviceUid":"PC-JOHN-001"}}
```

### A.4 Notification

```json
{"frame":"notification","topic":"device:PC-JOHN-001","payload":{"Type":"AnalysisResult","Timestamp":"2026-08-19T10:15:45Z","DeviceUid":"PC-JOHN-001","Data":{"TypeName":"AnalysisResultNotification","AlertType":"UrlAlert","Severity":"Medium"}}}
```

### A.5 Error

```json
{"frame":"error","id":null,"code":"auth.not_authenticated","message":"Connection is not authenticated. Send RequestToken first."}
```
