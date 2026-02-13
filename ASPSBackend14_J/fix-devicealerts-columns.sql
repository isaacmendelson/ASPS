-- Add missing Entity base class columns to DeviceAlerts table
-- Run this if you get "Unknown column 'DateDeleted' in 'field list'" error

USE ASPSBackend2DB;

-- Add DateDeleted if it doesn't exist
ALTER TABLE DeviceAlerts 
ADD COLUMN IF NOT EXISTS DateDeleted DATETIME AFTER DateModified;

-- Verify the columns
DESCRIBE DeviceAlerts;

SELECT 'DeviceAlerts table updated successfully!' AS Status;
