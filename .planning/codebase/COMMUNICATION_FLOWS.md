# Communication Flows

**Analysis Date:** 2026-02-13

## System Overview

This is a distributed anti-scam system with 5 primary components communicating over 4 distinct protocol layers:

```
Chrome Extension (JavaScript)
    ↕ WebSocket (ports 8080-8484)
Desktop App (Python)
    ↕ ZMQ REQ/REP (port 50001) + ZMQ PUB/SUB (port 50002)
ASPSBackend (C#)
    ↕ NetMQ CQRS (ports 5555, 5556)
WebApi (C# ASP.NET)
    ↕ HTTP REST + SignalR
Browser/Admin Dashboard
```

## Communication Protocols Summary

| Protocol | Ports | Pattern | Encryption | Purpose |
|----------|-------|---------|------------|---------|
| WebSocket | 8080-8484 | Bidirectional | None | Extension ↔ Desktop |
| ZeroMQ REQ/REP | 50001 | Request/Response | CurveZMQ | Desktop → Backend (alerts) |
| ZeroMQ PUB/SUB | 50002 | Publish/Subscribe | CurveZMQ | Backend → Desktop (notifications) |
| NetMQ REP | 5555 | Request/Response | None | WebApi → Backend (CQRS) |
| NetMQ REP | 5556 | Request/Response | None | WebApi → Backend (gateway) |
| HTTP REST | 5001/7001 | Request/Response | HTTPS (prod) | Browser → WebApi |
| SignalR | 49569 | WebSocket | None | WebApi → Browser (notifications) |

## Component 1: Chrome Extension

**Location:** `/c/Users/pc/Desktop/asps/apps/extension/chrome/`

**Technology:** JavaScript (Manifest V3 Service Worker)

**Entry Points:**
- `background.js` - Service worker handling all communication
- `content.js` - Injected into pages, collects page data
- `popup.js` - Extension popup UI

### Outgoing: Extension → Desktop App (WebSocket)

**Protocol:** WebSocket client
**Ports:** Tries 8080, 8181, 8282, 8383, 8484 in order
**Connection:** `ws://localhost:{port}`
**Implementation:** `services/ConnectionService.js`

**Message Types:**

1. **ping** - Connection keepalive
```javascript
{
  "type": "ping"
}
```

2. **url_check** - Request URL analysis
```javascript
{
  "type": "url_check",
  "url": "https://example.com",
  "trackers": [{"Type": "fbPixel", "Value": "123"}],
  "iframes": ["ads.example.com"]
}
```

3. **user_auth** - Send user email to desktop
```javascript
{
  "type": "user_auth",
  "email": "user@example.com"
}
```

4. **user_signout** - User signed out
```javascript
{
  "type": "user_signout"
}
```

5. **keepalive** - Keep service worker alive
```javascript
{
  "type": "keepalive"
}
```

6. **heartbeat_ping** - Dead connection detection
```javascript
{
  "type": "heartbeat_ping"
}
```

**Connection Management:**
- Automatic reconnection with exponential backoff (1s → 30s max)
- Heartbeat every 10 seconds, dead after 3 missed (30s total)
- Keepalive every 20 seconds to prevent service worker termination
- Message queue survives service worker restarts (session storage)
- Badge color: Green (connected), Red (disconnected), Amber (reconnecting)

**Files:**
- `services/ConnectionService.js` - WebSocket client with reconnection
- `services/MessageQueueService.js` - Queues messages during disconnection
- `background.js` - Main service worker, sets up all handlers

### Incoming: Desktop App → Extension (WebSocket)

**Message Types:**

1. **pong** - Ping response
```javascript
{
  "type": "pong",
  "status": "ok",
  "email": "user@example.com"  // Desktop shares email
}
```

2. **url_result** - URL analysis result
```javascript
{
  "type": "url_result",
  "url": "https://example.com",
  "score": 75,
  "riskType": [1, 3],
  "protectiveAction": 2,  // 0=None, 1=Ignore, 2=Warn, 3=Modal, 4=Block
  "cached": false,
  "analyzing": false  // If true, final result coming via notification
}
```

3. **remote_access_alert** - Remote access app detected
```javascript
{
  "type": "remote_access_alert",
  "toolId": 1,  // 1=AnyDesk, 2=TeamViewer, etc.
  "remote_app": "1",
  "direction": "incoming",  // 'incoming', 'outgoing', 'unknown'
  "remote_country": "United States",
  "remote_country_code": "US",
  "confidence": "high"  // 'low', 'medium', 'high'
}
```

