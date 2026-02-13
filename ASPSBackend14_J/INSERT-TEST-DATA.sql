-- Insert test data into ASPSBackend2DB database
USE aspsbackend2db;

-- Insert a test user
INSERT INTO Users (`Key`, KeycloakUserId, FirstName, LastName, Address, City, State, Zip, Country, PhoneNumber, Role, GuardianKey, Locale, Timezone, DateCreated, DateModified, DateDeleted, IsDeleted, IsDisabled)
VALUES 
('11111111-1111-1111-1111-111111111111', 'keycloak-user-001', 'John', 'Doe', '123 Main St', 'New York', 'NY', '10001', 'USA', '+1-555-0100', 0, NULL, 'en-US', -5, NOW(), NULL, NULL, 0, 0),
('22222222-2222-2222-2222-222222222222', 'keycloak-user-002', 'Jane', 'Smith', '456 Oak Ave', 'Los Angeles', 'CA', '90001', 'USA', '+1-555-0200', 0, NULL, 'en-US', -8, NOW(), NULL, NULL, 0, 0);

-- Insert test devices for John Doe
INSERT INTO UserDevices (`Key`, UserKey, Discriminator, DeviceType, DeviceUid, PhoneNumber, OperatingSystem, MAC, IMEI, BiosSerial, Make, Model, Serial, MonitoringStatus, AggregateVersionField, Type, MotherboardSerial, UserAgent, Timezone, DateCreated, IsDeleted, IsDisabled)
VALUES 
('33333333-3333-3333-3333-333333333333', '11111111-1111-1111-1111-111111111111', 'PC', 0, 'PC-JOHN-001', NULL, 0, 'AA:BB:CC:DD:EE:01', NULL, 'BIOS-001', 'Dell', 'Latitude 5420', 'SN-001', 1, 0, 0, 'MB-001', 'Mozilla/5.0', -5, NOW(), 0, 0),
('44444444-4444-4444-4444-444444444444', '11111111-1111-1111-1111-111111111111', 'Phone', 1, 'PHONE-JOHN-001', '+1-555-0101', 1, NULL, 'IMEI-001', NULL, 'Apple', 'iPhone 13', 'SN-002', 1, 0, NULL, NULL, NULL, NULL, NOW(), 0, 0);

-- Insert test devices for Jane Smith
INSERT INTO UserDevices (`Key`, UserKey, Discriminator, DeviceType, DeviceUid, PhoneNumber, OperatingSystem, MAC, IMEI, BiosSerial, Make, Model, Serial, MonitoringStatus, AggregateVersionField, Type, MotherboardSerial, UserAgent, Timezone, DateCreated, IsDeleted, IsDisabled)
VALUES 
('55555555-5555-5555-5555-555555555555', '22222222-2222-2222-2222-222222222222', 'PC', 0, 'PC-JANE-001', NULL, 0, 'AA:BB:CC:DD:EE:02', NULL, 'BIOS-002', 'HP', 'EliteBook 840', 'SN-003', 1, 0, 0, 'MB-002', 'Mozilla/5.0', -8, NOW(), 0, 0);

-- Insert test user accounts
INSERT INTO UserAccounts (`Key`, UserKey, AccountType, LoginUrl, UserName, PasswordHash, Is2FactorAuth, LoginPhoneNumber, DateCreated, IsDeleted, IsDisabled)
VALUES 
('66666666-6666-6666-6666-666666666666', '11111111-1111-1111-1111-111111111111', 0, 'https://gmail.com', 'john.doe@gmail.com', 'hashed_password_001', 1, '+1-555-0101', NOW(), 0, 0),
('77777777-7777-7777-7777-777777777777', '22222222-2222-2222-2222-222222222222', 1, 'https://facebook.com', 'jane.smith', 'hashed_password_002', 0, '', NOW(), 0, 0);

SELECT 'Test data inserted successfully!' AS Status;
SELECT COUNT(*) AS UserCount FROM Users;
SELECT COUNT(*) AS DeviceCount FROM UserDevices;
SELECT COUNT(*) AS AccountCount FROM UserAccounts;
