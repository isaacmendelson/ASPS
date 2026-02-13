-- Diagnostic queries to see what's in the database

USE ASPSBackend2DB;

-- Check all users (including deleted)
SELECT 
    `Key`,
    KeycloakUserId,
    FirstName,
    LastName,
    IsDeleted,
    IsDisabled,
    DateCreated
FROM Users;

-- Check for any NULL keys
SELECT COUNT(*) as UsersWithNullKey FROM Users WHERE `Key` IS NULL;

-- Check deleted/disabled status
SELECT 
    COUNT(*) as Total,
    SUM(CASE WHEN IsDeleted = 1 THEN 1 ELSE 0 END) as Deleted,
    SUM(CASE WHEN IsDisabled = 1 THEN 1 ELSE 0 END) as Disabled,
    SUM(CASE WHEN IsDeleted = 0 AND IsDisabled = 0 THEN 1 ELSE 0 END) as Active
FROM Users;

-- Show the actual Key values
SELECT `Key`, FirstName, LastName FROM Users;

-- Check if Key format is correct (should be like "User|guid|")
SELECT 
    `Key`,
    CASE 
        WHEN `Key` LIKE 'User|%|%' THEN 'Valid Format'
        ELSE 'Invalid Format'
    END as KeyFormat,
    FirstName
FROM Users;