4. **remote_access_session_end** - Session ended
```javascript
{
  "type": "remote_access_session_end"
}
```

5. **remote_access_app_closed** - App closed
```javascript
{
  "type": "remote_access_app_closed"
}
```

6. **heartbeat_pong** - Heartbeat response
```javascript
{
  "type": "heartbeat_pong"
}
```

**Result Handling:**
- Results cached in `CacheService` (in-memory + chrome.storage)
- Per-tab scores tracked separately from global state
- Icon color updated based on protective action
- Warnings injected into page via content script

**Files:**
- `background.js` - Handles all incoming messages
- `services/ScanService.js` - Processes URL results
- `services/CacheService.js` - Caches results
- `services/ProtectionService.js` - Executes protective actions
- `services/IconService.js` - Updates extension icon

## Component 2: Desktop App (Python)

**Location:** `/c/Users/pc/Desktop/asps/apps/desktop/win/src/`

**Technology:** Python 3.x with PyZMQ, WebSockets, pystray

**Entry Points:**
- `main.py` - Application entry point
- `extension_server.py` - WebSocket server for extension
- `zmq_client.py` - ZMQ client for backend
- `notification_client.py` - ZMQ subscriber for notifications

### Outgoing: Desktop App → Extension (WebSocket Server)

**Protocol:** WebSocket server (asyncio)
**Ports:** Tries 8080, 8181, 8282, 8383, 8484 (same as extension)
**Implementation:** `extension_server.py`

**Message Handling:**
- `handlers/extension_handler.py` - Routes messages by type
- Heartbeat and keepalive handled silently (no logs)
- All other messages logged with full JSON dump

**Response to url_check:**
```python
{
  "type": "url_result",
  "url": "https://example.com",
  "score": 75,
  "riskType": [1, 3],
  "protectiveAction": 2,
  "cached": False,
  "analyzing": True  # Backend processing async
}
```

**Broadcasting:**
- Can broadcast to all connected extensions via `broadcast()` method
- Used for remote access alerts from background monitors

**Files:**
- `extension_server.py` - WebSocket server
- `handlers/extension_handler.py` - Message router
- `services/scan_service.py` - URL check handler

### Outgoing: Desktop App → Backend (ZMQ REQ/REP)

**Protocol:** ZeroMQ REQ/REP socket (request-response)
**Port:** 50001
**Endpoint:** `tcp://127.0.0.1:50001` (local) or `tcp://100.88.78.75:50001` (production)
**Encryption:** CurveZMQ (enabled in Phase 4)
**Implementation:** `zmq_client.py`

**Authentication Flow:**

1. **RequestToken** - Get authentication token
```python
{
  "MessageType": "RequestToken",
  "DeviceUid": "PC-JOHN-001",
  "Email": "user@example.com"
}
```

Response:
```python
{
  "status": "TokenCreated",
  "token": "eyJ...",
  "expiration": "2026-02-14T12:00:00Z",
  "deviceUid": "PC-JOHN-001",
  "serverPublicKey": "qPsk#8DY:n9ovp[vQ!YcOnOX[f/.i@.g^f#b:!ik"  # Z85 encoded
}
```

2. **RefreshToken** - Renew expired token
```python
{
  "MessageType": "RefreshToken",
  "DeviceUid": "PC-JOHN-001",
  "Token": "old_token_here",
  "Timestamp": "2026-02-13T12:00:00Z"
}
```

**Alert Messages:**

1. **UrlAlert** - Suspicious URL detected
```python
{
  "AlertType": "UrlAlert",
  "DeviceInfo": {
    "DeviceUid": "PC-JOHN-001",
    "DeviceType": 1,  # PersonalComputer
    "OperatingSystem": 1,  # Windows
    "MAC": "00:11:22:33:44:55"
  },
  "Timestamp": "2026-02-13T12:00:00Z",
  "Priority": 1,  # Medium
  "Token": "eyJ...",
  "Url": "https://example.com",
  "Trackers": [{"Type": "fbPixel", "Value": "123"}],
  "IFrameDomains": ["ads.example.com"],
  "UserAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
}
```

Response (new async format):
```python
{
  "success": True,
  "message": "Alert processed successfully",
  "alertType": "UrlAlert",
  "deviceUid": "PC-JOHN-001",
  "timestamp": "2026-02-13T12:00:00Z",
  "priority": "Medium"
}
```

