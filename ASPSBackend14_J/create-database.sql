-- ASPSBackend System2 Database Creation Script
-- Run this in MySQL if EF migrations are not working

CREATE DATABASE IF NOT EXISTS ASPSBackend2DB;
USE ASPSBackend2DB;

-- Users table (Tag and TypeName are computed, not stored)
CREATE TABLE IF NOT EXISTS Users (
    `Key` VARCHAR(500) PRIMARY KEY,
    KeycloakUserId VARCHAR(100) NOT NULL UNIQUE,
    FirstName VARCHAR(100),
    LastName VARCHAR(100),
    Address VARCHAR(255),
    City VARCHAR(100),
    State VARCHAR(100),
    Zip VARCHAR(20),
    Country VARCHAR(100),
    PhoneNumber VARCHAR(50),
    Role INT,
    GuardianKey INT,
    Locale VARCHAR(20),
    Timezone INT,
    DateCreated DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    DateModified DATETIME,
    DateDeleted DATETIME,
    IsDeleted BOOLEAN DEFAULT FALSE,
    IsDisabled BOOLEAN DEFAULT FALSE,
    INDEX idx_keycloak (KeycloakUserId)
);

-- UserDevices table (Discriminator is set by EF based on type)
CREATE TABLE IF NOT EXISTS UserDevices (
    `Key` VARCHAR(500) PRIMARY KEY,
    Discriminator VARCHAR(50) NOT NULL,
    AggregateVersionField INT DEFAULT 0,
    UserKey VARCHAR(500),
    DeviceType INT,
    DeviceUid VARCHAR(255) NOT NULL UNIQUE,
    PhoneNumber VARCHAR(50),
    OperatingSystem INT,
    MAC VARCHAR(50),
    IMEI VARCHAR(50),
    BiosSerial VARCHAR(255),
    Make VARCHAR(100),
    Model VARCHAR(100),
    Serial VARCHAR(100),
    MonitoringStatus INT DEFAULT 0,
    -- PersonalComputer specific
    Type INT,
    MotherboardSerial VARCHAR(255),
    UserAgent VARCHAR(500),
    Timezone INT,
    -- Common fields
    DateCreated DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    DateModified DATETIME,
    DateDeleted DATETIME,
    IsDeleted BOOLEAN DEFAULT FALSE,
    IsDisabled BOOLEAN DEFAULT FALSE,
    FOREIGN KEY (UserKey) REFERENCES Users(`Key`) ON DELETE CASCADE,
    INDEX idx_device_uid (DeviceUid),
    INDEX idx_user_key (UserKey)
);

-- UserAccounts table
CREATE TABLE IF NOT EXISTS UserAccounts (
    `Key` VARCHAR(500) PRIMARY KEY,
    UserKey VARCHAR(500) NOT NULL,
    AccountType INT,
    LoginUrl VARCHAR(500),
    UserName VARCHAR(255),
    PasswordHash VARCHAR(500),
    Is2FactorAuth BOOLEAN DEFAULT FALSE,
    LoginPhoneNumber VARCHAR(50),
    DateCreated DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    DateModified DATETIME,
    DateDeleted DATETIME,
    IsDeleted BOOLEAN DEFAULT FALSE,
    IsDisabled BOOLEAN DEFAULT FALSE,
    FOREIGN KEY (UserKey) REFERENCES Users(`Key`) ON DELETE CASCADE,
    INDEX idx_user_key (UserKey),
    INDEX idx_username (UserName)
);

-- AnalysisResults table
CREATE TABLE IF NOT EXISTS AnalysisResults (
    `Key` VARCHAR(500) PRIMARY KEY,
    Discriminator VARCHAR(100) NOT NULL,
    UserKey VARCHAR(500) NOT NULL,
    JsonValue TEXT,
    HasError BOOLEAN,
    ErrorMessage TEXT,
    Timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    IsFromCache BOOLEAN DEFAULT FALSE,
    -- UrlAnalysisResultContainer specific
    Domain VARCHAR(500),
    Url VARCHAR(2000),
    -- Common fields
    DateCreated DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    DateModified DATETIME,
    DateDeleted DATETIME,
    IsDeleted BOOLEAN DEFAULT FALSE,
    IsDisabled BOOLEAN DEFAULT FALSE,
    INDEX idx_user_key (UserKey),
    INDEX idx_timestamp (Timestamp)
);

