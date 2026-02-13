-- Fix incorrect discriminator values in AnalysisResults table
-- Issue: Records have "UrlAlert" but should have "UrlAnalysisResult"

USE ASPSBackend2DB;

-- Step 1: Check current discriminator values
SELECT Discriminator, COUNT(*) as Count
FROM AnalysisResults
GROUP BY Discriminator;

-- Step 2: Update incorrect discriminators
-- Change "UrlAlert" to "UrlAnalysisResult"
UPDATE AnalysisResults
SET Discriminator = 'UrlAnalysisResult'
WHERE Discriminator = 'UrlAlert';

-- Step 3: Verify the fix
SELECT Discriminator, COUNT(*) as Count
FROM AnalysisResults
GROUP BY Discriminator;

-- Step 4: Show sample records
SELECT `Key`, Discriminator, Timestamp, UserKey
FROM AnalysisResults
ORDER BY Timestamp DESC
LIMIT 5;
