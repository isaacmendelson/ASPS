# ASPS - Anti-Scam Protection System
## Complete Data Flow Documentation

---

# 1. Overview

ASPS (Anti-Scam Protection System) is a real-time fraud detection system that monitors user browsing activity and remote access applications to identify and prevent scam attempts. The system consists of three main components:

1. **Chrome Extension** - Monitors browsing activity
2. **Desktop Agent** (Python/Windows) - Bridge between extension and backend
3. **Backend Server** (.NET) - Analysis engine and persistence

---

# 2. Component Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           USER'S DEVICE                                  │
│  ┌─────────────────┐    WebSocket    ┌─────────────────────────────┐   │
│  │ Chrome Extension │◄──────────────►│    Desktop Agent (Python)    │   │
│  │                  │ Ports 8080-8484│                               │   │
│  │  - background.js │ (first free)   │  - extension_handler.py       │   │
│  │  - ScanService   │                │  - scan_service.py            │   │
│  │  - popup.js      │                │  - notification_handler.py    │   │
│  └─────────────────┘                 │  - zmq_client.py              │   │
│                                      └──────────────┬────────────────┘   │
└──────────────────────────────────────────────────────┼───────────────────┘
                                                       │
                                  ZMQ Port 50001 (Desktop REQ → Backend ROUTER)
                                  ZMQ Port 50002 (PUB/SUB, per-device topic)
                                  Both encrypted with CurveZMQ
                                                       │
┌──────────────────────────────────────────────────────┼───────────────────┐
│                         BACKEND SERVER (.NET)        │                   │
│  ┌───────────────────────────────────────────────────▼─────────────────┐ │
│  │         NetMQAlertIngress (IAlertIngress, IHostedService)             │ │
│  │        (ZMQ ROUTER Socket - handles concurrent connections)          │ │
│  │        delegates message parsing/routing to AlertProcessor           │ │
│  └────────────────────────────────┬────────────────────────────────────┘ │
│                                   │                                      │
│                      ┌────────────▼────────────┐                        │
│                      │   DomainEventPublisher   │                        │
│                      └────────────┬────────────┘                        │
│                                   │                                      │
│         ┌─────────────────────────┼─────────────────────────┐           │
│         │                         │                         │           │
│         ▼                         ▼                         ▼           │
│  ┌─────────────┐         ┌───────────────┐         ┌──────────────┐    │
│  │AlertPersist-│         │   ASView      │         │UDAnalysis    │    │
│  │enceActor    │         │(In-Memory DB) │         │Manager       │    │
│  └─────────────┘         └───────────────┘         └──────────────┘    │
│         │                         │                         │           │
│         ▼                         │                         ▼           │
│  ┌─────────────┐                  │                 ┌──────────────┐    │
│  │  Database   │                  │                 │ UDUserAnalyzer│   │
│  │  (MySQL)    │                  │                 │ (Fraud Logic) │   │
│  └─────────────┘                  │                 └──────────────┘    │
│                                   │                                      │
│                      ┌────────────▼────────────┐                        │
│                      │ NetMQNotificationEgress   │                        │
│                      │ (INotificationEgress)     │                        │
│                      │  (ZMQ PUB Socket)        │                        │
│                      └─────────────────────────┘                        │
└──────────────────────────────────────────────────────────────────────────┘
```

---

# 3. Detailed Data Flow

## 3.1 URL Detection & Initial Request

### Step 1: Extension Detects URL
**Location:** `apps/extension/chrome/background.js`

When a user navigates to a new page:

```javascript
// Tab update listener
chrome.tabs.onUpdated.addListener((tabId, changeInfo, tab) => {
  if (changeInfo.url && tab.active) {
    scanService.scan(tabId, changeInfo.url);
  }
});
```

### Step 2: ScanService Processes URL
**Location:** `apps/extension/chrome/services/ScanService.js`

```javascript
async scan(tabId, url) {
  // 1. Skip local URLs (localhost, 127.0.0.1)
  if (this.isLocalUrl(url)) return null;
  
  // 2. Check cache first
  const cached = cacheService.get(url);
  if (cached) {
    this.handleResult(cached, true);
    return cached;
  }
  
  // 3. Collect page info (trackers, iframes)
  const pageInfo = await this.getPageInfo(tabId);
  
  // 4. Send to Desktop Agent via WebSocket
  const message = {
    type: 'url_check',
    url: url,
    trackers: pageInfo?.trackers || [],
    iframes: pageInfo?.iframes || [],
    ipAddress: connectionService.getDeviceIpAddress(),
    tabId: tabId.toString()
  };
  
  connectionService.send(message);
}
```

---

## 3.2 Desktop Agent Processing

### Step 3: Extension Handler Receives Message
**Location:** `apps/desktop/win/src/handlers/extension_handler.py`

```python
async def handle_message(self, data: Dict[str, Any]) -> Optional[Dict[str, Any]]:
    msg_type = data.get('type', '')
    
    handlers = {
        'url_check': self._handle_url_check,
        'track_url_alert': self._handle_track_url_alert,
        'ping': self._handle_ping,
        # ...
    }
    
    return handler(data)

