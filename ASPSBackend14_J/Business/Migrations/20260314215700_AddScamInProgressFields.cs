using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Business.Migrations
{
    /// <inheritdoc />
    public partial class AddScamInProgressFields : Migration
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

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add missing TrackUrlAlertEntity fields
            AddColumnIfNotExists(migrationBuilder, "FromUrl", "TEXT NULL");
            AddColumnIfNotExists(migrationBuilder, "Duration", "INT NULL");
            AddColumnIfNotExists(migrationBuilder, "ScamInProgressKey", "VARCHAR(255) NULL");
            AddColumnIfNotExists(migrationBuilder, "TabId", "VARCHAR(255) NULL");
            AddColumnIfNotExists(migrationBuilder, "Timezone", "VARCHAR(100) NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "FromUrl", table: "DeviceAlerts");
            migrationBuilder.DropColumn(name: "Duration", table: "DeviceAlerts");
            migrationBuilder.DropColumn(name: "ScamInProgressKey", table: "DeviceAlerts");
            migrationBuilder.DropColumn(name: "TabId", table: "DeviceAlerts");
            migrationBuilder.DropColumn(name: "Timezone", table: "DeviceAlerts");
        }
    }
}
