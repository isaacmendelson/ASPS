# Database Setup Guide - IMPORTANT!

## ⚠️ Database Connection Required

The admin interface requires a database connection to work. You have two options:

---

## **Option 1: Use Your MySQL Database** (Recommended)

### Step 1: Update Connection String

Edit `WebApi/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "server=localhost;port=3306;database=YOUR_DB_NAME;user=root;password=YOUR_PASSWORD;"
  }
}
```

Replace:
- `YOUR_DB_NAME` → Your actual database name (e.g., `aspsbackend`)
- `YOUR_PASSWORD` → Your MySQL password

### Step 2: Verify Database Exists

```bash
mysql -u root -p
```

```sql
SHOW DATABASES LIKE '%asps%';
USE your_database_name;
SHOW TABLES;
```

You should see tables like: `Users`, `UserDevices`, `DeviceAlerts`, etc.

### Step 3: Run

```bash
dotnet run --project WebApi
```

---

## **Option 2: Use In-Memory Database** (For Testing Only)

If you don't have MySQL set up or want to test the UI:

### Step 1: Remove Connection String

In `WebApi/appsettings.json`, remove or comment out the ConnectionStrings section:

```json
{
  "Logging": { ... },
  "AllowedHosts": "*",
  // "ConnectionStrings": { ... },  // Commented out
  "NetMQ": { ... }
}
```

### Step 2: Run

```bash
dotnet run --project WebApi
```

The application will automatically use an in-memory database. 

**Note:** Data will be lost when you stop the application.

---

## **Quick Database Check**

To find your database name:

```bash
mysql -u root -p
```

```sql
SHOW DATABASES;
```

Look for databases like:
- `aspsbackend`
- `ASPSBackend`
- `BackendSystemDb`
- Or any database with your tables

---

## **Connection String Examples**

### Local MySQL
```json
"DefaultConnection": "server=localhost;port=3306;database=aspsbackend;user=root;password=mypassword;"
```

### Remote MySQL
```json
"DefaultConnection": "server=192.168.1.100;port=3306;database=aspsbackend;user=admin;password=mypassword;"
```

### Different Port
```json
"DefaultConnection": "server=localhost;port=3307;database=aspsbackend;user=root;password=mypassword;"
```

---

## **Troubleshooting**

### Error: "Unable to resolve service for type 'IUserRepository'"
**Cause:** Repositories not registered (this is now fixed in Program.cs)

### Error: "Unable to connect to MySQL"
**Solution:** 
1. Verify MySQL is running: `sudo systemctl status mysql`
2. Check password is correct
3. Verify database exists

### Error: "Table 'Users' doesn't exist"
**Solution:** 
1. Run your database creation scripts
2. Or use in-memory database for testing

### Error: "Access denied for user"
**Solution:** Update password in connection string

---

## **What's Been Fixed**

✅ **Program.cs** - Added repository registrations:
```csharp
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserDeviceRepository, UserDeviceRepository>();
builder.Services.AddScoped<IDeviceAlertRepository, DeviceAlertRepository>();
// ... and more
```

✅ **Program.cs** - Added DbContext registration:
```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
```

✅ **appsettings.json** - Added ConnectionStrings section

---

## **Testing the Fix**

After updating the connection string:

```bash
# 1. Build
dotnet build

# 2. Run
dotnet run --project WebApi

# 3. Check console for:
"WebApi started"
"Admin Dashboard: http://localhost:5001"

# 4. Open browser
http://localhost:5001

# 5. You should see:
- Dashboard with real counts
- Working navigation
- No DI errors
```

---

## **Summary**

The error was caused by missing Dependency Injection registrations. This has been fixed by:

1. ✅ Registering all repositories in Program.cs
2. ✅ Registering DbContext in Program.cs  
3. ✅ Adding connection string to appsettings.json

**Next step:** Update your database password in appsettings.json and run!