2. **RemoteAccessAlert** - Remote access app detected
```python
{
  "AlertType": "RemoteAccessAlert",
  "DeviceInfo": {
    "DeviceUid": "PC-JOHN-001",
    "DeviceType": 1,
    "OperatingSystem": 1,
    "MAC": "00:11:22:33:44:55"
  },
  "Timestamp": "2026-02-13T12:00:00Z",
  "Priority": 1,
  "Token": "eyJ...",
  "RemoteAccessApp": "1",  # 1=AnyDesk, 2=TeamViewer, etc.
  "RunningProcesses": 2,
  "ConnectionUrl": "192.168.1.100",
  "ConnectionStatus": "1",  # 0=Unknown, 1=Open, 2=Closed
  "SessionStatus": "1",
  "Direction": "incoming",
  "Confidence": "high",
  "RemoteCountry": "United States",
  "RemoteCountryCode": "US"
}
```

**Connection Lifecycle:**
- New REQ socket created for each request
- CURVE encryption keys applied if available
- 5-second timeout on send/receive
- Socket closed after response
- Lazy Pirate pattern NOT implemented (Phase 5 planned)

**CurveZMQ Setup:**
```python
# Apply CURVE encryption
if server_public_key:
    client_public, client_secret = zmq.curve_keypair()
    socket.setsockopt(zmq.CURVE_PUBLICKEY, client_public)
    socket.setsockopt(zmq.CURVE_SECRETKEY, client_secret)
    socket.setsockopt(zmq.CURVE_SERVERKEY, server_public_key)
```

**Files:**
- `zmq_client.py` - REQ/REP client
- `services/scan_service.py` - Sends UrlAlerts
- `services/monitor_service.py` - Sends RemoteAccessAlerts
- `auth_manager.py` - Token management

### Incoming: Backend → Desktop (ZMQ PUB/SUB)

**Protocol:** ZeroMQ SUB socket (publish-subscribe)
**Port:** 50002
**Endpoint:** `tcp://127.0.0.1:50002` (local) or `tcp://100.88.78.75:50002` (production)
**Encryption:** CurveZMQ (enabled in Phase 4)
**Topic:** `device:{DeviceUid}` (e.g., `device:PC-JOHN-001`)
**Implementation:** `notification_client.py`

**Notification Format:**

Multipart message: `[topic_bytes, message_bytes]`

Message:
```python
{
  "Type": "AnalysisResult",
  "Timestamp": "2026-02-13T12:00:00Z",
  "DeviceUid": "PC-JOHN-001",
  "Data": {
    "AlertType": "UrlAnalysisComplete",
    "Severity": "Medium",
    "Message": "URL analysis completed",
    "AnalysisResult": {
      "TypeName": "UrlAnalysisResult",
      "Url": "https://example.com",
      "Domain": "example.com",
      "analysis_time_ms": 1234,
      "IsFromCache": False,
      "risk_assessment": {
        "risk_score": 75,
        "risk_level": "medium",
        "is_scam": True,
        "confidence": 0.85
      },
      "Recommendation": "Block this site"
    },
    "Indicators": [...],
    "ProtectiveActions": [...]
  }
}
```

**Subscription:**
```python
# Subscribe to device-specific topic
topic = f"device:{device_uid}"
socket.subscribe(topic.encode('utf-8'))
```

**Heartbeat:**
- 5-second recv timeout (prevents blocking)
- Heartbeat printed every ~2 minutes (24 * 5s) to show listening
- Background thread listens continuously

**Notification Handling:**
- Parsed and logged to console with full details
- Forwarded to `handlers/notification_handler.py`
- Broadcasts to extension via WebSocket
- Caches result locally

**Files:**
- `notification_client.py` - SUB client
- `handlers/notification_handler.py` - Processes notifications
- `services/scan_service.py` - Matches pending URLs

## Component 3: ASPSBackend (C#)

**Location:** `/c/Users/pc/Desktop/asps/ASPSBackend14_J/`

**Technology:** C# .NET 8.0 with NetMQ, Entity Framework Core, MySQL

**Entry Points:**
- `ASPSBackend/Program.cs` - Console application
- `Business/Messaging/RealTimeAlertListener.cs` - ZMQ listener
- `Business/Messaging/NotificationPublisher.cs` - ZMQ publisher
- `Business/Messaging/CQRSGateway.cs` - CQRS gateway for WebApi

### Incoming: Desktop/Python → Backend (NetMQ REP)

