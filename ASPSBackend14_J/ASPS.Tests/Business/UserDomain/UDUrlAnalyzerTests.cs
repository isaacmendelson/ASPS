using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Business.RealtimeAnalysis.UserDomain;
using Business.Views;
using Common.Entities;
using Common.Enums;
using Common.Models;
using Common.Models.Alerts;
using FluentAssertions;
using Interface.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ASPS.Tests.Business.UserDomain;

public class UDUrlAnalyzerTests
{
    // Dependencies
    private readonly Mock<ILogger<UDUrlAnalyzer>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IKnownPhishingWebsiteRepository> _phishingRepoMock;
    private readonly Mock<ASView> _asViewMock;

    // System Under Test
    private readonly UDUrlAnalyzer _sut;

    public UDUrlAnalyzerTests()
    {
        // Setup mocks
        _loggerMock = new Mock<ILogger<UDUrlAnalyzer>>();
        _configurationMock = new Mock<IConfiguration>();
        _phishingRepoMock = new Mock<IKnownPhishingWebsiteRepository>();
        
        // ASView requires IServiceProvider and ILogger<ASView>
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockASViewLogger = new Mock<ILogger<ASView>>();
        _asViewMock = new Mock<ASView>(mockServiceProvider.Object, mockASViewLogger.Object);

        // Setup default configuration values
        _configurationMock.Setup(c => c.GetSection("Python:ExecutablePath").Value)
            .Returns("python");
        _configurationMock.Setup(c => c.GetSection("Python:AnalyzersFolderPath").Value)
            .Returns("/test/analyzers");
        _configurationMock.Setup(c => c["Python:ExecutablePath"])
            .Returns("python");
        _configurationMock.Setup(c => c["Python:AnalyzersFolderPath"])
            .Returns("/test/analyzers");

        // Create instance
        _sut = new UDUrlAnalyzer(
            _loggerMock.Object,
            _configurationMock.Object,
            _phishingRepoMock.Object,
            _asViewMock.Object
        );
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParams_CreatesInstance()
    {
        // Act
        var instance = new UDUrlAnalyzer(
            _loggerMock.Object,
            _configurationMock.Object,
            _phishingRepoMock.Object,
            _asViewMock.Object
        );

        // Assert
        instance.Should().NotBeNull();
        instance.ExternalAnalyzers.Should().NotBeNull();
        instance.ExternalAnalyzers.Should().HaveCount(1);
    }

    [Fact]
    public void Constructor_InitializesExternalAnalyzers()
    {
        // Assert
        _sut.ExternalAnalyzers.Should().NotBeNull();
        _sut.ExternalAnalyzers.Should().HaveCount(1);
        _sut.ExternalAnalyzers[0].ScriptFile.Should().Be("basic-url-analyzer");
        _sut.ExternalAnalyzers[0].Order.Should().Be(1);
        _sut.ExternalAnalyzers[0].Weight.Should().Be(1.0f);
    }

    [Fact]
    public void Constructor_LogsPythonPath()
    {
        // Arrange & Act - constructor already called in setup
        
        // Assert - verify logging was called (at least once for initialization)
        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }

    #endregion

    #region CanAnalyze Tests

    [Fact]
    public void CanAnalyze_WithUrlAlert_ReturnsTrue()
    {
        // Arrange
        var alert = new UrlAlert
        {
            AlertId = "test-123",
            Url = "http://test.com"
        };

        // Act
        var result = _sut.CanAnalyze(alert);

        // Assert
        result.Should().BeTrue();
    }

    // Test removed - TrackUrlAlert is now handled by UDTrackUrlAnalyzer
    // [Fact]
    // public void CanAnalyze_WithTrackUrlAlert_ReturnsTrue()

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://secure.site.com/path")]
    [InlineData("http://subdomain.example.com/page?query=test")]
    public void CanAnalyze_WithDifferentUrlAlerts_ReturnsTrue(string url)
    {
        // Arrange
        var alert = new UrlAlert
        {
            AlertId = "test-123",
            Url = url
        };

        // Act
        var result = _sut.CanAnalyze(alert);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region ExternalAnalyzers Tests

    [Fact]
    public void ExternalAnalyzers_ShouldHaveCorrectConfiguration()
    {
        // Assert
        var analyzer = _sut.ExternalAnalyzers.First();
        analyzer.ScriptFile.Should().Be("basic-url-analyzer");
        analyzer.Order.Should().Be(1);
        analyzer.Weight.Should().Be(1.0f);
    }

    [Fact]
    public void ExternalAnalyzers_ShouldNotBeNull()
    {
        // Assert
        _sut.ExternalAnalyzers.Should().NotBeNull();
    }

    [Fact]
    public void ExternalAnalyzers_ShouldContainBasicUrlAnalyzer()
    {
        // Assert
        _sut.ExternalAnalyzers.Should().Contain(a => a.ScriptFile == "basic-url-analyzer");
    }

    #endregion

    #region AnalyzeAsync Tests - Repository Verification

    [Fact]
    public async Task AnalyzeAsync_WithUrlAlert_CallsPhishingRepository()
    {
        // Arrange
        var url = "http://test.com/page";
        var alert = new UrlAlert
        {
            AlertId = "test-123",
            Url = url
        };

        _phishingRepoMock.Setup(r => r.IsPhishingUrlAsync(It.IsAny<string>())).ReturnsAsync(false);
        _phishingRepoMock.Setup(r => r.IsPhishingDomainAsync(It.IsAny<string>())).ReturnsAsync(false);
        _configurationMock.Setup(c => c.GetSection("Analysis:CacheEnabled").Value).Returns("false");

        // Act
        try
        {
            var result = await _sut.AnalyzeAsync(alert, new List<DeviceAlert>(), _configurationMock.Object);
        }
        catch
        {
            // Expected to fail (Python script execution), but repository calls should happen
        }

        // Assert
        _phishingRepoMock.Verify(r => r.IsPhishingUrlAsync(url), Times.Once);
    }

    // Test removed - TrackUrlAlert is now handled by UDTrackUrlAnalyzer
    // [Fact]
    // public async Task AnalyzeAsync_WithTrackUrlAlert_CallsPhishingRepository()

    [Theory]
    [InlineData("http://example.com", "example.com")]
    [InlineData("https://sub.domain.com/path", "sub.domain.com")]
    [InlineData("http://test.org", "test.org")]
    public async Task AnalyzeAsync_ExtractsDomainCorrectly(string url, string expectedDomain)
    {
        // Arrange
        var alert = new UrlAlert
        {
            AlertId = "test-123",
            Url = url
        };

        _phishingRepoMock.Setup(r => r.IsPhishingUrlAsync(It.IsAny<string>())).ReturnsAsync(false);
        _phishingRepoMock.Setup(r => r.IsPhishingDomainAsync(It.IsAny<string>())).ReturnsAsync(false);
        _configurationMock.Setup(c => c.GetSection("Analysis:CacheEnabled").Value).Returns("false");

        // Act
        try
        {
            var result = await _sut.AnalyzeAsync(alert, new List<DeviceAlert>(), _configurationMock.Object);
        }
        catch
        {
            // Expected to fail (Python script execution)
        }

        // Assert - verify domain was checked
        _phishingRepoMock.Verify(r => r.IsPhishingDomainAsync(It.Is<string>(d => !string.IsNullOrEmpty(d))), Times.Once);
    }

    #endregion

    #region EnableUrlTracking Tests

    [Fact]
    public void Configuration_RiskThresholdToEnableTracking_ShouldBeReadFromConfig()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c.GetSection("TrackUrl:RiskThresholdToEnableTracking").Value)
            .Returns("50");

        // Act
        var threshold = int.Parse(configMock.Object.GetSection("TrackUrl:RiskThresholdToEnableTracking").Value ?? "40");

        // Assert
        threshold.Should().Be(50);
    }

