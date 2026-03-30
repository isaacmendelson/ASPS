using Business.RealtimeAnalysis;
using Business.RealtimeAnalysis.Indicators;
using Business.RealtimeAnalysis.UserDomain;
using Common.Enums;
using Common.Interfaces;
using Xunit;

namespace ASPS.Tests.Business.UserDomain;

public class AnalyzerResultTests
{
    [Fact]
    public void Constructor_WithAllParameters_ShouldInitialize()
    {
        // Arrange
        var severity = Severity.High;
        var message = "Test message";
        var indicators = new List<IIndicator>();
        var actions = new List<IProtectiveAction>();
        var details = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var result = new AnalyzerResult(severity, message, indicators, actions, details);

        // Assert
        Assert.Equal(severity, result.Severity);
        Assert.Equal(message, result.Message);
        Assert.Equal(indicators, result.Indicators);
        Assert.Equal(actions, result.ProtectiveActions);
        Assert.Equal(details, result.Details);
    }

    [Fact]
    public void Constructor_WithSeverityAndMessage_ShouldInitialize()
    {
        // Arrange
        var severity = Severity.Medium;
        var message = "Simple test";

        // Act
        var result = new AnalyzerResult(severity, message);

        // Assert
        Assert.Equal(severity, result.Severity);
        Assert.Equal(message, result.Message);
        Assert.NotNull(result.Details);
        Assert.Empty(result.Details);
    }

    [Fact]
    public void Properties_CanBeModified()
    {
        // Arrange
        var result = new AnalyzerResult(Severity.Low, "Initial");

        // Act
        result.Severity = Severity.Critical;
        result.Message = "Updated message";
        result.Details["newKey"] = "newValue";

        // Assert
        Assert.Equal(Severity.Critical, result.Severity);
        Assert.Equal("Updated message", result.Message);
        Assert.Contains("newKey", result.Details.Keys);
    }

    [Fact]
    public void Details_ShouldBeEmptyByDefault()
    {
        // Act
        var result = new AnalyzerResult(Severity.Low, "Test");

        // Assert
        Assert.NotNull(result.Details);
        Assert.Empty(result.Details);
    }
}