def _handle_url_check(self, data: Dict[str, Any]) -> Dict[str, Any]:
    return self.scan_service.check_url(
        url=data.get('url'),
        trackers=data.get('trackers', []),
        iframes=data.get('iframes', []),
        ip_address=data.get('ipAddress'),
        tab_id=data.get('tabId')
    )
```

### Step 4: ScanService Sends to Backend
**Location:** `apps/desktop/win/src/services/scan_service.py`

```python
def check_url(self, url, trackers, iframes, ip_address, tab_id):
    # 1. Check local cache
    cached = self.cache.get(url)
    if cached:
        return self._create_result(url, cached.score, cached.risk_type, cached=True)
    
    # 2. Verify authentication
    if not self.auth_manager.is_valid():
        self.auth_manager.authenticate()
    
    # 3. Send to backend via ZMQ
    response = self.zmq_client.send_url_alert(
        device_uid=self.device_id,
        url=url,
        token=self.auth_manager.get_token(),
        trackers=trackers,
        iframes=iframes,
        ip_address=ip_address,
        tab_id=tab_id
    )
    
    # 4. Return acknowledgment (actual result comes via notification)
    return {"success": True, "message": "Analysis in progress"}
```

### Step 5: ZMQ Client Sends Alert
**Location:** `apps/desktop/win/src/zmq_client.py`

```python
def send_url_alert(self, device_uid, url, token, trackers, iframes, ip_address, tab_id):
    alert = {
        "AlertType": "UrlAlert",
        "DeviceInfo": {
            "DeviceUid": device_uid,
            "DeviceType": 1,  # PersonalComputer
            "OperatingSystem": 1,  # Windows
        },
        "Timestamp": datetime.utcnow().isoformat(),
        "Token": token,
        "Url": url,
        "Trackers": trackers,
        "IFrameDomains": iframes,
        "IPAddress": ip_address,
        "TabId": tab_id
    }
    
    # Send via ZMQ REQ socket to port 50001
    return self._send_and_receive(alert)
```

---

## 3.3 Backend Processing

### Step 6: NetMQAlertIngress Receives Alert, AlertProcessor Routes It
**Location:** `Business/Messaging/NetMQAlertIngress.cs` (socket lifecycle) + `Business/Messaging/AlertProcessor.cs` (parsing/routing, extracted in Phase 3 of the ASPS-675 messaging refactoring)

```csharp
// NetMQAlertIngress.cs — socket-level wrapper
private async Task ProcessRouterMessageAsync(byte[] identity, byte[] messageBytes)
{
    var message = Encoding.UTF8.GetString(messageBytes);
    var result = await _alertProcessor.RouteMessageAsync(message);
    SendRouterResponse(identity, result);
}

