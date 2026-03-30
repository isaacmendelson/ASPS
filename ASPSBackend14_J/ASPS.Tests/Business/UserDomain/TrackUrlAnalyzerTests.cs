using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Business.RealtimeAnalysis;
using Business.RealtimeAnalysis.UserDomain;
using Business.Views;
using Common.Entities;
using Common.Enums;
using Common.Models;
using Common.Models.Alerts;
using FluentAssertions;
using Interface.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ASPS.Tests.Business.UserDomain;

/// <summary>
/// Unit tests for TrackUrlAnalyzer
/// ASPS-247: TrackUrlAnalyzer - New Analyzer Implementation
/// ASPS-254: TrackedDomains Integration - URL Matching
/// </summary>
public class TrackUrlAnalyzerTests
{
    // Dependencies
    private readonly Mock<ILogger<TrackUrlAnalyzer>> _loggerMock;
    private readonly Mock<ASView> _asViewMock;
    private readonly Mock<ITrackedDomainRepository> _trackedDomainRepositoryMock;
    private readonly Mock<IConfiguration> _configurationMock;

    // System Under Test
    private readonly TrackUrlAnalyzer _sut;

    public TrackUrlAnalyzerTests()
    {
        // Setup mocks
        _loggerMock = new Mock<ILogger<TrackUrlAnalyzer>>();
        _configurationMock = new Mock<IConfiguration>();
        _trackedDomainRepositoryMock = new Mock<ITrackedDomainRepository>();
        
        // ASView requires IServiceProvider and ILogger<ASView>
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockASViewLogger = new Mock<ILogger<ASView>>();
        _asViewMock = new Mock<ASView>(mockServiceProvider.Object, mockASViewLogger.Object);

        // Default: no tracked domains
        _trackedDomainRepositoryMock
            .Setup(x => x.GetAllActiveAsync())
            .ReturnsAsync(new List<TrackedDomain>());

        // Create instance
        _sut = new TrackUrlAnalyzer(
            _loggerMock.Object,
            _asViewMock.Object,
            _trackedDomainRepositoryMock.Object
        );
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParams_CreatesInstance()
    {
        // Act
        var instance = new TrackUrlAnalyzer(
            _loggerMock.Object,
            _asViewMock.Object,
            _trackedDomainRepositoryMock.Object
        );

        // Assert
        instance.Should().NotBeNull();
        instance.ExternalAnalyzers.Should().NotBeNull();
        instance.ExternalAnalyzers.Should().BeEmpty();
    }

    #endregion

    #region CanAnalyze Tests

    [Fact]
    public void CanAnalyze_WithTrackUrlAlert_ReturnsTrue()
    {
        // Arrange
        var alert = new TrackUrlAlert
        {
            Url = "https://example.com",
            AlertId = "test-123"
        };

        // Act
        var result = _sut.CanAnalyze(alert);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanAnalyze_WithUrlAlert_ReturnsFalse()
    {
        // Arrange
        var alert = new UrlAlert
        {
            Url = "https://example.com",
            AlertId = "test-123"
        };

        // Act
        var result = _sut.CanAnalyze(alert);

        // Assert
        result.Should().BeFalse();
    }

    // RemoteAccessAlert constructor is protected, so we skip this test
    // The analyzer will return false for non-TrackUrlAlert types anyway

    #endregion

    #region AnalyzeAsync Tests

    [Fact]
    public async Task AnalyzeAsync_WithNullAlert_ReturnsLowSeverity()
    {
        // Arrange
        var historicalAlerts = new List<DeviceAlert>();

        // Act
        var result = await _sut.AnalyzeAsync(null!, historicalAlerts, _configurationMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Severity.Should().Be(Severity.Low);
        result.Message.Should().Contain("Invalid alert type");
    }

    [Fact]
    public async Task AnalyzeAsync_WithEmptyUrl_ReturnsLowSeverity()
    {
        // Arrange
        var alert = new TrackUrlAlert
        {
            Url = "",
            AlertId = "test-123"
        };
        var historicalAlerts = new List<DeviceAlert>();

        // Act
        var result = await _sut.AnalyzeAsync(alert, historicalAlerts, _configurationMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Severity.Should().Be(Severity.Low);
        result.Message.Should().Contain("empty");
    }

    [Fact]
    public async Task AnalyzeAsync_WithValidUrl_ReturnsSuccess()
    {
        // Arrange
        var alert = new TrackUrlAlert
        {
            Url = "https://example.com/page",
            FromUrl = "https://google.com",
            Duration = 60,
            AlertId = "test-123",
            IPAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0",
            TabId = "tab-1",
            Timezone = "UTC"
        };
        var historicalAlerts = new List<DeviceAlert>();
        _asViewMock.Setup(x => x.IsSafeDomain(It.IsAny<string>())).Returns(false);

        // Act
        var result = await _sut.AnalyzeAsync(alert, historicalAlerts, _configurationMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Severity.Should().Be(Severity.Low);
        result.Message.Should().Contain("TrackUrl analysis completed");
        result.Details.Should().ContainKey("results");
        result.Details.Should().ContainKey("url");
        result.Details.Should().ContainKey("duration");
        result.Details.Should().ContainKey("domain");
    }

    [Fact]
    public async Task AnalyzeAsync_WithSafeDomain_ReturnsLowSeverityAndZeroRiskScore()
    {
        // Arrange
        var alert = new TrackUrlAlert
        {
            Url = "https://google.com/search",
            FromUrl = "",
            Duration = 120,
            AlertId = "test-456"
        };
        var historicalAlerts = new List<DeviceAlert>();
        _asViewMock.Setup(x => x.IsSafeDomain("google.com")).Returns(true);

        // Act
        var result = await _sut.AnalyzeAsync(alert, historicalAlerts, _configurationMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Severity.Should().Be(Severity.Low);
        result.Details["is_safe_domain"].Should().Be(true);
        
        var results = result.Details["results"] as TrackUrlAnalysisResult[];
        results.Should().NotBeNull();
        results.Should().HaveCount(1);
        results![0].risk_assessment?.risk_score.Should().Be(5); // ASPS-318: NEW scale - LOW risk (safe domain)
    }

    [Fact]
    public async Task AnalyzeAsync_WithLongDuration_ReturnsMediumSeverity()
    {
        // Arrange
        var alert = new TrackUrlAlert
        {
            Url = "https://suspicious-site.com/page",
            FromUrl = "",
            Duration = 350, // > 300 seconds (5 minutes)
            AlertId = "test-789"
        };
        var historicalAlerts = new List<DeviceAlert>();
        _asViewMock.Setup(x => x.IsSafeDomain(It.IsAny<string>())).Returns(false);

        // Act
        var result = await _sut.AnalyzeAsync(alert, historicalAlerts, _configurationMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Severity.Should().Be(Severity.Medium);
        
        var results = result.Details["results"] as TrackUrlAnalysisResult[];
        results.Should().NotBeNull();
        results![0].risk_assessment?.risk_score.Should().Be(40); // ASPS-318: NEW scale - MEDIUM risk (long duration)
    }

    [Fact]
    public async Task AnalyzeAsync_WithVeryLongDuration_ReturnsHighSeverity()
    {
        // Arrange
        var alert = new TrackUrlAlert
        {
            Url = "https://suspicious-site.com/page",
            FromUrl = "",
            Duration = 650, // > 600 seconds (10 minutes)
            AlertId = "test-abc"
        };
        var historicalAlerts = new List<DeviceAlert>();
        _asViewMock.Setup(x => x.IsSafeDomain(It.IsAny<string>())).Returns(false);

        // Act
        var result = await _sut.AnalyzeAsync(alert, historicalAlerts, _configurationMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Severity.Should().Be(Severity.High);
        
        var results = result.Details["results"] as TrackUrlAnalysisResult[];
        results.Should().NotBeNull();
        results![0].risk_assessment?.risk_score.Should().Be(60); // ASPS-318: NEW scale - HIGH risk (very long duration)
    }

    [Fact]
    public async Task AnalyzeAsync_WithScamInProgressKey_ReturnsHighSeverityAndProtectiveAction()
    {
        // Arrange
        var alert = new TrackUrlAlert
        {
            Url = "https://scam-site.com/login",
            FromUrl = "",
            Duration = 100,
            ScamInProgressKey = "scam-session-12345",
            AlertId = "test-scam"
        };
        var historicalAlerts = new List<DeviceAlert>();
        _asViewMock.Setup(x => x.IsSafeDomain(It.IsAny<string>())).Returns(false);

        // Act
        var result = await _sut.AnalyzeAsync(alert, historicalAlerts, _configurationMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Severity.Should().Be(Severity.High);
        result.ProtectiveActions.Should().NotBeNull();
        result.ProtectiveActions.Should().HaveCount(1);
        (result.ProtectiveActions![0] as ProtectiveAction)?.Message.Should().Contain("scam detected");
        
        var results = result.Details["results"] as TrackUrlAnalysisResult[];
        results.Should().NotBeNull();
        results![0].risk_assessment?.risk_score.Should().Be(90); // ASPS-318: NEW scale - HIGH risk (scam in progress)
        results[0].ScamInProgressKey.Should().Be("scam-session-12345");
    }

    [Fact]
    public async Task AnalyzeAsync_ResultContainsAllTrackUrlData()
    {
        // Arrange
        var alert = new TrackUrlAlert
        {
            Url = "https://example.com/page",
            FromUrl = "https://referrer.com",
            Duration = 150,
            ScamInProgressKey = "",
            IPAddress = "10.0.0.1",
            UserAgent = "Chrome/91.0",
            TabId = "tab-abc123",
            Timezone = "America/New_York",
            AlertId = "test-complete"
        };
        var historicalAlerts = new List<DeviceAlert>();
        _asViewMock.Setup(x => x.IsSafeDomain(It.IsAny<string>())).Returns(false);

        // Act
        var result = await _sut.AnalyzeAsync(alert, historicalAlerts, _configurationMock.Object);

        // Assert
        var results = result.Details["results"] as TrackUrlAnalysisResult[];
        results.Should().NotBeNull();
        results.Should().HaveCount(1);
        
        var analysisResult = results![0];
        analysisResult.Url.Should().Be("https://example.com/page");
        analysisResult.FromUrl.Should().Be("https://referrer.com");
        analysisResult.Duration.Should().Be(150);
        analysisResult.IPAddress.Should().Be("10.0.0.1");
        analysisResult.UserAgent.Should().Be("Chrome/91.0");
        analysisResult.TabId.Should().Be("tab-abc123");
        analysisResult.Timezone.Should().Be("America/New_York");
        analysisResult.Domain.Should().Be("example.com");
        analysisResult.IsSafeDomain.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeAsync_LogsAnalysisInformation()
    {
        // Arrange
        var alert = new TrackUrlAlert
        {
            Url = "https://test.com",
            Duration = 30,
            AlertId = "log-test"
        };
        var historicalAlerts = new List<DeviceAlert>();
        _asViewMock.Setup(x => x.IsSafeDomain(It.IsAny<string>())).Returns(false);

        // Act
        await _sut.AnalyzeAsync(alert, historicalAlerts, _configurationMock.Object);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Analyzing TrackUrlAlert")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task AnalyzeAsync_WithNullFromUrl_HandlesGracefully()
    {
        // Arrange
        var alert = new TrackUrlAlert
        {
            Url = "https://example.com",
            FromUrl = null!,
            Duration = 60,
            AlertId = "test-null-from"
        };
        var historicalAlerts = new List<DeviceAlert>();
        _asViewMock.Setup(x => x.IsSafeDomain(It.IsAny<string>())).Returns(false);

        // Act
        var result = await _sut.AnalyzeAsync(alert, historicalAlerts, _configurationMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Severity.Should().Be(Severity.Low);
    }

    [Fact]
    public async Task AnalyzeAsync_WithZeroDuration_ReturnsLowSeverity()
    {
        // Arrange
        var alert = new TrackUrlAlert
        {
            Url = "https://example.com",
            Duration = 0,
            AlertId = "test-zero-duration"
        };
        var historicalAlerts = new List<DeviceAlert>();
        _asViewMock.Setup(x => x.IsSafeDomain(It.IsAny<string>())).Returns(false);

        // Act
        var result = await _sut.AnalyzeAsync(alert, historicalAlerts, _configurationMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Severity.Should().Be(Severity.Low);
    }

    #endregion

    #region TrackedDomain Integration Tests (ASPS-254)

    [Fact]
    public async Task AnalyzeAsync_WithExactDomainMatch_ReturnsTrackedDomainInfo()
    {
        // Arrange
        var trackedDomains = new List<TrackedDomain>
        {
            new TrackedDomain("google-analytics.com", "Analytics"),
            new TrackedDomain("facebook.com", "Social")
        };

        _trackedDomainRepositoryMock
            .Setup(x => x.GetAllActiveAsync())
            .ReturnsAsync(trackedDomains);

        var alert = new TrackUrlAlert
        {
            Url = "https://google-analytics.com/collect",
            Duration = 30,
            AlertId = "test-tracked-exact"
        };
        var historicalAlerts = new List<DeviceAlert>();
        _asViewMock.Setup(x => x.IsSafeDomain(It.IsAny<string>())).Returns(false);

        // Act
        var result = await _sut.AnalyzeAsync(alert, historicalAlerts, _configurationMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Details["is_tracked_domain"].Should().Be(true);
        
        var trackedDomainDict = result.Details["tracked_domain"] as Dictionary<string, object>;
        trackedDomainDict.Should().NotBeNull();
        trackedDomainDict!["domain"].Should().Be("google-analytics.com");
        trackedDomainDict["category"].Should().Be("Analytics");
        trackedDomainDict["is_exact_match"].Should().Be(true);

        var results = result.Details["results"] as TrackUrlAnalysisResult[];
        results.Should().NotBeNull();
        results![0].TrackedDomain.Should().NotBeNull();
        results[0].TrackedDomain!.Domain.Should().Be("google-analytics.com");
        results[0].TrackedDomain.Category.Should().Be("Analytics");
        results[0].TrackedDomain.IsExactMatch.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_WithSubdomainMatch_ReturnsTrackedDomainInfo()
    {
        // Arrange
        var trackedDomains = new List<TrackedDomain>
        {
            new TrackedDomain("google.com", "Search"),
            new TrackedDomain("doubleclick.net", "Advertising")
        };

        _trackedDomainRepositoryMock
            .Setup(x => x.GetAllActiveAsync())
            .ReturnsAsync(trackedDomains);

        var alert = new TrackUrlAlert
        {
            Url = "https://ads.google.com/pagead",
            Duration = 45,
            AlertId = "test-tracked-subdomain"
        };
        var historicalAlerts = new List<DeviceAlert>();
        _asViewMock.Setup(x => x.IsSafeDomain(It.IsAny<string>())).Returns(false);

        // Act
        var result = await _sut.AnalyzeAsync(alert, historicalAlerts, _configurationMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Details["is_tracked_domain"].Should().Be(true);
        
        var trackedDomainDict = result.Details["tracked_domain"] as Dictionary<string, object>;
        trackedDomainDict.Should().NotBeNull();
        trackedDomainDict!["domain"].Should().Be("google.com");
        trackedDomainDict["category"].Should().Be("Search");
        trackedDomainDict["is_exact_match"].Should().Be(false);

        var results = result.Details["results"] as TrackUrlAnalysisResult[];
        results.Should().NotBeNull();
        results![0].TrackedDomain.Should().NotBeNull();
        results[0].TrackedDomain!.Domain.Should().Be("google.com");
        results[0].TrackedDomain.Category.Should().Be("Search");
        results[0].TrackedDomain.IsExactMatch.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeAsync_WithNoTrackedDomainMatch_ReturnsNullTrackedDomain()
    {
        // Arrange
        var trackedDomains = new List<TrackedDomain>
        {
            new TrackedDomain("facebook.com", "Social"),
            new TrackedDomain("twitter.com", "Social")
        };

        _trackedDomainRepositoryMock
            .Setup(x => x.GetAllActiveAsync())
            .ReturnsAsync(trackedDomains);

        var alert = new TrackUrlAlert
        {
            Url = "https://example.com/page",
            Duration = 60,
            AlertId = "test-no-tracked"
        };
        var historicalAlerts = new List<DeviceAlert>();
        _asViewMock.Setup(x => x.IsSafeDomain(It.IsAny<string>())).Returns(false);

        // Act
        var result = await _sut.AnalyzeAsync(alert, historicalAlerts, _configurationMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Details["is_tracked_domain"].Should().Be(false);
        result.Details["tracked_domain"].Should().BeNull();

        var results = result.Details["results"] as TrackUrlAnalysisResult[];
        results.Should().NotBeNull();
        results![0].TrackedDomain.Should().BeNull();
    }

    [Fact]
    public async Task AnalyzeAsync_WithEmptyTrackedDomainsList_ReturnsNullTrackedDomain()
    {
        // Arrange
        _trackedDomainRepositoryMock
            .Setup(x => x.GetAllActiveAsync())
            .ReturnsAsync(new List<TrackedDomain>());

        var alert = new TrackUrlAlert
        {
            Url = "https://example.com/page",
            Duration = 60,
            AlertId = "test-empty-tracked"
        };
        var historicalAlerts = new List<DeviceAlert>();
        _asViewMock.Setup(x => x.IsSafeDomain(It.IsAny<string>())).Returns(false);

        // Act
        var result = await _sut.AnalyzeAsync(alert, historicalAlerts, _configurationMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Details["is_tracked_domain"].Should().Be(false);
        result.Details["tracked_domain"].Should().BeNull();

        var results = result.Details["results"] as TrackUrlAnalysisResult[];
        results.Should().NotBeNull();
        results![0].TrackedDomain.Should().BeNull();
    }

    [Fact]
    public async Task AnalyzeAsync_WithMultiLevelSubdomain_MatchesCorrectly()
    {
        // Arrange
        var trackedDomains = new List<TrackedDomain>
        {
            new TrackedDomain("google.com", "Search")
        };

        _trackedDomainRepositoryMock
            .Setup(x => x.GetAllActiveAsync())
            .ReturnsAsync(trackedDomains);

        var alert = new TrackUrlAlert
        {
            Url = "https://www.ads.google.com/pagead",
            Duration = 20,
            AlertId = "test-multilevel-subdomain"
        };
        var historicalAlerts = new List<DeviceAlert>();
        _asViewMock.Setup(x => x.IsSafeDomain(It.IsAny<string>())).Returns(false);

        // Act
        var result = await _sut.AnalyzeAsync(alert, historicalAlerts, _configurationMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Details["is_tracked_domain"].Should().Be(true);
        
        var results = result.Details["results"] as TrackUrlAnalysisResult[];
        results.Should().NotBeNull();
        results![0].TrackedDomain.Should().NotBeNull();
        results[0].TrackedDomain!.Domain.Should().Be("google.com");
        results[0].TrackedDomain.IsExactMatch.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeAsync_TrackedDomainRepository_CalledOnce()
    {
        // Arrange
        var alert = new TrackUrlAlert
        {
            Url = "https://example.com",
            Duration = 30,
            AlertId = "test-repo-call"
        };
        var historicalAlerts = new List<DeviceAlert>();
        _asViewMock.Setup(x => x.IsSafeDomain(It.IsAny<string>())).Returns(false);

        // Act
        await _sut.AnalyzeAsync(alert, historicalAlerts, _configurationMock.Object);

        // Assert
        _trackedDomainRepositoryMock.Verify(
            x => x.GetAllActiveAsync(), 
            Times.Once);
    }

    #endregion
}
