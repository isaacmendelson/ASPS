using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Business.Migrations
{
    /// <inheritdoc />
    public partial class ASPS620_AddNotificationOutboxAndCursor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceNotificationCursors",
                columns: table => new
                {
                    DeviceUid = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastAcknowledgedMessageId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    LastAcknowledgedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TotalAcknowledged = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceNotificationCursors", x => x.DeviceUid);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OutboxNotifications",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DeviceUid = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserKeyField = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NotificationType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PayloadJson = table.Column<string>(type: "LONGTEXT", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeliveryAttempts = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxNotifications", x => x.MessageId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxNotifications_AcknowledgedAt",
                table: "OutboxNotifications",
                column: "AcknowledgedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxNotifications_CreatedAt",
                table: "OutboxNotifications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxNotifications_DeviceUid",
                table: "OutboxNotifications",
                column: "DeviceUid");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxNotifications_DeviceUid_AcknowledgedAt",
                table: "OutboxNotifications",
                columns: new[] { "DeviceUid", "AcknowledgedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceNotificationCursors");

            migrationBuilder.DropTable(
                name: "OutboxNotifications");
        }
    }
}
