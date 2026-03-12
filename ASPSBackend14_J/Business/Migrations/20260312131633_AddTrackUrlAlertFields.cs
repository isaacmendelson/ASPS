using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Business.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackUrlAlertFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First, convert existing large varchar columns to TEXT to free up row space
            // Using procedure to check column existence before modifying
            migrationBuilder.Sql(@"
                DROP PROCEDURE IF EXISTS ModifyColumnIfExists;
                CREATE PROCEDURE ModifyColumnIfExists()
                BEGIN
                    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'DeviceAlerts' AND COLUMN_NAME = 'Url' AND DATA_TYPE = 'varchar') THEN
                        ALTER TABLE DeviceAlerts MODIFY COLUMN Url TEXT NULL;
                    END IF;
                    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'DeviceAlerts' AND COLUMN_NAME = 'TrackerKeys' AND DATA_TYPE = 'varchar') THEN
                        ALTER TABLE DeviceAlerts MODIFY COLUMN TrackerKeys TEXT NULL;
                    END IF;
                    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'DeviceAlerts' AND COLUMN_NAME = 'IFrameDomains' AND DATA_TYPE = 'varchar') THEN
                        ALTER TABLE DeviceAlerts MODIFY COLUMN IFrameDomains TEXT NULL;
                    END IF;
                    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'DeviceAlerts' AND COLUMN_NAME = 'UserAgent' AND DATA_TYPE = 'varchar') THEN
                        ALTER TABLE DeviceAlerts MODIFY COLUMN UserAgent TEXT NULL;
                    END IF;
                    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'DeviceAlerts' AND COLUMN_NAME = 'ConnectionUrl' AND DATA_TYPE = 'varchar') THEN
                        ALTER TABLE DeviceAlerts MODIFY COLUMN ConnectionUrl TEXT NULL;
                    END IF;
                END;
                CALL ModifyColumnIfExists();
                DROP PROCEDURE IF EXISTS ModifyColumnIfExists;
            ");

            // Add TrackerCount if not exists
            migrationBuilder.Sql(@"
                SET @columnExists = (
                    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() 
                    AND TABLE_NAME = 'DeviceAlerts' 
                    AND COLUMN_NAME = 'TrackerCount'
                );
                SET @sql = IF(@columnExists = 0, 
                    'ALTER TABLE DeviceAlerts ADD COLUMN TrackerCount INT NULL', 
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Add TrackingType if not exists
            migrationBuilder.Sql(@"
                SET @columnExists = (
                    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() 
                    AND TABLE_NAME = 'DeviceAlerts' 
                    AND COLUMN_NAME = 'TrackingType'
                );
                SET @sql = IF(@columnExists = 0, 
                    'ALTER TABLE DeviceAlerts ADD COLUMN TrackingType VARCHAR(100) NULL', 
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrackerCount",
                table: "DeviceAlerts");

            migrationBuilder.DropColumn(
                name: "TrackingType",
                table: "DeviceAlerts");
        }
    }
}
