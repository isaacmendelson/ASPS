using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Business.Data.EF;

#nullable disable

namespace Business.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260728190000_ASPS611_AddMessageEnvelopeIdentity")]
public partial class ASPS611_AddMessageEnvelopeIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("SchemaVersion", "DeviceAlerts", "varchar(16)", nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.AddColumn<Guid>("MessageId", "DeviceAlerts", "char(36)", nullable: true)
            .Annotation("MySql:CharSet", "ascii");
        migrationBuilder.AddColumn<Guid>("CorrelationId", "DeviceAlerts", "char(36)", nullable: true)
            .Annotation("MySql:CharSet", "ascii");
        migrationBuilder.AddColumn<Guid>("RequestId", "DeviceAlerts", "char(36)", nullable: true)
            .Annotation("MySql:CharSet", "ascii");
        migrationBuilder.AddColumn<string>("CanonicalUrl", "DeviceAlerts", "TEXT", nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.CreateIndex("IX_DeviceAlerts_MessageId", "DeviceAlerts", "MessageId", unique: true);
        migrationBuilder.CreateIndex("IX_DeviceAlerts_CorrelationId", "DeviceAlerts", "CorrelationId");
        migrationBuilder.CreateIndex("IX_DeviceAlerts_RequestId", "DeviceAlerts", "RequestId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_DeviceAlerts_MessageId", "DeviceAlerts");
        migrationBuilder.DropIndex("IX_DeviceAlerts_CorrelationId", "DeviceAlerts");
        migrationBuilder.DropIndex("IX_DeviceAlerts_RequestId", "DeviceAlerts");
        migrationBuilder.DropColumn("SchemaVersion", "DeviceAlerts");
        migrationBuilder.DropColumn("MessageId", "DeviceAlerts");
        migrationBuilder.DropColumn("CorrelationId", "DeviceAlerts");
        migrationBuilder.DropColumn("RequestId", "DeviceAlerts");
        migrationBuilder.DropColumn("CanonicalUrl", "DeviceAlerts");
    }
}
