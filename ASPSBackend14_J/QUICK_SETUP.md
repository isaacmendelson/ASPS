# Quick Setup Guide - Admin Dashboard

## Prerequisites
✅ Your existing ASPSBackend database is running
✅ MySQL is accessible on localhost:3306

## Setup Steps

### Step 1: Update Connection String

Edit `WebApi/appsettings.json` and update the password:

```json
"ConnectionStrings": {
  "DefaultConnection": "server=localhost;port=3306;database=aspsbackend;user=root;password=YOUR_ACTUAL_PASSWORD;"
}
```

Replace:
- `aspsbackend` with your actual database name
- `YOUR_ACTUAL_PASSWORD` with your MySQL password
- `root` with your MySQL username if different

### Step 2: Check Database Name

Make sure the database name matches what you're currently using. 

Common names:
- `aspsbackend`
- `ASPSBackend`
- `BackendSystemDb`

You can check with:
```sql
SHOW DATABASES;
```

### Step 3: Build

```bash
cd ASPSBackend
dotnet build
```

Should compile with **zero errors**.

### Step 4: Run

```bash
dotnet run --project WebApi
```

Or if you have the ASPSBackend project:
```bash
dotnet run --project ASPSBackend
```

### Step 5: Access Dashboard

Open browser to one of these URLs (check console output):
- **http://localhost:5000**
- **http://localhost:5001**
- **http://localhost:7000**

The exact port depends on your `launchSettings.json`.

## Troubleshooting

### Error: "Connection refused"
**Cause:** Wrong connection string or database not running

**Fix:**
1. Verify MySQL is running:
   ```bash
   sudo systemctl status mysql  # Linux
   mysql.server status          # macOS
   ```

2. Test connection:
   ```bash
   mysql -u root -p
   ```

3. Verify database exists:
   ```sql
   SHOW DATABASES LIKE '%asps%';
   ```

### Error: "Cannot find database"
**Cause:** Wrong database name in connection string

**Fix:** Update database name in `appsettings.json` to match your actual database.

### Error: "Access denied for user"
**Cause:** Wrong password or username

**Fix:** Update username/password in `appsettings.json`.

### Port Already in Use
**Cause:** Another process is using port 5000

**Fix:** 
1. Kill the process:
   ```bash
   lsof -ti:5000 | xargs kill -9  # macOS/Linux
   ```

2. Or change port in `Properties/launchSettings.json`:
   ```json
   "applicationUrl": "http://localhost:5050"
   ```

### Dashboard Shows Zero Counts
**Cause:** Database tables are empty

**Fix:** This is normal if you haven't added data yet. The dashboard works correctly and will show counts when you add:
- Users via your existing API
- Devices via device registration
- Alerts from device messages

## What the Dashboard Shows

The admin dashboard displays:
1. **Total Users** - Count from `Users` table
2. **Total Devices** - Count from `UserDevices` table  
3. **Total Alerts** - Count from `DeviceAlerts` table
4. **Phishing Sites** - Count from `KnownPhishingWebsites` table

## Success Indicators

✅ Console shows "WebApi started"
✅ Browser loads the dashboard page
✅ No error messages in console
✅ Cards show numbers (even if zero)

## Next Steps

Once the dashboard is running:
1. Add test data to see non-zero counts
2. Navigate using the sidebar (features coming soon)
3. Request additional features as needed

## Need Help?

If still not working, provide:
1. Complete error message from console
2. Your connection string (with password removed)
3. Result of `SHOW DATABASES;` command
4. MySQL version: `mysql --version`
