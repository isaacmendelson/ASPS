using Business.RealtimeAnalysis.UserDomain;
using Common.Entities;
using Common.Enums;
using Common.Models;
using Common.Models.Alerts;
using Xunit;

namespace ASPS.Tests.Business.UserDomain;

public class RemoteAccessAnalysisResultTests
{
    [Fact]
    public void Constructor_ShouldInitializeAllProperties()
    {
        // Arrange
        var app = RemoteAccessApp.AnyDesk;
        var processes = 3;
        var url = "https://example.com";
        var status = ConnectionStatus.Open;
        var count = 5;
        var session = 1;
        var direction = RemoteAccessDirection.In;
        var tabs = new BrowserTab[] 
        { 
            new BrowserTab("Test", "Mozilla/5.0", "http://test.com", DateTime.UtcNow, true) 
        };
        var riskAssessment = new RiskAssessment(50f, "Medium risk", false, 1f); // MEDIUM risk

        // Act
        var result = new RemoteAccessAnalysisResult(
            app, processes, url, status, direction, count, session, tabs, riskAssessment);

        // Assert
        Assert.Equal(app, result.RemoteAccessApp);
        Assert.Equal(processes, result.RunningProcesses);
        Assert.Equal(url, result.ConnectionUrl);
        Assert.Equal(status, result.ConnectionStatus);
        Assert.Equal(direction, result.RemoteAccessDirection);
        Assert.Equal(count, result.ConnectionsCount);
        Assert.Equal(session, result.SessionStatus);
        Assert.Equal(tabs, result.BrowserTabs);
        Assert.Equal(riskAssessment, result.risk_assessment);
        Assert.True(result.Success);
    }

    [Fact]
    public void Constructor_WithNullBrowserTabs_ShouldWork()
    {
        // Arrange & Act
        var result = new RemoteAccessAnalysisResult(
            RemoteAccessApp.TeamViewer, 
            1, 
            "http://test.com", 
            ConnectionStatus.Closed, 
            RemoteAccessDirection.Unknown,
            0, 
            0, 
            null, 
            null);

        // Assert
        Assert.Null(result.BrowserTabs);
        Assert.Null(result.risk_assessment);
    }

    [Fact]
    public void Constructor_WithMultipleBrowserTabs_ShouldStoreAll()
    {
        // Arrange
        var tabs = new BrowserTab[]
        {
            new BrowserTab("Test 1", "Agent", "http://test1.com", DateTime.UtcNow, true),
            new BrowserTab("Test 2", "Agent", "http://test2.com", DateTime.UtcNow, false),
            new BrowserTab("Test 3", "Agent", "http://test3.com", DateTime.UtcNow, true)
        };

        // Act
        var result = new RemoteAccessAnalysisResult(
            RemoteAccessApp.AnyDesk, 2, "http://remote.com", 
            ConnectionStatus.Open,RemoteAccessDirection.Unknown, 3, 1, tabs, null);

        // Assert
        Assert.Equal(3, result.BrowserTabs.Length);
        Assert.Equal("http://test1.com", result.BrowserTabs[0].Url);
    }

    [Theory]
    [InlineData(RemoteAccessApp.AnyDesk)]
    [InlineData(RemoteAccessApp.TeamViewer)]
    [InlineData(RemoteAccessApp.Unknown)]
    public void Constructor_WithDifferentRemoteAccessApps_ShouldWork(RemoteAccessApp app)
    {
        // Act
        var result = new RemoteAccessAnalysisResult(
            app, 1, "http://test.com", ConnectionStatus.Open, RemoteAccessDirection.Unknown, 
            1, 0, null, null);

        // Assert
        Assert.Equal(app, result.RemoteAccessApp);
    }

    [Theory]
    [InlineData(ConnectionStatus.Open)]
    [InlineData(ConnectionStatus.Closed)]
    public void Constructor_WithDifferentConnectionStatuses_ShouldWork(ConnectionStatus status)
    {
        // Act
        var result = new RemoteAccessAnalysisResult(
            RemoteAccessApp.AnyDesk, 1, "http://test.com", 
            status, RemoteAccessDirection.Unknown, 1, 0, null, null);

        // Assert
        Assert.Equal(status, result.ConnectionStatus);
    }

    [Fact]
    public void TypeName_ShouldReturnCorrectValue()
    {
        // Arrange
        var result = new RemoteAccessAnalysisResult(
            RemoteAccessApp.AnyDesk, 1, "", ConnectionStatus.Open, RemoteAccessDirection.Unknown,
            0, 0, null, null);

        // Act & Assert
        Assert.Equal("RemoteAccessAnalysisResult", result.TypeName);
    }

    [Fact]
    public void Success_ShouldDefaultToTrue()
    {
        // Arrange & Act
        var result = new RemoteAccessAnalysisResult(
            RemoteAccessApp.TeamViewer, 0, "", ConnectionStatus.Closed, RemoteAccessDirection.Unknown,
            0, 0, null, null);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public void RiskAssessment_WithHighScore_ShouldBeStored()
    {
        // Arrange
        var highRisk = new RiskAssessment(95f, "Critical risk", true, 1f); // HIGH risk - dangerous

        // Act
        var result = new RemoteAccessAnalysisResult(
            RemoteAccessApp.AnyDesk, 5, "http://malicious.com", 
            ConnectionStatus.Open, RemoteAccessDirection.Unknown, 10, 1, null, highRisk);

        // Assert
        Assert.NotNull(result.risk_assessment);
        Assert.Equal(95f, result.risk_assessment.risk_score);
        Assert.True(result.risk_assessment.is_scam);
    }
}
