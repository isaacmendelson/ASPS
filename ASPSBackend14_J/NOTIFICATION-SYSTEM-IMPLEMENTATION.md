# Notification System Implementation

## Overview
Added real-time notification system using ZeroMQ PUB/SUB pattern. Clients can subscribe to analysis result notifications before sending alerts and receive updates as analysis completes.

---

## Architecture

### **Pattern: Publisher-Subscriber (PUB/SUB)**

```
┌─────────────────────┐
│  Python Client      │
│                     │
│  1. SUB socket      │───────┐
│     Subscribe to    │       │
│     "device:PC-001" │       │
│                     │       │
│  2. REQ socket      │───┐   │
│     Send UrlAlert   │   │   │
└─────────────────────┘   │   │
                          │   │
                          ↓   ↓
┌──────────────────────────────────────────────┐
│  ASPSBackend                                 │
│                                              │
│  ┌────────────────────────────────────────┐ │
│  │ RealTimeAlertListener (REP socket)     │ │
│  │ Port: 50001                            │ │
│  │ • Receives alerts                      │ │
│  │ • Fires DeviceAlertReceived event     │ │
│  └────────────────────────────────────────┘ │
│              ↓                               │
│  ┌────────────────────────────────────────┐ │
│  │ UDAnalysis                             │ │
│  │ • Runs analyzers                       │ │
│  │ • Fires AnalysisResultReceived event  │ │
│  └────────────────────────────────────────┘ │
│              ↓                               │
│  ┌────────────────────────────────────────┐ │
│  │ NotificationPublisherActor             │ │
│  │ • Listens for AnalysisResultReceived  │ │
│  │ • Calls NotificationPublisher         │ │
│  └────────────────────────────────────────┘ │
│              ↓                               │
│  ┌────────────────────────────────────────┐ │
│  │ NotificationPublisher (PUB socket)     │ │
│  │ Port: 50002                            │ │
│  │ • Publishes to "device:{uid}" topic   │ │
│  │ • Publishes to "user:{key}" topic     │ │
│  └────────────────────────────────────────┘ │
└──────────────────────────────────────────────┘
              │
              │ PUB/SUB
              ↓
    ┌─────────────────────┐
    │  Python Client      │
    │  SUB socket         │
    │  Receives           │
    │  notification       │
    └─────────────────────┘
```

---

## Backend Components

### **1. NotificationPublisher**
**File:** `Business/Messaging/NotificationPublisher.cs`

**Responsibilities:**
- Manages PUB socket on port 50002
- Publishes analysis results to subscribers
- Uses topic-based routing

**Topics:**
- `device:{deviceUid}` - Device-specific notifications
- `user:{userKeyField}` - User-level notifications

**Message Format:**
```json
{
  "Type": "AnalysisResult",
  "Timestamp": "2026-01-12T15:30:00Z",
  "DeviceUid": "PC-JOHN-001",
  "UserKeyField": "11111111-1111-1111-1111-111111111111",
  "Data": {
    "AlertType": "UrlAlert",
    "Severity": "Medium",
    "Message": "URL analysis completed",
    "AnalyzerResults": { ... },
    "Details": { ... },
    "DeviceAlertKey": "...",
    "AnalysisTimestamp": "2026-01-12T15:30:00Z"
  }
}
```

### **2. NotificationPublisherActor**
**File:** `Business/RealtimeAnalysis/NotificationPublisherActor.cs`

**Responsibilities:**
- Implements `IDomainEventHandler`
- Listens for `AnalysisResultReceived` events
- Formats and publishes notifications

**Event Flow:**
```
AnalysisResultReceived Event
    ↓
NotificationPublisherActor.Handle()
    ↓
Format notification payload
    ↓
NotificationPublisher.PublishAnalysisResult()
    ↓
Publish to device topic
Publish to user topic
```

### **3. Configuration**
**File:** `ASPSBackend/appsettings.json`

```json
{
  "NetMQ": {
    "BusinessEndpoint": "tcp://*:5555",
    "RealTimeListenerPort": 50001,
    "RealTimeListenerMode": "Rep",
    "NotificationPublisherPort": 50002
  }
}
```

### **4. Service Registration**
**File:** `ASPSBackend/Program.cs`

```csharp
// Add Notification Publisher
services.AddSingleton<Business.Messaging.NotificationPublisher>();

// Add Event Handlers for Analysis Results
services.AddSingleton<IDomainEventHandler, AnalysisPersistenceActor>();
services.AddSingleton<IDomainEventHandler, NotificationPublisherActor>();
```

