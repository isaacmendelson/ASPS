# ASPSBackend System2 - Complete Solution

## 🎯 Overview

A comprehensive .NET 8 backend system for device monitoring, real-time alert processing, and user domain analysis. Built with CQRS, NetMQ messaging, Entity Framework, and MySQL.

## 📁 Solution Structure

```
ASPSBackend/
├── Common/              # Shared entities, enums, Key/Tag system, CQRS base
├── Interface/           # Repository and service interfaces  
├── Business/            # Core business logic, EF repositories, NetMQ, analyzers
├── ASPSBackend/         # Main startup project (Console app)
└── WebApi/              # REST API (ASP.NET Core)
```

## ✨ Key Features

### 1. Key/Tag Entity System
- All entities use `Key` (Type/Value/InstanceName) as primary identifier
- `Tag` system for entity classification and metadata
- XML serializable Keys for interoperability
- EF value converters for seamless database storage

### 2. CQRS with NetMQ
- WebApi sends Commands/Queries to Business via NetMQ (tcp://localhost:5555)
- Command/Query handlers in Business layer
- Asynchronous message processing
- Clean separation of read and write operations

### 3. Real-Time Alert Processing  
- NetMQ listener on port 50001 for device alerts
- Multiple alert types: RemoteAccessAlert, UrlAlert
- Domain event system (DeviceAlertReceived)
- In-memory alert repository for fast access

### 4. User Domain Analysis System
- **UDAnalysisManager**: Background service per active user
- **UDAnalysis**: Analyzes alerts for each device
- **Specific Analyzers**: 
  - UDRemoteAccessAnalyzer (detects remote access sessions)
  - UDPhishingAnalyzer (detects malicious URLs)
- **AnalysisResultContainer**: Stores analysis results in database
- **AlertFlags**: Tracks generated security flags

### 5. ASView (In-Memory Cache)
- Loads Users, UserDevices, UserAccounts into memory on startup
- IDomainEventHandler - responds to domain events
- IBackgroundTask - runs continuously
- High-performance data access

## 🗄️ Database Entities

### Core Entities (All use Key/Tag system)
- **User**: KeycloakUserId, FirstName, LastName, Role, Guardian relationship
- **UserDevice** (abstract): DeviceUid, MonitoringStatus
  - **PersonalComputer**: MotherboardSerial, BiosSerial, UserAgent
  - **SmartPhone**: PhoneNumber
- **UserAccount**: AccountType, LoginUrl, UserName, 2FA support
- **AnalysisResultContainer**: JSON storage, error handling
  - **UrlAnalysisResultContainer**: Domain, Url specific analysis
- **AlertFlag**: Tracks security alerts (Open/Closed status)

### Supporting Classes
- **Key**: Type, Value, InstanceName (IEquatable, IXmlSerializable)
- **Tag**: Key, Name, Type, BaseType (IEquatable)
- **DeviceInfo**: DeviceUid, IP, UserAgent, OperatingSystem
- **DeviceAlert**: Priority, DeviceInfo, Timestamp
  - **RemoteAccessAlert**: App type, connection status, session info
  - **UrlAlert**: URL, Trackers, IFrames

## 🚀 Quick Start

### Prerequisites
- .NET 8 SDK
- MySQL 8.0+
- Visual Studio 2022 (or VS Code)

### Setup Steps

1. **Extract ZIP** to your location

2. **Update Connection String**  
   Edit `ASPSBackend/appsettings.json`:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "server=localhost;port=3306;database=ASPSBackend2DB;user=root;password=YOUR_PASSWORD;"
   }
   ```

3. **Create Database**
   ```bash
   mysql -u root -p < create-database.sql
   ```

4. **Restore & Build**
   ```bash
   dotnet restore
   dotnet build
   ```

5. **Configure Multi-Startup** (Visual Studio)
   - Right-click Solution → Properties
   - Multiple startup projects
   - Set ASPSBackend and WebApi to Start
   - ASPSBackend MUST be first

6. **Run**
   ```bash
   Press F5 in Visual Studio
   ```
   OR manually:
   ```bash
   # Terminal 1
   cd ASPSBackend && dotnet run
   
   # Terminal 2 (wait for ASPSBackend to start)
   cd WebApi && dotnet run
   ```

7. **Access Swagger UI**
   ```
   https://localhost:7001/swagger
   ```

## 📡 Architecture

### Communication Flow

```
Device → NetMQ (50001) → RealTimeAlertListener
                              ↓
                         DeviceAlertReceived Event
                              ↓
                         UDAnalysisManager
                              ↓
                         UDAnalysis + Analyzers
                              ↓
                         AlertFlags + Results

