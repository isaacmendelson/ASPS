-- Change all char(36) columns to varchar(36) for EF Core 7 compatibility
-- This script properly handles foreign key constraints

USE aspsbackend2db;

-- Disable foreign key checks temporarily
SET FOREIGN_KEY_CHECKS = 0;

-- Users table
ALTER TABLE Users MODIFY COLUMN `Key` VARCHAR(36) NOT NULL;

-- UserDevices table  
ALTER TABLE UserDevices MODIFY COLUMN `Key` VARCHAR(36) NOT NULL;
ALTER TABLE UserDevices MODIFY COLUMN `UserKey` VARCHAR(36) NOT NULL;

-- UserAccounts table
ALTER TABLE UserAccounts MODIFY COLUMN `Key` VARCHAR(36) NOT NULL;
ALTER TABLE UserAccounts MODIFY COLUMN `UserKey` VARCHAR(36) NOT NULL;

-- AnalysisResults table (if exists)
ALTER TABLE AnalysisResults MODIFY COLUMN `Key` VARCHAR(36) NOT NULL;
ALTER TABLE AnalysisResults MODIFY COLUMN `UserKey` VARCHAR(36) NOT NULL;

-- DeviceAlerts table (if exists)
ALTER TABLE DeviceAlerts MODIFY COLUMN `Key` VARCHAR(36) NOT NULL;
ALTER TABLE DeviceAlerts MODIFY COLUMN `UserKey` VARCHAR(36) NULL;

-- Re-enable foreign key checks
SET FOREIGN_KEY_CHECKS = 1;

SELECT 'All GUID columns changed from CHAR(36) to VARCHAR(36)' AS Status;