---

## Python Client

### **Enhanced Client**
**File:** `python-client-with-notifications.py`

**Features:**
1. **NotificationListener class** - Background thread for receiving notifications
2. **Automatic subscription** - Subscribes before sending alert
3. **Real-time display** - Shows notifications as they arrive
4. **Clean shutdown** - Ctrl+C to exit gracefully

**Usage:**

```bash
# Install dependencies
pip install pyzmq

# Run client
python python-client-with-notifications.py
```

**Workflow:**

```
1. User selects mode 2 (Notification Mode)
   ↓
2. User selects device (John or Jane)
   ↓
3. User enters URL to analyze
   ↓
4. Client creates SUB socket
   ↓
5. Client subscribes to "device:{deviceUid}"
   ↓
6. Client creates REQ socket
   ↓
7. Client sends UrlAlert
   ↓
8. Client receives immediate response (ACK)
   ↓
9. Client displays "Listening for notifications..."
   ↓
10. Backend analyzes URL
    ↓
11. Backend publishes notification
    ↓
12. Client receives notification
    ↓
13. Client displays formatted results
    ↓
14. Client continues listening
    ↓
15. User presses Ctrl+C to exit
```

**Example Output:**

```
🚀 ASPSBackend Python Client - With Notifications
======================================================================

📋 Choose mode:
1. Simple URL Alert (no notifications)
2. URL Alert + Subscribe to notifications (RECOMMENDED)
3. Exit

Enter choice (1-3, default=2): 2

======================================================================
📡 Notification Mode
======================================================================

User 1: John or 2: Jane (default=1): 1

👤 Selected: PC-JOHN-001

Enter URL to analyze (default=http://example.com): https://suspicious-site.com

🔗 URL: https://suspicious-site.com

📡 Subscribed to notifications for device: PC-JOHN-001
   Listening on tcp://localhost:50002
======================================================================
🎧 Listening for notifications on topic: 'device:PC-JOHN-001'...

======================================================================
📤 SENDING URL ALERT
======================================================================
🖥️  Device: PC-JOHN-001
🔗 URL: https://suspicious-site.com
⚠️  Priority: 1

✅ Alert sent to backend!
⏳ Waiting for immediate response...

📨 Immediate Response:
----------------------------------------------------------------------
{
  "Status": "Accepted",
  "Message": "Alert received and queued for processing"
}
----------------------------------------------------------------------

======================================================================
🎧 Now listening for analysis notifications...
   (Analysis may take a few seconds)
   Press Ctrl+C to exit
======================================================================

======================================================================
🔔 NOTIFICATION RECEIVED
======================================================================
📌 Topic: device:PC-JOHN-001
⏰ Timestamp: 2026-01-12T15:30:45Z
🖥️  Device: PC-JOHN-001

📊 Analysis Result:
   Alert Type: UrlAlert
   Severity: High
   Message: URL analysis completed: 1/1 analyzers succeeded

🔍 Analyzer Results:
   • UDUrlAnalyzer
     URL: https://suspicious-site.com
     Domain: suspicious-site.com
     Risk Score: 75
     Is Scam: True

📝 Additional Details:
   analyzers_run: 1
   analyzers_total: 1
======================================================================
🎧 Listening for more notifications... (Ctrl+C to exit)

^C
⚠️  Ctrl+C detected

📡 Stopped listening for notifications

======================================================================
✅ Done!
======================================================================
```

---

## Testing

### **1. Start ASPSBackend:**
```bash
cd ASPSBackend
dotnet run --project ASPSBackend
```

**Expected logs:**
```
[INFO] NotificationPublisher started on tcp://*:50002
[INFO] UDAnalysisManager created for user Key(User, ...) with 2 event handlers
[INFO] Registered event handler: AnalysisPersistenceActor
[INFO] Registered event handler: NotificationPublisherActor
```

### **2. Run Python Client:**
```bash
python python-client-with-notifications.py
```

**Select option 2** for notification mode

### **3. Verify Notification Flow:**

**Backend logs should show:**
```
[INFO] Alert routed to UDAnalysisManager for user: Key(User, ...)
[DEBUG] Fired AnalysisResultReceived event: UDUrlAnalyzer, Severity: Medium
[INFO] [AnalysisPersistenceActor] Saved analysis result: Key=...
[DEBUG] Published notification to topic 'device:PC-JOHN-001'
[DEBUG] Published notification to topic 'user:11111111-...'
[INFO] [NotificationPublisherActor] Published notification for device PC-JOHN-001
```