// AlertProcessor.cs — message-type routing + token validation
internal Task<object> ProcessAlertAsync(string message, JObject jObject)
{
    // 1. Deserialize alert
    var alert = JsonConvert.DeserializeObject<UrlAlert>(message);
    
    // 2. Validate token
    var tokenValidation = _tokenStore.ValidateToken(deviceUid, alert.Token);
    if (tokenValidation != TokenValidationResult.Valid)
        return Task.FromResult(new { status = "InvalidToken" });
    
    // 3. Find user device
    var userDevice = _asView.FindUserDeviceByDeviceUid(deviceUid);
    var user = _asView.FindUserByKey(userDevice.UserKey);
    
    // 4. Create domain event
    var domainEvent = new DeviceAlertReceived(alert, ...);
    
    // 5. ACK immediately, process in background
    _ = Task.Run(() => DispatchAlertInBackground(domainEvent, userDevice.UserKey, deviceUid));
    
    return Task.FromResult(new {
        success = true,
        message = "Alert accepted — analysis in progress"
    });
}
```

### Step 7: Background Analysis Dispatch
**Location:** `Business/Messaging/AlertProcessor.cs`

```csharp
private async Task DispatchAlertInBackground(DeviceAlertReceived domainEvent, Key userKey, string deviceUid)
{
    // 1. Publish to all registered handlers
    _domainEventPublisher.Register(domainEvent);
    _domainEventPublisher.RaiseAll();
    
    // 2. Get or create analysis manager for user
    var userManager = _userDomainService.GetOrCreateManagerForUser(userKey);
    
    // 3. Handle the event
    userManager.Handle(domainEvent);
}
```

---

## 3.4 Domain Events & Handlers

### Event: DeviceAlertReceived

**Registered Handlers:**
1. `AlertPersistenceActor` - Saves alert to database
2. `ASView` - Updates in-memory cache
3. `UDAnalysisManager` - Triggers analysis

### Step 8: UDAnalysisManager Handles Alert
**Location:** `Business/RealtimeAnalysis/UserDomain/UDAnalysisManager.cs`

```csharp
public async Task Handle(IDomainEvent evt)
{
    switch (evt)
    {
        case DeviceAlertReceived alertEvent:
            await HandleDeviceAlertReceived(alertEvent);
            break;
        case AnalysisResultAdded analysisResultEvent:
            await HandleAnalysisResultAdded(analysisResultEvent);
            break;
    }
}
```

### Step 9: UDAnalysis Runs Analyzers
**Location:** `Business/RealtimeAnalysis/UserDomain/UDAnalysis.cs`

The system runs multiple specialized analyzers:

1. **UDUrlAnalyzer** - Phishing detection, ML-based risk scoring
2. **UDTrackUrlAnalyzer** - Tracks risky URLs over time
3. **UDRemoteAccessAnalyzer** - Monitors remote access applications
4. **UDPhishingAnalyzer** - Known phishing database lookup

```csharp
// Analyzers are called in sequence
foreach (var analyzer in _analyzers)
{
    if (analyzer.CanAnalyze(alert))
    {
        var result = await analyzer.AnalyzeAsync(alert);
        results[analyzer.Name] = result;
    }
}
```

### Step 10: External Python Analyzer (ML)
**Location:** `Analyzers/basic-url-analyzer/`

For URL analysis, the backend invokes the Python analyzer **as a subprocess** (not HTTP):

```csharp
// Business/RealtimeAnalysis/UserDomain/UDUrlAnalyzer.cs
var psi = new ProcessStartInfo {
    FileName = pythonExePath,
    Arguments = $"\"{scriptPath}\" \"{url}\" --json",
    UseShellExecute = false,
    RedirectStandardOutput = true,
    CreateNoWindow = true
};
// Parse JSON from stdout; 30s timeout.
```

The Python analyzer performs:
- **WHOIS lookup** (domain age, registrar, country, privacy)
- **Content scrape** (patterns, trackers, forms, urgency language)
- **ML Risk Scoring** (phishing probability via scikit-learn)
- **Risk Assessor** (weighted aggregation → final `risk_score` + `risk_level`)

The subprocess returns a JSON object on stdout that the Backend parses and stores as the `AnalysisResults.JsonValue` blob. See `ARCHITECTURE.md §7` for the full output schema.

---

## 3.5 Analysis Result Events

### Event: AnalysisResultReceived
Fired when an analyzer completes.

### Event: AnalysisResultAdded
Fired after the result is persisted to database.

### Step 11: UDUserAnalyzer - User-Level Analysis
**Location:** `Business/RealtimeAnalysis/UserDomain/UDUserAnalyzer.cs`

```csharp
public async Task Handle(IDomainEvent evt)
{
    switch(evt)
    {
        case AnalysisResultAdded analysisEvent:
            await HandleAnalysisResultAddedAsync(analysisEvent);
            break;
    }
    
    // Always check for immediate danger
    var isImmediateDanger = DetectImmediateDanger();
}
```

### Step 12: Immediate Danger Detection
**Location:** `Business/RealtimeAnalysis/UserDomain/UDUserAnalyzer.cs`

```csharp
private bool DetectImmediateDanger()
{
    // Check if user has active remote access session
    var activeRemoteAccess = _remoteAccessStatus
        .Where(i => i.isRemoteAccessSessionActive && 
                    i.RemoteAccessDirection == RemoteAccessDirection.In);
    
    // Check if user is browsing sensitive websites
    foreach (var deviceUid in userDevices)
    {
        if (activeRemoteAccess.Contains(deviceUid))
        {
            if (BrowserTabs[deviceUid].Any(tab => IsSensitiveWebsite(tab.Url)))
            {
                // IMMEDIATE DANGER: Remote access active + sensitive site
                _immediateDangers.Add(new ImmediateDanger(...));
                return true;
            }
        }
    }
    return false;
}

