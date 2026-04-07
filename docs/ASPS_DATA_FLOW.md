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
│  │                  │    Port 8765   │                               │   │
│  │  - background.js │                │  - extension_handler.py       │   │
│  │  - ScanService   │                │  - scan_service.py            │   │
│  │  - popup.js      │                │  - notification_handler.py    │   │
│  └─────────────────┘                 │  - zmq_client.py              │   │
│                                      └──────────────┬────────────────┘   │
└──────────────────────────────────────────────────────┼───────────────────┘
                                                       │
                                          ZMQ (Port 50001 REQ/REP)
                                          ZMQ (Port 50002 PUB/SUB)
                                                       │
┌──────────────────────────────────────────────────────┼───────────────────┐
│                         BACKEND SERVER (.NET)        │                   │
│  ┌───────────────────────────────────────────────────▼─────────────────┐ │
│  │                    RealTimeAlertListener                             │ │
│  │        (ZMQ ROUTER Socket - handles concurrent connections)          │ │
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
│                      │ NotificationPublisher    │                        │
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

### Step 6: RealTimeAlertListener Receives Alert
**Location:** `Business/Messaging/RealTimeAlertListener.cs`

```csharp
private async Task ProcessRouterMessageAsync(byte[] identity, byte[] messageBytes)
{
    var message = Encoding.UTF8.GetString(messageBytes);
    var result = await RouteMessageAsync(message);
    SendRouterResponse(identity, result);
}

private Task<object> ProcessAlertAsync(string message, JObject jObject)
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
**Location:** `Business/Messaging/RealTimeAlertListener.cs`

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

For URL analysis, the backend calls an external Python service:

```
Backend → HTTP POST → Python Analyzer → Response
```

The Python analyzer performs:
- **URL Inspector** (10 checks on URL string alone)
- **Category Classification** (58 categories)
- **ML Risk Scoring** (phishing probability)
- **Reputation Lookup**

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

### Step 13: NotificationPublisher Sends Result
**Location:** `Business/Messaging/NotificationPublisher.cs`

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
Triggered when remote access app is detected.

```json
{
  "AlertType": "RemoteAccessAlert",
  "RemoteAccessApp": "AnyDesk",
  "ConnectionStatus": "Open",
  "SessionStatus": "Open",
  "RemoteAccessDirection": "In",
  "BrowserTabs": [...]
}
```

---

# 5. Protective Actions

The system can trigger various protective actions based on risk level:

| Action | Level | Description |
|--------|-------|-------------|
| LogEvent | 0 | Log for monitoring |
| Notify | 1 | Show notification to user |
| Warn | 2 | Display warning popup |
| Block | 3 | Block the URL |
| ForceClose | 4 | Force close the tab |

---

# 6. Immediate Danger Scenarios

The system detects "immediate danger" when:

1. **Remote Access + Sensitive Site**: User has an active remote access session (AnyDesk, TeamViewer) AND is browsing a banking/crypto site
2. **Extended Risky Browsing**: User spends >5 minutes on a risky URL while remote access is active
3. **Scam-in-Progress**: Known scam pattern detected (e.g., tech support scam flow)

When immediate danger is detected:
- Log warning
- Raise `ImmediateDangerAlert`
- Notify user urgently
- Potentially block or force-close tabs

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

*Document generated for NotebookLLM presentation*
*Last updated: April 2026*
