# Database Migration Guide

## Methods to Update Database Structure

### Method 1: Drop and Recreate Table (Development Only - DATA LOSS)

**Use when:** You're in development and can lose existing alert data.

```sql
USE ASPSBackend2DB;

-- Drop the table
DROP TABLE IF EXISTS DeviceAlerts;

-- Recreate with new structure
-- (Run the CREATE TABLE statement from RESET-DATABASE.sql or add-device-alerts-table.sql)
```

**Quick command:**
```bash
mysql -u root -p < add-device-alerts-table.sql
```

---

### Method 2: ALTER TABLE (Production Safe - PRESERVES DATA)

**Use when:** You need to keep existing data in production.

#### Add New Column
```sql
USE ASPSBackend2DB;

-- Example: Add a new column
ALTER TABLE DeviceAlerts 
ADD COLUMN NewColumnName VARCHAR(255) NULL
AFTER ExistingColumn;

-- Example: Add index
ALTER TABLE DeviceAlerts
ADD INDEX idx_new_column (NewColumnName);
```

#### Modify Existing Column
```sql
-- Change column type
ALTER TABLE DeviceAlerts
MODIFY COLUMN ColumnName VARCHAR(500) NOT NULL;

-- Change column name
ALTER TABLE DeviceAlerts
CHANGE COLUMN OldName NewName VARCHAR(500) NOT NULL;
```

#### Drop Column
```sql
ALTER TABLE DeviceAlerts
DROP COLUMN ColumnName;
```

---

### Method 3: EF Core Migrations (Recommended for Complex Changes)

**Note:** We're not currently using EF Core migrations in this project (we use manual SQL scripts), but here's how you would if needed:

#### Step 1: Enable Migrations
```bash
cd ASPSBackend/Business
dotnet ef migrations add AddDeviceAlertsTable --startup-project ../ASPSBackend
```

#### Step 2: Apply Migration
```bash
dotnet ef database update --startup-project ../ASPSBackend
```

#### Step 3: Generate SQL Script
```bash
dotnet ef migrations script --startup-project ../ASPSBackend -o migration.sql
```

---

### Method 4: Migration Script with Data Preservation

**Use when:** You need to change structure while keeping data.

```sql
USE ASPSBackend2DB;

START TRANSACTION;

-- Step 1: Create new table with updated structure
CREATE TABLE DeviceAlerts_New (
    `Key` VARCHAR(500) NOT NULL PRIMARY KEY,
    Discriminator VARCHAR(50) NOT NULL,
    -- ... new structure with all fields
    -- Add your new columns here
    NewColumn VARCHAR(255) NULL,
    INDEX idx_deviceuid (DeviceUid),
    INDEX idx_timestamp (`Timestamp`),
    INDEX idx_userkey (UserKey)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Step 2: Copy data from old table to new table
INSERT INTO DeviceAlerts_New (
    `Key`, Discriminator, AlertType, Priority, `Timestamp`, Token,
    DeviceUid, DeviceType, OperatingSystem, MAC, UserKey,
    -- Map old columns to new columns
    RemoteAccessApp, RunningProcesses, ConnectionUrl, ConnectionStatus,
    ConnectionsCount, SessionStatus,
    Url, TrackerKeys, IFrameDomains, UserAgent,
    DateCreated, IsDeleted, IsDisabled
)
SELECT 
    `Key`, Discriminator, AlertType, Priority, `Timestamp`, Token,
    DeviceUid, DeviceType, OperatingSystem, MAC, UserKey,
    RemoteAccessApp, RunningProcesses, ConnectionUrl, ConnectionStatus,
    ConnectionsCount, SessionStatus,
    Url, TrackerKeys, IFrameDomains, UserAgent,
    DateCreated, IsDeleted, IsDisabled
FROM DeviceAlerts;

-- Step 3: Drop old table
DROP TABLE DeviceAlerts;

-- Step 4: Rename new table
RENAME TABLE DeviceAlerts_New TO DeviceAlerts;

COMMIT;

-- Verify
SELECT COUNT(*) as TotalAlerts FROM DeviceAlerts;
SELECT 'Migration completed successfully' AS Status;
```

---

## Common Database Update Scenarios

### Scenario 1: Add UserKey Column (Already Done)

This was added in the initial creation. If you need to add it retroactively:

```sql
ALTER TABLE DeviceAlerts
ADD COLUMN UserKey VARCHAR(500) NULL
AFTER MAC;

ALTER TABLE DeviceAlerts
ADD INDEX idx_userkey (UserKey);
```

### Scenario 2: Add New Alert Type

1. **Create new Entity class** in `Common/Entities/DeviceAlerts.cs`:
```csharp
public class SmsAlertEntity : DeviceAlert
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string MessageContent { get; set; } = string.Empty;
}
```

