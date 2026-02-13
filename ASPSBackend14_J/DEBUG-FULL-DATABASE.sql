-- COMPREHENSIVE DATABASE DEBUG SCRIPT
-- Run this to verify database is correctly set up

USE ASPSBackend2DB;

SELECT '========================================' AS '';
SELECT 'STEP 1: Verify Database Connection' AS '';
SELECT '========================================' AS '';
SELECT DATABASE() AS CurrentDatabase;
SELECT VERSION() AS MySQLVersion;

SELECT '' AS '';
SELECT '========================================' AS '';
SELECT 'STEP 2: Check Tables Exist' AS '';
SELECT '========================================' AS '';
SHOW TABLES;

SELECT '' AS '';
SELECT '========================================' AS '';
SELECT 'STEP 3: Check Users Table Structure' AS '';
SELECT '========================================' AS '';
DESCRIBE Users;

SELECT '' AS '';
SELECT '========================================' AS '';
SELECT 'STEP 4: Check Users Data (RAW)' AS '';
SELECT '========================================' AS '';
SELECT * FROM Users;

SELECT '' AS '';
SELECT 'Count of Users:' AS '';
SELECT 
    COUNT(*) AS Total,
    SUM(CASE WHEN IsDeleted = 0 THEN 1 ELSE 0 END) AS Active,
    SUM(CASE WHEN IsDeleted = 1 THEN 1 ELSE 0 END) AS Deleted
FROM Users;

SELECT '' AS '';
SELECT '========================================' AS '';
SELECT 'STEP 5: Check UserDevices Table Structure' AS '';
SELECT '========================================' AS '';
DESCRIBE UserDevices;

SELECT '' AS '';
SELECT '========================================' AS '';
SELECT 'STEP 6: Check UserDevices Data (RAW)' AS '';
SELECT '========================================' AS '';
SELECT * FROM UserDevices;

SELECT '' AS '';
SELECT 'Count of UserDevices:' AS '';
SELECT 
    COUNT(*) AS Total,
    SUM(CASE WHEN IsDeleted = 0 THEN 1 ELSE 0 END) AS Active,
    SUM(CASE WHEN IsDeleted = 1 THEN 1 ELSE 0 END) AS Deleted
FROM UserDevices;

SELECT '' AS '';
SELECT 'UserDevices by Discriminator:' AS '';
SELECT 
    Discriminator,
    COUNT(*) AS Count
FROM UserDevices
GROUP BY Discriminator;

SELECT '' AS '';
SELECT '========================================' AS '';
SELECT 'STEP 7: Verify Foreign Keys' AS '';
SELECT '========================================' AS '';
SELECT 
    ud.DeviceUid,
    ud.UserKey AS DeviceUserKey,
    u.`Key` AS ActualUserKey,
    CASE WHEN u.`Key` IS NULL THEN 'MISSING USER!' ELSE 'OK' END AS Status
FROM UserDevices ud
LEFT JOIN Users u ON ud.UserKey = u.`Key`;

SELECT '' AS '';
SELECT '========================================' AS '';
SELECT 'STEP 8: Expected vs Actual' AS '';
SELECT '========================================' AS '';
SELECT 'Expected: 2 users, 4 devices' AS '';
SELECT CONCAT('Actual: ', 
    (SELECT COUNT(*) FROM Users WHERE IsDeleted = 0), ' users, ',
    (SELECT COUNT(*) FROM UserDevices WHERE IsDeleted = 0), ' devices'
) AS ActualCounts;

SELECT '' AS '';
SELECT '========================================' AS '';
SELECT 'STEP 9: Connection String Check' AS '';
SELECT '========================================' AS '';
SELECT 'Verify your appsettings.json has:' AS '';
SELECT 'server=localhost;port=3306;database=ASPSBackend2DB;user=root;password=your_password' AS ConnectionString;

SELECT '' AS '';
SELECT '========================================' AS '';
SELECT 'DIAGNOSTIC COMPLETE' AS '';
SELECT '========================================' AS '';
