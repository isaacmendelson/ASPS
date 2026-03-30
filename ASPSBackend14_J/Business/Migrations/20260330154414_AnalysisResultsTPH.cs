using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Business.Migrations
{
    /// <inheritdoc />
    public partial class AnalysisResultsTPH : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Domain and Url columns already exist from a prior migration.
            // Only add the new columns that are missing.

            migrationBuilder.AddColumn<string>(
                name: "FromUrl",
                table: "AnalysisResults",
                type: "TEXT",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "RemoteAccessApp",
                table: "AnalysisResults",
                type: "int",
                nullable: true,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SessionStatus",
                table: "AnalysisResults",
                type: "int",
                nullable: true,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FromUrl",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "RemoteAccessApp",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "SessionStatus",
                table: "AnalysisResults");
        }
    }
}
