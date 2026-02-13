-- Rollback: Remove DeviceAlertKeyField from AnalysisResults table

USE aspsbackend2db;

-- Remove the DeviceAlertKeyField column
ALTER TABLE AnalysisResults 
DROP COLUMN DeviceAlertKeyField;

-- Verify the change
DESCRIBE AnalysisResults;

-- Success message
SELECT 'DeviceAlertKeyField column removed from AnalysisResults table' AS Status;
