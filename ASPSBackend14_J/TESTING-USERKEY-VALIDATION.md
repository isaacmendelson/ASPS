# Testing DeviceAlert with UserKey Validation

## Setup

### 1. Reset Database and Populate Test Data

```bash
# Reset database
mysql -u root -p < RESET-DATABASE.sql

# Populate test users and devices
mysql -u root -p < populate-test-data.sql
```

### 2. Test Users and Devices

**User 1: John Doe**
- Key: `User|test-user-001|`
- Devices:
  - `PC-JOHN-001` - Desktop (Windows)
  - `PHONE-JOHN-001` - iPhone (IOS)

**User 2: Jane Smith**
- Key: `User|test-user-002|`
- Devices:
  - `PC-JANE-001` - Laptop (Windows)
  - `PHONE-JANE-001` - Android

## Testing Scenarios

### Scenario 1: Valid Alert from Known Device

Send alert from John's PC:

```python
import zmq
import json

context = zmq.Context()
socket = context.socket(zmq.REQ)
socket.connect("tcp://localhost:50001")

alert = {
    "AlertType": "RemoteAccessAlert",
    "Priority": 2,
    "Timestamp": "2025-12-30T20:00:00Z",
    "Token": "test-token-123",
    "DeviceInfo": {
        "DeviceUid": "PC-JOHN-001",  # ✅ Known device
        "OperatingSystem": 1
    },
    "RemoteAccessApp": 1,
    "RunningProcesses": 5,
    "ConnectionUrl": "https://remote.example.com",
    "ConnectionStatus": 1,
    "ConnectionsCount": 1,
    "SessionStatus": 1
}

socket.send(json.dumps(alert).encode('utf-8'))
response = socket.recv()
print(json.loads(response))
```

**Expected Response:**
```json
{
  "success": true,
  "message": "Alert processed successfully",
  "alertType": "RemoteAccessAlert",
  "deviceUid": "PC-JOHN-001",
  "timestamp": "2025-12-30T20:00:00Z",
  "priority": "High"
}
```

### Scenario 2: Alert from Unknown Device (DeviceNotFound)

Send alert from unregistered device:

```python
alert = {
    "AlertType": "UrlAlert",
    "DeviceInfo": {
        "DeviceUid": "PC-UNKNOWN-999",  # ❌ Unknown device
        "OperatingSystem": 1
    },
    "Url": "https://suspicious-site.com",
    "Trackers": [],
    "IFrameDomains": [],
    "UserAgent": "Mozilla/5.0..."
}

socket.send(json.dumps(alert).encode('utf-8'))
response = socket.recv()
print(json.loads(response))
```

**Expected Response:**
```json
{
  "success": false,
  "message": "DeviceNotFound",
  "error": "Device not found: PC-UNKNOWN-999"
}
```

### Scenario 3: Check Database

After sending valid alerts:

```sql
USE ASPSBackend2DB;

-- Check saved alerts
SELECT 
    `Key`,
    DeviceUid,
    UserKey,
    AlertType,
    Priority,
    Timestamp,
    Discriminator
FROM DeviceAlerts
WHERE DeviceUid IN ('PC-JOHN-001', 'PC-JANE-001')
ORDER BY Timestamp DESC;

-- Check alert with user info
SELECT 
    da.`Key` AS AlertKey,
    da.DeviceUid,
    da.AlertType,
    da.Timestamp,
    u.FirstName,
    u.LastName,
    ud.DeviceName
FROM DeviceAlerts da
LEFT JOIN Users u ON da.UserKey = u.`Key`
LEFT JOIN UserDevices ud ON da.DeviceUid = ud.DeviceUid
ORDER BY da.Timestamp DESC
LIMIT 10;
```

## How It Works

1. **Device sends alert** with `DeviceUid`
2. **RealTimeAlertListener** looks up device in ASView:
   ```csharp
   var userDevice = _asView.FindUserDeviceByDeviceUid(deviceUid);
   ```
3. **Validates user exists**:
   ```csharp
   var user = _asView.FindUserByKey(userDevice.UserKey);
   if (user == null) throw new DomainException("UserNotFound", ...);
   ```
4. **Saves alert** with `UserKey` to database
5. **Returns response** to device

## Error Codes

| Code | Description | HTTP Equivalent |
|------|-------------|-----------------|
| `DeviceNotFound` | DeviceUid not found in UserDevices | 404 |
| `UserNotFound` | User record not found for device | 404 |

## Monitoring

Check ASPSBackend console for:

```
✓ ASView loaded: 2 users, 4 devices, 0 accounts
INFO: Alert from device PC-JOHN-001 associated with user User|test-user-001|
INFO: Device alert saved to database: Alert|<guid>|
```