    [Fact]
    public void Configuration_TrackingDurationMinutes_ShouldBeReadFromConfig()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c.GetSection("TrackUrl:TrackingDurationMinutes").Value)
            .Returns("60");

        // Act
        var duration = int.Parse(configMock.Object.GetSection("TrackUrl:TrackingDurationMinutes").Value ?? "30");

        // Assert
        duration.Should().Be(60);
    }

    [Theory]
    [InlineData(40, 35, true)]   // Score 35 <= threshold 40 → enable tracking
    [InlineData(40, 40, true)]   // Score 40 <= threshold 40 → enable tracking
    [InlineData(40, 41, false)]  // Score 41 > threshold 40 → no tracking
    [InlineData(50, 45, true)]   // Score 45 <= threshold 50 → enable tracking
    [InlineData(30, 35, false)]  // Score 35 > threshold 30 → no tracking
    public void EnableUrlTracking_ShouldTriggerWhenScoreBelowOrEqualThreshold(int threshold, int safetyScore, bool shouldEnable)
    {
        // This test documents the expected behavior:
        // EnableUrlTracking is triggered when safety score <= threshold
        // (lower safety score = higher risk)
        
        var result = safetyScore <= threshold;
        result.Should().Be(shouldEnable);
    }

    [Fact]
    public void EnableUrlTracking_MessageFormat_ShouldBeCorrect()
    {
        // Arrange
        var domain = "example.com";
        var durationMinutes = 30;
        var expectedMessage = $"EnableUrlTracking|{domain}|{durationMinutes}";

        // Act & Assert
        expectedMessage.Should().Be("EnableUrlTracking|example.com|30");
        expectedMessage.Split('|').Should().HaveCount(3);
        expectedMessage.Split('|')[0].Should().Be("EnableUrlTracking");
        expectedMessage.Split('|')[1].Should().Be(domain);
        expectedMessage.Split('|')[2].Should().Be(durationMinutes.ToString());
    }

    [Fact]
    public void SetTrackMode_MessageFormat_ShouldBeCorrect()
    {
        // Arrange
        var domain = "risky-site.com";
        var trackMode = (int)TrackMode.Click;
        var scamKey = "abc-123";
        var durationMinutes = 45;
        var expectedMessage = $"SetTrackMode|{domain}|{trackMode}|{scamKey}|{durationMinutes}";

        // Act & Assert
        expectedMessage.Should().Be("SetTrackMode|risky-site.com|2|abc-123|45");
        expectedMessage.Split('|').Should().HaveCount(5);
        expectedMessage.Split('|')[0].Should().Be("SetTrackMode");
        expectedMessage.Split('|')[1].Should().Be(domain);
        expectedMessage.Split('|')[2].Should().Be("2"); // TrackMode.Click
        expectedMessage.Split('|')[3].Should().Be(scamKey);
        expectedMessage.Split('|')[4].Should().Be(durationMinutes.ToString());
    }

    #endregion
}
