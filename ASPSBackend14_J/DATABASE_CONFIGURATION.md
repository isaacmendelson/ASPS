# Database Configuration - Business Layer ONLY

## ✅ Proper Architecture Implemented

The database configuration is now **ONLY** in the Business layer (ASPSBackend project), not WebApi!

---

## 🎯 Architecture

```
WebApi Layer
  ├── NO database configuration ✅
  ├── NO connection string in appsettings.json ✅
  ├── Loads config from ASPSBackend/appsettings.json ✅
  ├── NO DbContext registration ✅
  ├── NO Repository access ✅
  └── ONLY uses Command/Query Handlers ✅

Business Layer (ASPSBackend project)
  ├── Database configuration in appsettings.json ✅
  ├── Connection string ✅
  ├── DbContext registration ✅
  ├── Repository implementations ✅
  └── Command/Query Handlers ✅
```

---

## 📁 Configuration Files

### **WebApi/appsettings.json** (NO database config)
```json
{
  "Logging": { ... },
  "AllowedHosts": "*",
  "NetMQ": {
    "BusinessEndpoint": "tcp://localhost:5555"
  }
}
```
**Notice:** NO ConnectionStrings section! ✅

### **ASPSBackend/appsettings.json** (HAS database config) ⭐
```json
{
  "Logging": { ... },
  "ConnectionStrings": {
    "DefaultConnection": "server=localhost;port=3306;database=ASPSBackend2DB;user=root;password=YOUR_PASSWORD;"
  },
  "NetMQ": { ... },
  "Python": { ... }
}
```
**This is THE ONLY place to configure database!** ✅

---

## 🔧 How It Works

### **1. WebApi Startup (Program.cs)**
```csharp
// WebApi loads configuration from ASPSBackend project
var businessConfigPath = Path.Combine(
    Directory.GetCurrentDirectory(), 
    "..", 
    "ASPSBackend", 
    "appsettings.json");

var businessConfig = new ConfigurationBuilder()
    .AddJsonFile(businessConfigPath, ...)
    .Build();

var connectionString = businessConfig.GetConnectionString("DefaultConnection");

// Passes connection string to Business layer
builder.Services.AddBusinessServices(connectionString);
```

### **2. Business Service Registration**
```csharp
public static IServiceCollection AddBusinessServices(
    this IServiceCollection services, 
    string connectionString)  // ← Receives connection string
{
    // Registers DbContext (Business layer only)
    services.AddDbContext<AppDbContext>(...);
    
    // Registers Repositories (Business layer only)
    services.AddScoped<IUserRepository, UserRepository>();
    
    // Registers Handlers (WebApi uses these)
    services.AddScoped<AdminQueryHandlers>();
    
    return services;
}
```

### **3. WebApi Pages Use Handlers**
```csharp
public class IndexModel : PageModel
{
    // ✅ ONLY has handler dependency
    private readonly AdminQueryHandlers _queryHandlers;
    
    // ❌ NO repository dependency
    // ❌ NO DbContext dependency
    // ❌ NO connection string
}
```

---

## 🗄️ Database Setup

### **Update Connection String**

Edit **`ASPSBackend/appsettings.json`** (NOT WebApi):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "server=localhost;port=3306;database=YOUR_DB_NAME;user=root;password=YOUR_PASSWORD;"
  }
}
```

Replace:
- `YOUR_DB_NAME` - Your database name
- `YOUR_PASSWORD` - Your MySQL password

**Example:**
```json
"DefaultConnection": "server=localhost;port=3306;database=ASPSBackend2DB;user=root;password=mypassword123;"
```

---

## 📂 File Structure

```
ASPSBackend/
├── ASPSBackend/
│   └── appsettings.json              ← Database config HERE ✅
├── Business/
│   ├── Services/
│   │   └── BusinessServiceRegistration.cs  ← Registers services
│   ├── Data/EF/
│   │   └── Repositories/             ← Repository implementations
│   └── Handlers/                     ← Command/Query handlers
└── WebApi/
    ├── appsettings.json              ← NO database config ✅
    ├── Program.cs                    ← Loads ASPSBackend/appsettings.json
    └── Pages/                        ← Uses handlers only
