using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Business.Migrations
{
    /// <inheritdoc />
    public partial class AddRoadmapsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: EF auto-scaffolded DropColumn calls for WebsiteCategories.IsDeleted
            // and WebsiteCategories.IsDisabled here. Those drops were a pre-existing
            // model-vs-DB drift (the entity doesn't have those columns but the table
            // does). We removed them so this migration is strictly additive: it ONLY
            // creates the Roadmaps table. The drift should be resolved separately.

            migrationBuilder.CreateTable(
                name: "Roadmaps",
                columns: table => new
                {
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Data = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    DateCreated = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastUpdatedBy = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsArchived = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roadmaps", x => x.Key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Roadmaps_IsArchived",
                table: "Roadmaps",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_Roadmaps_Name",
                table: "Roadmaps",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down only undoes what Up did — drop the Roadmaps table.
            // (We removed the WebsiteCategories AddColumns scaffolded by EF;
            // see the matching note in Up.)
            migrationBuilder.DropTable(
                name: "Roadmaps");
        }
    }
}
