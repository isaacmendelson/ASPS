using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Business.Migrations
{
    /// <inheritdoc />
    public partial class UnifyWebAlertFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop duplicate columns IF THEY EXIST (they may not exist in all environments)
            // Using raw SQL with IF EXISTS for MySQL compatibility
            migrationBuilder.Sql(@"
                SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'DeviceAlerts' AND COLUMN_NAME = 'UrlAlertEntity_TrackerKeys' AND TABLE_SCHEMA = DATABASE());
                SET @sql = IF(@col_exists > 0, 'ALTER TABLE DeviceAlerts DROP COLUMN UrlAlertEntity_TrackerKeys', 'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'DeviceAlerts' AND COLUMN_NAME = 'UrlAlertEntity_Url' AND TABLE_SCHEMA = DATABASE());
                SET @sql = IF(@col_exists > 0, 'ALTER TABLE DeviceAlerts DROP COLUMN UrlAlertEntity_Url', 'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'DeviceAlerts' AND COLUMN_NAME = 'UrlAlertEntity_UserAgent' AND TABLE_SCHEMA = DATABASE());
                SET @sql = IF(@col_exists > 0, 'ALTER TABLE DeviceAlerts DROP COLUMN UrlAlertEntity_UserAgent', 'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Add TabId column IF NOT EXISTS
            migrationBuilder.Sql(@"
                SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'DeviceAlerts' AND COLUMN_NAME = 'TabId' AND TABLE_SCHEMA = DATABASE());
                SET @sql = IF(@col_exists = 0, 'ALTER TABLE DeviceAlerts ADD COLUMN TabId VARCHAR(100) NULL', 'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'DeviceAlerts' AND COLUMN_NAME = 'TabId' AND TABLE_SCHEMA = DATABASE());
                SET @sql = IF(@col_exists > 0, 'ALTER TABLE DeviceAlerts DROP COLUMN TabId', 'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Note: We don't recreate the duplicate columns in Down() 
            // since they shouldn't exist in the new schema
        }
    }
}