WebApi (7001) → NetMQ (5555) → Business Layer → MySQL
```

### Ports
- **5555**: NetMQ CQRS (Commands/Queries)
- **50001**: NetMQ Real-time Alerts
- **7001**: WebApi (HTTPS)
- **5001**: WebApi (HTTP)
- **3306**: MySQL Database

## 🔌 API Endpoints

### Users
```
GET    /api/users                              # List all users
GET    /api/users/{keyType}/{keyValue}         # Get user by key
GET    /api/users/{keyType}/{keyValue}/details # Get user with devices & accounts
POST   /api/users                              # Create user
PUT    /api/users/{keyType}/{keyValue}         # Update user  
DELETE /api/users/{keyType}/{keyValue}         # Delete user (soft)
```

### UserDevices
```
POST   /api/userdevices                        # Create device
PUT    /api/userdevices/{keyType}/{keyValue}   # Update device
DELETE /api/userdevices/{keyType}/{keyValue}   # Delete device (soft)
```

## 📝 Example Usage

### Create a User
```bash
curl -X POST https://localhost:7001/api/users \
  -H "Content-Type: application/json" \
  -d '{
    "keycloakUserId": "user-123",
    "firstName": "John",
    "lastName": "Doe",
    "email": "john@example.com",
    "role": 1
  }'
```

### Get User with Details
```bash
curl https://localhost:7001/api/users/User/user-001/details
```

### Send Device Alert (Python example)
```python
import zmq, json

context = zmq.Context()
socket = context.socket(zmq.PUSH)
socket.connect("tcp://localhost:50001")

alert = {
    "AlertType": "RemoteAccessAlert",
    "Priority": 2,
    "DeviceInfo": {"DeviceUid": "device-001"},
    "RemoteAccessApp": 1,
    "ConnectionStatus": 1
}

socket.send_json(alert)
```

## 🏗️ Technology Stack

- **.NET 8** - Latest LTS framework
- **Entity Framework Core 8.0.2** - ORM
- **Pomelo.EntityFrameworkCore.MySql 8.0.2** - MySQL provider
- **NetMQ 4.0.1.13** - Messaging (ZeroMQ for .NET)
- **Newtonsoft.Json 13.0.3** - JSON serialization
- **Swashbuckle 6.5.0** - Swagger/OpenAPI
- **MySQL 8.0+** - Database

## 📚 Documentation

- **SETUP_GUIDE.md** - Detailed setup instructions
- **MULTI_STARTUP_GUIDE.md** - Multi-project startup configuration
- **IMPLEMENTATION_STATUS.md** - Complete feature checklist
- **create-database.sql** - Database creation script

## 🔐 Security Features

- Soft delete pattern (data never physically removed)
- Device monitoring status control
- Alert severity levels
- User guardian relationships
- 2FA support for accounts

## 🎨 Design Patterns

- **Repository Pattern** - Data access abstraction
- **CQRS** - Command Query Responsibility Segregation  
- **Domain Events** - Decoupled event handling
- **Background Services** - Long-running tasks
- **Dependency Injection** - Loose coupling
- **TPH (Table Per Hierarchy)** - UserDevice inheritance
- **Value Converters** - Key/Tag database storage

## 📈 Scalability

- In-memory caching (ASView)
- Async message processing
- Per-user analysis managers
- Horizontal scaling ready (stateless WebApi)

## 🛠️ Extending the System

### Add New Analyzer
```csharp
public class MyCustomAnalyzer : ISpecificAnalyzer
{
    public bool CanAnalyze(DeviceAlert alert) { /* ... */ }
    public Task<AnalyzerResult> AnalyzeAsync(DeviceAlert alert, List<DeviceAlert> history) { /* ... */ }
}
```

### Add New Command
```csharp
// 1. Create command in Business/Commands/
public class MyCommand : Command { }

// 2. Create handler
public class MyCommandHandler
{
    public async Task<MyCommandResult> HandleAsync(MyCommand cmd) { /* ... */ }
}

// 3. Register in NetMQMessageProcessor
// 4. Add controller endpoint
```

## 🐛 Troubleshooting

See **SETUP_GUIDE.md** for common issues and solutions.

## 📄 License

This is a complete solution ready for development and deployment.

