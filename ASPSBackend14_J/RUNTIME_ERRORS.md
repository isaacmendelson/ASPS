# Runtime Error Troubleshooting

## Common Runtime Errors & Solutions

### 1. MySQL Connection Errors

#### Error: "Unable to connect to any of the specified MySQL hosts"
**Cause:** MySQL not running or wrong connection string

**Solution:**
```bash
# Check if MySQL is running
sudo systemctl status mysql      # Linux
mysql.server status             # macOS
net start MySQL                 # Windows

# Test connection manually
mysql -u root -p

# If connection works, update appsettings.json with correct:
# - server (usually localhost)
# - port (usually 3306)
# - database name
# - username
# - password
```

#### Error: "Access denied for user 'root'@'localhost'"
**Cause:** Wrong password in connection string

**Solution:**
Update `WebApi/appsettings.json` with correct password:
```json
"DefaultConnection": "server=localhost;port=3306;database=aspsbackend;user=root;password=CORRECT_PASSWORD;"
```

#### Error: "Unknown database 'aspsbackend'"
**Cause:** Database doesn't exist with that name

**Solution:**
Find your actual database name:
```sql
mysql -u root -p
SHOW DATABASES;
```

Then update `appsettings.json` with the correct database name.

---

### 2. DbContext / Entity Framework Errors

#### Error: "No database provider has been configured"
**Cause:** Missing DbContext registration in Program.cs

**Solution:**
Ensure Program.cs has:
```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
```

#### Error: "Table 'Users' doesn't exist"
**Cause:** Database tables haven't been created

**Solution:**
Run your existing migration scripts or create tables:
```bash
# If you have migrations
dotnet ef database update

# Or run your SQL scripts
mysql -u root -p aspsbackend < create-database.sql
```

---

### 3. Razor Pages Errors

#### Error: "The view 'Index' was not found"
**Cause:** Razor Pages not properly configured

**Solution:**
Ensure Program.cs has:
```csharp
// In services section
builder.Services.AddRazorPages();

// In app configuration section
app.MapRazorPages();
```

#### Error: "404 Not Found" when accessing root
**Cause:** No default route configured

**Solution:**
Access explicitly: `http://localhost:5000/Index`

Or add to Program.cs:
```csharp
app.MapGet("/", () => Results.Redirect("/Index"));
```

---

### 4. Port / Network Errors

#### Error: "Address already in use"
**Cause:** Port 5000 already taken

**Solution:**
```bash
# Find and kill process using port
lsof -ti:5000 | xargs kill -9  # macOS/Linux
netstat -ano | findstr :5000   # Windows (note PID, then: taskkill /PID xxxx /F)

# Or change port in Properties/launchSettings.json
```

#### Error: "Connection refused" in browser
**Cause:** App not actually running or wrong URL

**Solution:**
1. Check console output for actual URL
2. Look for "Now listening on: http://localhost:XXXX"
3. Use that exact URL

---

### 5. SignalR Errors

#### Error: "Failed to start the connection"
**Cause:** SignalR hub not mapped correctly

**Solution:**
Ensure Program.cs has:
```csharp
app.MapHub<WebApi.Hubs.NotificationsHub>("/notificationshub");
```

---

## Diagnostic Steps

### Step 1: Check Application Starts
Look for this in console:
```
========================================
WebApi started
Admin Dashboard: http://localhost:5000
========================================
```

### Step 2: Check Database Connection
Add this temporary code to Program.cs (before app.Run()):
```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.CanConnect();
        Console.WriteLine("✅ Database connection successful!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Database connection failed: {ex.Message}");
    }
}
```

### Step 3: Test URL
```bash
curl http://localhost:5000
# Should return HTML, not error
```

### Step 4: Check Logs
Look at console output for detailed error messages. They usually indicate:
- Connection string issues
- Missing tables
- Permission problems

---

## Quick Fixes Checklist

Before asking for help, verify:

- [ ] MySQL is running
- [ ] Connection string has correct password
- [ ] Database name matches what's in MySQL
- [ ] Database has tables (run `SHOW TABLES;`)
- [ ] Port is not in use by another process
- [ ] Console shows "WebApi started" message
- [ ] No red error messages in console

---

## Get Detailed Error Info

If you see a runtime error:

1. **Copy the FULL error message** from console
2. **Note the error type** (e.g., MySqlException, InvalidOperationException)
3. **Check the stack trace** for the first line in YOUR code
4. **Provide these details** for accurate help

Example good error report:
```
Error Type: MySql.Data.MySqlClient.MySqlException
Message: Table 'aspsbackend.Users' doesn't exist
Stack Trace: at Business.Data.EF.AppDbContext...
Connection String: server=localhost;database=aspsbackend;user=root
```

---

## Still Not Working?

Provide:
1. Complete error message (copy/paste from console)
2. Your connection string (remove password)
3. Result of `SHOW TABLES;` in MySQL
4. .NET version: `dotnet --version`
5. MySQL version: `mysql --version`

This will help diagnose the issue quickly!
