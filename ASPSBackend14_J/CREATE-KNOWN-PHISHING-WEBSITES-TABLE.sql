-- ============================================================================
-- Migration: Create KnownPhishingWebsites Table
-- Description: Stores known phishing URLs for URL analysis
-- Date: 2026-01-12
-- ============================================================================

USE ASPSBackend2DB;

-- Drop table if exists (for clean migration)
-- DROP TABLE IF EXISTS KnownPhishingWebsites;

-- Create KnownPhishingWebsites table
CREATE TABLE IF NOT EXISTS KnownPhishingWebsites (
    `Key` INT AUTO_INCREMENT PRIMARY KEY,
    Url TEXT NOT NULL,
    Domain VARCHAR(255) NOT NULL,
    DateCreated DATETIME NOT NULL,
    DateDeleted DATETIME NULL,
    Source VARCHAR(100) NULL,
    
    -- Indexes for performance
    INDEX idx_domain (Domain),
    INDEX idx_date_deleted (DateDeleted),
    INDEX idx_url (Url(255))  -- Index first 255 chars of TEXT column
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Verify table creation
SELECT 
    'Table created successfully' AS Status,
    COUNT(*) AS RecordCount 
FROM 
    KnownPhishingWebsites;

-- Show table structure
DESCRIBE KnownPhishingWebsites;
