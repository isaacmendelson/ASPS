using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Business.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackUrlAlertFields : Migration
    {
        private void AddColumnIfNotExists(MigrationBuilder migrationBuilder, string columnName, string columnDef)
        {
            migrationBuilder.Sql($@"
                SET @colExists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'DeviceAlerts' AND COLUMN_NAME = '{columnName}');
                SET @sql = IF(@colExists = 0, 'ALTER TABLE DeviceAlerts ADD COLUMN {columnName} {columnDef}', 'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");
        }

        private void ModifyColumnIfExists(MigrationBuilder migrationBuilder, string columnName, string newDef)
        {
            migrationBuilder.Sql($@"
                SET @colExists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'DeviceAlerts' AND COLUMN_NAME = '{columnName}');
                SET @sql = IF(@colExists > 0, 'ALTER TABLE DeviceAlerts MODIFY COLUMN {columnName} {newDef}', 'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert existing large varchar columns to TEXT
            ModifyColumnIfExists(migrationBuilder, "Url", "TEXT NULL");
            ModifyColumnIfExists(migrationBuilder, "TrackerKeys", "TEXT NULL");
            ModifyColumnIfExists(migrationBuilder, "IFrameDomains", "TEXT NULL");
            ModifyColumnIfExists(migrationBuilder, "UserAgent", "TEXT NULL");
            ModifyColumnIfExists(migrationBuilder, "ConnectionUrl", "TEXT NULL");

            // Add TrackUrlAlertEntity columns (TPH creates separate columns with prefix)
            AddColumnIfNotExists(migrationBuilder, "TrackerCount", "INT NULL");
            AddColumnIfNotExists(migrationBuilder, "TrackingType", "VARCHAR(100) NULL");
            AddColumnIfNotExists(migrationBuilder, "UrlAlertEntity_Url", "TEXT NULL");
            AddColumnIfNotExists(migrationBuilder, "UrlAlertEntity_TrackerKeys", "TEXT NULL");
            AddColumnIfNotExists(migrationBuilder, "UrlAlertEntity_UserAgent", "TEXT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TrackerCount", table: "DeviceAlerts");
            migrationBuilder.DropColumn(name: "TrackingType", table: "DeviceAlerts");
            migrationBuilder.DropColumn(name: "UrlAlertEntity_Url", table: "DeviceAlerts");
            migrationBuilder.DropColumn(name: "UrlAlertEntity_TrackerKeys", table: "DeviceAlerts");
            migrationBuilder.DropColumn(name: "UrlAlertEntity_UserAgent", table: "DeviceAlerts");
        }
    }
}
