using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Business.Migrations
{
    /// <inheritdoc />
    public partial class AddTabClosedAlertAndImmediateDanger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ImmediateDanger",
                table: "DeviceAlerts",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TabClosedUrl",
                table: "DeviceAlerts",
                type: "TEXT",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImmediateDanger",
                table: "DeviceAlerts");

            migrationBuilder.DropColumn(
                name: "TabClosedUrl",
                table: "DeviceAlerts");
        }
    }
}
