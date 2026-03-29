using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Business.Migrations
{
    /// <inheritdoc />
    public partial class AddedColumnsToAnalysisResultsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnalysisKeyField",
                table: "DeviceAlerts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "BankWebsites",
                columns: table => new
                {
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Domain = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BankName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Country = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    DateCreated = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DateModified = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DateDeleted = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankWebsites", x => x.Key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "BlacklistedPhoneNumbers",
                columns: table => new
                {
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PhoneNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Source = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DateCreated = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DateDeleted = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlacklistedPhoneNumbers", x => x.Key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
                name: "IX_BankWebsites_DateDeleted",
                table: "BankWebsites",
                column: "DateDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_BankWebsites_Domain",
                table: "BankWebsites",
                column: "Domain");

            migrationBuilder.CreateIndex(
                name: "IX_BankWebsites_IsActive",
                table: "BankWebsites",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_BlacklistedPhoneNumbers_DateDeleted",
                table: "BlacklistedPhoneNumbers",
                column: "DateDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_BlacklistedPhoneNumbers_PhoneNumber",
                table: "BlacklistedPhoneNumbers",
                column: "PhoneNumber");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankWebsites");

            migrationBuilder.DropTable(
                name: "BlacklistedPhoneNumbers");

            migrationBuilder.DropTable(
                name: "SensitiveSites");

            migrationBuilder.DropColumn(
                name: "AnalysisKeyField",
                table: "DeviceAlerts");
        }
    }
}
