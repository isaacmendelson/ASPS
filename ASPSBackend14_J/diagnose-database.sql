-- Diagnostic Script: Check Database State
-- Run this to verify data exists in the database

USE ASPSBackend2DB;

SELECT '========================================' AS '';
SELECT 'DATABASE DIAGNOSTICS' AS '';
SELECT '========================================' AS '';

-- Check if database exists and is selected
SELECT DATABASE() AS CurrentDatabase;

SELECT '' AS '';
SELECT 'TABLE: Users' AS '';
SELECT COUNT(*) AS TotalRecords FROM Users;
SELECT COUNT(*) AS ActiveRecords FROM Users WHERE IsDeleted = 0;
SELECT `Key`, FirstName, LastName, KeycloakUserId, IsDeleted, IsDisabled 
FROM Users;

SELECT '' AS '';
SELECT 'TABLE: UserDevices' AS '';
SELECT COUNT(*) AS TotalRecords FROM UserDevices;
SELECT COUNT(*) AS ActiveRecords FROM UserDevices WHERE IsDeleted = 0;
SELECT `Key`, UserKey, DeviceUid, Discriminator, DeviceType, OperatingSystem, Make, Model, IsDeleted, IsDisabled
FROM UserDevices;

SELECT '' AS '';
SELECT 'TABLE: UserAccounts' AS '';
SELECT COUNT(*) AS TotalRecords FROM UserAccounts;
SELECT COUNT(*) AS ActiveRecords FROM UserAccounts WHERE IsDeleted = 0;

SELECT '' AS '';
SELECT 'TABLE: DeviceAlerts' AS '';
SELECT COUNT(*) AS TotalRecords FROM DeviceAlerts;
SELECT COUNT(*) AS ActiveRecords FROM DeviceAlerts WHERE IsDeleted = 0;

SELECT '' AS '';
SELECT '========================================' AS '';
SELECT 'EXPECTED RESULTS:' AS '';
SELECT '  Users: 2 active records' AS '';
SELECT '  UserDevices: 4 active records' AS '';
SELECT '  UserAccounts: 0 records' AS '';
SELECT '  DeviceAlerts: 0 records (initially)' AS '';
SELECT '========================================' AS '';