**Client should receive and display notification with:**
- Topic
- Timestamp
- Device UID
- Analysis results
- Risk assessment
- Details

---

## Features

### **1. Topic-Based Routing**
- Clients only receive notifications for subscribed devices/users
- No unnecessary network traffic
- Scalable to many clients

### **2. Asynchronous Notifications**
- Client gets immediate ACK when sending alert
- Actual analysis results arrive later
- Non-blocking for client

### **3. Multiple Subscribers**
- Multiple clients can subscribe to same device
- All receive the same notifications
- Useful for monitoring dashboards

### **4. User-Level Subscriptions**
- Can subscribe to `user:{userKey}` to receive ALL device notifications for a user
- Useful for user-level monitoring

---

## Advanced Usage

### **Subscribe to Multiple Devices:**
```python
# In NotificationListener.__init__:
devices = ["PC-JOHN-001", "PC-JOHN-LAPTOP", "PHONE-JOHN-001"]
for device in devices:
    topic = f"device:{device}"
    self.socket.subscribe(topic.encode('utf-8'))
```

### **Subscribe to User-Level Notifications:**
```python
# Subscribe to all devices for a user
user_key = "11111111-1111-1111-1111-111111111111"
topic = f"user:{user_key}"
self.socket.subscribe(topic.encode('utf-8'))
```

### **Filter Notifications by Severity:**
```python
def _handle_notification(self, topic, message_json):
    notification = json.loads(message_json)
    data = notification.get('Data', {})
    severity = data.get('Severity', '')
    
    # Only show high/critical
    if severity in ['High', 'Critical']:
        print(f"🚨 ALERT: {severity} severity detected!")
        # Display full notification
```

---

## Network Ports

| Port  | Socket Type | Purpose                     | Direction        |
|-------|-------------|-----------------------------|------------------|
| 50001 | REP         | Receive device alerts       | Client → Server  |
| 50002 | PUB         | Publish analysis results    | Server → Clients |
| 5555  | PUSH        | Business logic (unused now) | Internal         |

---

## Troubleshooting

### **No Notifications Received:**

1. **Check backend is running:**
   ```bash
   netstat -an | grep 50002
   ```
   Should show: `*:50002` LISTENING

2. **Check subscription topic:**
   - Make sure device UID matches exactly
   - Topics are case-sensitive

3. **Check backend logs:**
   - Should see "Published notification to topic 'device:...'"

4. **Test with simple subscriber:**
   ```python
   import zmq
   context = zmq.Context()
   socket = context.socket(zmq.SUB)
   socket.connect("tcp://localhost:50002")
   socket.subscribe(b"")  # Subscribe to all
   print("Waiting...")
   print(socket.recv_multipart())
   ```

### **Backend Not Publishing:**

1. **Verify NotificationPublisherActor is registered:**
   ```csharp
   services.AddSingleton<IDomainEventHandler, NotificationPublisherActor>();
   ```

2. **Check analysis completes:**
   - Look for "Analysis completed" logs
   - Check AnalysisResultReceived event is fired

3. **Verify configuration:**
   - `appsettings.json` has `NotificationPublisherPort`

---

## Benefits

1. ✅ **Real-time feedback** - Clients get results immediately
2. ✅ **Decoupled** - Backend doesn't need to know about clients
3. ✅ **Scalable** - PUB/SUB handles many subscribers efficiently
4. ✅ **Flexible** - Can subscribe to device or user level
5. ✅ **Non-blocking** - Clients can do other work while waiting
6. ✅ **Reliable** - ZeroMQ handles reconnection automatically

---

## Future Enhancements

1. **WebSocket gateway** - For web clients
2. **Persistent subscriptions** - Store active subscriptions
3. **Message replay** - Replay missed notifications
4. **Filtering** - Server-side filtering by severity/type
5. **Authentication** - Verify clients can subscribe to their devices only
6. **Compression** - For large analysis results
7. **Batching** - Batch multiple notifications for efficiency

---

## Summary

✅ PUB/SUB notification system implemented
✅ Backend publishes analysis results to subscribers
✅ Python client subscribes and listens for notifications
✅ Real-time feedback for analysis completion
✅ Topic-based routing (device and user level)
✅ Clean architecture with event-driven design
✅ Comprehensive error handling and logging

**Ready to use!** 🚀
