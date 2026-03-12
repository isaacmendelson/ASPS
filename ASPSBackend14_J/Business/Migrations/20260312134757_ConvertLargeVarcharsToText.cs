using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Business.Migrations
{
    /// <inheritdoc />
    public partial class ConvertLargeVarcharsToText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert large varchar columns to TEXT to avoid row size limit
            // These columns are in DeviceAlerts table (TPH inheritance)
            
            migrationBuilder.Sql(@"
                ALTER TABLE DeviceAlerts 
                MODIFY COLUMN Url TEXT NULL,
                MODIFY COLUMN TrackerKeys TEXT NULL,
                MODIFY COLUMN IFrameDomains TEXT NULL,
                MODIFY COLUMN UserAgent TEXT NULL,
                MODIFY COLUMN ConnectionUrl TEXT NULL,
                MODIFY COLUMN UrlAlertEntity_Url TEXT NULL,
                MODIFY COLUMN UrlAlertEntity_TrackerKeys TEXT NULL,
                MODIFY COLUMN UrlAlertEntity_UserAgent TEXT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE DeviceAlerts 
                MODIFY COLUMN Url VARCHAR(2000) NULL,
                MODIFY COLUMN TrackerKeys VARCHAR(5000) NULL,
                MODIFY COLUMN IFrameDomains VARCHAR(5000) NULL,
                MODIFY COLUMN UserAgent VARCHAR(1000) NULL,
                MODIFY COLUMN ConnectionUrl VARCHAR(2000) NULL,
                MODIFY COLUMN UrlAlertEntity_Url VARCHAR(2000) NULL,
                MODIFY COLUMN UrlAlertEntity_TrackerKeys VARCHAR(5000) NULL,
                MODIFY COLUMN UrlAlertEntity_UserAgent VARCHAR(1000) NULL;
            ");
        }
    }
}
