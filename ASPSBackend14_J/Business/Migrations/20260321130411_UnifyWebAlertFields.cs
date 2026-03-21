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
            // Drop the duplicate columns that were created for UrlAlertEntity
            // (Url, UserAgent are now shared via WebAlertEntity base class)
            migrationBuilder.DropColumn(
                name: "UrlAlertEntity_TrackerKeys",
                table: "DeviceAlerts");

            migrationBuilder.DropColumn(
                name: "UrlAlertEntity_Url",
                table: "DeviceAlerts");

            migrationBuilder.DropColumn(
                name: "UrlAlertEntity_UserAgent",
                table: "DeviceAlerts");

            // Add TabId column for WebAlertEntity (shared by UrlAlert and TrackUrlAlert)
            migrationBuilder.AddColumn<string>(
                name: "TabId",
                table: "DeviceAlerts",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TabId",
                table: "DeviceAlerts");

            migrationBuilder.AddColumn<string>(
                name: "UrlAlertEntity_TrackerKeys",
                table: "DeviceAlerts",
                type: "TEXT",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "UrlAlertEntity_Url",
                table: "DeviceAlerts",
                type: "TEXT",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "UrlAlertEntity_UserAgent",
                table: "DeviceAlerts",
                type: "TEXT",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
