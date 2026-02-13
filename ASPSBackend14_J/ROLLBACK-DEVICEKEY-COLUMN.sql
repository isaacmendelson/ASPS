-- Rollback: Remove DeviceKey column from DeviceAlerts
-- Use this if you need to undo the migration

USE ASPSBackend2DB;

-- Drop foreign key constraint if it exists
-- ALTER TABLE DeviceAlerts DROP FOREIGN KEY fk_devicealerts_device;

-- Drop index
DROP INDEX idx_devicealerts_devicekey ON DeviceAlerts;

-- Drop column
ALTER TABLE DeviceAlerts DROP COLUMN DeviceKey;

-- Verify
DESCRIBE DeviceAlerts;
