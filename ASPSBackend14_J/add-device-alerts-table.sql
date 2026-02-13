-- Add DeviceAlerts table to existing database
-- Run this after creating the database with create-database-clean.sql or create-database.sql

USE ASPSBackend2DB;

-- Drop table if exists (for clean recreation)
DROP TABLE IF EXISTS DeviceAlerts;

-- Create DeviceAlerts table with discriminator for different alert types
CREATE TABLE DeviceAlerts (
    `Key` VARCHAR(500) NOT NULL PRIMARY KEY,
    Discriminator VARCHAR(50) NOT NULL,  -- 'RemoteAccess' or 'Url'
    AlertType VARCHAR(100) NOT NULL,
    Priority INT NOT NULL,
    `Timestamp` DATETIME NOT NULL,
    Token VARCHAR(500),
    DateCreated DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    IsDeleted TINYINT(1) NOT NULL DEFAULT 0,
    IsDisabled TINYINT(1) NOT NULL DEFAULT 0,
    
    -- Device Information (flattened)
    DeviceUid VARCHAR(500) NOT NULL,
    DeviceType INT NOT NULL,
    OperatingSystem INT NOT NULL,
    MAC VARCHAR(100),
    
    -- Optional: Link to user
    UserKey VARCHAR(500) NULL,
    
    -- RemoteAccessAlertEntity specific fields
    RemoteAccessApp INT NULL,
    RunningProcesses INT NULL,
    ConnectionUrl VARCHAR(2000) NULL,
    ConnectionStatus INT NULL,
    ConnectionsCount INT NULL,
    SessionStatus INT NULL,
    
    -- UrlAlertEntity specific fields
    Url VARCHAR(2000) NULL,
    TrackerKeys VARCHAR(4000) NULL,  -- JSON array
    IFrameDomains VARCHAR(4000) NULL,  -- JSON array
    UserAgent VARCHAR(1000) NULL,
    
    -- Indexes for performance
    INDEX idx_deviceuid (DeviceUid),
    INDEX idx_timestamp (`Timestamp`),
    INDEX idx_userkey (UserKey),
    INDEX idx_discriminator (Discriminator),
    INDEX idx_deleted (IsDeleted)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Success message
SELECT 'DeviceAlerts table created successfully' AS Status;
