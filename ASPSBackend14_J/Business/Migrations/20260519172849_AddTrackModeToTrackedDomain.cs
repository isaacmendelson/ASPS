using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Business.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackModeToTrackedDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TrackMode",
                table: "TrackedDomains",
                type: "int",
                nullable: false,
                // 1 = Surf (Common.Enums.TrackMode). Existing rows back-fill
                // to the sensible tracking default rather than 0=None.
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrackMode",
                table: "TrackedDomains");
        }
    }
}
