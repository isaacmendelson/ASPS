# CQRS Architecture - Clean Separation of Concerns

## ✅ Architecture Overview

Your solution now follows a **proper CQRS pattern** with clean separation between layers:

```
WebApi Layer (Presentation)
    ↓ Sends Commands/Queries
Business Layer (Handlers)
    ↓ Uses Repositories
Data Layer (Repositories)
    ↓ Access Database
Database (MySQL/EF Core)
```

## 🎯 Key Principle

**WebApi NEVER accesses repositories directly.**
**Only Business layer handlers access repositories.**

---

## 📊 Data Flow

### **Read Operations (Queries)**

```
1. User visits /Users page
2. WebApi → Creates GetUsersWithDeviceCountsQuery
3. WebApi → Calls AdminQueryHandlers.HandleAsync(query)
4. Business → Handler uses repositories to get data
5. Business → Returns QueryResult with data
6. WebApi → Displays data in view
```

### **Write Operations (Commands)**

```
1. User clicks "Create User"
2. WebApi → Creates CreateUserAdminCommand
3. WebApi → Calls AdminCommandHandlers.HandleAsync(command)
4. Business → Handler uses repositories to save data
5. Business → Returns CommandResult with success/error
6. WebApi → Redirects or shows message
```

---

## 📁 File Structure

### **Commands & Queries (Business/)**

```
Business/
├── Commands/
│   ├── UserCommands.cs         (CreateUser, UpdateUser, DeleteUser)
│   └── AdminCommands.cs         (CreateUserAdminCommand - new!)
├── Queries/
│   ├── UserQueries.cs          (GetAllUsers, GetUserByKey)
│   └── AdminQueries.cs         (Dashboard, Devices, Alerts - new!)
└── Handlers/
    ├── UserCommandHandlers.cs  (Handles user commands)
    ├── UserQueryHandlers.cs    (Handles user queries)
    ├── AdminCommandHandlers.cs (Handles admin commands - new!)
    └── AdminQueryHandlers.cs   (Handles admin queries - new!)
```

### **WebApi Pages**

```
WebApi/Pages/
├── Index.cshtml.cs           → Uses AdminQueryHandlers
├── Users/Index.cshtml.cs     → Uses AdminQueryHandlers + AdminCommandHandlers
├── Devices/Index.cshtml.cs   → Uses AdminQueryHandlers
└── DeviceAlerts/Index.cshtml.cs → Uses AdminQueryHandlers
```

---

## 🔧 Implementation Details

### **WebApi Dependencies (Program.cs)**

```csharp
// ✅ Repositories registered (but NOT exposed to WebApi code)
builder.Services.AddScoped<IUserRepository, UserRepository>();
// ... other repositories

// ✅ Handlers registered (WebApi uses ONLY these)
builder.Services.AddScoped<UserCommandHandlers>();
builder.Services.AddScoped<UserQueryHandlers>();
builder.Services.AddScoped<AdminCommandHandlers>();
builder.Services.AddScoped<AdminQueryHandlers>();
```

### **WebApi Page Example (Dashboard)**

```csharp
public class IndexModel : PageModel
{
    // ✅ Injects HANDLER, not repository
    private readonly AdminQueryHandlers _queryHandlers;

    public async Task OnGetAsync()
    {
        // ✅ Creates query
        var query = new GetDashboardStatsQuery();
        
        // ✅ Sends to Business layer
        var result = await _queryHandlers.HandleAsync(query);
        
        // ✅ Uses result
        if (result.Success)
        {
            UsersCount = result.UsersCount;
            // ...
        }
    }
}
```

### **Business Handler Example**

```csharp
public class AdminQueryHandlers
{
    // ✅ Handler injects repositories
    private readonly IUserRepository _userRepository;
    // ...

    public async Task<GetDashboardStatsQueryResult> HandleAsync(GetDashboardStatsQuery query)
    {
        // ✅ Handler accesses database via repositories
        var users = await _userRepository.GetAllAsync();
        
        // ✅ Returns result
        return new GetDashboardStatsQueryResult
        {
            Success = true,
            UsersCount = users.Count()
        };
    }
}
```

