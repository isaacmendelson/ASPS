# Real-Time Alert Listener Configuration Guide

The ASPSBackend alert listener supports two socket modes for receiving device alerts from remote clients.

## Socket Modes

### 1. REP Mode (Request-Response) - **RECOMMENDED**
- **Two-way communication**: Clients send alerts and receive responses
- **Acknowledgment**: Clients know if the alert was processed successfully
- **Error feedback**: Clients receive error messages if processing fails
- **Use case**: Production environments where you need confirmation

**Configuration:**
```json
{
  "NetMQ": {
    "RealTimeListenerPort": 50001,
    "RealTimeListenerMode": "Rep"
  }
}
```

**Response Format:**
```json
{
  "success": true,
  "message": "Alert processed successfully",
  "alertType": "RemoteAccessAlert",
  "deviceUid": "PC-12345",
  "timestamp": "2025-12-28T20:00:00.000Z",
  "priority": "High"
}
```

**Python Client:**
```python
import zmq

socket = zmq.Context().socket(zmq.REQ)
socket.connect("tcp://localhost:50001")

# Send alert
socket.send(json.dumps(alert).encode('utf-8'))

# Wait for response
response = socket.recv().decode('utf-8')
print(response)
```

### 2. PULL Mode (Fire-and-Forget)
- **One-way communication**: Clients send alerts, no response
- **High throughput**: Slightly faster for bulk operations
- **No confirmation**: Client doesn't know if alert was processed
- **Use case**: High-volume scenarios where confirmation isn't critical

**Configuration:**
```json
{
  "NetMQ": {
    "RealTimeListenerPort": 50001,
    "RealTimeListenerMode": "Pull"
  }
}
```

**Python Client:**
```python
import zmq

socket = zmq.Context().socket(zmq.PUSH)
socket.connect("tcp://localhost:50001")

# Send alert (no response)
socket.send(json.dumps(alert).encode('utf-8'))
```

## Configuration File Location

Edit the socket mode in:
```
ASPSBackend/appsettings.json
```

## Valid Mode Values

- `"Rep"` - Request-Response mode (default)
- `"Pull"` - Fire-and-forget mode

## Example Configurations

### Development (with responses for debugging)
```json
{
  "NetMQ": {
    "RealTimeListenerPort": 50001,
    "RealTimeListenerMode": "Rep"
  }
}
```

### High-Volume Production (fire-and-forget)
```json
{
  "NetMQ": {
    "RealTimeListenerPort": 50001,
    "RealTimeListenerMode": "Pull"
  }
}
```

## Changing Modes

1. **Edit appsettings.json**
2. **Restart ASPSBackend**
3. **Update your Python client** to use matching socket type:
   - REP mode → Use `zmq.REQ` socket
   - PULL mode → Use `zmq.PUSH` socket

## Console Output

When starting, ASPSBackend will show the active mode:

```
✓ Real-time alert listener started (tcp://*:50001, Mode: Rep)
```

or

```
✓ Real-time alert listener started (tcp://*:50001, Mode: Pull)
```

## Response Examples

### Success Response (REP mode only)
```json
{
  "success": true,
  "message": "Alert processed successfully",
  "alertType": "UrlAlert",
  "deviceUid": "PC-12345",
  "timestamp": "2025-12-28T20:15:30.123Z",
  "priority": "Medium"
}
```

### Error Response (REP mode only)
```json
{
  "success": false,
  "message": "Unknown alert type",
  "alertType": "InvalidType"
}
```

### Deserialization Error (REP mode only)
```json
{
  "success": false,
  "message": "Failed to deserialize alert"
}
```

### UTF-8 Decode Error (REP mode only)
```json
{
  "success": false,
  "message": "Failed to decode message as UTF-8",
  "error": "Invalid byte sequence"
}
```

## Python Client Example

Use the included `python-client-example.py` which automatically detects and supports both modes:

```bash
python python-client-example.py
```

It will prompt you to choose:
1. REQ/REP mode (waits for response)
2. PUSH/PULL mode (fire-and-forget)

## Troubleshooting

**Problem:** Python client times out waiting for response

**Solution:** Check that server mode is "Rep" in appsettings.json, or use PUSH/PULL mode

---

**Problem:** "Address already in use" error

**Solution:** Port 50001 is already bound. Change `RealTimeListenerPort` or stop other services using that port

---

**Problem:** No response received in REP mode

**Solution:** Check server logs for processing errors. Response is only sent after alert is processed.

## Performance Considerations

- **REP mode**: ~5-10% slower due to response overhead
- **PULL mode**: Slightly faster, but no confirmation
- For most applications, REP mode is recommended for reliability
- Use PULL mode only for high-volume (1000+ alerts/sec) scenarios
