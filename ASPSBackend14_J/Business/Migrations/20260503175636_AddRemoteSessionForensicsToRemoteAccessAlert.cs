using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Business.Migrations
{
    /// <inheritdoc />
    public partial class AddRemoteSessionForensicsToRemoteAccessAlert : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConnectionId",
                table: "DeviceAlerts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LoggedUser",
                table: "DeviceAlerts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RemoteId",
                table: "DeviceAlerts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RemoteName",
                table: "DeviceAlerts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Software",
                table: "DeviceAlerts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConnectionId",
                table: "DeviceAlerts");

            migrationBuilder.DropColumn(
                name: "LoggedUser",
                table: "DeviceAlerts");

            migrationBuilder.DropColumn(
                name: "RemoteId",
                table: "DeviceAlerts");

            migrationBuilder.DropColumn(
                name: "RemoteName",
                table: "DeviceAlerts");

            migrationBuilder.DropColumn(
                name: "Software",
                table: "DeviceAlerts");
        }
    }
}