```

---

## ✅ Benefits

### **1. True Separation of Concerns**
- WebApi = Presentation layer (NO data access)
- Business = Logic + Data access
- Database config isolated in Business layer

### **2. Security**
- WebApi cannot directly access database
- Connection string not in WebApi project
- All data access goes through handlers

### **3. Centralized Configuration**
- ONE place to configure database (ASPSBackend/appsettings.json)
- No duplicate connection strings
- Easy to maintain

### **4. Flexibility**
- Can run WebApi without ASPSBackend project (will fail gracefully)
- Can change database without touching WebApi code
- Can add multiple Business configurations for different environments

---

## 🔍 Verification

### **Check WebApi has NO database config:**
```bash
cat WebApi/appsettings.json | grep -i connection
# Should return nothing ✅
```

### **Check ASPSBackend HAS database config:**
```bash
cat ASPSBackend/appsettings.json | grep -i connection
# Should show connection string ✅
```

### **Check WebApi Program.cs loads from ASPSBackend:**
```bash
cat WebApi/Program.cs | grep businessConfigPath
# Should show it loads from ../ASPSBackend/appsettings.json ✅
```

---

## 🚀 Running the Application

```bash
# 1. Navigate to solution root
cd ASPSBackend

# 2. Update database password in ASPSBackend project
vim ASPSBackend/appsettings.json
# Edit the ConnectionStrings section

# 3. Build
dotnet build

# 4. Run WebApi
dotnet run --project WebApi

# 5. Check console output
# Should see:
# ✓ Loaded configuration from: .../ASPSBackend/appsettings.json
# ✓ Business layer services registered
# ✓ Database: ASPSBackend2DB
```

---

## ⚠️ Troubleshooting

### **Error: "ASPSBackend/appsettings.json not found"**
**Cause:** WebApi cannot find the ASPSBackend configuration file.

**Solution:** Ensure you're running from the solution root and the file exists:
```bash
ls ASPSBackend/appsettings.json
```

### **Error: "ConnectionStrings:DefaultConnection not found"**
**Cause:** The connection string is not configured in ASPSBackend/appsettings.json.

**Solution:** Add the ConnectionStrings section:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "server=localhost;port=3306;database=mydb;user=root;password=mypass;"
  }
}
```

### **Error: "Unable to connect to MySQL server"**
**Cause:** Database credentials are incorrect or MySQL is not running.

**Solution:** 
1. Check MySQL is running: `sudo systemctl status mysql`
2. Verify credentials in ASPSBackend/appsettings.json
3. Test connection: `mysql -u root -p`

---

## 📊 Data Flow

### **Configuration Loading:**
```
1. WebApi starts
2. Reads ../ASPSBackend/appsettings.json
3. Extracts ConnectionStrings:DefaultConnection
4. Passes to BusinessServiceRegistration
5. Business layer registers DbContext with connection string
```

### **Query Flow:**
```
1. User visits /Users
2. WebApi Page creates GetUsersQuery
3. Page calls AdminQueryHandlers.HandleAsync(query)
4. Handler (in Business) uses IUserRepository
5. Repository (in Business) uses DbContext
6. DbContext connects using connection string from ASPSBackend config
7. Result flows back to WebApi Page
```

**WebApi NEVER touches database or connection string directly!** ✅

---

## 🎯 Summary

### **Configuration Location:**
- ✅ **ASPSBackend/appsettings.json** - Database config HERE
- ❌ **WebApi/appsettings.json** - NO database config

### **WebApi Knows:**
- ✅ Where to load Business config (ASPSBackend/appsettings.json)
- ✅ How to create Commands/Queries
- ✅ How to call Handlers
- ❌ Database connection string
- ❌ DbContext
- ❌ Repositories

### **Business Knows:**
- ✅ Database connection string
- ✅ DbContext
- ✅ Repositories
- ✅ Handlers
- ❌ HTTP/UI
- ❌ Razor Pages

**Perfect separation with centralized configuration!** 🎉
