using Business.RealtimeAnalysis;
using Business.RealtimeAnalysis.Indicators;
using Business.RealtimeAnalysis.UserDomain;
using Common.Enums;
using Common.Models;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ASPS.Tests.Business.UserDomain;

public class UDAnalysisResultTests
{
    [Fact]
    public void Constructor_ShouldInitializeAllProperties()
    {
        // Arrange
        var analysisLevel = AnalysisLevel.Device;
        var severity = Severity.High;
        var analyzerResults = new Dictionary<string, Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>>();
        var timestamp = DateTime.UtcNow;
        var mockConfig = new Mock<IConfiguration>();
        var user = CreateMockUser();

        // Act
        var result = new UDAnalysisResult(analysisLevel, severity, analyzerResults, timestamp, user, mockConfig.Object);

        // Assert
        Assert.Equal(severity, result.OverallSeverity);
        Assert.NotNull(result.AnalyzerResults);
        Assert.Equal(timestamp, result.AnalysisTimestamp);
        Assert.Equal(user, result.User);
    }

    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var mockConfig = new Mock<IConfiguration>();
        var result = new UDAnalysisResult(
            AnalysisLevel.Device,
            Severity.Low,
            new Dictionary<string, Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>>(),
            DateTime.UtcNow,
            CreateMockUser(),
            mockConfig.Object
        );

        // Act
        result.OverallSeverity = Severity.Critical;
        var newTimestamp = DateTime.UtcNow.AddHours(1);
        result.AnalysisTimestamp = newTimestamp;

        // Assert
        Assert.Equal(Severity.Critical, result.OverallSeverity);
        Assert.Equal(newTimestamp, result.AnalysisTimestamp);
    }

    private UDUser CreateMockUser()
    {
        var userKey = new Key("User", "test-user-result-123");
        var userInfo = new UserInfo(
            userKey,
            "keycloak-result-123",
            "Test",
            "Result",
            "456 Test Ave",
            "TestCity",
            "TestState",
            "54321",
            "US",
            "+9876543210",
            UserRole.Self,
            false,
            DateTime.UtcNow,
            null,
            "en-US",
            0
        );
        var riskAssessment = new RiskAssessment(0, "", false, 1);
        
        return new UDUser(userKey, userInfo, riskAssessment, null, null, null, false);
    }
}
