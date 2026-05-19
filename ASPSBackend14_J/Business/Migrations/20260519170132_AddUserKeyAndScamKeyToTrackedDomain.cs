using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Business.Migrations
{
    /// <inheritdoc />
    public partial class AddUserKeyAndScamKeyToTrackedDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScamInProgressKey",
                table: "TrackedDomains",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "UserKey",
                table: "TrackedDomains",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScamInProgressKey",
                table: "TrackedDomains");

            migrationBuilder.DropColumn(
                name: "UserKey",
                table: "TrackedDomains");
        }
    }
}