private bool IsSensitiveWebsite(string url)
{
    // Sensitive categories: banking, crypto_exchange, healthcare
    var category = GetWebsiteCategory(url);
    return sensitiveCategories.Contains(category);
}
```

---

## 3.6 Notification Flow

### Step 13: NetMQNotificationEgress Sends Result
**Location:** `Business/Messaging/NetMQNotificationEgress.cs` (implements `INotificationEgress`; renamed from `NotificationPublisher.cs` in Phase 2 of the ASPS-675 messaging refactoring)

```csharp
public void PublishAnalysisResult(string deviceUid, string userKeyField, 
    AnalysisResultNotification notification)
{
    var json = JsonConvert.SerializeObject(new {
        Type = "AnalysisResult",
        Timestamp = DateTime.UtcNow,
        DeviceUid = deviceUid,
        Data = notification
    });
    
    // Publish to ZMQ PUB socket (port 50002)
    var topic = $"device:{deviceUid}";
    _publisherSocket.SendMoreFrame(topic).SendFrame(json);
}
```

### Step 14: Agent Receives Notification
**Location:** `apps/desktop/win/src/handlers/notification_handler.py`

```python
def handle(self, notification: Dict[str, Any]):
    data = notification.get('Data', {})
    
    # Extract analysis result
    analysis = self._extract_analysis(data)
    
    # Execute protective actions
    protective_actions = data.get('ProtectiveActions', [])
    self.protection_service.execute_actions(
        protective_actions,
        analysis['url'],
        data
    )
    
    # Update local cache
    cache_data = self._update_cache(analysis, protective_actions)
    
    # Broadcast to extension
    await self._broadcast_to_extension(analysis, cache_data, protective_actions)
```

### Step 15: Extension Receives Result
**Location:** `apps/extension/chrome/services/ScanService.js`

```javascript
handleResult(data, fromCache = false) {
    // Extract server values
    const riskScore = data.score;
    const riskTypes = data.riskType || [];
    const protectiveAction = data.protectiveAction || 0;
    
    // Update UI
    stateManager.update({
        'scan.loading': false,
        'scan.result': { score: riskScore, riskType: riskTypes }
    });
    
    // Store in local storage for popup
    chrome.storage.local.set({
        currentPageScore: riskScore,
        currentPageRiskType: riskTypes,
        currentPageAction: protectiveAction
    });
}
```

---

# 4. Alert Types

## 4.1 UrlAlert
Triggered when user visits a URL.

```json
{
  "AlertType": "UrlAlert",
  "Url": "https://example.com",
  "Trackers": [...],
  "IFrameDomains": [...],
  "IPAddress": "192.168.1.100",
  "TabId": "123"
}
```

## 4.2 TrackUrlAlert
Triggered for extended time on risky URLs.

```json
{
  "AlertType": "TrackUrlAlert",
  "Url": "https://risky-site.com",
  "FromUrl": "https://original-site.com",
  "Duration": 300,
  "ScamInProgressKey": "...",
  "TabId": "123"
}
```

## 4.3 RemoteAccessAlert

Triggered when a remote-access app changes state (opened / closed / session_started / session_ended). Sampling cadence is adaptive: 5 s when an app is running, 30 s when idle, **2 s while the agent is in DangerMode** (between `ImmediateDangerNotification` and `ImmediateDangerEndedNotification`). Close / session-end events are debounced (1 s / 4 s) — the debounce is bypassed entirely while DangerMode is active.

Supported apps: AnyDesk, TeamViewer, ChromeRemoteDesktop, RustDesk (mapped to VNC id), VNC, RemotePC, Splashtop, RDP, QuickAssist, ConnectWise, LogMeIn.

```json
{
  "AlertType": "RemoteAccessAlert",
  "RemoteAccessApp": 1,           // enum value (AnyDesk=1, TeamViewer=2, …, RDP=8, QuickAssist=9, ConnectWise=10)
  "Software": "AnyDesk",
  "RunningProcesses": 3,
  "ConnectionUrl": "203.0.113.42",
  "ConnectionStatus": 1,          // Open=1, Closed=2
  "ConnectionsCount": 2,
  "SessionStatus": 1,             // Open=1, Closed=2

  // Direction (string on the wire — backend stores into Direction column)
  "Direction": "incoming",        // 'incoming' | 'outgoing' | 'unknown'
  "Confidence": "high",           // 'low' | 'medium' | 'high'
  "RemoteCountry": "Nigeria",
  "RemoteCountryCode": "NG",

  // Session forensics (populated when log/trace provides)
  "RemoteId":      "1458399339",  // AnyDesk numeric ID / TV Partner ID
  "RemoteName":    "DESKTOP-X1",
  "LoggedUser":    "isaac",        // local user logged on at session time
  "ConnectionId":  "abc-1234-…",   // GUID from TV Connections_incoming.txt
  "RemoteOS":      "Windows 11",
  "RemoteVersion": "8.0.13",
  "ConnectionType":"direct",       // 'direct' | 'relay'
  "FileTransferActive": false,
  "FileTransfers":     0,

  // Browser tabs — attached only when Direction == 'incoming' (default policy);
  // backend can override at runtime via SetBrowserTabsPolicyNotification.
  "BrowserTabs": [
    { "url": "https://bank.example.com/login", "title": "Sign in" }
  ]
}
```

The full payload is persisted to the `DeviceAlerts` table as a `RemoteAccessAlert` discriminator row — see [ARCHITECTURE.md §5.6](../ARCHITECTURE.md#56-database-schema-mysql) for the column list.

---

# 5. Protective Actions

Each `AnalysisResult` carries an array of `ProtectiveAction` items with `{ ActionType, ActionLevel }`. The Extension and Desktop Agent pick which ones to execute based on `ActionLevel` (`Device` / `User` / `Protector`).

Source: `Common/Enums/Enumerations.cs → ProtectiveActionType`

| Value | Name | Typical handler |
|-------|------|-----------------|
| 0 | None | — |
| 1 | DisplayNotification | Extension banner / Desktop toast |
| 2 | EmailNotification | Backend (SMTP) — sent to user / protector |
| 3 | SoundAlert | Desktop — OS-level sound |
| 4 | BlockUrl | Extension — replaces page with block screen |
| 5 | UserDisplayNotification | Extension — modal-style overlay in current tab |
| 6 | QuarantineDevice | Backend — flags device for admin review |
| 7 | BlockRemoteAccess | Desktop — terminates the active remote-access session |
| 8 | EnableUrlTracking | Backend — switches the URL into long-duration tracking |
| 9 | SetTrackMode | Backend — adjusts the tracking sensitivity for the flow |

---

# 6. Immediate Danger Scenarios

## 6.1 Triggers

The system detects "immediate danger" when:

1. **Remote Access + Sensitive Site** *(implemented)* — incoming remote-access session + open browser tab on a sensitive (banking / crypto) domain.
2. **Extended Risky Browsing** *(planned)* — user spends >5 min on a risky URL while remote access is active.
3. **Scam-in-Progress** *(planned)* — known scam pattern detected (e.g., tech-support scam flow).

## 6.2 End-to-end flow (RemoteAccess + sensitive site)

```
Agent: RemoteAccessAlert (Direction='incoming') + RemoteAccessAnalysisResult.SensitiveUrl
   │
