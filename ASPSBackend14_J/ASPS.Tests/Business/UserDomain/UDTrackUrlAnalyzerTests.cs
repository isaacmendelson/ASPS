using System.Collections.Generic;
using System.Threading.Tasks;
using Business.RealtimeAnalysis.UserDomain;
using Business.Views;
using Common.Enums;
using Common.Models;
using Common.Models.Alerts;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ASPS.Tests.Business.UserDomain;

public class UDTrackUrlAnalyzerTests
{
    private readonly Mock<ILogger<UDTrackUrlAnalyzer>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ASView> _asViewMock;
    private readonly UDTrackUrlAnalyzer _sut;

    public UDTrackUrlAnalyzerTests()
    {
        _loggerMock = new Mock<ILogger<UDTrackUrlAnalyzer>>();
        _configurationMock = new Mock<IConfiguration>();
        
        // Setup configuration sections
        var riskThresholdSection = new Mock<IConfigurationSection>();
        riskThresholdSection.Setup(s => s.Value).Returns("40");
        _configurationMock.Setup(c => c.GetSection("TrackUrl:RiskThresholdToEnableTracking")).Returns(riskThresholdSection.Object);
        
        var trackingDurationSection = new Mock<IConfigurationSection>();
        trackingDurationSection.Setup(s => s.Value).Returns("3000");
        _configurationMock.Setup(c => c.GetSection("TrackUrl:TrackingDurationMinutes")).Returns(trackingDurationSection.Object);
        
        var criticalSection = new Mock<IConfigurationSection>();
        criticalSection.Setup(s => s.Value).Returns("80");
        _configurationMock.Setup(c => c.GetSection("Analysis:SeverityScoreThresholdCritical")).Returns(criticalSection.Object);
        
        var highSection = new Mock<IConfigurationSection>();
        highSection.Setup(s => s.Value).Returns("80");
        _configurationMock.Setup(c => c.GetSection("Analysis:SeverityScoreThresholdHigh")).Returns(highSection.Object);
        
        var mediumSection = new Mock<IConfigurationSection>();
        mediumSection.Setup(s => s.Value).Returns("80");
        _configurationMock.Setup(c => c.GetSection("Analysis:SeverityScoreThresholdMedium")).Returns(mediumSection.Object);
        
        // Mock ASView
        var services = new ServiceCollection();
        var mockASViewLogger = new Mock<ILogger<ASView>>();
        services.AddSingleton(mockASViewLogger.Object);
        var serviceProvider = services.BuildServiceProvider();
        _asViewMock = new Mock<ASView>(serviceProvider, mockASViewLogger.Object, _configurationMock.Object);

        _sut = new UDTrackUrlAnalyzer(_loggerMock.Object, _configurationMock.Object, _asViewMock.Object);
    }

    [Fact]
    public void Constructor_WithValidParams_CreatesInstance()
    {
        var instance = new UDTrackUrlAnalyzer(_loggerMock.Object, _configurationMock.Object, _asViewMock.Object);
        instance.Should().NotBeNull();
        instance.ExternalAnalyzers.Should().NotBeNull();
        instance.ExternalAnalyzers.Should().BeEmpty();
    }

    [Fact]
    public void CanAnalyze_WithTrackUrlAlert_ReturnsTrue()
    {
        var alert = new TrackUrlAlert { Url = "https://test.com", Duration = 60000 };
        _sut.CanAnalyze(alert).Should().BeTrue();
    }

