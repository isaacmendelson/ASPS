START TRANSACTION;

ALTER TABLE `DeviceAlerts` ADD `SchemaVersion` varchar(16) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `DeviceAlerts` ADD `MessageId` char(36) CHARACTER SET ascii NULL;

ALTER TABLE `DeviceAlerts` ADD `CorrelationId` char(36) CHARACTER SET ascii NULL;

ALTER TABLE `DeviceAlerts` ADD `RequestId` char(36) CHARACTER SET ascii NULL;

ALTER TABLE `DeviceAlerts` ADD `CanonicalUrl` TEXT CHARACTER SET utf8mb4 NULL;

CREATE UNIQUE INDEX `IX_DeviceAlerts_MessageId` ON `DeviceAlerts` (`MessageId`);

CREATE INDEX `IX_DeviceAlerts_CorrelationId` ON `DeviceAlerts` (`CorrelationId`);

CREATE INDEX `IX_DeviceAlerts_RequestId` ON `DeviceAlerts` (`RequestId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260728190000_ASPS611_AddMessageEnvelopeIdentity', '7.0.20');

COMMIT;

