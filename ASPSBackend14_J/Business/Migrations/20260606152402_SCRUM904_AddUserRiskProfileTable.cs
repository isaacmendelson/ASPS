using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Business.Migrations
{
    /// <inheritdoc />
    public partial class SCRUM904_AddUserRiskProfileTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserRiskProfiles",
                columns: table => new
                {
                    UserKey = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VulnerabilityScore = table.Column<double>(type: "double", nullable: false),
                    ExposureScore = table.Column<double>(type: "double", nullable: false),
                    RiskyUrlWeight = table.Column<double>(type: "double", nullable: false),
                    SuspiciousCallWeight = table.Column<double>(type: "double", nullable: false),
                    RemoteAccessWeight = table.Column<double>(type: "double", nullable: false),
                    ScamInProgressWeight = table.Column<double>(type: "double", nullable: false),
                    AggregationPeriodDays = table.Column<int>(type: "int", nullable: false),
                    TimeDecayFactor = table.Column<double>(type: "double", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRiskProfiles", x => x.UserKey);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserRiskProfiles");
        }
    }
}
