-- Migration: Add DeviceKey Foreign Key to DeviceAlerts
-- Date: 2026-01-24
-- Description: Adds DeviceKey column and foreign key constraint to link alerts to specific UserDevice records

-- Step 1: Add DeviceKey column
ALTER TABLE DeviceAlerts 
ADD COLUMN DeviceKey VARCHAR(36) NULL AFTER UserKey;

-- Step 2: Create index on DeviceKey for performance
CREATE INDEX idx_devicealerts_devicekey ON DeviceAlerts(DeviceKey);

-- Step 3: Add foreign key constraint
ALTER TABLE DeviceAlerts
ADD CONSTRAINT fk_devicealerts_device
FOREIGN KEY (DeviceKey) REFERENCES UserDevices(`Key`)
ON DELETE SET NULL
ON UPDATE CASCADE;

-- Step 4: Add foreign key constraint for UserKey (if not already exists)
-- Check if constraint exists first
SELECT COUNT(*) INTO @fk_exists 
FROM information_schema.TABLE_CONSTRAINTS 
WHERE CONSTRAINT_SCHEMA = DATABASE()
  AND TABLE_NAME = 'DeviceAlerts'
  AND CONSTRAINT_NAME = 'fk_devicealerts_user'
  AND CONSTRAINT_TYPE = 'FOREIGN KEY';

-- Add FK only if it doesn't exist
SET @sql = IF(@fk_exists = 0,
    'ALTER TABLE DeviceAlerts ADD CONSTRAINT fk_devicealerts_user FOREIGN KEY (UserKey) REFERENCES Users(`Key`) ON DELETE SET NULL ON UPDATE CASCADE',
    'SELECT "FK already exists" AS status');

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Step 5: Verify the changes
DESCRIBE DeviceAlerts;

SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    CONSTRAINT_NAME,
    REFERENCED_TABLE_NAME,
    REFERENCED_COLUMN_NAME
FROM
    information_schema.KEY_COLUMN_USAGE
WHERE
    TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'DeviceAlerts'
    AND REFERENCED_TABLE_NAME IS NOT NULL;