Backend: UDUserAnalyzer.DetectImmediateDanger()
   │  (matches active session against open sensitive tabs in UDUser.BrowserTabs)
   ├──► raise ImmediateDangerDetected on UDUserAnalyzer publisher
   │       │
   │       └──► ImmediateDangerPersistanceActor (singleton)
   │              ├── INSERT into ImmediateDangers table
   │              └── BuildPerUserHandlers(includeSingletons=true).Raise(ImmediateDangerAdded)
   │                     ├── ASView.HandleImmediateDangerAdded   → cache update
   │                     ├── UDAnalysisManager.Handle            → delegates to UDUserAnalyzer
   │                     │     └── UDUserAnalyzer.HandleImmediateDangerAdded
   │                     │           └── raise ImmediateDangerEvent
   │                     │                  └── NotificationPublisherActor
   │                     │                        └── PublishImmediateDangerEvent
   │                     │                              └── AGENT (PUB:50002, topic device:{uid})
   │                     └── UDAnalysis.Handle                   → log only (no re-raise; prevents duplicate)
   │
   ├──► AGENT receives ImmediateDangerNotification
   │       ├── danger_mode.activate()                            → 2s polling + no debounce
   │       ├── ProtectionService.show_display_notification_actions()
   │       │     └── DisplayNotification ProtectiveActions → CenteredToast (locked, red)
   │       └── broadcast typed event 'immediate_danger_started' to Extension
   │
[…the user disconnects the remote session, or closes the sensitive tab…]
   │
Agent: next RemoteAccessAlert with Direction!='incoming' OR no sensitive tab
   │
Backend: UDUserAnalyzer.DetectImmediateDanger()
   ├──► raise ImmediateDangerEnded on UDUserAnalyzer publisher
   │       ├── ImmediateDangerPersistanceActor               → UPDATE EndTime = UtcNow
   │       │     └── BuildPerUserHandlers(includeSingletons=false).Raise(ImmediateDangerEnded)
   │       │            ├── UDAnalysisManager.Handle         → delegates clear-up to UDUserAnalyzer
   │       │            └── UDAnalysis.Handle                → log only
   │       └── NotificationPublisherActor (singleton, received via the original raise — NOT the re-publish)
   │             └── PublishImmediateDangerEnded
   │                   └── AGENT (PUB:50002)
   │
   └──► AGENT receives ImmediateDangerEndedNotification
           ├── danger_mode.deactivate()                       → revert to adaptive polling
           ├── ProtectionService.show_display_notification_actions(risk_level='none')
           │     └── CenteredToast.transform_to_cleared()     → same window: red→green, Close button added
           └── broadcast typed event 'immediate_danger_ended' to Extension