2. **Update AppDbContext.cs**:
```csharp
public DbSet<SmsAlertEntity> SmsAlerts { get; set; }

modelBuilder.Entity<DeviceAlertEntity>(entity =>
{
    entity.HasDiscriminator<string>("Discriminator")
        .HasValue<RemoteAccessAlertEntity>("RemoteAccess")
        .HasValue<UrlAlertEntity>("Url")
        .HasValue<SmsAlertEntity>("Sms");  // Add new discriminator
});

modelBuilder.Entity<SmsAlertEntity>(entity =>
{
    entity.Property(e => e.PhoneNumber).HasMaxLength(50);
    entity.Property(e => e.MessageContent).HasMaxLength(2000);
});
```

3. **Add columns to database**:
```sql
ALTER TABLE DeviceAlerts
ADD COLUMN PhoneNumber VARCHAR(50) NULL,
ADD COLUMN MessageContent VARCHAR(2000) NULL;
```

### Scenario 3: Change Column Size

```sql
-- Increase ConnectionUrl size
ALTER TABLE DeviceAlerts
MODIFY COLUMN ConnectionUrl VARCHAR(3000);

-- Increase TrackerKeys size
ALTER TABLE DeviceAlerts
MODIFY COLUMN TrackerKeys VARCHAR(8000);
```

### Scenario 4: Add Computed Column

```sql
-- Add computed column for alert age
ALTER TABLE DeviceAlerts
ADD COLUMN AlertAgeDays INT GENERATED ALWAYS AS 
    (DATEDIFF(NOW(), `Timestamp`)) VIRTUAL;
```

---

## Best Practices

### 1. Always Backup First
```bash
# Backup entire database
mysqldump -u root -p ASPSBackend2DB > backup_$(date +%Y%m%d_%H%M%S).sql

# Backup just DeviceAlerts table
mysqldump -u root -p ASPSBackend2DB DeviceAlerts > devicealerts_backup.sql

# Restore if needed
mysql -u root -p ASPSBackend2DB < backup_20231230_120000.sql
```

### 2. Test in Development First
```bash
# Create test database
mysql -u root -p -e "CREATE DATABASE ASPSBackend2DB_Test;"
mysql -u root -p ASPSBackend2DB_Test < RESET-DATABASE.sql
mysql -u root -p ASPSBackend2DB_Test < populate-test-data.sql

# Test migration
mysql -u root -p ASPSBackend2DB_Test < migration-script.sql

# Verify
mysql -u root -p ASPSBackend2DB_Test -e "DESCRIBE DeviceAlerts;"
```

### 3. Use Transactions for Complex Changes
```sql
START TRANSACTION;

-- Your changes here
ALTER TABLE DeviceAlerts ADD COLUMN NewColumn VARCHAR(255);

-- Verify
SELECT COUNT(*) FROM DeviceAlerts;

-- If everything looks good
COMMIT;

-- If something went wrong
-- ROLLBACK;
```

### 4. Version Your Migration Scripts

Create numbered migration files:
```
migrations/
  001_initial_schema.sql
  002_add_device_alerts.sql
  003_add_userkey_column.sql
  004_add_sms_alerts.sql
```

Track applied migrations in a table:
```sql
CREATE TABLE __MigrationHistory (
    MigrationId VARCHAR(150) PRIMARY KEY,
    ProductVersion VARCHAR(50),
    AppliedDate DATETIME DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO __MigrationHistory (MigrationId, ProductVersion)
VALUES ('003_add_userkey_column', '1.0.0');
```

---

## Current DeviceAlerts Structure

```sql
DESCRIBE DeviceAlerts;
```

**Current columns:**
- Key (Primary Key)
- Discriminator (RemoteAccess/Url)
- AlertType
- Priority
- Timestamp
- Token
- DeviceUid (Indexed)
- DeviceType
- OperatingSystem
- MAC
- UserKey (Indexed) ✅ Added
- RemoteAccessApp (nullable)
- RunningProcesses (nullable)
- ConnectionUrl (nullable)
- ConnectionStatus (nullable)
- ConnectionsCount (nullable)
- SessionStatus (nullable)
- Url (nullable)
- TrackerKeys (nullable)
- IFrameDomains (nullable)
- UserAgent (nullable)
- DateCreated
- IsDeleted
- IsDisabled

---

## Quick Reference Commands

```bash
# Show current structure
mysql -u root -p ASPSBackend2DB -e "DESCRIBE DeviceAlerts;"

# Count alerts
mysql -u root -p ASPSBackend2DB -e "SELECT COUNT(*) FROM DeviceAlerts;"

# Show recent alerts
mysql -u root -p ASPSBackend2DB -e "SELECT * FROM DeviceAlerts ORDER BY Timestamp DESC LIMIT 5;"

# Drop and recreate (CAUTION: DATA LOSS)
mysql -u root -p < add-device-alerts-table.sql

# Full reset (CAUTION: ALL DATA LOSS)
mysql -u root -p < RESET-DATABASE.sql
mysql -u root -p < populate-test-data.sql
```
