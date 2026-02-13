# Admin Frontend No Data - Troubleshooting Guide

## ❌ PROBLEM: Admin Frontend Shows No Data

The admin dashboard loads but shows no data (all zeros or empty lists).

---

## 🔍 DIAGNOSIS CHECKLIST

### **1. Is ASPSBackend Running?** ⭐ MOST COMMON ISSUE

```bash
# Check if ASPSBackend process is running
ps aux | grep ASPSBackend

# Or check the terminal where you started it
# Should see: "✓ CQRS Gateway started (tcp://*:5556)"
```

**If NOT running:**
```bash
cd ASPSBackend
dotnet run --project ASPSBackend
```

**Look for this output:**
```
========================================
✓ ASView started
✓ NetMQ CQRS processor started (tcp://*:5555)
✓ Real-time alert listener started (tcp://*:50001, Mode: Rep)
✓ CQRS Gateway started (tcp://*:5556) - Listening for WebApi Commands/Queries
========================================
```

### **2. Is WebApi Running?**

```bash
# Check if WebApi is running
ps aux | grep WebApi

# Or check terminal
# Should see: "WebApi started - Distributed CQRS Architecture"
```

**If NOT running:**
```bash
cd ASPSBackend
dotnet run --project WebApi
```

**Access:**
```
http://localhost:5001
```

### **3. Check WebApi Logs**

When you load the dashboard, you should see logs like:

```
info: WebApi.Pages.IndexModel[0]
      Loading dashboard via CQRS (NetMQ)
```

**If you see timeout errors:**
```
Error sending query GetDashboardStatsQuery
Communication error: Operation timed out
```

This means WebApi can't reach ASPSBackend's CQRS Gateway.

### **4. Check ASPSBackend Logs**

When dashboard loads, you should see:

```
Received message: {"MessageType":"Query","QueryType":"GetDashboardStatsQuery"...}
Sent response: {"Success":true,"UsersCount":5,...}
```

**If you DON'T see these logs:**
- WebApi is not connecting to CQRS Gateway
- Check ports and firewall

### **5. Verify Ports**

```bash
# Check if port 5556 is listening (CQRS Gateway)
netstat -an | grep 5556
# Should show: tcp  0  0.0.0.0:5556  *:*  LISTEN

# Or use:
lsof -i :5556
```

**If port NOT listening:**
- ASPSBackend is not running
- CQRSGateway failed to start
- Check ASPSBackend startup logs for errors

---

## 🔧 COMMON ISSUES & FIXES

### **Issue 1: ASPSBackend Not Running**

**Symptoms:**
- Dashboard loads but shows zeros
- No WebApi logs about "Received response"
- Port 5556 not listening

**Fix:**
```bash
# Start ASPSBackend
cd ASPSBackend
dotnet run --project ASPSBackend

# Wait for:
# "✓ CQRS Gateway started (tcp://*:5556)"
```

### **Issue 2: Port Conflict**

**Symptoms:**
```
SocketException: Address already in use
```

**Fix:**
```bash
# Find what's using port 5556
lsof -i :5556

# Kill the process
kill -9 <PID>

# Or change port in both:
# - ASPSBackend/Program.cs (CQRSGateway endpoint)
# - WebApi/appsettings.json (CQRS:Endpoint)
```

### **Issue 3: NetMQ Timeout**

**Symptoms:**
- Dashboard takes forever to load
- Eventually shows zeros
- WebApi logs show timeout errors

**Fix:**
1. Ensure ASPSBackend is running
2. Check firewall isn't blocking localhost:5556
3. Restart both processes

### **Issue 4: Wrong Endpoint Configuration**

**Symptoms:**
- WebApi can't connect
- Connection refused errors

**Fix:**

Check **WebApi/appsettings.json**:
```json
{
  "CQRS": {
    "Endpoint": "tcp://localhost:5556"  // ← Must match ASPSBackend
  }
}
```

Check **ASPSBackend** CQRSGateway starts on same port (5556).

### **Issue 5: Database Empty**

**Symptoms:**
- Both processes running
- Communication working
- Still shows zeros

**Fix:**
```sql
-- Check if data exists
SELECT COUNT(*) FROM Users;
SELECT COUNT(*) FROM UserDevices;
SELECT COUNT(*) FROM DeviceAlerts;

-- If all zero, insert test data
-- Run: populate-test-data.sql
```