```

> **Why the asymmetry between Added and Ended re-publish?** `Added` is constructed inside `ImmediateDangerPersistanceActor` (no singleton has seen it yet — singletons must be included so ASView and others react). `Ended` is raised on `UDUserAnalyzer`'s publisher first, so all singletons (including `NotificationPublisherActor`) already received it; the re-publish only needs to reach the per-user handlers (`UDAnalysisManager` + `UDAnalysis`) which live outside DI. Including singletons twice would publish the agent notification twice.

## 6.3 BrowserTabsPolicy override flow

```
Backend admin/automation
  └── _notificationPublisher.PublishSetBrowserTabsPolicy(
          deviceUid, userKey, mode, validUntil)
        └── PUB:50002 → topic device:{uid} or user:{key}
              ↓
              AGENT NotificationHandler._handle_set_browser_tabs_policy
                └── browser_tabs_policy.set_override(mode, valid_until)

[On every RemoteAccessAlert, BrowserTabs is included only when:]
  - browser_tabs_policy.get_effective_mode() returns 'always', OR
  - mode is 'incoming_only' (default) AND alert direction == 'incoming'

[After valid_until expires, get_effective_mode() returns the
 built-in default 'incoming_only'. Override is not persisted across
 agent restarts.]
```

---

# 7. Summary Flow Diagram

```
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│ Extension│───►│  Agent   │───►│ Backend  │───►│ Analyzers│
│ detects  │    │ receives │    │ receives │    │ run      │
│ URL      │    │ & sends  │    │ alert    │    │          │
└──────────┘    └──────────┘    └──────────┘    └──────────┘
                                                      │
                                                      ▼
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│ Extension│◄───│  Agent   │◄───│ Backend  │◄───│ User     │
│ shows    │    │ receives │    │ publishes│    │ Analyzer │
│ result   │    │ notif    │    │ result   │    │ checks   │
└──────────┘    └──────────┘    └──────────┘    └──────────┘
```

---

# 8. WebApi (Admin) Data Flow

The WebApi project **never touches MySQL directly**. Every read and write travels over NetMQ to the Backend's `NetMQCqrsTransport` (`ICqrsTransport`, port 5556), which dispatches to a registered handler via `CqrsHandlerRegistry`.

```
┌──────────────────┐                    ┌─────────────────────────┐
│  Admin browser   │                    │  Backend (.NET)         │
│  (Razor / SPA)   │                    │                         │
│                  │  HTTP / SignalR    │  ┌──────────────────┐   │
│  • Razor Pages   │ ◄────────────────► │  │ NetMQCqrsTransport│   │
│  • SignalR hub   │                    │  │ (port 5556)       │   │
└────────┬─────────┘                    │  └────────┬──────────┘   │
         │                              │           │              │
         │ ICQRSClient.SendQueryAsync   │           ▼              │
         │ ICQRSClient.SendCommandAsync │  ┌──────────────────┐   │
         │  (bridged by                 │  │ CqrsHandlerRegistry│  │
         │  NetMQCqrsClientAdapter →     │  │  → Handlers + Repos│  │
         │  ICqrsClient/NetMQCqrsClient) │  └────────┬──────────┘   │
         │  NetMQ REQ                   │           │              │
         └──────────────────────────────►           ▼              │
                                          │  ┌──────────┐          │
                                          │  │  MySQL   │          │
                                          │  └──────────┘          │
                                          └─────────────────────────┘
```

## Step W1 — Admin lists roadmaps

```
Admin opens /Roadmaps
   ↓ Razor IndexModel.OnGetAsync
   ↓ ICQRSClient.SendQueryAsync<ListRoadmapsQueryResult>(new ListRoadmapsQuery { IncludeArchived = false })
   ↓ NetMQ REQ to backend:5556 (JSON, TypeNameHandling.None)
   ↓ NetMQCqrsTransport authenticates the envelope, routes by QueryType via CqrsHandlerRegistry
   ↓ RoadmapQueryHandlers.HandleAsync(ListRoadmapsQuery)
   ↓ IRoadmapRepository.ListAsync → SELECT * FROM Roadmaps WHERE IsArchived = 0 ORDER BY LastUpdatedAt DESC
   ↓ Result back over NetMQ
   ← Razor renders the list
```

## Step W2 — Real-time admin notifications

`/notificationshub` is a SignalR hub. When the Backend publishes an analysis result (Step 13 above), the WebApi's notification subscriber forwards it to admin browsers:

```
Backend ZMQ PUB (50002, topic "device:*")
   ↓ WebApi NotificationSubscriber (BackgroundService)
   ↓ IHubContext<NotificationsHub>.Clients.All.SendAsync("alert", ...)
   ↓ Admin dashboard updates live (counts / charts / feed)
