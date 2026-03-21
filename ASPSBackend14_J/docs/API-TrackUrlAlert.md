# TrackUrlAlert API Documentation

## Overview
API endpoints for submitting TrackUrlAlert data from browser extensions and monitoring agents.

## Endpoint

### POST /api/alerts/trackurl

Submit a TrackUrlAlert for processing.

**URL:** `/api/alerts/trackurl`  
**Method:** `POST`  
**Content-Type:** `application/json`

---

## Request Format

### TrackUrlAlertDto

```json
{
  "deviceUid": "string (required)",
  "url": "string (required)",
  "fromUrl": "string (optional)",
  "duration": 0,
  "scamInProgressKey": "string (optional)",
  "ipAddress": "string (optional)",
  "userAgent": "string (optional)",
  "tabId": "string (optional)",
  "timezone": "string (optional)",
  "timestamp": "2026-03-21T14:00:00Z",
  "priority": "string (optional)"
}
```

### Field Descriptions

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `deviceUid` | string | ✅ Yes | Unique identifier for the device |
| `url` | string | ✅ Yes | The current URL being visited (must not be local/loopback) |
| `fromUrl` | string | No | The previous URL (referrer) |
| `duration` | integer | No | Duration spent on the page in seconds (must be non-negative) |
| `scamInProgressKey` | string | No | Key for identifying scam-in-progress scenarios |
| `ipAddress` | string | No | IP address of the request |
| `userAgent` | string | No | User agent string from the browser |
| `tabId` | string | No | Browser tab identifier |
| `timezone` | string | No | User's timezone |
| `timestamp` | datetime | No | Timestamp of the alert (UTC, must not be in future) |
| `priority` | string | No | Alert priority level |

---

## Validation Rules

### Required Fields
- ✅ `deviceUid` must not be null or empty
- ✅ `url` must not be null or empty

### URL Validation
- ❌ **Rejected URLs:**
  - `localhost`
  - `127.0.0.1` (or any `127.*.*.*`)
  - `::1` (IPv6 loopback)
  - `0.0.0.0`

### Value Constraints
- ✅ `duration` must be non-negative (>= 0)
- ✅ `timestamp` must not be more than 5 minutes in the future

---

## Response Format

### Success Response (200 OK)

```json
{
  "success": true,
  "message": "TrackUrlAlert received and queued for processing",
  "deviceUid": "test-device-123",
  "url": "https://example.com",
  "timestamp": "2026-03-21T14:00:00.0000000Z",
  "note": "Alert processing handled by RealTimeAlertListener via ZeroMQ"
}
```

### Error Responses

#### 400 Bad Request - Null DTO
```json
{
  "success": false,
  "message": "Alert data is required"
}
```

#### 400 Bad Request - Missing DeviceUid
```json
{
  "success": false,
  "message": "DeviceUid is required"
}
```

#### 400 Bad Request - Missing URL
```json
{
  "success": false,
  "message": "Url is required"
}
```

#### 400 Bad Request - Local URL
```json
{
  "success": false,
  "message": "Local URLs are not analyzed"
}
```

#### 400 Bad Request - Invalid Duration
```json
{
  "success": false,
  "message": "Duration must be non-negative"
}
```

#### 400 Bad Request - Future Timestamp
```json
{
  "success": false,
  "message": "Timestamp cannot be in the future"
}
```

#### 500 Internal Server Error
```json
{
  "success": false,
  "message": "Internal server error"
}
```

---

## Example Usage

### cURL Example

```bash
curl -X POST https://api.example.com/api/alerts/trackurl \
  -H "Content-Type: application/json" \
  -d '{
    "deviceUid": "device-abc-123",
    "url": "https://suspicious-site.com/login",
    "fromUrl": "https://google.com",
    "duration": 30,
    "ipAddress": "192.168.1.100",
    "userAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
    "tabId": "chrome-tab-42",
    "timezone": "America/New_York",
    "priority": "high",
    "timestamp": "2026-03-21T14:00:00Z"
  }'
```

### JavaScript (Browser Extension)

```javascript
async function submitTrackUrlAlert(alertData) {
  const response = await fetch('https://api.example.com/api/alerts/trackurl', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      deviceUid: alertData.deviceUid,
      url: alertData.url,
      fromUrl: alertData.fromUrl || '',
      duration: alertData.duration || 0,
      ipAddress: alertData.ipAddress || '',
      userAgent: navigator.userAgent,
      tabId: alertData.tabId || '',
      timezone: Intl.DateTimeFormat().resolvedOptions().timeZone,
      priority: alertData.priority || 'medium',
      timestamp: new Date().toISOString()
    })
  });
  
  return await response.json();
}
```

### Python (Agent)

```python
import requests
from datetime import datetime, timezone

def submit_track_url_alert(device_uid, url, **kwargs):
    payload = {
        "deviceUid": device_uid,
        "url": url,
        "fromUrl": kwargs.get("from_url", ""),
        "duration": kwargs.get("duration", 0),
        "ipAddress": kwargs.get("ip_address", ""),
        "userAgent": kwargs.get("user_agent", ""),
        "tabId": kwargs.get("tab_id", ""),
        "timezone": kwargs.get("timezone", "UTC"),
        "priority": kwargs.get("priority", "medium"),
        "timestamp": datetime.now(timezone.utc).isoformat()
    }
    
    response = requests.post(
        "https://api.example.com/api/alerts/trackurl",
        json=payload,
        headers={"Content-Type": "application/json"}
    )
    
    return response.json()
```

---

## Integration Notes

### Alert Pipeline Integration
- Alerts are logged and acknowledged immediately
- Actual processing is handled asynchronously by `RealTimeAlertListener` via ZeroMQ
- This HTTP endpoint serves as an **alternative submission method** to the ZeroMQ socket

### Logging
The endpoint logs:
- **INFO**: Successful alert reception with device UID, URL, duration, and priority
- **WARNING**: Validation failures (missing fields, local URLs, invalid values)
- **DEBUG**: Local URL rejections
- **ERROR**: Unexpected exceptions during processing

---

## Testing

### Unit Tests Coverage
All tests are located in: `ASPS.Tests/WebApi/Controllers/AlertsControllerTests.cs`

Test coverage includes:
- ✅ Valid DTO submission → 200 OK
- ✅ Null DTO → 400 Bad Request
- ✅ Empty DeviceUid → 400 Bad Request
- ✅ Empty URL → 400 Bad Request
- ✅ Local URLs (localhost, 127.0.0.1, etc.) → 400 Bad Request
- ✅ Negative duration → 400 Bad Request
- ✅ Future timestamp → 400 Bad Request
- ✅ Zero duration (valid) → 200 OK
- ✅ All optional fields populated → 200 OK
- ✅ Proper logging for valid requests

**Test Status:** ✅ All 14 tests passing

---

## Related Components

### Data Models
- **DTO:** `Interface/Analysis/TrackUrlAlertDto.cs`
- **Entity:** `Common/Entities/TrackUrlAlertEntity.cs`
- **Model:** `Common/Models/Alerts/TrackUrlAlert.cs`

### Controller
- **Location:** `WebApi/Controllers/AlertsController.cs`
- **Route:** `/api/alerts/trackurl`

### Tests
- **Location:** `ASPS.Tests/WebApi/Controllers/AlertsControllerTests.cs`
- **Coverage:** 14 unit tests

---

## Change History

| Date | Version | Changes |
|------|---------|---------|
| 2026-03-21 | 1.0 | Initial implementation - ASPS-252 |

