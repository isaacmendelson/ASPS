-- Check what's in the Users table
SELECT * FROM Users;

-- Check table structure
DESCRIBE Users;

-- Check for NULL values in Key column
SELECT COUNT(*) as NullKeys FROM Users WHERE `Key` IS NULL;
SELECT COUNT(*) as EmptyKeys FROM Users WHERE `Key` = '';
