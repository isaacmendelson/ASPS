-- ASPSBackend System2 Database Creation Script (CLEAN - No Sample Data)
-- Use this if you want to start with an empty database

DROP DATABASE IF EXISTS ASPSBackend2DB;
CREATE DATABASE ASPSBackend2DB;
USE ASPSBackend2DB;

-- Users table
CREATE TABLE Users (
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

-- UserDevices table (Discriminator is Key.Type value)
CREATE TABLE UserDevices (
    `Key` VARCHAR(500) PRIMARY KEY,
    Discriminator VARCHAR(50) NOT NULL,
    AggregateVersionField INT DEFAULT 0,
    UserKey VARCHAR(500) NULL,
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
    FOREIGN KEY (UserKey) REFERENCES Users(`Key`) ON DELETE SET NULL,
    INDEX idx_device_uid (DeviceUid),
    INDEX idx_user_key (UserKey)
);

-- UserAccounts table
CREATE TABLE UserAccounts (
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

-- AnalysisResults table (Discriminator is Key.Type value)
CREATE TABLE AnalysisResults (
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
CREATE TABLE AlertFlags (
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
CREATE TABLE __EFMigrationsHistory (
    MigrationId VARCHAR(150) NOT NULL PRIMARY KEY,
    ProductVersion VARCHAR(32) NOT NULL
);

INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) 
VALUES ('00000000000000_InitialCreate', '8.0.2');

SELECT 'Clean database created successfully!' as Status;
SELECT 'No sample data - use API to create users' as Info;
SELECT 'Note: Tag and TypeName are computed properties, not stored in DB' as Note;
SELECT 'Note: Discriminator column stores Key.Type value' as Note2;
SHOW TABLES;
