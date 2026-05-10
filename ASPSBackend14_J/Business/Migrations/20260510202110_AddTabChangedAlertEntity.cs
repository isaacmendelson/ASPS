using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Business.Migrations
{
    /// <inheritdoc />
    public partial class AddTabChangedAlertEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLoggedIn",
                table: "DeviceAlerts",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSensitiveWebsite",
                table: "DeviceAlerts",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TabChangedUrl",
                table: "DeviceAlerts",
                type: "TEXT",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLoggedIn",
                table: "DeviceAlerts");

            migrationBuilder.DropColumn(
                name: "IsSensitiveWebsite",
                table: "DeviceAlerts");

            migrationBuilder.DropColumn(
                name: "TabChangedUrl",
                table: "DeviceAlerts");
        }
    }
}
