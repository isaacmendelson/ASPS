# Python Client Troubleshooting Guide

## Common Issues and Solutions

### 1. "utf-8 codec can't decode byte 0xff"

**Problem:** Python client is sending binary data that cannot be decoded as UTF-8.

**Solution:** Ensure you're encoding your JSON as UTF-8:

```python
# ✅ CORRECT
message_json = json.dumps(alert_dict)
message_bytes = message_json.encode('utf-8')
socket.send(message_bytes)

# ❌ WRONG
socket.send(alert_dict)  # Don't send dict directly
socket.send(b'\xff\xff...')  # Don't send raw bytes
```

### 2. Expected Message Format

The C# server expects JSON messages with this structure:

```json
{
  "AlertType": "RemoteAccessAlert",  // or "UrlAlert" or "DeviceAlert"
  "DeviceInfo": {
    "DeviceUid": "unique-device-id",
    "DeviceType": 1,  // 1=PC, 2=Phone
    "OperatingSystem": 1,  // 1=Windows, 2=Linux, 3=iOS, 4=Android
    "MAC": "00:11:22:33:44:55"
  },
  "Timestamp": "2025-12-28T20:00:00Z",
  "Priority": 1,  // 0=Low, 1=Medium, 2=High, 3=Critical
  // Additional fields based on AlertType...
}
```

### 3. RemoteAccessAlert Fields

```python
{
  "AlertType": "RemoteAccessAlert",
  "DeviceInfo": { ... },
  "Timestamp": "2025-12-28T20:00:00Z",
  "Priority": 2,
  "SessionId": "session-id",
  "RemoteIP": "192.168.1.100",
  "Protocol": "RDP",  // or "SSH", "VNC", etc.
  "Duration": 3600  // in seconds
}
```

### 4. UrlAlert Fields

```python
{
  "AlertType": "UrlAlert",
  "DeviceInfo": { ... },
  "Timestamp": "2025-12-28T20:00:00Z",
  "Priority": 1,
  "Url": "https://example.com/page",
  "Domain": "example.com",
  "Category": "Malware"  // or "Phishing", "Adult", etc.
}
```

### 5. Installing Dependencies

```bash
pip install pyzmq
```

### 6. Testing Connection

```python
import zmq
import json

context = zmq.Context()
socket = context.socket(zmq.PUSH)
socket.connect("tcp://localhost:50001")

# Send simple test message
test_alert = {
    "AlertType": "DeviceAlert",
    "DeviceInfo": {
        "DeviceUid": "TEST-001",
        "DeviceType": 1,
        "OperatingSystem": 1,
        "MAC": "00:00:00:00:00:00"
    },
    "Timestamp": "2025-12-28T20:00:00Z",
    "Priority": 1
}

message = json.dumps(test_alert).encode('utf-8')
socket.send(message)
print("Test message sent!")

socket.close()
context.term()
```

### 7. Common Python Mistakes

❌ **Don't do this:**
```python
# Sending raw bytes
socket.send(b'\xff\xfe...')

# Sending Python object directly
socket.send(alert_dict)

# Using pickle
socket.send(pickle.dumps(alert_dict))

# Wrong encoding
socket.send(message.encode('latin-1'))
```

✅ **Do this:**
```python
# Convert to JSON, then encode as UTF-8
message_json = json.dumps(alert_dict)
message_bytes = message_json.encode('utf-8')
socket.send(message_bytes)
```

### 8. Debugging

Add logging to see what you're sending:

```python
import logging

logging.basicConfig(level=logging.DEBUG)

message_json = json.dumps(alert)
print(f"JSON: {message_json}")

message_bytes = message_json.encode('utf-8')
print(f"Bytes length: {len(message_bytes)}")
print(f"First 100 bytes: {message_bytes[:100]}")

socket.send(message_bytes)
```

### 9. Check Server Logs

After sending a message, check the ASPSBackend console for:

```
INFO: Received device alert: {"AlertType":"DeviceAlert",...}
INFO: Device alert processed: TEST-001
```

If you see errors, they will show:
```
ERROR: Failed to decode message as UTF-8: ...
ERROR: Received bytes (hex): FF-FE-...
```

### 10. Example Working Client

See `python-client-example.py` for a complete working example that:
- Connects to the server
- Sends properly formatted JSON alerts
- Encodes as UTF-8
- Handles different alert types

Run it with:
```bash
python python-client-example.py
```

### 11. Port Configuration

Default alert listener port: **50001**

Change it in:
- **Server:** `ASPSBackend/Program.cs` → `new RealTimeAlertListener(..., port: 50001)`
- **Client:** `socket.connect("tcp://localhost:50001")`

### 12. Network Issues

If connection fails:
- Check firewall allows port 50001
- Verify ASPSBackend is running
- Check the console shows: "Real-time alert listener started on tcp://*:50001"
- Try `telnet localhost 50001` to test connectivity
