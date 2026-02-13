-- Complete Database Reset Script with GUID Keys
-- This drops everything and recreates from scratch with GUID-based keys

DROP DATABASE IF EXISTS ASPSBackend2DB;

CREATE DATABASE IF NOT EXISTS ASPSBackend2DB;
USE ASPSBackend2DB;

-- Users table
CREATE TABLE IF NOT EXISTS Users (
    `Key` CHAR(36) NOT NULL PRIMARY KEY,
    KeycloakUserId VARCHAR(100) NOT NULL UNIQUE,
    FirstName VARCHAR(100) NOT NULL DEFAULT '',
    LastName VARCHAR(100) NOT NULL DEFAULT '',
    Address VARCHAR(255) NOT NULL DEFAULT '',
    City VARCHAR(100) NOT NULL DEFAULT '',
    State VARCHAR(100) NOT NULL DEFAULT '',
    Zip VARCHAR(20) NOT NULL DEFAULT '',
    Country VARCHAR(100) NOT NULL DEFAULT '',
    PhoneNumber VARCHAR(50) NOT NULL DEFAULT '',
    Role INT NOT NULL,
    GuardianKey INT,
    Locale VARCHAR(20),
    Timezone INT,
    DateCreated DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    DateModified DATETIME,
    DateDeleted DATETIME,
    IsDeleted BOOLEAN DEFAULT FALSE,
    IsDisabled BOOLEAN DEFAULT FALSE,
    INDEX idx_keycloak (KeycloakUserId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- UserDevices table (TPH with Discriminator)
CREATE TABLE IF NOT EXISTS UserDevices (
    `Key` CHAR(36) NOT NULL PRIMARY KEY,
    Discriminator VARCHAR(50) NOT NULL,
    AggregateVersionField INT DEFAULT 0,
    UserKey CHAR(36),
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
    INDEX idx_user_key (UserKey),
    INDEX idx_discriminator (Discriminator)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- UserAccounts table
CREATE TABLE IF NOT EXISTS UserAccounts (
    `Key` CHAR(36) NOT NULL PRIMARY KEY,
    UserKey CHAR(36) NOT NULL,
    AccountType INT,
    LoginUrl VARCHAR(500) NOT NULL DEFAULT '',
    UserName VARCHAR(255) NOT NULL DEFAULT '',
    PasswordHash VARCHAR(500) NOT NULL DEFAULT '',
    Is2FactorAuth BOOLEAN DEFAULT FALSE,
    LoginPhoneNumber VARCHAR(50) NOT NULL DEFAULT '',
    DateCreated DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    DateModified DATETIME,
    DateDeleted DATETIME,
    IsDeleted BOOLEAN DEFAULT FALSE,
    IsDisabled BOOLEAN DEFAULT FALSE,
    FOREIGN KEY (UserKey) REFERENCES Users(`Key`) ON DELETE CASCADE,
    INDEX idx_user_key (UserKey),
    INDEX idx_username (UserName)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- AnalysisResults table
CREATE TABLE IF NOT EXISTS AnalysisResults (
    `Key` CHAR(36) NOT NULL PRIMARY KEY,
    Discriminator VARCHAR(100) NOT NULL,
    UserKey CHAR(36) NOT NULL,
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
    INDEX idx_timestamp (Timestamp),
    INDEX idx_discriminator (Discriminator)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- DeviceAlerts table (TPH with Discriminator)
CREATE TABLE IF NOT EXISTS DeviceAlerts (
    `Key` CHAR(36) NOT NULL PRIMARY KEY,
    Discriminator VARCHAR(50) NOT NULL,
    AlertType VARCHAR(100) NOT NULL DEFAULT '',
    Priority INT,
    Timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Token VARCHAR(500) NOT NULL DEFAULT '',
    DeviceUid VARCHAR(255) NOT NULL DEFAULT '',
    DeviceType INT,
    OperatingSystem INT,
    MAC VARCHAR(50) NOT NULL DEFAULT '',
    UserKey CHAR(36),
    -- RemoteAccessAlert specific
    RemoteAccessApp INT,
    RunningProcesses INT,
    ConnectionUrl VARCHAR(2000),
    ConnectionStatus INT,
    ConnectionsCount INT,
    SessionStatus INT,
    -- UrlAlert specific
    Url VARCHAR(2000),
    TrackerKeys VARCHAR(5000),
    IFrameDomains VARCHAR(5000),
    UserAgent VARCHAR(1000),
    -- Common Entity fields
    DateCreated DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    DateModified DATETIME,
    DateDeleted DATETIME,
    IsDeleted BOOLEAN DEFAULT FALSE,
    IsDisabled BOOLEAN DEFAULT FALSE,
    INDEX idx_device_uid (DeviceUid),
    INDEX idx_timestamp (Timestamp),
    INDEX idx_user_key (UserKey),
    INDEX idx_priority (Priority),
    INDEX idx_discriminator (Discriminator)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

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
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- EF Migrations History
CREATE TABLE IF NOT EXISTS __EFMigrationsHistory (
    MigrationId VARCHAR(150) NOT NULL PRIMARY KEY,
    ProductVersion VARCHAR(32) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) 
VALUES ('00000000000000_InitialCreate', '8.0.2')
ON DUPLICATE KEY UPDATE ProductVersion = ProductVersion;

-- Sample Data with GUIDs - ALL FIELDS POPULATED (no NULLs for non-nullable fields)
INSERT INTO Users (`Key`, KeycloakUserId, FirstName, LastName, Address, City, State, Zip, Country, PhoneNumber, Role, DateCreated)
VALUES 
    (
        '550e8400-e29b-41d4-a716-446655440001',
        'keycloak-john-001',
        'John',
        'Doe',
        '123 Main St',
        'Springfield',
        'IL',
        '62701',
        'USA',
        '555-0100',
        1,  -- Self
        NOW()
    ),
    (
        '550e8400-e29b-41d4-a716-446655440002',
        'keycloak-jane-002',
        'Jane',
        'Smith',
        '456 Oak Ave',
        'Chicago',
        'IL',
        '60601',
        'USA',
        '555-0200',
        1,  -- Self
        NOW()
    )
ON DUPLICATE KEY UPDATE FirstName = FirstName;

-- User Devices - ALL REQUIRED FIELDS POPULATED
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
    MAC,
    DateCreated,
    IsDeleted
)
VALUES
    -- John's Personal Computer
    (
        '650e8400-e29b-41d4-a716-446655440001',
        'PC',
        '550e8400-e29b-41d4-a716-446655440001',
        'PC-JOHN-001',
        1,  -- PersonalComputer
        1,  -- Windows
        1,  -- Enabled
        'Dell',
        'XPS 15',
        '00:11:22:33:44:55',
        NOW(),
        0
    ),
    -- John's Smartphone
    (
        '650e8400-e29b-41d4-a716-446655440002',
        'Phone',
        '550e8400-e29b-41d4-a716-446655440001',
        'PHONE-JOHN-001',
        2,  -- SmartPhone
        5,  -- IOS
        1,  -- Enabled
        'Apple',
        'iPhone 14',
        'AA:BB:CC:DD:EE:FF',
        NOW(),
        0
    ),
    -- Jane's Personal Computer
    (
        '650e8400-e29b-41d4-a716-446655440003',
        'PC',
        '550e8400-e29b-41d4-a716-446655440002',
        'PC-JANE-001',
        1,  -- PersonalComputer
        1,  -- Windows
        1,  -- Enabled
        'HP',
        'Pavilion',
        '11:22:33:44:55:66',
        NOW(),
        0
    ),
    -- Jane's Smartphone
    (
        '650e8400-e29b-41d4-a716-446655440004',
        'Phone',
        '550e8400-e29b-41d4-a716-446655440002',
        'PHONE-JANE-001',
        2,  -- SmartPhone
        4,  -- Android
        1,  -- Enabled
        'Samsung',
        'Galaxy S23',
        '22:33:44:55:66:77',
        NOW(),
        0
    )
ON DUPLICATE KEY UPDATE Make = Make;

SELECT '========================================' AS '';
SELECT 'Database created successfully with GUID keys!' as Status;
SELECT '========================================' AS '';

-- Show created users
SELECT 'USERS:' AS '';
SELECT `Key`, FirstName, LastName, KeycloakUserId, Address, City, PhoneNumber FROM Users;

-- Show created devices
SELECT '' AS '';
SELECT 'USER DEVICES:' AS '';
SELECT `Key`, UserKey, DeviceUid, Make, Model, Discriminator, OperatingSystem, MAC
FROM UserDevices
ORDER BY UserKey, Discriminator;

-- Show all tables
SELECT '' AS '';
SELECT 'TABLES:' AS '';
SHOW TABLES;

SELECT '' AS '';
SELECT '========================================' AS '';
SELECT 'Test Devices for Alert Testing:' AS '';
SELECT '  PC-JOHN-001    - John Doe Desktop (Windows)' AS '';
SELECT '  PHONE-JOHN-001 - John Doe iPhone (IOS)' AS '';
SELECT '  PC-JANE-001    - Jane Smith Laptop (Windows)' AS '';
SELECT '  PHONE-JANE-001 - Jane Smith Android' AS '';
SELECT '========================================' AS '';
