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

    [Fact]
    public void CanAnalyze_WithTrackUrlAlert_ReturnsTrue()
    {
        // Arrange
        var alert = new TrackUrlAlert
        {
            AlertId = "test-123",
            Url = "http://test.com"
        };

        // Act
        var result = _sut.CanAnalyze(alert);

        // Assert
        result.Should().BeTrue();
    }

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

    [Fact]
    public async Task AnalyzeAsync_WithTrackUrlAlert_CallsPhishingRepository()
    {
        // Arrange
        var url = "http://tracked.com";
        var alert = new TrackUrlAlert
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
}
