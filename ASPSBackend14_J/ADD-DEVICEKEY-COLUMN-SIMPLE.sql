-- Migration: Add DeviceKey column to DeviceAlerts
-- Date: 2026-01-24

USE ASPSBackend2DB;

-- Step 1: Add DeviceKey column
ALTER TABLE DeviceAlerts 
ADD COLUMN DeviceKey VARCHAR(36) NULL;

-- Step 2: Create index on DeviceKey
CREATE INDEX idx_devicealerts_devicekey ON DeviceAlerts(DeviceKey);

-- Step 3: Add foreign key constraint (optional - can be skipped if causes issues)
-- Uncomment if you want FK constraint:
-- ALTER TABLE DeviceAlerts
-- ADD CONSTRAINT fk_devicealerts_device
-- FOREIGN KEY (DeviceKey) REFERENCES UserDevices(`Key`)
-- ON DELETE SET NULL
-- ON UPDATE CASCADE;

-- Step 4: Verify the change
DESCRIBE DeviceAlerts;

-- Step 5: Show indexes
SHOW INDEX FROM DeviceAlerts;
