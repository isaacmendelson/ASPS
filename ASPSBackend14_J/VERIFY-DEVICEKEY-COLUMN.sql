-- Verify DeviceKey column exists

USE ASPSBackend2DB;

-- Check if column exists
SELECT 
    COLUMN_NAME,
    COLUMN_TYPE,
    IS_NULLABLE,
    COLUMN_KEY,
    COLUMN_DEFAULT
FROM 
    information_schema.COLUMNS
WHERE 
    TABLE_SCHEMA = 'ASPSBackend2DB'
    AND TABLE_NAME = 'DeviceAlerts'
    AND COLUMN_NAME = 'DeviceKey';

-- Show all columns in DeviceAlerts
DESCRIBE DeviceAlerts;