```

---

# 9. Roadmap Module Data Flow

The Roadmap editor is a single-page app embedded in the admin Razor page. It uses the same JSON shape as the standalone HTML viewer in `docs/roadmap-presentation-editable.html`, so the SPA bundle works in both places.

## Step R1 — Edit page hydrates from DB

```
Admin opens /Roadmaps/Edit/3
   ↓ EditModel.OnGetAsync
   ↓ ICQRSClient.SendQueryAsync<GetRoadmapByIdQueryResult>(new GetRoadmapByIdQuery { Id = 3 })
   ↓ Backend → RoadmapRepository.GetByIdAsync(3) → returns row { Data, Version, ... }
   ← Razor inlines the data into the page:
       <script id="initial-roadmap-data" type="application/json">{...}</script>
       <script>window.RoadmapAdmin = { initial, save, markDirty, getCurrentData }</script>
   ↓ /js/roadmap-spa.js loads, reads window.RoadmapAdmin.initial.data,
     parses the JSON blob into state, renders the SPA into <div class="rm-spa">
```

## Step R2 — User edits → debounced save

```
User drags an item / toggles a status / types in modal
   ↓ SPA mutates `state` and calls save()
   ↓ save() debounces 800ms, then:
   ↓ POST /Roadmaps/Edit/3?handler=Save
       Body: { Id: 3, ExpectedVersion: 5, Data: "<json string>" }
       Headers: RequestVerificationToken (from antiforgery)
   ↓ EditModel.OnPostSaveAsync
   ↓ ICQRSClient.SendCommandAsync<SaveRoadmapCommandResult>(SaveRoadmapCommand { ... UpdatedBy: User.Identity.Name })
   ↓ RoadmapRepository.UpdateAsync — compare-and-swap on Version (optimistic concurrency)
       └─ if Version mismatch → return null → SaveRoadmapCommandResult { ConcurrencyConflict = true, NewVersion = serverVersion }
       └─ else            → bump Version, set LastUpdatedAt + LastUpdatedBy → return new Version
   ← Response: { success, newVersion, lastUpdatedAt, lastUpdatedBy, concurrencyConflict }
   ↓ SPA updates the "✓ נשמר" badge, header timestamp, version chip
       └─ on conflict → modal: "שינויים מרוחקים — לטעון מחדש?" → reload
```

## Step R3 — Export Viewer (offline HTML)

```
Admin clicks "ייצוא Viewer"
   ↓ GET /Roadmaps/Edit/3?handler=Viewer
   ↓ EditModel.OnGetViewerAsync
       1. Re-fetches the latest roadmap data (CQRS GetRoadmapByIdQuery)
       2. Reads /css/roadmap-spa.css and /js/roadmap-spa.js from disk
       3. Reads Pages/Roadmaps/_RoadmapSpaBody.cshtml as plain HTML (strips Razor comment header)
       4. Builds a self-contained HTML file:
          - Heebo font from Google CDN
          - <style> ... inlined CSS ... </style>
          - SPA body
          - <script> window.RoadmapAdmin = { initial: <embedded JSON>, save: noop, ... } </script>
          - <script> ... inlined SPA JS ... </script>
   ← Response: text/html, attachment, filename "roadmap-{Name}-{date}.html"
```

The exported file is fully offline-viewable. `save()` becomes a no-op (one-time alert "this is the offline viewer").

---

# 10. Mobile Agent Data Flow (target spec)

Android and iOS agents do not exist yet, but the Backend is mobile-aware. When they're built, their data flow will be **identical to Desktop** for URL alerts, with extra mobile-specific signals on top.

```
┌────────────────────────────────────────────────────┐
│               USER'S MOBILE DEVICE                 │
│                                                    │
│  ┌───────────────┐   ┌──────────────────────────┐  │
│  │ Android       │   │  Mobile Agent            │  │
│  │ Accessibility │──►│  • ZMQ-CURVE client      │  │
│  │ Service       │   │    (port 50001 REQ)      │  │
│  │ (URL hooks)   │   │  • ZMQ SUB listener      │  │
│  ├───────────────┤   │    (port 50002, topic    │  │
│  │ SMS BroadcastR│──►│    "device:{deviceUid}") │  │
│  ├───────────────┤   │                          │  │
│  │ CallScreening │──►│  Sends:                  │  │
│  ├───────────────┤   │   - UrlAlert             │  │
│  │ App-Detect    │──►│   - SmsAlert (Android)   │  │
│  │ (RemoteAccess)│   │   - PhoneAlert           │  │
│  └───────────────┘   │   - RemoteAccessAlert    │  │
│                      │                          │  │
│  ┌───────────────┐   │   Receives:              │  │
│  │ iOS:          │   │   - AnalysisResult       │  │
│  │ Network Ext.  │──►│     (with ProtectiveAct.)│  │
│  │ Message Filt. │──►│                          │  │
│  │ Call Directory│──►│                          │  │
│  └───────────────┘   └──────────────────────────┘  │
└────────────────────────┬───────────────────────────┘
                         │
                         │  Same wire-protocol as Desktop:
                         │   - ZMQ REQ → Backend ROUTER (50001, CURVE)
                         │   - ZMQ SUB ← Backend PUB     (50002, CURVE)
                         │
                         │  DeviceInfo.OperatingSystem = 4 (Android) | 5 (iOS)
                         │  DeviceInfo.DeviceType      = 2 (MobilePhone)
                         ▼
                  ┌──────────────────┐
                  │   ASPS Backend   │
                  │  (unchanged —    │
                  │   mobile-agnostic)│
                  └──────────────────┘