-- AlertFlags table
CREATE TABLE IF NOT EXISTS AlertFlags (
    `Key` INT AUTO_INCREMENT PRIMARY KEY,
    UserKey INT NOT NULL,
    SensorType INT,
    Created DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    AlertFlagType INT,
    Status INT,
    IsDeleted BOOLEAN DEFAULT FALSE,
    Deleted DATETIME,
    Modified DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_user_status (UserKey, Status)
);

-- EF Migrations History
CREATE TABLE IF NOT EXISTS __EFMigrationsHistory (
    MigrationId VARCHAR(150) NOT NULL PRIMARY KEY,
    ProductVersion VARCHAR(32) NOT NULL
);

INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) 
VALUES ('00000000000000_InitialCreate', '8.0.2')
ON DUPLICATE KEY UPDATE ProductVersion = ProductVersion;

-- Sample Data with CORRECT Key format (Type|Value|InstanceName)
-- Key format: Type|Value|InstanceName (empty string if no instance)

INSERT INTO Users (`Key`, KeycloakUserId, FirstName, LastName, Role, DateCreated)
VALUES 
    (
        'User|user-001|',
        'keycloak-123',
        'John',
        'Doe',
        1,
        NOW()
    ),
    (
        'User|user-002|',
        'keycloak-456',
        'Jane',
        'Smith',
        1,
        NOW()
    )
ON DUPLICATE KEY UPDATE FirstName = FirstName;

-- User Devices for John Doe and Jane Smith
INSERT INTO UserDevices (
    `Key`,
    Discriminator,
    UserKey,
    DeviceUid,
    DeviceType,
    OperatingSystem,
    MonitoringStatus,
    Make,
    Model,
    DateCreated,
    IsDeleted
)
VALUES
    -- John's Personal Computer
    (
        'Device|john-pc-001|',
        'PC',
        'User|user-001|',
        'PC-JOHN-001',
        1,  -- PersonalComputer
        1,  -- Windows
        1,  -- Enabled
        'Dell',
        'Desktop',
        NOW(),
        0
    ),
    -- John's Smartphone
    (
        'Device|john-phone-001|',
        'Phone',
        'User|user-001|',
        'PHONE-JOHN-001',
        2,  -- SmartPhone
        5,  -- IOS
        1,  -- Enabled
        'Apple',
        'iPhone 14',
        NOW(),
        0
    ),
    -- Jane's Personal Computer
    (
        'Device|jane-pc-001|',
        'PC',
        'User|user-002|',
        'PC-JANE-001',
        1,  -- PersonalComputer
        1,  -- Windows
        1,  -- Enabled
        'HP',
        'Laptop',
        NOW(),
        0
    ),
    -- Jane's Smartphone
    (
        'Device|jane-phone-001|',
        'Phone',
        'User|user-002|',
        'PHONE-JANE-001',
        2,  -- SmartPhone
        4,  -- Android
        1,  -- Enabled
        'Samsung',
        'Galaxy S23',
        NOW(),
        0
    )
ON DUPLICATE KEY UPDATE Make = Make;

SELECT 'Database created successfully!' as Status;
SELECT 'Sample users and devices created with correct Key format' as Info;
SELECT 'Tag and TypeName are computed properties (not in DB)' as Note;

-- Show created users
SELECT 'USERS:' AS '';
SELECT `Key`, FirstName, LastName, KeycloakUserId FROM Users;

-- Show created devices
SELECT '' AS '';
SELECT 'USER DEVICES:' AS '';
SELECT `Key`, UserKey, DeviceUid, Make, Model, Discriminator, OperatingSystem 
FROM UserDevices
ORDER BY UserKey, Discriminator;

-- Show all tables
SELECT '' AS '';
SELECT 'TABLES:' AS '';
SHOW TABLES;