**Protocol:** NetMQ REP socket (ResponseSocket)
**Port:** 50001 (configurable via `NetMQ:RealTimeListenerPort`)
**Mode:** REP (two-way communication)
**Encryption:** CurveZMQ (enabled via `Security:CurveEnabled`)
**Implementation:** `Business/Messaging/RealTimeAlertListener.cs`

**Server Configuration:**
```json
{
  "NetMQ": {
    "RealTimeListenerPort": 50001,
    "RealTimeListenerMode": "Rep"
  },
  "Security": {
    "CurveEnabled": true,
    "ServerPublicKeyZ85": "qPsk#8DY:n9ovp[vQ!YcOnOX[f/.i@.g^f#b:!ik"
  }
}
```

**Message Router:**
- Checks `MessageType` field to route
- `RequestToken` → HandleRequestToken
- `RegisterDevice` → HandleRegisterDevice
- `RefreshToken` → HandleRefreshToken
- No `MessageType` → ProcessAlertAsync (UrlAlert or RemoteAccessAlert)

**Token Management:**
- Tokens stored in `TokenStore` (in-memory)
- Default expiration: 1440 minutes (24 hours)
- Token validation required for all alerts
- Invalid/expired token → requires refresh or re-authentication

**Alert Processing:**
1. Validate token
2. Look up device in ASView (in-memory cache)
3. Look up user for device
4. Deserialize alert (UrlAlert or RemoteAccessAlert)
5. Create domain event: `DeviceAlertReceived`
6. Route to `UDAnalysisManager` for user
7. Persist to database via `AlertPersistenceActor`
8. Send immediate response to client
9. Analyze async (results sent via PUB/SUB later)

**Files:**
- `Business/Messaging/RealTimeAlertListener.cs` - REP listener
- `Business/Services/TokenStore.cs` - Token management
- `Business/Services/CurveKeyManager.cs` - CURVE key management
- `Business/Views/ASView.cs` - In-memory cache of users/devices

### Outgoing: Backend → Desktop/Python (NetMQ PUB)

**Protocol:** NetMQ PUB socket (PublisherSocket)
**Port:** 50002 (configurable via `NetMQ:NotificationPublisherPort`)
**Encryption:** CurveZMQ (enabled via `Security:CurveEnabled`)
**Implementation:** `Business/Messaging/NotificationPublisher.cs`

**Topic Format:**
- Device-specific: `device:{deviceUid}` (e.g., `device:PC-JOHN-001`)
- User-specific: `user:{userKeyField}` (broadcasts to all user devices)

**Publishing:**
```csharp
// Multipart message
publisherSocket.SendMoreFrame(topic).SendFrame(json);
```

**Notification Trigger:**
- `NotificationPublisherActor` subscribes to `AnalysisResultProduced` events
- Triggered when analysis completes (via UDAnalysisManager)
- Serialized with `TypeNameHandling.Auto` for polymorphism

**Files:**
- `Business/Messaging/NotificationPublisher.cs` - PUB publisher
- `Business/Messaging/NotificationPublisherActor.cs` - Event handler
- `Business/RealtimeAnalysis/UserDomain/UDAnalysisManager.cs` - Analysis coordinator

### Incoming: WebApi → Backend (NetMQ REP)

**Protocol:** NetMQ REP sockets
**Ports:**
- 5555 - Legacy business endpoint
- 5556 - CQRS Gateway (Commands/Queries)
**Pattern:** Request-Response (synchronous)
**Encryption:** None
**Implementation:** `Business/Messaging/CQRSGateway.cs`

**Purpose:** WebApi has ZERO database access - all operations via NetMQ

**CQRS Messages:**
- Commands: CreateUser, UpdateUser, RegisterDevice, etc.
- Queries: GetUsers, GetDevices, GetAnalysisResults, etc.

**Flow:**
1. WebApi receives HTTP request
2. WebApi creates Command/Query object
3. WebApi sends via `CQRSClient` (NetMQ REQ socket) to port 5556
4. Backend `CQRSGateway` receives
5. Backend routes to appropriate handler
6. Backend executes against database
7. Backend returns result
8. WebApi returns HTTP response

**Files:**
- `Business/Messaging/CQRSGateway.cs` - Gateway listener (port 5556)
- `Business/Messaging/NetMQMessageProcessor.cs` - Legacy processor (port 5555)
- `WebApi/Services/CQRSClient.cs` - WebApi client

## Component 4: WebApi (C# ASP.NET)

**Location:** `/c/Users/pc/Desktop/asps/ASPSBackend14_J/WebApi/`

**Technology:** ASP.NET Core 8.0, SignalR