---

## 📋 Available Queries

### **Dashboard**
- `GetDashboardStatsQuery` → Returns counts for all entities

### **Users**
- `GetAllUsersQuery` → Returns all users
- `GetUsersWithDeviceCountsQuery` → Returns users with device counts
- `GetUserByKeyQuery` → Returns single user
- `GetUserDetailsQuery` → Returns user with details
- `GetUserDevicesQuery` → Returns user's devices

### **Devices**
- `GetAllDevicesQuery` → Returns all devices
- `GetDevicesByUserQuery` → Returns devices for a user

### **Alerts**
- `GetRecentAlertsQuery` → Returns alerts within time range
- `GetAlertsByDeviceQuery` → Returns alerts for a device

### **Phishing**
- `GetAllPhishingWebsitesQuery` → Returns all phishing sites

---

## 📋 Available Commands

### **Users**
- `CreateUserCommand` → Create basic user
- `CreateUserAdminCommand` → Create user with all fields
- `UpdateUserCommand` → Update user
- `DeleteUserCommand` → Delete user

---

## ✅ Benefits of This Architecture

### **1. Separation of Concerns**
- WebApi = Presentation only
- Business = Logic + data access
- Clean boundaries

### **2. Testability**
- Easy to test handlers in isolation
- Mock repositories in tests
- WebApi tests don't need database

### **3. Maintainability**
- Business logic in one place
- Changes to data access don't affect WebApi
- Clear data flow

### **4. Flexibility**
- Easy to add caching in handlers
- Easy to add validation
- Easy to add authorization
- Easy to switch data sources

### **5. Scalability**
- Handlers can be moved to separate service
- Can add message queue between layers
- Can implement CQRS with different read/write stores

---

## 🔄 Adding New Features

### **To Add a New Query:**

1. **Create Query class** in `Business/Queries/`:
```csharp
public class GetSomethingQuery : Query
{
    public int SomeParameter { get; set; }
}

public class GetSomethingQueryResult : QueryResult
{
    public List<Something> Items { get; set; } = new();
}
```

2. **Add Handler method** in appropriate handler:
```csharp
public async Task<GetSomethingQueryResult> HandleAsync(GetSomethingQuery query)
{
    var items = await _repository.GetSomethingAsync(query.SomeParameter);
    return new GetSomethingQueryResult
    {
        Success = true,
        Items = items.ToList()
    };
}
```

3. **Use in WebApi**:
```csharp
var query = new GetSomethingQuery { SomeParameter = 123 };
var result = await _queryHandlers.HandleAsync(query);
```

### **To Add a New Command:**

1. **Create Command class** in `Business/Commands/`:
```csharp
public class DoSomethingCommand : Command
{
    public string Data { get; set; } = string.Empty;
}

public class DoSomethingCommandResult : CommandResult
{
    // Additional result data if needed
}
```

2. **Add Handler method**:
```csharp
public async Task<DoSomethingCommandResult> HandleAsync(DoSomethingCommand command)
{
    // Business logic here
    await _repository.SaveAsync(something);
    
    return new DoSomethingCommandResult
    {
        Success = true,
        Message = "Done!"
    };
}
```

3. **Use in WebApi**:
```csharp
var command = new DoSomethingCommand { Data = "test" };
var result = await _commandHandlers.HandleAsync(command);
```

---

## 🎯 Summary

**WebApi Responsibilities:**
- ✅ Receive HTTP requests
- ✅ Create Commands/Queries
- ✅ Call Handlers
- ✅ Display results
- ❌ NEVER access repositories
- ❌ NEVER access database

**Business Responsibilities:**
- ✅ Receive Commands/Queries
- ✅ Execute business logic
- ✅ Access repositories
- ✅ Return results
- ❌ NEVER know about HTTP/UI

**Clean separation maintained!** 🎉
