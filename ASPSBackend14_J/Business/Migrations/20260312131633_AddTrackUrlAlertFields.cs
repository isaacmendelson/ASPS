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