**Entry Points:**
- `Program.cs` - ASP.NET application
- `Controllers/*Controller.cs` - REST API endpoints
- `Hubs/NotificationsHub.cs` - SignalR hub
- `Pages/**/*.cshtml.cs` - Razor Pages for admin UI

### Incoming: Browser → WebApi (HTTP REST)

**Protocol:** HTTP/HTTPS
**Ports:**
- 5001 - HTTP (development)
- 7001 - HTTPS (production)
**Base URLs:**
- Dev: `http://localhost:5001`
- Prod: `http://100.88.78.75:5001`

**REST Endpoints:**

- `GET /api/users` - List users
- `POST /api/users` - Create user
- `GET /api/users/{id}` - Get user details
- `GET /api/userdevices` - List devices
- `POST /api/userdevices` - Register device
- Admin Dashboard: `/` (Razor Pages)
- Device Login: `/DeviceLogin` (QR code registration)

**Architecture Note:**
WebApi has ZERO direct database access. All data operations go through NetMQ to ASPSBackend.

**Files:**
- `Controllers/UsersController.cs`
- `Controllers/UserDevicesController.cs`
- `Pages/**/*.cshtml.cs`

### Outgoing: WebApi → Browser (SignalR)

**Protocol:** SignalR (WebSocket + fallbacks)
**Port:** 49569
**Endpoint:** `/notificationshub`
**Implementation:** `Hubs/NotificationsHub.cs`

**Purpose:** Real-time notifications to admin dashboard

**Client Connection:**
```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5001/notificationshub")
  .build();
```

**Messages:**
- Analysis results
- Alert notifications
- System status updates

**Files:**
- `Hubs/NotificationsHub.cs`

## Component 5: Python Clients

**Location:** `/c/Users/pc/Desktop/asps/python_clients/`

**Purpose:** Testing and standalone alert submission

**File:** `python-client-with-notifications.py`

### Usage: Direct Backend Communication

**ZMQ REQ/REP (port 50001):**
- Sends UrlAlert or RemoteAccessAlert
- Same message format as desktop app
- No CurveZMQ encryption

**ZMQ PUB/SUB (port 50002):**
- Subscribes to `device:{deviceUid}` topic
- Listens for analysis results
- Prints full notification details

**Example Flow:**
```python
# 1. Start notification listener
listener = NotificationListener(device_uid, port=50002)
listener.start()

# 2. Send alert via REQ socket
context = zmq.Context()
socket = context.socket(zmq.REQ)
socket.connect("tcp://localhost:50001")
socket.send(json.dumps(alert).encode('utf-8'))
response = socket.recv()

# 3. Wait for notification on SUB socket
# (handled by listener thread)
```

## Data Flow Diagrams

### URL Analysis Flow (Complete)

```
Chrome Extension (User visits URL)
    │
    │ WebSocket: url_check
    │ {url, trackers, iframes}
    ↓
Desktop App (Python)
    │
    │ Check local cache
    │ ↓ (miss)
    │
    │ ZMQ REQ/REP (port 50001)
    │ UrlAlert {DeviceInfo, Url, Token, ...}
    ↓
ASPSBackend (C#)
    │
    │ 1. Validate token
    │ 2. Look up device/user
    │ 3. Persist alert to DB
    │ 4. Send immediate response: {"success": true, "message": "Alert processed"}
    │ 5. Route to UDAnalysisManager
    │ 6. Async analysis (Python analyzers)
    │
    │ (Desktop receives response, tells extension "analyzing=true")
    │
    │ 7. Analysis completes
    │ 8. Publish to NotificationPublisher
    │
    │ ZMQ PUB/SUB (port 50002)
    │ Topic: device:PC-JOHN-001
    │ {Data: {AnalysisResult: {risk_score, ...}}}
    ↓
Desktop App (Python)
    │
    │ 1. Receive notification
    │ 2. Match to pending URL
    │ 3. Cache result
    │ 4. Forward to extension
    │
    │ WebSocket: url_result
    │ {score, riskType, protectiveAction, analyzing=false}
    ↓
Chrome Extension
    │
    │ 1. Update icon color
    │ 2. Execute protective action
    │ 3. Show warning if needed
    ↓
User (sees result)
```

### Remote Access Detection Flow

