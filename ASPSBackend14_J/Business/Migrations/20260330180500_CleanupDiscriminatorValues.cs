using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Business.Migrations
{
    /// <summary>
    /// Migration to cleanup discriminator values in AnalysisResults table:
    /// 1. Convert old 'Vm' suffix values to new format (without Vm)
    /// 2. Delete records with empty discriminator
    /// </summary>
    public partial class CleanupDiscriminatorValues : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert old discriminator values (with Vm suffix) to new format (without Vm)
            migrationBuilder.Sql(
                "UPDATE AnalysisResults SET Discriminator = 'UrlAnalysisResult' WHERE Discriminator = 'UrlAnalysisResultVm';");
            
            migrationBuilder.Sql(
                "UPDATE AnalysisResults SET Discriminator = 'TrackUrlAnalysisResult' WHERE Discriminator = 'TrackUrlAnalysisResultVm';");
            
            migrationBuilder.Sql(
                "UPDATE AnalysisResults SET Discriminator = 'RemoteAccessAnalysisResult' WHERE Discriminator = 'RemoteAccessAnalysisResultVm';");

            // Convert short/invalid discriminator values to correct format
            migrationBuilder.Sql(
                "UPDATE AnalysisResults SET Discriminator = 'UrlAnalysisResult' WHERE Discriminator = 'UrlAlert';");
            
            migrationBuilder.Sql(
                "UPDATE AnalysisResults SET Discriminator = 'TrackUrlAnalysisResult' WHERE Discriminator = 'TrackUrlAlert';");
            
            migrationBuilder.Sql(
                "UPDATE AnalysisResults SET Discriminator = 'RemoteAccessAnalysisResult' WHERE Discriminator = 'RemoteAccessAlert';");

            // Delete records with empty or null discriminator
            migrationBuilder.Sql(
                "DELETE FROM AnalysisResults WHERE Discriminator = '' OR Discriminator IS NULL;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert to old discriminator values (with Vm suffix)
            migrationBuilder.Sql(
                "UPDATE AnalysisResults SET Discriminator = 'UrlAnalysisResultVm' WHERE Discriminator = 'UrlAnalysisResult';");
            
            migrationBuilder.Sql(
                "UPDATE AnalysisResults SET Discriminator = 'TrackUrlAnalysisResultVm' WHERE Discriminator = 'TrackUrlAnalysisResult';");
            
            migrationBuilder.Sql(
                "UPDATE AnalysisResults SET Discriminator = 'RemoteAccessAnalysisResultVm' WHERE Discriminator = 'RemoteAccessAnalysisResult';");
            
            // Note: Cannot restore deleted records with empty discriminator
        }
    }
}
