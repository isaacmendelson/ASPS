# ASPSBackend System2 - Complete Setup Guide

## Prerequisites

- .NET 8 SDK (https://dotnet.microsoft.com/download/dotnet/8.0)
- MySQL Server 8.0+
- Visual Studio 2022 (recommended) or VS Code

## Quick Setup

### 1. Extract Solution
Extract the ZIP file to your desired location.

### 2. Update Connection String
Edit `ASPSBackend/appsettings.json`:
```json
"ConnectionStrings": {
  "DefaultConnection": "server=localhost;port=3306;database=ASPSBackend2DB;user=root;password=YOUR_PASSWORD;"
}
```

### 3. Create Database

**Option A: Using SQL Script (Recommended)**
```bash
mysql -u root -p < create-database.sql
```

**Option B: Using EF Migrations**
```bash
cd ASPSBackend
dotnet ef migrations add InitialCreate --project ../Business/Business.csproj --startup-project ASPSBackend.csproj
dotnet ef database update --project ../Business/Business.csproj --startup-project ASPSBackend.csproj
```

### 4. Restore NuGet Packages
```bash
dotnet restore
```

### 5. Build Solution
```bash
dotnet build
```

### 6. Configure Multi-Startup in Visual Studio

1. Right-click Solution → **Properties**
2. Select **Multiple startup projects**
3. Set both **ASPSBackend** and **WebApi** to **Start**
4. Ensure **ASPSBackend** is listed FIRST (starts before WebApi)
5. Click **OK**

### 7. Run Solution
Press **F5** in Visual Studio, or:

**Terminal 1 (ASPSBackend):**
```bash
cd ASPSBackend
dotnet run
```

**Terminal 2 (WebApi):**
```bash
cd WebApi
dotnet run
```

### 8. Access Swagger UI
Open browser: **https://localhost:7001/swagger**

## Architecture Overview

### Projects Structure

```
ASPSBackend/
├── Common/              # Entities, Enums, Key/Tag system
├── Interface/           # Repository interfaces
├── Business/            # Core logic, EF, NetMQ, Analysis
├── ASPSBackend/         # Main startup (Port 5555 CQRS, Port 50001 Alerts)
└── WebApi/              # REST API (Port 7001)
```

### Communication Flow

```
WebApi (7001) 
    ↓ NetMQ (5555)
ASPSBackend Business Layer
    ↓
MySQL Database

Device Alerts → NetMQ (50001) → UDAnalysisManagers → Analysis
```

### Key Components

**1. Key/Tag Entity System**
- All entities use `Key` (Type/Value/InstanceName)
- `Tag` for classification
- XML serializable

**2. CQRS with NetMQ**
- WebApi sends Commands/Queries via NetMQ (tcp://localhost:5555)
- Business handlers process and return results
- Async messaging pattern

**3. Real-Time Alert Processing**
- NetMQ listener on port 50001
- Receives DeviceAlerts from remote devices
- Fires DeviceAlertReceived events

**4. Analysis System**
- **UDAnalysisManager**: One per active user
- **UDAnalysis**: Analyzes alerts per device
- **Analyzers**: UDRemoteAccessAnalyzer, UDPhishingAnalyzer
- Results stored in AnalysisResultContainer

**5. ASView**
- Loads Users, UserDevices, UserAccounts into memory
- Background service, IDomainEventHandler

## API Endpoints

### Users
- `GET /api/users` - Get all users
- `GET /api/users/{keyType}/{keyValue}` - Get user by key
- `GET /api/users/{keyType}/{keyValue}/details` - Get user with devices & accounts
- `POST /api/users` - Create user
- `PUT /api/users/{keyType}/{keyValue}` - Update user
- `DELETE /api/users/{keyType}/{keyValue}` - Delete user (soft)

### UserDevices
- `POST /api/userdevices` - Create device
- `PUT /api/userdevices/{keyType}/{keyValue}` - Update device
- `DELETE /api/userdevices/{keyType}/{keyValue}` - Delete device (soft)

## Testing the System

### 1. Create a User
```bash
curl -X POST https://localhost:7001/api/users \
  -H "Content-Type: application/json" \
  -d '{
    "keycloakUserId": "test-user-123",
    "firstName": "Test",
    "lastName": "User",
    "email": "test@example.com",
    "role": 1
  }'
```

### 2. Get User Details
```bash
curl https://localhost:7001/api/users/User/user-001/details
```

### 3. Send Device Alert (Simulated)
```bash
# Using NetMQ client (Python example)
import zmq
import json

context = zmq.Context()
socket = context.socket(zmq.PUSH)
socket.connect("tcp://localhost:50001")

alert = {
    "AlertType": "RemoteAccessAlert",
    "Priority": 2,
    "DeviceInfo": {
        "DeviceUid": "device-001",
        "IP": "192.168.1.100"
    },
    "RemoteAccessApp": 1,
    "RunningProcesses": 1,
    "ConnectionStatus": 1
}

socket.send_json(alert)
```

## Database Entities

### Core Entities
- **User**: Key, Tag, KeycloakUserId, FirstName, LastName, Role
- **UserDevice**: Abstract (PersonalComputer, SmartPhone)
- **UserAccount**: Key, UserKey, AccountType, LoginUrl
- **AnalysisResultContainer**: Analysis results storage
- **AlertFlag**: Alert tracking

### Key/Tag System
- **Key**: Type, Value, InstanceName
- **Tag**: Key, Name, Type, BaseType
- All entities inherit from Entity (has Key and Tag)

## Troubleshooting

### Port Already in Use
If ports 5555, 50001, or 7001 are in use, update:
- `ASPSBackend/appsettings.json`: NetMQ ports
- `WebApi/Properties/launchSettings.json`: WebApi port

### Database Connection Failed
- Verify MySQL is running: `mysql -u root -p`
- Check connection string in `appsettings.json`
- Ensure database exists: `SHOW DATABASES;`

### NetMQ Connection Timeout
- Ensure ASPSBackend starts BEFORE WebApi
- Check firewall settings for ports 5555 and 50001

### EF Migrations Error
- Install dotnet-ef: `dotnet tool install --global dotnet-ef`
- Or use the provided SQL script: `create-database.sql`

## Advanced Configuration

### Adding New Analyzers
1. Create class implementing `ISpecificAnalyzer`
2. Add to `UDAnalysisManager` constructor
3. Implement `CanAnalyze` and `AnalyzeAsync` methods

### Adding New Alert Types
1. Create class inheriting from `DeviceAlert`
2. Add to `Common/Models/Alerts/`
3. Update `RealTimeAlertListener` deserialization

### Custom Commands/Queries
1. Create in `Business/Commands/` or `Business/Queries/`
2. Create handler in `Business/Handlers/`
3. Update `NetMQMessageProcessor` routing
4. Add controller endpoint in WebApi

## Production Deployment

1. **Update Connection Strings** - Use production database
2. **Configure Ports** - Ensure firewall rules allow NetMQ ports
3. **Enable HTTPS** - Configure SSL certificates
4. **Logging** - Configure production logging (Serilog, etc.)
5. **Monitoring** - Add health checks and metrics
6. **Security** - Implement authentication/authorization

## Support

For issues, check:
- Logs in console output
- `IMPLEMENTATION_STATUS.md` for feature status
- `README.md` for architecture overview
