# DeviceKey Column Migration Guide

## ❌ ERROR: "Unknown column 'DeviceKey' in 'field list'"

This error occurs because the `DeviceKey` column doesn't exist in the database yet, but the EF Core model expects it.

---

## ✅ SOLUTION: Run Migration Script

### **Quick Fix:**

```bash
mysql -u root -p ASPSBackend2DB < ADD-DEVICEKEY-COLUMN-SIMPLE.sql
```

Enter your MySQL password when prompted.

---

## 📋 Step-by-Step Instructions

### **Step 1: Verify Current State**

```bash
mysql -u root -p ASPSBackend2DB < VERIFY-DEVICEKEY-COLUMN.sql
```

If DeviceKey column doesn't exist, you'll see empty result set.

### **Step 2: Run Migration**

```bash
mysql -u root -p ASPSBackend2DB < ADD-DEVICEKEY-COLUMN-SIMPLE.sql
```

Expected output:
```
Query OK, 0 rows affected (0.XX sec)
```

### **Step 3: Verify Migration Succeeded**

```bash
mysql -u root -p ASPSBackend2DB < VERIFY-DEVICEKEY-COLUMN.sql
```

Should show:
```
COLUMN_NAME  | COLUMN_TYPE  | IS_NULLABLE | COLUMN_KEY
DeviceKey    | varchar(36)  | YES         | MUL
```

### **Step 4: Restart ASPSBackend**

```bash
# Stop current ASPSBackend process (Ctrl+C)
# Start again
dotnet run --project ASPSBackend
```

### **Step 5: Test Alert Processing**

Send a test alert - it should now save successfully without the error.

---

## 🔧 Manual Migration (MySQL Workbench / Command Line)

If you prefer to run commands manually:

```sql
USE ASPSBackend2DB;

-- Add column
ALTER TABLE DeviceAlerts 
ADD COLUMN DeviceKey VARCHAR(36) NULL;

-- Add index
CREATE INDEX idx_devicealerts_devicekey ON DeviceAlerts(DeviceKey);

-- Verify
DESCRIBE DeviceAlerts;
```

---

## 🗂️ Migration Scripts Reference

### **ADD-DEVICEKEY-COLUMN-SIMPLE.sql**
- Adds `DeviceKey` column
- Creates index
- Optionally adds FK constraint (commented out)

### **VERIFY-DEVICEKEY-COLUMN.sql**
- Checks if column exists
- Shows column details

### **ROLLBACK-DEVICEKEY-COLUMN.sql**
- Removes `DeviceKey` column
- Removes index
- Use if you need to undo the migration

### **ADD-DEVICEKEY-COLUMN.sql** (Original)
- More complex version with FK checking
- Use if you want automatic FK creation

---

## ⚠️ Important Notes

### **1. Foreign Key Constraint (Optional)**

The simple migration script does NOT create the FK constraint by default (it's commented out).

**Why?**
- FK constraints can cause issues if data integrity isn't perfect
- You can add it later after verifying data
- Application works fine without FK constraint (EF Core manages relationships)

**To add FK constraint later:**
```sql
ALTER TABLE DeviceAlerts
ADD CONSTRAINT fk_devicealerts_device
FOREIGN KEY (DeviceKey) REFERENCES UserDevices(`Key`)
ON DELETE SET NULL
ON UPDATE CASCADE;
```

### **2. Existing Alerts**

Existing alerts in the database will have `DeviceKey = NULL` after migration. This is fine because:
- Column is nullable
- Old alerts don't have device reference
- New alerts will populate DeviceKey

### **3. Column Position**

The column is added at the end of the table. This doesn't affect functionality.

---

## 🧪 Testing After Migration

### **Test 1: Verify Column Exists**

```sql
SELECT KeyField, DeviceKey, DeviceUid 
FROM DeviceAlerts 
LIMIT 5;
```

Expected: `DeviceKey` column appears (values likely NULL for old records).

### **Test 2: Insert Test Alert**

Run your Python client or send a test alert. Should succeed without error.

### **Test 3: Check New Alert**

```sql
SELECT KeyField, DeviceKey, DeviceUid, AlertType
FROM DeviceAlerts 
ORDER BY Timestamp DESC 
LIMIT 1;
```

New alerts should have `DeviceKey` populated (if device exists in UserDevices).

---

## 🔄 Rollback (If Needed)

If you encounter issues and need to rollback:

```bash
mysql -u root -p ASPSBackend2DB < ROLLBACK-DEVICEKEY-COLUMN.sql
```

**Warning:** This removes the `DeviceKey` column. You'll need to:
1. Update the code to remove DeviceKey references
2. Rebuild the application
3. Restart

---

## 📊 Database Schema After Migration

```sql
CREATE TABLE DeviceAlerts (
    `Key` VARCHAR(36) PRIMARY KEY,
    UserKey VARCHAR(36),
    DeviceKey VARCHAR(36),        -- ← NEW COLUMN
    DeviceUid VARCHAR(255),
    AlertType VARCHAR(100),
    Priority INT,
    Timestamp DATETIME,
    OperatingSystem INT,
    MAC VARCHAR(50),
    Token VARCHAR(500),
    DeviceType INT,
    Discriminator VARCHAR(50),
    -- Type-specific columns...
    
    INDEX idx_devicealerts_userkey (UserKey),
    INDEX idx_devicealerts_devicekey (DeviceKey),  -- ← NEW INDEX
    INDEX idx_devicealerts_deviceuid (DeviceUid)
);
```

---

## ✅ Quick Checklist

- [ ] Backup database (optional but recommended)
- [ ] Stop ASPSBackend application
- [ ] Run VERIFY script (confirm column doesn't exist)
- [ ] Run ADD-DEVICEKEY-COLUMN-SIMPLE.sql
- [ ] Run VERIFY script again (confirm column exists)
- [ ] Start ASPSBackend application
- [ ] Test alert processing
- [ ] Verify no errors in logs

---

## 🆘 Troubleshooting

### **Error: "Table 'DeviceAlerts' doesn't exist"**
- Database doesn't have DeviceAlerts table
- Run your main database creation script first

### **Error: "Duplicate column name 'DeviceKey'"**
- Column already exists
- Run VERIFY script to check
- No action needed if column exists

### **Error: "Access denied"**
- MySQL user doesn't have ALTER TABLE permissions
- Use root user or user with sufficient privileges

### **Foreign Key Constraint Fails**
- Uncomment FK creation in script only if:
  - All devices referenced in alerts exist in UserDevices
  - Or accept that some alerts will have NULL DeviceKey

---

## 📝 Summary

**Problem:** EF Core expects `DeviceKey` column, but database doesn't have it.

**Solution:** Run migration script to add column.

**Command:**
```bash
mysql -u root -p ASPSBackend2DB < ADD-DEVICEKEY-COLUMN-SIMPLE.sql
```

**Result:** Application can now save alerts with Device relationships.

**Verification:**
```bash
mysql -u root -p ASPSBackend2DB < VERIFY-DEVICEKEY-COLUMN.sql
```