```
Desktop App (Background monitor detects AnyDesk)
    │
    │ 1. Process detection
    │ 2. GeoIP lookup
    │ 3. Direction analysis
    │
    │ ZMQ REQ/REP (port 50001)
    │ RemoteAccessAlert {RemoteAccessApp: 1, Direction: "incoming", ...}
    ↓
ASPSBackend (C#)
    │
    │ 1. Validate token
    │ 2. Persist alert
    │ 3. Analyze (RemoteAccessIndicator)
    │ 4. Send response
    │
    │ ZMQ PUB/SUB (port 50002)
    │ Notification with analysis
    ↓
Desktop App (Python)
    │
    │ 1. Receive notification
    │ 2. Update tray icon (turn red)
    │ 3. Broadcast to extensions
    │
    │ WebSocket: remote_access_alert
    │ {toolId: 1, direction: "incoming", remote_country: "US"}
    ↓
Chrome Extension
    │
    │ 1. Inject warning into ALL tabs
    │ 2. Show full-screen modal
    │ 3. Buttons: "I initiated this", "Close session", "Continue anyway"
    ↓
User (responds to warning)
```

### Authentication Flow

```
Desktop App (First run)
    │
    │ ZMQ REQ/REP (port 50001)
    │ {"MessageType": "RequestToken", "DeviceUid": "PC-JOHN-001"}
    ↓
ASPSBackend
    │
    │ 1. Look up device in ASView
    │ 2. Device not found → return DeviceNotRecognized
    │
    │ Response: {"status": "DeviceNotRecognized"}
    ↓
Desktop App
    │
    │ Opens browser to device registration page
    │
    │ HTTP GET
    │ http://localhost:5001/DeviceLogin?uid=PC-JOHN-001
    ↓
WebApi (Razor Page)
    │
    │ Shows QR code and registration form
    ↓
User
    │
    │ Scans QR or enters email
    │
    │ HTTP POST /DeviceLogin
    │ {DeviceUid: "PC-JOHN-001", Email: "user@example.com"}
    ↓
WebApi
    │
    │ NetMQ REQ (port 5556)
    │ RegisterDeviceCommand
    ↓
ASPSBackend
    │
    │ 1. Look up user by email
    │ 2. Create device entity
    │ 3. Save to database
    │ 4. Add to ASView cache
    │
    │ Response: {"success": true}
    ↓
WebApi
    │
    │ HTTP 200 OK
    │ "Device registered successfully"
    ↓
Desktop App (Retry RequestToken)
    │
    │ ZMQ REQ/REP (port 50001)
    │ {"MessageType": "RequestToken", "DeviceUid": "PC-JOHN-001"}
    ↓
ASPSBackend
    │
    │ 1. Device found in ASView
    │ 2. Create token (24-hour expiration)
    │ 3. Store in TokenStore
    │
    │ Response: {"status": "TokenCreated", "token": "eyJ...", "serverPublicKey": "..."}
    ↓
Desktop App
    │
    │ 1. Save token to local storage
    │ 2. Apply CURVE server public key
    │ 3. Start notification listener
    │ 4. Ready for alerts
```

## Port Summary

| Port | Protocol | Direction | Encryption | Purpose |
|------|----------|-----------|------------|---------|
| 8080-8484 | WebSocket | Extension ↔ Desktop | None | Bidirectional messaging |
| 50001 | ZeroMQ REQ/REP | Desktop → Backend | CurveZMQ | Alert submission + auth |
| 50002 | ZeroMQ PUB/SUB | Backend → Desktop | CurveZMQ | Analysis notifications |
| 5555 | NetMQ REP | WebApi → Backend | None | Legacy CQRS |
| 5556 | NetMQ REP | WebApi → Backend | None | CQRS Gateway |
| 5001 | HTTP | Browser → WebApi | None (dev) | REST API + Admin UI |
| 7001 | HTTPS | Browser → WebApi | TLS | REST API + Admin UI (prod) |
| 49569 | SignalR | WebApi → Browser | None | Real-time notifications |

## Security & Encryption

### CurveZMQ Implementation (Phase 4)

**Status:** Enabled in production

**Keys:**
- Server public key (Z85): `qPsk#8DY:n9ovp[vQ!YcOnOX[f/.i@.g^f#b:!ik`
- Server secret key: Stored in `appsettings.json` (Security:ServerSecretKey)
- Client keypair: Generated per-connection

**Encrypted Channels:**
- Desktop → Backend (port 50001): REQ/REP with CURVE
- Backend → Desktop (port 50002): PUB/SUB with CURVE

**Unencrypted Channels:**
- Extension ↔ Desktop (WebSocket): Local only, no encryption
- WebApi ↔ Backend (NetMQ): Internal process communication
- Browser ↔ WebApi: HTTPS in production, HTTP in development

