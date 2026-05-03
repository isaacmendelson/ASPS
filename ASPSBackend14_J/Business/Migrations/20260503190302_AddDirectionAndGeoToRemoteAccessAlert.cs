using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Business.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectionAndGeoToRemoteAccessAlert : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Confidence",
                table: "DeviceAlerts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Direction",
                table: "DeviceAlerts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RemoteCountry",
                table: "DeviceAlerts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RemoteCountryCode",
                table: "DeviceAlerts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Confidence",
                table: "DeviceAlerts");

            migrationBuilder.DropColumn(
                name: "Direction",
                table: "DeviceAlerts");

            migrationBuilder.DropColumn(
                name: "RemoteCountry",
                table: "DeviceAlerts");

            migrationBuilder.DropColumn(
                name: "RemoteCountryCode",
                table: "DeviceAlerts");
        }
    }
}
