using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Business.Migrations
{
    /// <inheritdoc />
    public partial class AddSensitiveSitesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SensitiveSites",
                columns: table => new
                {
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DomainPattern = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RiskMultiplier = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 2.0m),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    DateCreated = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DateModified = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DateDeleted = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SensitiveSites", x => x.Key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SensitiveSites_Category",
                table: "SensitiveSites",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_SensitiveSites_DateDeleted",
                table: "SensitiveSites",
                column: "DateDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_SensitiveSites_DomainPattern",
                table: "SensitiveSites",
                column: "DomainPattern");

            migrationBuilder.CreateIndex(
                name: "IX_SensitiveSites_IsActive",
                table: "SensitiveSites",
                column: "IsActive");

            // Insert default sensitive sites as per requirements
            migrationBuilder.Sql(@"
                INSERT INTO SensitiveSites (DomainPattern, Category, RiskMultiplier, IsActive, DateCreated, DateModified)
                VALUES 
                    ('bankleumi.co.il', 'Banking', 2.5, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP()),
                    ('*.paypal.com', 'Banking', 2.5, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP()),
                    ('binance.com', 'Crypto', 3.0, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP()),
                    ('*.binance.com', 'Crypto', 3.0, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP()),
                    ('coinbase.com', 'Crypto', 3.0, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP()),
                    ('*.coinbase.com', 'Crypto', 3.0, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP()),
                    ('*.gov.il', 'Government', 2.0, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP()),
                    ('etoro.com', 'Trading', 2.5, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP()),
                    ('*.etoro.com', 'Trading', 2.5, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP()),
                    ('interactivebrokers.com', 'Trading', 2.5, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP()),
                    ('*.interactivebrokers.com', 'Trading', 2.5, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP());
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SensitiveSites");
        }
    }
}