---

## 📊 TESTING COMMUNICATION

### **Test 1: Check Endpoints**

```bash
# Terminal 1: Start ASPSBackend
cd ASPSBackend
dotnet run --project ASPSBackend

# Terminal 2: Start WebApi
cd ASPSBackend
dotnet run --project WebApi

# Terminal 3: Check ports
netstat -an | grep 5556  # CQRS Gateway
netstat -an | grep 5001  # WebApi HTTP
```

### **Test 2: Load Dashboard**

1. Open browser: `http://localhost:5001`
2. Watch **ASPSBackend** terminal for messages
3. Watch **WebApi** terminal for logs

**Expected ASPSBackend logs:**
```
Received message: GetDashboardStatsQuery
Sent response: {Success:true,UsersCount:5,...}
```

**Expected WebApi logs:**
```
Loading dashboard via CQRS (NetMQ)
Dashboard loaded: 5 users, 10 devices, 3 alerts
```

### **Test 3: Manual NetMQ Test**

Create a simple test:

```csharp
// TestCQRS.cs
using NetMQ;
using NetMQ.Sockets;

var socket = new RequestSocket();
socket.Connect("tcp://localhost:5556");

var query = "{\"MessageType\":\"Query\",\"QueryType\":\"GetDashboardStatsQuery\"}";
socket.SendFrame(query);

var response = socket.ReceiveFrameString();
Console.WriteLine($"Response: {response}");
```

Run:
```bash
dotnet script TestCQRS.cs
```

Should print response with data.

---

## 🎯 QUICK FIX CHECKLIST

- [ ] **1. Start ASPSBackend**
  ```bash
  dotnet run --project ASPSBackend
  ```

- [ ] **2. Wait for CQRS Gateway**
  ```
  ✓ CQRS Gateway started (tcp://*:5556)
  ```

- [ ] **3. Start WebApi**
  ```bash
  dotnet run --project WebApi
  ```

- [ ] **4. Check both processes running**
  ```bash
  ps aux | grep -E "(ASPSBackend|WebApi)"
  ```

- [ ] **5. Load dashboard**
  ```
  http://localhost:5001
  ```

- [ ] **6. Watch logs**
  - ASPSBackend: Should see "Received message"
  - WebApi: Should see "Dashboard loaded"

- [ ] **7. Verify data appears**
  - User count > 0
  - Device count > 0
  - etc.

---

## 🔍 DEBUG MODE

### **Enable Verbose Logging**

**WebApi/appsettings.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",          // ← Change to Debug
      "WebApi": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  }
}
```

**ASPSBackend/appsettings.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",          // ← Change to Debug
      "Business": "Debug"
    }
  }
}
```

Restart both processes and watch detailed logs.

---

## 📋 EXPECTED FLOW

### **When Dashboard Loads:**

```
1. Browser → GET http://localhost:5001
2. WebApi IndexModel.OnGetAsync() executes
3. Creates GetDashboardStatsQuery
4. CQRSClient sends via NetMQ to tcp://localhost:5556
5. ASPSBackend CQRSGateway receives message
6. Routes to AdminQueryHandlers.HandleAsync()
7. Handler queries database via repositories
8. Returns GetDashboardStatsQueryResult
9. CQRSGateway sends response via NetMQ
10. CQRSClient receives response
11. WebApi displays data in Razor page
```

**Any break in this chain = no data**

---

## ⚠️ ARCHITECTURE REMINDER

```
WebApi (Port 5001)
  ├── NO database ✅
  ├── Uses CQRSClient
  └── Sends queries to port 5556

ASPSBackend (Separate Process)
  ├── CQRSGateway listens on port 5556
  ├── Handlers process queries
  ├── Repositories access database
  └── Returns results via NetMQ
```

**Both processes MUST be running!**

---

## ✅ SUMMARY

**Most Common Issue:** ASPSBackend not running

**Quick Fix:**
1. Start ASPSBackend: `dotnet run --project ASPSBackend`
2. Wait for "CQRS Gateway started"
3. Start WebApi: `dotnet run --project WebApi`
4. Load `http://localhost:5001`

**Verify:**
- Both terminals show activity when dashboard loads
- ASPSBackend logs show "Received message"
- WebApi logs show "Dashboard loaded"
- Data appears on page

**If still no data:** Check database has records, check logs for errors, enable debug logging.
