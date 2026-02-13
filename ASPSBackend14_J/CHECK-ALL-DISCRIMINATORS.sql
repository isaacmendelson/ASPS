-- Check all discriminator values across all tables

USE ASPSBackend2DB;

-- Check AnalysisResults discriminators
SELECT 'AnalysisResults' as TableName, Discriminator, COUNT(*) as Count
FROM AnalysisResults
GROUP BY Discriminator

UNION ALL

-- Check DeviceAlerts discriminators
SELECT 'DeviceAlerts' as TableName, Discriminator, COUNT(*) as Count
FROM DeviceAlerts
GROUP BY Discriminator

UNION ALL

-- Check UserDevices discriminators
SELECT 'UserDevices' as TableName, Discriminator, COUNT(*) as Count
FROM UserDevices
GROUP BY Discriminator

ORDER BY TableName, Discriminator;
