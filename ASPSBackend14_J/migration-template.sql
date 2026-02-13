-- Migration Script Template
-- Copy this template for each database structure change
-- 
-- Migration: [DESCRIPTION]
-- Date: [DATE]
-- Author: [NAME]
-- 
-- IMPORTANT: Always backup before running!
-- mysqldump -u root -p ASPSBackend2DB > backup_before_migration.sql

USE ASPSBackend2DB;

-- Safety check: Verify we're in the right database
SELECT DATABASE() as CurrentDatabase;

START TRANSACTION;

-- ============================================
-- MIGRATION CODE STARTS HERE
-- ============================================

-- Example 1: Add a new column
-- ALTER TABLE DeviceAlerts
-- ADD COLUMN NewColumnName VARCHAR(255) NULL
-- AFTER ExistingColumn;

-- Example 2: Modify existing column
-- ALTER TABLE DeviceAlerts
-- MODIFY COLUMN ColumnName VARCHAR(500) NOT NULL;

-- Example 3: Add index
-- ALTER TABLE DeviceAlerts
-- ADD INDEX idx_new_column (NewColumnName);

-- Example 4: Add new discriminator value (for new alert type)
-- First add columns for new type:
-- ALTER TABLE DeviceAlerts
-- ADD COLUMN NewTypeField1 VARCHAR(255) NULL,
-- ADD COLUMN NewTypeField2 INT NULL;
-- 
-- Then update AppDbContext.cs to include new discriminator value

-- ============================================
-- MIGRATION CODE ENDS HERE
-- ============================================

-- Verification queries
SELECT '========================================' AS '';
SELECT 'Verifying migration...' AS Status;
SELECT '========================================' AS '';

-- Check table structure
SHOW COLUMNS FROM DeviceAlerts;

-- Count records (should match before migration)
SELECT COUNT(*) as TotalAlerts FROM DeviceAlerts;

-- Show sample data
SELECT * FROM DeviceAlerts LIMIT 3;

-- ============================================
-- REVIEW THE OUTPUT ABOVE
-- If everything looks correct, run: COMMIT;
-- If something is wrong, run: ROLLBACK;
-- ============================================

-- COMMIT;  -- Uncomment after reviewing
-- ROLLBACK;  -- Uncomment to undo changes
