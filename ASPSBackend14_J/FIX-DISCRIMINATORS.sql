-- FIX DISCRIMINATOR VALUES
-- This script will fix any wrong discriminator values in the database

USE ASPSBackend2DB;

SELECT '========================================' AS '';
SELECT 'BEFORE FIX' AS '';
SELECT '========================================' AS '';

-- Show current state
SELECT 'Current UserDevices:' AS '';
SELECT DeviceUid, Discriminator, IsDeleted FROM UserDevices;

SELECT '' AS '';
SELECT 'Discriminator counts:' AS '';
SELECT Discriminator, COUNT(*) AS Count 
FROM UserDevices 
GROUP BY Discriminator;

-- Fix wrong discriminators
SELECT '' AS '';
SELECT '========================================' AS '';
SELECT 'FIXING DISCRIMINATORS' AS '';
SELECT '========================================' AS '';

UPDATE UserDevices 
SET Discriminator = 'PC' 
WHERE Discriminator = 'PersonalComputer';

SELECT CONCAT('Updated ', ROW_COUNT(), ' PersonalComputer -> PC') AS Result;

UPDATE UserDevices 
SET Discriminator = 'Phone' 
WHERE Discriminator = 'SmartPhone';

SELECT CONCAT('Updated ', ROW_COUNT(), ' SmartPhone -> Phone') AS Result;

SELECT '' AS '';
SELECT '========================================' AS '';
SELECT 'AFTER FIX' AS '';
SELECT '========================================' AS '';

-- Show fixed state
SELECT 'Fixed UserDevices:' AS '';
SELECT DeviceUid, Discriminator, UserKey, IsDeleted FROM UserDevices;

SELECT '' AS '';
SELECT 'Discriminator counts (should be PC and Phone):' AS '';
SELECT Discriminator, COUNT(*) AS Count 
FROM UserDevices 
GROUP BY Discriminator;

SELECT '' AS '';
SELECT '========================================' AS '';
SELECT 'VERIFICATION' AS '';
SELECT '========================================' AS '';

SELECT 
    CASE 
        WHEN EXISTS (SELECT 1 FROM UserDevices WHERE Discriminator NOT IN ('PC', 'Phone'))
        THEN '❌ WRONG discriminators still exist!'
        ELSE '✓ All discriminators are correct'
    END AS Status;

SELECT '' AS '';
SELECT 'Expected discriminators: PC, Phone' AS '';
SELECT 'If you see other values, EF Core will ignore those records!' AS '';
