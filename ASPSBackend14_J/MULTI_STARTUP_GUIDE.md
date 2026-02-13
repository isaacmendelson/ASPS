# Multi-Startup Configuration

This solution is configured to start both ASPSBackend and WebApi simultaneously.

## Visual Studio 2022 Setup

1. Right-click on the **Solution** in Solution Explorer
2. Select **Properties** (or **Configure Startup Projects**)
3. Select **Multiple startup projects**
4. For **ASPSBackend**: Set Action to **Start**
5. For **WebApi**: Set Action to **Start**
6. **IMPORTANT**: Use the arrows to ensure **ASPSBackend is listed FIRST**
   - ASPSBackend must start before WebApi
   - ASPSBackend initializes the NetMQ endpoint that WebApi connects to
7. Click **OK**

## Command Line Setup

If running from command line, start in this order:

### Terminal 1: Start ASPSBackend
```bash
cd ASPSBackend
dotnet run
```

Wait for the message: "NetMQ CQRS processor started (tcp://*:5555)"

### Terminal 2: Start WebApi
```bash
cd WebApi
dotnet run
```

## Startup Order is Critical

ASPSBackend MUST start first because:
1. It creates the NetMQ response socket on port 5555
2. WebApi creates a request socket that connects to port 5555
3. If WebApi starts first, it will fail to connect

## Verification

When both are running, you should see:

**ASPSBackend Console:**
```
✓ ASView started
✓ NetMQ CQRS processor started (tcp://*:5555)
✓ Real-time alert listener started (tcp://*:50001)
✓ UDAnalysisManagers initialized
ASPSBackend is running. Press Ctrl+C to exit.
```

**WebApi Console:**
```
WebApi started
Swagger UI: https://localhost:7001/swagger
```

Then open browser to: https://localhost:7001/swagger