    [Fact]
    public void CanAnalyze_WithUrlAlert_ReturnsFalse()
    {
        var alert = new UrlAlert { Url = "https://test.com" };
        _sut.CanAnalyze(alert).Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeAsync_WithSafeDomain_ReturnsLowRisk()
    {
        // Arrange: Mock IsSafeDomain to return true for google.com
        _asViewMock.Setup(v => v.IsSafeDomain("google.com")).Returns(true);
        
        var alert = new TrackUrlAlert { Url = "https://google.com/path", Duration = 300000, AlertId = "alert-1" };

        // Act
        var result = await _sut.AnalyzeAsync(alert, new List<DeviceAlert>(), _configurationMock.Object);

        // Assert
        result.Severity.Should().Be(Severity.Low);
        result.Details["risk_score"].Should().Be(5);
        result.Details["risk_reason"].Should().Be("Whitelisted domain (SafeDomains)");
    }

    [Fact]
    public async Task AnalyzeAsync_WithScamInProgressKey_ReturnsHighRisk()
    {
        // Arrange: Mock IsSafeDomain to return false
        _asViewMock.Setup(v => v.IsSafeDomain(It.IsAny<string>())).Returns(false);
        
        var alert = new TrackUrlAlert { Url = "https://scam.com", Duration = 60000, ScamInProgressKey = "scam-123", AlertId = "alert-2" };

        // Act
        var result = await _sut.AnalyzeAsync(alert, new List<DeviceAlert>(), _configurationMock.Object);

        // Assert - ScamInProgressKey results in 90 risk score, which is >= 80 threshold for Critical
        result.Severity.Should().Be(Severity.Critical);
        result.Details["risk_score"].Should().Be(90);
        result.Details["scam_in_progress_key"].Should().Be("scam-123");
    }

    [Fact]
    public async Task AnalyzeAsync_WithDurationOver10Minutes_ReturnsHighRisk()
    {
        var alert = new TrackUrlAlert { Url = "https://suspicious.com", Duration = 720000, AlertId = "alert-3" };

        var result = await _sut.AnalyzeAsync(alert, new List<DeviceAlert>(), _configurationMock.Object);

        result.Severity.Should().Be(Severity.High);
        result.Details["risk_score"].Should().Be(60);
        result.Details["duration_ms"].Should().Be(720000);
    }

    [Fact]
    public async Task AnalyzeAsync_WithDurationOver5Minutes_ReturnsMediumRisk()
    {
        var alert = new TrackUrlAlert { Url = "https://suspicious.com", Duration = 420000, AlertId = "alert-4" };

        var result = await _sut.AnalyzeAsync(alert, new List<DeviceAlert>(), _configurationMock.Object);

        result.Severity.Should().Be(Severity.Medium);
        result.Details["risk_score"].Should().Be(40);
    }

    [Fact]
    public async Task AnalyzeAsync_WithShortDuration_ReturnsLowRisk()
    {
        var alert = new TrackUrlAlert { Url = "https://example.com", Duration = 60000, AlertId = "alert-5" };

        var result = await _sut.AnalyzeAsync(alert, new List<DeviceAlert>(), _configurationMock.Object);

        result.Severity.Should().Be(Severity.Low);
        result.Details["risk_score"].Should().Be(20);
    }

    [Fact]
    public async Task AnalyzeAsync_WithHighRisk_AddsNotificationAction()
    {
        var alert = new TrackUrlAlert { Url = "https://scam.com", Duration = 900000, AlertId = "alert-6" };

        var result = await _sut.AnalyzeAsync(alert, new List<DeviceAlert>(), _configurationMock.Object);

        result.ProtectiveActions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_WithUrlAlert_ReturnsLowSeverity()
    {
        var alert = new UrlAlert { Url = "https://example.com" };

        var result = await _sut.AnalyzeAsync(alert, new List<DeviceAlert>(), _configurationMock.Object);

        result.Severity.Should().Be(Severity.Low);
        result.Message.Should().Be("Alert is not a TrackUrlAlert");
    }

    [Fact]
    public async Task AnalyzeAsync_PopulatesAllDetails()
    {
        var alert = new TrackUrlAlert 
        { 
            Url = "https://test.com/page",
            FromUrl = "https://referrer.com",
            Duration = 180000,
            Timezone = "UTC",
            AlertId = "alert-8"
        };

        var result = await _sut.AnalyzeAsync(alert, new List<DeviceAlert>(), _configurationMock.Object);

        result.Details.Should().ContainKey("url");
        result.Details.Should().ContainKey("domain");
        result.Details.Should().ContainKey("from_url");
        result.Details.Should().ContainKey("duration_ms");
        result.Details.Should().ContainKey("timezone");
        result.Details.Should().ContainKey("risk_score");
        result.Details.Should().ContainKey("risk_reason");
    }
}