```

## Step M1 — URL alert from mobile (illustrative)

```
User taps a link in WhatsApp on Android
   ↓ Android opens link in Chrome
   ↓ Accessibility service reads URL bar text
   ↓ Mobile Agent builds UrlAlert (same JSON as Desktop §3.2 step 5),
     sets DeviceInfo.OperatingSystem = 4 (Android)
   ↓ ZMQ REQ → Backend ROUTER:50001 (CURVE-encrypted)
   ↓ Backend processes identically to Desktop alert (§3.3)
   ← Backend ZMQ PUB → topic "device:ANDROID-7c9f..."
   ↓ Mobile Agent receives AnalysisResult
   ↓ Executes ProtectiveActions:
     • DisplayNotification → Android NotificationChannel
     • BlockUrl            → Accessibility-injected overlay or in-app warning
     • SoundAlert          → System notification sound
```

## Step M2 — SMS scan (Android only)

```
Incoming SMS
   ↓ BroadcastReceiver fires (READ_SMS permission)
   ↓ Mobile Agent builds SmsAlert (new alert type — to be added):
     {
       AlertType: "SmsAlert",
       DeviceInfo: { OperatingSystem: 4, DeviceType: 2, ... },
       Token: "...",
       SenderId: "+972..." or "BANK-ALERT",
       Body: "<full text>",
       Timestamp: "..."
     }
   ↓ ZMQ REQ → Backend (CURVE) → analysis pipeline
   ← AnalysisResult: ProtectiveActions
     • UserDisplayNotification → Android shows scam-warning overlay above the SMS app
     • Email/SMS to protector  → if user has linked one
```

## Step M3 — Call screening

**Android (real-time):**
```
Incoming call
   ↓ CallScreeningService receives the call attempt
   ↓ Local lookup against cached BlacklistedPhoneNumbers (synced periodically)
   ↓ If match: respondToCall(REJECT_CALL) and notify Backend (PhoneAlert)
   ↓ Else: pass-through, optionally raise PhoneAlert for analysis
```

**iOS (pre-call only):**
```
On app install / periodic refresh
   ↓ Mobile Agent fetches BlacklistedPhoneNumbers from Backend
   ↓ Passes to Call Directory Extension via host app
   ↓ iOS prompts user: "Allow ASPS to identify and block calls?"
   ↓ All future calls from blocked numbers → silenced + labelled "Suspected Scam"
```

iOS cannot raise a real-time alert per call (no API), so the analysis path is replaced by **periodic blocklist sync**.

## Step M4 — New alert types (to be added to Backend)

| Alert | Source | Backend changes needed |
|-------|--------|------------------------|
| `SmsAlert` | Android only | New entity/handler; reuse existing analysis pipeline + ML model on text |
| `EmailAlert` | Both (via OAuth, Gmail/Outlook APIs — not OS hooks) | New entity/handler; ML on subject + body + headers |
| `PhoneAlert` | Android (real-time), iOS (post-hoc) | Lookup against BlacklistedPhoneNumbers (already exists, JIRA: ASPS-282) |
| `AppInstallAlert` | Android only (PackageInstaller observer) | New — flags installation of remote-access apps |

Until these alert types are implemented, mobile agents that need them should fall back to `UrlAlert` with metadata (e.g., synthetic URL `sms://+972...`).

---

*Document generated for NotebookLLM presentation*
*Last updated: 2026-04-29 — drift fixes (port 8765 → 8080-8484, HTTP → subprocess, Protective Actions enum), added §8 (WebApi data flow), §9 (Roadmap module flow), §10 (Mobile agent target flow).*