**Key Exchange:**
- Server public key sent in `RequestToken` response
- Desktop stores and applies to all subsequent connections
- Client generates ephemeral keypair per connection

**Configuration:**
```json
{
  "Security": {
    "CurveEnabled": true,
    "ServerPublicKey": "UsaAZRpYTlYc5QPyYmLsr5xvCOMxOThZwYV56CSY9A0=",
    "ServerSecretKey": "vmio3xwetlTUsHEaa9AVpgDO03wnTCIkTvBQm3rjz+w=",
    "ServerPublicKeyZ85": "qPsk#8DY:n9ovp[vQ!YcOnOX[f/.i@.g^f#b:!ik"
  }
}
```

**Files:**
- `Business/Services/CurveKeyManager.cs` - Key management
- `zmq_client.py` - Client-side CURVE setup
- `notification_client.py` - SUB client CURVE setup

## Message Formats

### Standard Alert Structure

All alerts (URL and RemoteAccess) share this base structure:

```json
{
  "AlertType": "UrlAlert" | "RemoteAccessAlert",
  "DeviceInfo": {
    "DeviceUid": "PC-JOHN-001",
    "DeviceType": 1,
    "OperatingSystem": 1,
    "MAC": "00:11:22:33:44:55"
  },
  "Timestamp": "2026-02-13T12:00:00Z",
  "Priority": 1,
  "Token": "eyJ...",
  // Alert-specific fields below
}
```

### Enums Reference

**DeviceType:**
- 1 = PersonalComputer
- 2 = Mobile
- 3 = Tablet

**OperatingSystem:**
- 1 = Windows
- 2 = Linux
- 3 = Mac
- 4 = Android
- 5 = iOS

**Priority:**
- 0 = Low
- 1 = Medium
- 2 = High
- 3 = Critical

**RemoteAccessApp:**
- 1 = AnyDesk
- 2 = TeamViewer
- 3 = ChromeRemoteDesktop
- 4 = RemotePC
- 5 = LogMeIn
- 6 = Splashtop
- 7 = VNC
- 8 = RDP
- 9 = QuickAssist
- 10 = ConnectWise

**ConnectionStatus / SessionStatus:**
- 0 = Unknown
- 1 = Open
- 2 = Closed

**ProtectiveAction:**
- 0 = None
- 1 = Ignore
- 2 = WarnOnScreen
- 3 = ModalPopup
- 4 = Block

**RiskType (bit flags):**
- 0 = None
- 1 = Phishing
- 2 = Cloaking
- 3 = Impersonation
- 4 = FakeDom
- 5 = Unknown

## Connection Reliability

### Desktop → Backend (ZMQ)

**Current State (Phase 4):**
- New socket per request (no connection pooling)
- 5-second timeout on send/receive
- Immediate retry if token expired (after refresh)
- No retry on network errors (connection fails)

**Planned (Phase 5):**
- Lazy Pirate pattern: Automatic retry with timeout
- Connection pooling for frequent requests
- Exponential backoff on errors

### Extension → Desktop (WebSocket)

**Current State (Phase 4):**
- Automatic reconnection with exponential backoff
- Message queue survives service worker restarts
- Heartbeat dead connection detection (3 missed = dead)
- Keepalive prevents service worker termination
- Badge color shows connection state

**Reliability Features:**
- Immediate first retry (0s delay)
- Exponential backoff: 1s → 2s → 4s → 8s → 16s → 30s (max)
- Message queue in session storage (survives SW restart)
- Flush queue on reconnect

### Backend → Desktop (ZMQ PUB/SUB)

**Current State:**
- Background thread listens continuously
- 5-second recv timeout (prevents blocking)
- Heartbeat logging every ~2 minutes
- No automatic reconnection on failure

**Reliability:**
- Subscriber connects once on startup
- If connection lost, restart required
- PUB/SUB is fire-and-forget (no ACK)

## Communication Endpoints Reference

### Desktop App Configuration

**File:** `apps/desktop/win/src/config.py`

```python
# Extension WebSocket
EXTENSION_PORTS = [8080, 8181, 8282, 8383, 8484]

# Backend ZMQ
BACKEND_HOST = "127.0.0.1"  # or "100.88.78.75" for production
BACKEND_REQ_PORT = 50001
BACKEND_SUB_PORT = 50002

# CURVE server public key (Z85)
BACKEND_SERVER_PUBLIC_KEY_Z85 = "qPsk#8DY:n9ovp[vQ!YcOnOX[f/.i@.g^f#b:!ik"

# WebApi URL (for device registration)
WEBAPI_URL = "http://localhost:5001"  # or "http://100.88.78.75:5001"
```

