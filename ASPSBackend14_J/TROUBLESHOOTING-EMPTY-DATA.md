# ASView Empty Data - Troubleshooting Guide

## Quick Diagnosis

Run these commands in order to identify the issue:

### Step 1: Verify Database Has Data
```bash
mysql -u root -p < DEBUG-FULL-DATABASE.sql
```

**Expected output:**
- Users: 2 active records
- UserDevices: 4 active records
- Discriminators: PC (2), Phone (2)

**If you see 0 records:** Run `create-database.sql` to populate data.

### Step 2: Fix Discriminator Values (if needed)
```bash
mysql -u root -p < FIX-DISCRIMINATORS.sql
```

This will convert:
- `PersonalComputer` → `PC`
- `SmartPhone` → `Phone`

### Step 3: Verify Connection String

Check `ASPSBackend/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "server=localhost;port=3306;database=ASPSBackend2DB;user=root;password=YOUR_ACTUAL_PASSWORD;"
  }
}
```

**IMPORTANT:** Replace `YOUR_ACTUAL_PASSWORD` with your MySQL root password!

### Step 4: Check Application Logs

When you run the application, you should see:

```
ASView LoadDataAsync starting...
Fetching users from repository...
=== Repository<User>.GetAllAsync START ===
Total records in database table: 2
Records loaded from DB: 2
  Item: Key=User|user-001|, IsDeleted=False
  Item: Key=User|user-002|, IsDeleted=False
Records after IsDeleted filter: 2
=== Repository<User>.GetAllAsync END ===
Users fetched: 2 records

Fetching devices from repository...
=== Repository<UserDevice>.GetAllAsync START ===
Total records in database table: 4
Records loaded from DB: 4
  Item: Key=Device|john-pc-001|, IsDeleted=False
  ...
Records after IsDeleted filter: 4
=== Repository<UserDevice>.GetAllAsync END ===
Devices fetched: 4 records

ASView loaded: 2 users, 4 devices, 0 accounts
```

## Common Issues & Solutions

### Issue 1: "Total records in database table: 0"

**Cause:** Database is empty

**Solution:**
```bash
mysql -u root -p < create-database.sql
```

### Issue 2: "Records loaded from DB: 0" (but Total > 0)

**Cause:** Discriminator mismatch or wrong connection

**Solution:**
```bash
# Fix discriminators
mysql -u root -p < FIX-DISCRIMINATORS.sql

# Verify
mysql -u root -p ASPSBackend2DB -e "SELECT Discriminator, COUNT(*) FROM UserDevices GROUP BY Discriminator;"
```

Expected output:
```
+---------------+----------+
| Discriminator | COUNT(*) |
+---------------+----------+
| PC            |        2 |
| Phone         |        2 |
+---------------+----------+
```

### Issue 3: "Cannot connect to database"

**Possible causes:**
1. MySQL server not running
2. Wrong password in connection string
3. Database doesn't exist

**Solution:**
```bash
# Check MySQL is running
systemctl status mysql    # Linux
# or
services.msc             # Windows

# Test connection
mysql -u root -p -e "SHOW DATABASES;"

# Create database if missing
mysql -u root -p < create-database.sql
```

### Issue 4: Swagger shows empty users

**Cause:** Same as ASView - repositories returning empty

**Solution:** Follow steps 1-3 above

## Manual Database Check

Connect to MySQL and run:

```sql
USE ASPSBackend2DB;

-- Check users
SELECT * FROM Users WHERE IsDeleted = 0;

-- Check devices
SELECT * FROM UserDevices WHERE IsDeleted = 0;

-- Check discriminators
SELECT Discriminator, COUNT(*) FROM UserDevices GROUP BY Discriminator;
```

## Reset Everything (Nuclear Option)

If nothing works, completely reset:

```bash
# 1. Drop and recreate database
mysql -u root -p -e "DROP DATABASE ASPSBackend2DB;"
mysql -u root -p < create-database.sql

# 2. Verify data
mysql -u root -p < DEBUG-FULL-DATABASE.sql

# 3. Restart application
```

## Expected Console Output

When the application starts correctly, you should see:

```
========================================
ASPSBackend System2 Starting...
========================================
ASView LoadDataAsync starting...
Fetching users from repository...
=== Repository<User>.GetAllAsync START ===
Total records in database table: 2
Records loaded from DB: 2
Records after IsDeleted filter: 2
=== Repository<User>.GetAllAsync END ===
Users fetched: 2 records

Fetching devices from repository...
=== Repository<UserDevice>.GetAllAsync START ===
Total records in database table: 4
Records loaded from DB: 4
Records after IsDeleted filter: 4
=== Repository<UserDevice>.GetAllAsync END ===
Devices fetched: 4 records

Fetching accounts from repository...
=== Repository<UserAccount>.GetAllAsync START ===
Total records in database table: 0
Records loaded from DB: 0
Records after IsDeleted filter: 0
=== Repository<UserAccount>.GetAllAsync END ===
Accounts fetched: 0 records

ASView data loaded: 2 users, 4 devices, 0 accounts
========================================
✓ ASView started
✓ NetMQ CQRS processor started (tcp://*:5555)
✓ Real-time alert listener started (tcp://*:50001, Mode: Rep)
✓ UDAnalysisManagers initialized
========================================
```

## Still Not Working?

If you've tried everything and it's still not working:

1. **Check the exact SQL being executed** - look for EF Core SQL logs in console
2. **Verify MySQL version** - run `SELECT VERSION();`
3. **Check MySQL permissions** - ensure root user can access ASPSBackend2DB
4. **Look for errors in console** - any red error messages
5. **Test with standalone program**:
   ```bash
   # Edit TestDatabaseConnection.cs with your password
   # Then compile and run it separately
   ```

## Contact Points

- Connection String: `appsettings.json`
- DbContext Config: `Program.cs` line 66
- ASView Loading: `Business/Views/ASView.cs`
- Repository: `Business/Data/EF/Repositories/Repository.cs`
