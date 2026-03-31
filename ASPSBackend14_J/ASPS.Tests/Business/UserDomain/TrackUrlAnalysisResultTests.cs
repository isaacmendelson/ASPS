using System;
using Business.RealtimeAnalysis.UserDomain;
using Common.Models;
using FluentAssertions;
using Xunit;

namespace ASPS.Tests.Business.UserDomain;

/// <summary>
/// Unit tests for TrackUrlAnalysisResult
/// ASPS-250: Unit Tests - TrackUrlAlert Components
/// </summary>
public class TrackUrlAnalysisResultTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var url = "https://example.com/page";
        var fromUrl = "https://google.com";
        var duration = 120;
        var scamInProgressKey = "";
        var ipAddress = "192.168.1.100";
        var userAgent = "Mozilla/5.0";
        var tabId = "tab-456";
        var timezone = "America/New_York";
        var domain = "example.com";
        var isSafeDomain = false;
        var riskAssessment = new RiskAssessment(20, "Monitor", false, 0.8f);

        // Act
        var vm = new TrackUrlAnalysisResult(
            url, fromUrl, duration, scamInProgressKey,
            ipAddress, userAgent, tabId, timezone,
            domain, isSafeDomain, riskAssessment);

        // Assert
        vm.Should().NotBeNull();
        vm.Url.Should().Be(url);
        vm.FromUrl.Should().Be(fromUrl);
        vm.Duration.Should().Be(duration);
        vm.ScamInProgressKey.Should().Be(scamInProgressKey);
        vm.IPAddress.Should().Be(ipAddress);
        vm.UserAgent.Should().Be(userAgent);
        vm.TabId.Should().Be(tabId);
        vm.Timezone.Should().Be(timezone);
        vm.Domain.Should().Be(domain);
        vm.IsSafeDomain.Should().BeFalse();
        vm.risk_assessment.Should().Be(riskAssessment);
    }

    [Fact]
    public void Constructor_WithNullRiskAssessment_AcceptsValue()
    {
        // Arrange
        var url = "https://example.com";
        var domain = "example.com";

        // Act
        var vm = new TrackUrlAnalysisResult(
            url, "", 60, "", "", "", "", "",
            domain, false, false, null);

        // Assert
        vm.Should().NotBeNull();
        vm.risk_assessment.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithSafeDomain_SetsFlagCorrectly()
    {
        // Arrange
        var url = "https://google.com";
        var domain = "google.com";

        // Act
        var vm = new TrackUrlAnalysisResult(
            url, "", 60, "", "", "", "", "",
            domain, true, new RiskAssessment(0, "Safe", false, 1));

        // Assert
        vm.IsSafeDomain.Should().BeTrue();
        vm.risk_assessment?.risk_score.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithScamInProgressKey_PreservesValue()
    {
        // Arrange
        var scamKey = "scam-session-12345";

        // Act
        var vm = new TrackUrlAnalysisResult(
            "https://scam.com", "", 60, scamKey, "", "", "", "",
            "scam.com", false, new RiskAssessment(60, "High", true, 0.9f));

        // Assert
        vm.ScamInProgressKey.Should().Be(scamKey);
        vm.risk_assessment?.risk_score.Should().Be(60);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void TypeName_ReturnsCorrectValue()
    {
        // Arrange
        var vm = new TrackUrlAnalysisResult(
            "https://test.com", "", 60, "", "", "", "", "",
            "test.com", false, false, null);

        // Assert
        vm.TypeName.Should().Be("TrackUrlAnalysisResult");
    }

    [Fact]
    public void Url_HasCorrectValue()
    {
        // Arrange
        var url = "https://suspicious-site.com/login";

        // Act
        var vm = new TrackUrlAnalysisResult(
            url, "", 60, "", "", "", "", "",
            "suspicious-site.com", false, false, null);

        // Assert
        vm.Url.Should().Be(url);
    }

    [Fact]
    public void FromUrl_HasCorrectValue()
    {
        // Arrange
        var fromUrl = "https://referrer.com";

        // Act
        var vm = new TrackUrlAnalysisResult(
            "https://target.com", fromUrl, 60, "", "", "", "", "",
            "target.com", false, false, null);

        // Assert
        vm.FromUrl.Should().Be(fromUrl);
    }

    [Fact]
    public void Duration_HasCorrectValue()
    {
        // Arrange
        var duration = 300;

        // Act
        var vm = new TrackUrlAnalysisResult(
            "https://test.com", "", duration, "", "", "", "", "",
            "test.com", false, false, null);

        // Assert
        vm.Duration.Should().Be(duration);
    }

    [Fact]
    public void Domain_HasCorrectValue()
    {
        // Arrange
        var domain = "example.com";

        // Act
        var vm = new TrackUrlAnalysisResult(
            "https://example.com/page", "", 60, "", "", "", "", "",
            domain, false, false, null);

        // Assert
        vm.Domain.Should().Be(domain);
    }

    [Fact]
    public void IPAddress_HasCorrectValue()
    {
        // Arrange
        var ipAddress = "10.0.0.1";

        // Act
        var vm = new TrackUrlAnalysisResult(
            "https://test.com", "", 60, "", ipAddress, "", "", "",
            "test.com", false, false, null);

        // Assert
        vm.IPAddress.Should().Be(ipAddress);
    }

    [Fact]
    public void UserAgent_HasCorrectValue()
    {
        // Arrange
        var userAgent = "Chrome/96.0";

        // Act
        var vm = new TrackUrlAnalysisResult(
            "https://test.com", "", 60, "", "", userAgent, "", "",
            "test.com", false, false, null);

        // Assert
        vm.UserAgent.Should().Be(userAgent);
    }

    [Fact]
    public void TabId_HasCorrectValue()
    {
        // Arrange
        var tabId = "tab-123";

        // Act
        var vm = new TrackUrlAnalysisResult(
            "https://test.com", "", 60, "", "", "", tabId, "",
            "test.com", false, false, null);

        // Assert
        vm.TabId.Should().Be(tabId);
    }

    [Fact]
    public void Timezone_HasCorrectValue()
    {
        // Arrange
        var timezone = "Europe/London";

        // Act
        var vm = new TrackUrlAnalysisResult(
            "https://test.com", "", 60, "", "", "", "", timezone,
            "test.com", false, false, null);

        // Assert
        vm.Timezone.Should().Be(timezone);
    }

    #endregion

    #region Risk Assessment Tests

    [Fact]
    public void RiskAssessment_WithLowRisk_HasCorrectValues()
    {
        // Arrange
        var riskAssessment = new RiskAssessment(10, "Low", false, 0.7f);

        // Act
        var vm = new TrackUrlAnalysisResult(
            "https://test.com", "", 60, "", "", "", "", "",
            "test.com", false, riskAssessment);

        // Assert
        vm.risk_assessment.Should().NotBeNull();
        vm.risk_assessment?.risk_score.Should().Be(10);
        vm.risk_assessment?.risk_level.Should().Be("Low");
        vm.risk_assessment?.is_scam.Should().BeFalse();
    }

    [Fact]
    public void RiskAssessment_WithMediumRisk_HasCorrectValues()
    {
        // Arrange
        var riskAssessment = new RiskAssessment(20, "Medium", false, 0.6f);

        // Act
        var vm = new TrackUrlAnalysisResult(
            "https://test.com", "", 350, "", "", "", "", "",
            "test.com", false, riskAssessment);

        // Assert
        vm.risk_assessment?.risk_score.Should().Be(20);
        vm.risk_assessment?.risk_level.Should().Be("Medium");
    }

    [Fact]
    public void RiskAssessment_WithHighRisk_HasCorrectValues()
    {
        // Arrange
        var riskAssessment = new RiskAssessment(60, "High", true, 0.9f);

        // Act
        var vm = new TrackUrlAnalysisResult(
            "https://scam.com", "", 60, "scam-key", "", "", "", "",
            "scam.com", false, riskAssessment);

        // Assert
        vm.risk_assessment?.risk_score.Should().Be(60);
        vm.risk_assessment?.risk_level.Should().Be("High");
        vm.risk_assessment?.is_scam.Should().BeTrue();
    }

    [Fact]
    public void RiskAssessment_WithSafeDomain_HasZeroScore()
    {
        // Arrange
        var riskAssessment = new RiskAssessment(0, "Safe", false, 1);

        // Act
        var vm = new TrackUrlAnalysisResult(
            "https://google.com", "", 120, "", "", "", "", "",
            "google.com", true, riskAssessment);

        // Assert
        vm.IsSafeDomain.Should().BeTrue();
        vm.risk_assessment?.risk_score.Should().Be(0);
        vm.risk_assessment?.risk_level.Should().Be("Safe");
    }

    #endregion

    #region Edge Cases

    [Theory]
    [InlineData(0)]
    [InlineData(60)]
    [InlineData(300)]
    [InlineData(600)]
    [InlineData(3600)]
    public void Duration_WithVariousValues_AcceptsValue(int duration)
    {
        // Act
        var vm = new TrackUrlAnalysisResult(
            "https://test.com", "", duration, "", "", "", "", "",
            "test.com", false, false, null);

        // Assert
        vm.Duration.Should().Be(duration);
    }

    [Fact]
    public void Constructor_WithEmptyStrings_AcceptsValues()
    {
        // Act
        var vm = new TrackUrlAnalysisResult(
            "https://test.com", "", 60, "", "", "", "", "",
            "test.com", false, false, null);

        // Assert
        vm.Should().NotBeNull();
        vm.FromUrl.Should().BeEmpty();
        vm.ScamInProgressKey.Should().BeEmpty();
        vm.IPAddress.Should().BeEmpty();
        vm.UserAgent.Should().BeEmpty();
        vm.TabId.Should().BeEmpty();
        vm.Timezone.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithAllParameters_SetsAllProperties()
    {
        // Arrange
        var url = "https://example.com/page";
        var fromUrl = "https://google.com";
        var duration = 150;
        var scamKey = "scam-789";
        var ipAddress = "10.20.30.40";
        var userAgent = "Mozilla/5.0";
        var tabId = "tab-xyz";
        var timezone = "America/Los_Angeles";
        var domain = "example.com";
        var isSafeDomain = false;
        var riskAssessment = new RiskAssessment(25, "Medium", false, 0.75f);

        // Act
        var vm = new TrackUrlAnalysisResult(
            url, fromUrl, duration, scamKey,
            ipAddress, userAgent, tabId, timezone,
            domain, isSafeDomain, riskAssessment);

        // Assert
        vm.Url.Should().Be(url);
        vm.FromUrl.Should().Be(fromUrl);
        vm.Duration.Should().Be(duration);
        vm.ScamInProgressKey.Should().Be(scamKey);
        vm.IPAddress.Should().Be(ipAddress);
        vm.UserAgent.Should().Be(userAgent);
        vm.TabId.Should().Be(tabId);
        vm.Timezone.Should().Be(timezone);
        vm.Domain.Should().Be(domain);
        vm.IsSafeDomain.Should().BeFalse();
        vm.risk_assessment.Should().NotBeNull();
        vm.risk_assessment?.risk_score.Should().Be(25);
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://example.com")]
    [InlineData("https://example.com:8080")]
    [InlineData("https://example.com/path?query=value")]
    public void Url_WithVariousFormats_AcceptsValue(string url)
    {
        // Act
        var vm = new TrackUrlAnalysisResult(
            url, "", 60, "", "", "", "", "",
            "example.com", false, false, null);

        // Assert
        vm.Url.Should().Be(url);
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("sub.example.com")]
    [InlineData("deep.sub.example.com")]
    public void Domain_WithVariousFormats_AcceptsValue(string domain)
    {
        // Act
        var vm = new TrackUrlAnalysisResult(
            $"https://{domain}/page", "", 60, "", "", "", "", "",
            domain, false, false, null);

        // Assert
        vm.Domain.Should().Be(domain);
    }

    #endregion

    #region Inheritance Tests

    [Fact]
    public void TrackUrlAnalysisResult_InheritsFromAnalysisResult()
    {
        // Arrange
        var vm = new TrackUrlAnalysisResult(
            "https://test.com", "", 60, "", "", "", "", "",
            "test.com", false, false, null);

        // Assert
        vm.Should().BeAssignableTo<AnalysisResult>();
    }

    [Fact]
    public void TrackUrlAnalysisResult_CanAccessBaseProperties()
    {
        // Arrange & Act
        var vm = new TrackUrlAnalysisResult(
            "https://test.com", "", 60, "", "", "", "", "",
            "test.com", false, false, null)
        {
            Success = true,
            analyzed_at = DateTime.UtcNow
        };

        // Assert
        vm.Success.Should().BeTrue();
        vm.analyzed_at.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region Scam Detection Scenarios

    [Fact]
    public void ScamDetection_WithScamInProgressKey_HasHighRiskScore()
    {
        // Arrange
        var scamKey = "scam-session-99999";
        var riskAssessment = new RiskAssessment(60, "High", true, 0.95f);

        // Act
        var vm = new TrackUrlAnalysisResult(
            "https://scam-site.com/login", "", 100, scamKey,
            "192.168.1.1", "Chrome", "tab-1", "UTC",
            "scam-site.com", false, riskAssessment);

        // Assert
        vm.ScamInProgressKey.Should().Be(scamKey);
        vm.risk_assessment?.risk_score.Should().BeGreaterThanOrEqualTo(60);
        vm.risk_assessment?.is_scam.Should().BeTrue();
    }

    [Fact]
    public void ScamDetection_WithoutScamKey_HasLowerRisk()
    {
        // Arrange
        var riskAssessment = new RiskAssessment(10, "Low", false, 0.6f);

        // Act
        var vm = new TrackUrlAnalysisResult(
            "https://normal-site.com", "", 60, "",
            "", "", "", "",
            "normal-site.com", false, riskAssessment);

        // Assert
        vm.ScamInProgressKey.Should().BeEmpty();
        vm.risk_assessment?.risk_score.Should().BeLessThan(60);
        vm.risk_assessment?.is_scam.Should().BeFalse();
    }

    #endregion

    #region Safe Domain Tests

    [Fact]
    public void SafeDomain_WithWhitelistedDomain_HasCorrectFlags()
    {
        // Arrange
        var riskAssessment = new RiskAssessment(0, "Safe", false, 1);

        // Act
        var vm = new TrackUrlAnalysisResult(
            "https://google.com/search", "", 120, "",
            "", "", "", "",
            "google.com", true, riskAssessment);

        // Assert
        vm.IsSafeDomain.Should().BeTrue();
        vm.Domain.Should().Be("google.com");
        vm.risk_assessment?.risk_score.Should().Be(0);
    }

    [Fact]
    public void UnsafeDomain_WithUnknownSite_HasCorrectFlags()
    {
        // Arrange
        var riskAssessment = new RiskAssessment(20, "Monitor", false, 0.7f);

        // Act
        var vm = new TrackUrlAnalysisResult(
            "https://unknown-site.xyz", "", 60, "",
            "", "", "", "",
            "unknown-site.xyz", false, riskAssessment);

        // Assert
        vm.IsSafeDomain.Should().BeFalse();
        vm.Domain.Should().Be("unknown-site.xyz");
    }

    #endregion
}