### Backend Configuration

**File:** `ASPSBackend14_J/ASPSBackend/appsettings.json`

```json
{
  "NetMQ": {
    "BusinessEndpoint": "tcp://*:5555",
    "RealTimeListenerPort": 50001,
    "RealTimeListenerMode": "Rep",
    "NotificationPublisherPort": 50002
  },
  "Security": {
    "CurveEnabled": true,
    "ServerPublicKeyZ85": "qPsk#8DY:n9ovp[vQ!YcOnOX[f/.i@.g^f#b:!ik"
  }
}
```

### Extension Configuration

**File:** `apps/extension/chrome/services/ConnectionService.js`

```javascript
config: {
  ports: [8080, 8181, 8282, 8383, 8484],
  reconnectDelay: 5000,
  maxReconnectAttempts: 10,
  pingInterval: 30000,
  connectionTimeout: 2000,
  keepaliveInterval: 20000,
  heartbeatInterval: 10000,
  maxMissedHeartbeats: 3
}
```

## Active vs Legacy Code

### Active Communication Paths

**In Production:**
1. Extension → Desktop: WebSocket (ports 8080-8484)
2. Desktop → Backend: ZMQ REQ/REP (port 50001) with CurveZMQ
3. Backend → Desktop: ZMQ PUB/SUB (port 50002) with CurveZMQ
4. WebApi → Backend: NetMQ REP (port 5556)
5. Browser → WebApi: HTTP REST (port 5001)

### Dead Code / Legacy

**NOT USED:**
- `tcp_client.py` - Old TCP client (replaced by zmq_client.py)
- `signalr_client.py` - Old SignalR client (notifications moved to ZMQ PUB/SUB)
- NetMQ Business Endpoint (port 5555) - Legacy, replaced by CQRS Gateway (5556)

**Transitional:**
- Old synchronous alert responses (Score in response) - Being replaced by async (notification-based)
- REP socket mode can be changed to PULL (fire-and-forget) but currently REP

## Known Issues & Limitations

### Extension WebSocket

**Issue:** Service worker terminates after 30 seconds of inactivity
**Mitigation:** Keepalive messages every 20 seconds + alarm-based backup
**Impact:** Messages may queue during termination, flushed on restart

**Issue:** Chrome alarms have 30-second minimum in production
**Mitigation:** Immediate first retry, then alarm-based reconnect
**Impact:** Reconnection may be slower than ideal

### Desktop ZMQ

**Issue:** No connection pooling, new socket per request
**Impact:** Slower response times, connection overhead
**Planned:** Connection pooling in Phase 5

**Issue:** No automatic retry on network errors
**Impact:** Request fails immediately if backend unreachable
**Planned:** Lazy Pirate pattern in Phase 5

### Backend PUB/SUB

**Issue:** No delivery confirmation (fire-and-forget)
**Impact:** Notifications may be lost if client disconnected
**Mitigation:** Desktop queues messages during disconnection

**Issue:** Topic filtering happens at subscriber (all messages sent)
**Impact:** Bandwidth wasted if many devices
**Mitigation:** CurveZMQ encryption reduces exposure

## Testing & Diagnostics

### Diagnostic Tools

**Desktop App:**
- `diag_zmq_test.py` - Test ZMQ REQ/REP connection
- `diag_ws_test.py` - Test WebSocket server
- `curve_diagnostic.py` - Test CurveZMQ encryption

**Python Clients:**
- `python-client-with-notifications.py` - Full alert submission + notification listener

**Extension:**
- Popup UI shows connection status
- Badge color indicates health
- Console logs all messages

### Manual Testing

**Test URL Analysis:**
1. Open Chrome Extension
2. Visit any URL
3. Check extension icon color
4. Open popup to see score
5. Desktop app logs should show ZMQ REQ/REP
6. Backend logs should show alert received
7. Desktop notification listener logs result
8. Extension receives final result

**Test Remote Access Detection:**
1. Run Desktop App
2. Start AnyDesk
3. Desktop should detect and send RemoteAccessAlert
4. Backend processes alert
5. Desktop receives notification
6. Extension shows full-screen warning on all tabs

**Test Authentication:**
1. Delete Desktop App token file
2. Restart Desktop App
3. Should open browser to registration page
4. Register device
5. Desktop should receive token
6. Should start notification listener

---

*Communication flow analysis: 2026-02-13*
