using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Business.Migrations
{
    /// <inheritdoc />
    public partial class SCRUM904_AddUserRiskScoreAndConsentFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserConsentAuditLog",
                columns: table => new
                {
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserKey = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Source = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OldLevel = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NewLevel = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ChangedBy = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ChangedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserConsentAuditLog", x => x.Key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserConsentPreferences",
                columns: table => new
                {
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserKey = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Source = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Level = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GrantedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModified = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    GrantedBy = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserConsentPreferences", x => x.Key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserRiskScoreHistories",
                columns: table => new
                {
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserKey = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Confidence = table.Column<double>(type: "double", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    VulnerabilityScore = table.Column<double>(type: "double", nullable: false),
                    ExposureScore = table.Column<double>(type: "double", nullable: false),
                    LiveScore = table.Column<double>(type: "double", nullable: false),
                    CorrelationScore = table.Column<double>(type: "double", nullable: false),
                    SerializedSnapshot = table.Column<string>(type: "LONGTEXT", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRiskScoreHistories", x => x.Key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_UserConsentAuditLog_ChangedAt",
                table: "UserConsentAuditLog",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserConsentAuditLog_UserKey",
                table: "UserConsentAuditLog",
                column: "UserKey");

            migrationBuilder.CreateIndex(
                name: "IX_UserConsentPreferences_UserKey",
                table: "UserConsentPreferences",
                column: "UserKey");

            migrationBuilder.CreateIndex(
                name: "IX_UserConsentPreferences_UserKey_Source",
                table: "UserConsentPreferences",
                columns: new[] { "UserKey", "Source" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRiskScoreHistories_ComputedAt",
                table: "UserRiskScoreHistories",
                column: "ComputedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserRiskScoreHistories_UserKey",
                table: "UserRiskScoreHistories",
                column: "UserKey");

            migrationBuilder.CreateIndex(
                name: "IX_UserRiskScoreHistories_UserKey_ComputedAt",
                table: "UserRiskScoreHistories",
                columns: new[] { "UserKey", "ComputedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserConsentAuditLog");

            migrationBuilder.DropTable(
                name: "UserConsentPreferences");

            migrationBuilder.DropTable(
                name: "UserRiskScoreHistories");
        }
    }
}
