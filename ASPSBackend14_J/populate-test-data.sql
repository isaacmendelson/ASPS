-- Populate Test Data: 2 Users with 2 Devices Each
-- Run this after creating the database

USE ASPSBackend2DB;

-- Clear existing test data (optional)
DELETE FROM UserDevices WHERE UserKey LIKE 'User|test-%';
DELETE FROM Users WHERE `Key` LIKE 'User|test-%';

-- User 1: John Doe
INSERT INTO Users (
    `Key`, 
    KeycloakUserId, 
    FirstName, 
    LastName, 
    Address, 
    City, 
    State, 
    Zip, 
    Country, 
    PhoneNumber, 
    Role, 
    DateCreated, 
    IsDeleted
) VALUES (
    'User|test-user-001|', 
    'keycloak-john-001',
    'John',
    'Doe',
    '123 Main St',
    'New York',
    'NY',
    '10001',
    'USA',
    '+1-555-0101',
    1,  -- Self
    NOW(),
    0
);

-- User 2: Jane Smith
INSERT INTO Users (
    `Key`, 
    KeycloakUserId, 
    FirstName, 
    LastName, 
    Address, 
    City, 
    State, 
    Zip, 
    Country, 
    PhoneNumber, 
    Role, 
    DateCreated, 
    IsDeleted
) VALUES (
    'User|test-user-002|', 
    'keycloak-jane-002',
    'Jane',
    'Smith',
    '456 Oak Ave',
    'Los Angeles',
    'CA',
    '90001',
    'USA',
    '+1-555-0202',
    1,  -- Self
    NOW(),
    0
);

-- John's Personal Computer
INSERT INTO UserDevices (
    `Key`,
    UserKey,
    Discriminator,
    DeviceUid,
    DeviceName,
    OperatingSystem,
    MonitoringStatus,
    DateCreated,
    IsDeleted
) VALUES (
    'Device|john-pc-001|',
    'User|test-user-001|',
    'PC',
    'PC-JOHN-001',
    'John\'s Desktop',
    1,  -- Windows
    1,  -- Enabled
    NOW(),
    0
);

-- John's Smartphone
INSERT INTO UserDevices (
    `Key`,
    UserKey,
    Discriminator,
    DeviceUid,
    DeviceName,
    OperatingSystem,
    MonitoringStatus,
    DateCreated,
    IsDeleted
) VALUES (
    'Device|john-phone-001|',
    'User|test-user-001|',
    'Phone',
    'PHONE-JOHN-001',
    'John\'s iPhone',
    5,  -- IOS
    1,  -- Enabled
    NOW(),
    0
);

-- Jane's Personal Computer
INSERT INTO UserDevices (
    `Key`,
    UserKey,
    Discriminator,
    DeviceUid,
    DeviceName,
    OperatingSystem,
    MonitoringStatus,
    DateCreated,
    IsDeleted
) VALUES (
    'Device|jane-pc-001|',
    'User|test-user-002|',
    'PC',
    'PC-JANE-001',
    'Jane\'s Laptop',
    1,  -- Windows
    1,  -- Enabled
    NOW(),
    0
);

-- Jane's Smartphone
INSERT INTO UserDevices (
    `Key`,
    UserKey,
    Discriminator,
    DeviceUid,
    DeviceName,
    OperatingSystem,
    MonitoringStatus,
    DateCreated,
    IsDeleted
) VALUES (
    'Device|jane-phone-001|',
    'User|test-user-002|',
    'Phone',
    'PHONE-JANE-001',
    'Jane\'s Android',
    4,  -- Android
    1,  -- Enabled
    NOW(),
    0
);

-- Verification
SELECT '========================================' AS '';
SELECT 'Test Data Populated Successfully' AS Status;
SELECT '========================================' AS '';

SELECT 'USERS:' AS '';
SELECT `Key`, FirstName, LastName, PhoneNumber, KeycloakUserId 
FROM Users 
WHERE `Key` LIKE 'User|test-%';

SELECT '' AS '';
SELECT 'USER DEVICES:' AS '';
SELECT `Key`, UserKey, DeviceUid, DeviceName, Discriminator, OperatingSystem, MonitoringStatus
FROM UserDevices
WHERE UserKey LIKE 'User|test-%';

SELECT '' AS '';
SELECT '========================================' AS '';
SELECT 'Test Devices for Alert Testing:' AS '';
SELECT '  PC-JOHN-001    - John Doe Desktop (Windows)' AS '';
SELECT '  PHONE-JOHN-001 - John Doe iPhone (IOS)' AS '';
SELECT '  PC-JANE-001    - Jane Smith Laptop (Windows)' AS '';
SELECT '  PHONE-JANE-001 - Jane Smith Android (Android)' AS '';
SELECT '========================================' AS '';
