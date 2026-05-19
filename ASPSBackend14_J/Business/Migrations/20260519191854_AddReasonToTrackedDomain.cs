using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Business.Migrations
{
    /// <inheritdoc />
    public partial class AddReasonToTrackedDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "TrackedDomains",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Reason",
                table: "TrackedDomains");
        }
    }
}
