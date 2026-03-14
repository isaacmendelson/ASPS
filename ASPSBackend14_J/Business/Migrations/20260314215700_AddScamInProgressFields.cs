using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Business.Migrations
{
    /// <inheritdoc />
    public partial class AddScamInProgressFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "FromUrl", table: "DeviceAlerts", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<int>(name: "Duration", table: "DeviceAlerts", type: "int", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ScamInProgressKey", table: "DeviceAlerts", type: "varchar(255)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "TabId", table: "DeviceAlerts", type: "varchar(255)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "Timezone", table: "DeviceAlerts", type: "varchar(100)", nullable: true);
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
