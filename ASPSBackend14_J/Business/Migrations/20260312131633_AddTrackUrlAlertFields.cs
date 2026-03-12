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
            migrationBuilder.AddColumn<int>(
                name: "TrackerCount",
                table: "DeviceAlerts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrackingType",
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
                name: "TrackerCount",
                table: "DeviceAlerts");

            migrationBuilder.DropColumn(
                name: "TrackingType",
                table: "DeviceAlerts");
        }
    }
}
