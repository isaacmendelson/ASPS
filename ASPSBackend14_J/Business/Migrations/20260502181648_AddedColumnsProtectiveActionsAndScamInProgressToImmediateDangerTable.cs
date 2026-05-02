using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Business.Migrations
{
    /// <inheritdoc />
    public partial class AddedColumnsProtectiveActionsAndScamInProgressToImmediateDangerTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProtectiveActions",
                table: "ImmediateDangers");

            migrationBuilder.AddColumn<string>(
                name: "ProtectiveActionsJson",
                table: "ImmediateDangers",
                type: "TEXT",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ScamInProgressKey",
                table: "ImmediateDangers",
                type: "varchar(36)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ImmediateDangers_ScamInProgressKey",
                table: "ImmediateDangers",
                column: "ScamInProgressKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ImmediateDangers_ScamInProgressKey",
                table: "ImmediateDangers");

            migrationBuilder.DropColumn(
                name: "ProtectiveActionsJson",
                table: "ImmediateDangers");

            migrationBuilder.DropColumn(
                name: "ScamInProgressKey",
                table: "ImmediateDangers");

            migrationBuilder.AddColumn<string>(
                name: "ProtectiveActions",
                table: "ImmediateDangers",
                type: "TEXT",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
