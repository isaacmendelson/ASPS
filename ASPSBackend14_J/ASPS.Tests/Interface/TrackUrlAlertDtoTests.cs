using System;
using FluentAssertions;
using Interface.Analysis;
using Xunit;

namespace ASPS.Tests.Interface;

/// <summary>
/// Unit tests for TrackUrlAlertDto
/// ASPS-250: Unit Tests - TrackUrlAlert Components
/// </summary>
public class TrackUrlAlertDtoTests
{
    #region Constructor Tests

    [Fact]
    public void DefaultConstructor_CreatesInstance_WithUtcTimestamp()
    {
        // Act
        var dto = new TrackUrlAlertDto();

        // Assert
        dto.Should().NotBeNull();
        dto.Url.Should().BeEmpty();
        dto.FromUrl.Should().BeEmpty();
        dto.Duration.Should().Be(0);
        dto.ScamInProgressKey.Should().BeEmpty();
        dto.IPAddress.Should().BeEmpty();
        dto.UserAgent.Should().BeEmpty();
        dto.TabId.Should().BeEmpty();
        dto.Timezone.Should().BeEmpty();
        dto.Priority.Should().BeEmpty();
        dto.DeviceUid.Should().BeEmpty();
        dto.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var url = "https://example.com/page";
        var fromUrl = "https://google.com";
        var duration = 120;
        var deviceUid = "device-123";
        var ipAddress = "192.168.1.100";
        var userAgent = "Mozilla/5.0";
        var tabId = "tab-456";
        var timezone = "America/New_York";

        // Act
        var dto = new TrackUrlAlertDto(
            url, fromUrl, duration, deviceUid, 
            ipAddress, userAgent, tabId, timezone);

        // Assert
        dto.Should().NotBeNull();
        dto.Url.Should().Be(url);
        dto.FromUrl.Should().Be(fromUrl);
        dto.Duration.Should().Be(duration);
        dto.DeviceUid.Should().Be(deviceUid);
        dto.IPAddress.Should().Be(ipAddress);
        dto.UserAgent.Should().Be(userAgent);
        dto.TabId.Should().Be(tabId);
        dto.Timezone.Should().Be(timezone);
        dto.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_WithNullUrl_ThrowsArgumentNullException()
    {
        // Arrange
        string? url = null;

        // Act
        Action act = () => new TrackUrlAlertDto(
            url!, "fromUrl", 60, "device-123", 
            "127.0.0.1", "agent", "tab", "UTC");

        // Assert
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("url");
    }

    [Fact]
    public void Constructor_WithNullDeviceUid_ThrowsArgumentNullException()
    {
        // Arrange
        string? deviceUid = null;

        // Act
        Action act = () => new TrackUrlAlertDto(
            "https://test.com", "fromUrl", 60, deviceUid!, 
            "127.0.0.1", "agent", "tab", "UTC");

        // Assert
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("deviceUid");
    }

    [Fact]
    public void Constructor_WithNullFromUrl_SetsEmptyString()
    {
        // Act
        var dto = new TrackUrlAlertDto(
            "https://test.com", null!, 60, "device-123", 
            "127.0.0.1", "agent", "tab", "UTC");

        // Assert
        dto.FromUrl.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNullOptionalStrings_SetsEmptyStrings()
    {
        // Act
        var dto = new TrackUrlAlertDto(
            "https://test.com", "fromUrl", 60, "device-123", 
            null!, null!, null!, null!);

        // Assert
        dto.IPAddress.Should().BeEmpty();
        dto.UserAgent.Should().BeEmpty();
        dto.TabId.Should().BeEmpty();
        dto.Timezone.Should().BeEmpty();
    }

    #endregion

    #region Property Tests

    [Fact]
    public void Url_CanBeSetAndRetrieved()
    {
        // Arrange
        var dto = new TrackUrlAlertDto();
        var url = "https://suspicious-site.com";

        // Act
        dto.Url = url;

        // Assert
        dto.Url.Should().Be(url);
    }

    [Fact]
    public void FromUrl_CanBeSetAndRetrieved()
    {
        // Arrange
        var dto = new TrackUrlAlertDto();
        var fromUrl = "https://referrer.com";

        // Act
        dto.FromUrl = fromUrl;

        // Assert
        dto.FromUrl.Should().Be(fromUrl);
    }

    [Fact]
    public void Duration_CanBeSetAndRetrieved()
    {
        // Arrange
        var dto = new TrackUrlAlertDto();
        var duration = 300;

        // Act
        dto.Duration = duration;

        // Assert
        dto.Duration.Should().Be(duration);
    }

    [Fact]
    public void ScamInProgressKey_CanBeSetAndRetrieved()
    {
        // Arrange
        var dto = new TrackUrlAlertDto();
        var key = "scam-key-123";

        // Act
        dto.ScamInProgressKey = key;

        // Assert
        dto.ScamInProgressKey.Should().Be(key);
    }

    [Fact]
    public void IPAddress_CanBeSetAndRetrieved()
    {
        // Arrange
        var dto = new TrackUrlAlertDto();
        var ipAddress = "10.0.0.1";

        // Act
        dto.IPAddress = ipAddress;

        // Assert
        dto.IPAddress.Should().Be(ipAddress);
    }

    [Fact]
    public void UserAgent_CanBeSetAndRetrieved()
    {
        // Arrange
        var dto = new TrackUrlAlertDto();
        var userAgent = "Chrome/96.0";

        // Act
        dto.UserAgent = userAgent;

        // Assert
        dto.UserAgent.Should().Be(userAgent);
    }

    [Fact]
    public void TabId_CanBeSetAndRetrieved()
    {
        // Arrange
        var dto = new TrackUrlAlertDto();
        var tabId = "tab-789";

        // Act
        dto.TabId = tabId;

        // Assert
        dto.TabId.Should().Be(tabId);
    }

    [Fact]
    public void Timezone_CanBeSetAndRetrieved()
    {
        // Arrange
        var dto = new TrackUrlAlertDto();
        var timezone = "Europe/London";

        // Act
        dto.Timezone = timezone;

        // Assert
        dto.Timezone.Should().Be(timezone);
    }

    [Fact]
    public void Priority_CanBeSetAndRetrieved()
    {
        // Arrange
        var dto = new TrackUrlAlertDto();
        var priority = "High";

        // Act
        dto.Priority = priority;

        // Assert
        dto.Priority.Should().Be(priority);
    }

    [Fact]
    public void DeviceUid_CanBeSetAndRetrieved()
    {
        // Arrange
        var dto = new TrackUrlAlertDto();
        var deviceUid = "device-456";

        // Act
        dto.DeviceUid = deviceUid;

        // Assert
        dto.DeviceUid.Should().Be(deviceUid);
    }

    [Fact]
    public void Timestamp_CanBeSetAndRetrieved()
    {
        // Arrange
        var dto = new TrackUrlAlertDto();
        var timestamp = new DateTime(2026, 3, 21, 12, 0, 0, DateTimeKind.Utc);

        // Act
        dto.Timestamp = timestamp;

        // Assert
        dto.Timestamp.Should().Be(timestamp);
    }

    #endregion

    #region Edge Cases

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://example.com")]
    [InlineData("https://example.com:8080")]
    [InlineData("https://example.com/path?query=value")]
    [InlineData("https://user:pass@example.com/path#fragment")]
    public void Url_WithVariousFormats_AcceptsValue(string url)
    {
        // Act
        var dto = new TrackUrlAlertDto(
            url, "", 0, "device-123", "", "", "", "");

        // Assert
        dto.Url.Should().Be(url);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(300)]
    [InlineData(600)]
    [InlineData(3600)]
    public void Duration_WithVariousValues_AcceptsValue(int duration)
    {
        // Act
        var dto = new TrackUrlAlertDto(
            "https://test.com", "", duration, "device-123", "", "", "", "");

        // Assert
        dto.Duration.Should().Be(duration);
    }

    [Theory]
    [InlineData("192.168.1.1")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("2001:0db8:85a3:0000:0000:8a2e:0370:7334")] // IPv6
    public void IPAddress_WithVariousFormats_AcceptsValue(string ipAddress)
    {
        // Arrange
        var dto = new TrackUrlAlertDto();

        // Act
        dto.IPAddress = ipAddress;

        // Assert
        dto.IPAddress.Should().Be(ipAddress);
    }

    [Theory]
    [InlineData("UTC")]
    [InlineData("America/New_York")]
    [InlineData("Europe/London")]
    [InlineData("Asia/Tokyo")]
    [InlineData("Australia/Sydney")]
    public void Timezone_WithVariousValues_AcceptsValue(string timezone)
    {
        // Arrange
        var dto = new TrackUrlAlertDto();

        // Act
        dto.Timezone = timezone;

        // Assert
        dto.Timezone.Should().Be(timezone);
    }

    [Fact]
    public void Constructor_WithEmptyFromUrl_PreservesEmptyString()
    {
        // Act
        var dto = new TrackUrlAlertDto(
            "https://test.com", "", 60, "device-123", 
            "127.0.0.1", "agent", "tab", "UTC");

        // Assert
        dto.FromUrl.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithZeroDuration_AcceptsValue()
    {
        // Act
        var dto = new TrackUrlAlertDto(
            "https://test.com", "fromUrl", 0, "device-123", 
            "127.0.0.1", "agent", "tab", "UTC");

        // Assert
        dto.Duration.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithAllParameters_SetsAllProperties()
    {
        // Arrange
        var url = "https://example.com/page";
        var fromUrl = "https://google.com";
        var duration = 150;
        var deviceUid = "device-abc";
        var ipAddress = "10.20.30.40";
        var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
        var tabId = "tab-xyz";
        var timezone = "America/Los_Angeles";

        // Act
        var dto = new TrackUrlAlertDto(
            url, fromUrl, duration, deviceUid, 
            ipAddress, userAgent, tabId, timezone);

        // Assert
        dto.Url.Should().Be(url);
        dto.FromUrl.Should().Be(fromUrl);
        dto.Duration.Should().Be(duration);
        dto.DeviceUid.Should().Be(deviceUid);
        dto.IPAddress.Should().Be(ipAddress);
        dto.UserAgent.Should().Be(userAgent);
        dto.TabId.Should().Be(tabId);
        dto.Timezone.Should().Be(timezone);
        dto.ScamInProgressKey.Should().BeEmpty();
        dto.Priority.Should().BeEmpty();
        dto.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region Serialization Tests

    [Fact]
    public void DTO_CanBeInstantiatedForSerialization()
    {
        // Arrange & Act
        var dto = new TrackUrlAlertDto
        {
            Url = "https://example.com",
            FromUrl = "https://referrer.com",
            Duration = 200,
            ScamInProgressKey = "scam-key",
            IPAddress = "192.168.1.50",
            UserAgent = "Safari/15.0",
            TabId = "tab-999",
            Timezone = "UTC",
            Priority = "Medium",
            DeviceUid = "device-serialization",
            Timestamp = DateTime.UtcNow
        };

        // Assert
        dto.Should().NotBeNull();
        dto.Url.Should().Be("https://example.com");
        dto.FromUrl.Should().Be("https://referrer.com");
        dto.Duration.Should().Be(200);
        dto.ScamInProgressKey.Should().Be("scam-key");
        dto.IPAddress.Should().Be("192.168.1.50");
        dto.UserAgent.Should().Be("Safari/15.0");
        dto.TabId.Should().Be("tab-999");
        dto.Timezone.Should().Be("UTC");
        dto.Priority.Should().Be("Medium");
        dto.DeviceUid.Should().Be("device-serialization");
    }

    [Fact]
    public void DTO_WithScamInProgressKey_PreservesValue()
    {
        // Arrange
        var dto = new TrackUrlAlertDto
        {
            Url = "https://scam-site.com",
            ScamInProgressKey = "scam-session-12345",
            DeviceUid = "device-123"
        };

        // Assert
        dto.ScamInProgressKey.Should().Be("scam-session-12345");
    }

    #endregion

    #region Long User Agent Tests

    [Fact]
    public void UserAgent_WithLongString_AcceptsValue()
    {
        // Arrange
        var dto = new TrackUrlAlertDto();
        var longUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/96.0.4664.110 Safari/537.36";

        // Act
        dto.UserAgent = longUserAgent;

        // Assert
        dto.UserAgent.Should().Be(longUserAgent);
    }

    #endregion
}
