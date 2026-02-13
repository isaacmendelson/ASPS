-- Migration: Add DeviceAlertKeyField to AnalysisResults table
-- This column stores the Key of the DeviceAlert that initiated the analysis

USE aspsbackend2db;

-- Add the DeviceAlertKeyField column (nullable, VARCHAR(36) to match GUID format)
ALTER TABLE AnalysisResults 
ADD COLUMN DeviceAlertKeyField VARCHAR(36) NULL 
COMMENT 'Key of the DeviceAlert that initiated this analysis';

-- Verify the change
DESCRIBE AnalysisResults;

-- Display column info
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_TYPE,
    COLUMN_COMMENT
FROM 
    INFORMATION_SCHEMA.COLUMNS
WHERE 
    TABLE_SCHEMA = 'aspsbackend2db' 
    AND TABLE_NAME = 'AnalysisResults'
ORDER BY 
    ORDINAL_POSITION;

-- Success message
SELECT 'DeviceAlertKeyField column added successfully to AnalysisResults table' AS Status;
