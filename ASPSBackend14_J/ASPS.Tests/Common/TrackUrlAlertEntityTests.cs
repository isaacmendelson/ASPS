using Common.Entities;
using Common.Enums;
using Xunit;

namespace ASPS.Tests.Common;

public class TrackUrlAlertEntityTests
{
    [Fact]
    public void TrackUrlAlertEntity_TypeName_ReturnsCorrectValue()
    {
        // Arrange & Act
        var alert = new TrackUrlAlertEntity();

        // Assert
        Assert.Equal("TrackUrlAlert", alert.TypeName);
    }

    [Fact]
    public void TrackUrlAlertEntity_InheritsFromDeviceAlertEntity()
    {
        // Arrange & Act
        var alert = new TrackUrlAlertEntity();

        // Assert
        Assert.IsAssignableFrom<DeviceAlertEntity>(alert);
    }

    [Fact]
    public void TrackUrlAlertEntity_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var alert = new TrackUrlAlertEntity();

        // Assert
        Assert.Equal(string.Empty, alert.Url);
        Assert.Equal(string.Empty, alert.FromUrl);
        Assert.Equal(0, alert.Duration);
        Assert.Equal(string.Empty, alert.ScamInProgressKey);
        Assert.Equal(string.Empty, alert.IPAddress);
        Assert.Equal(string.Empty, alert.UserAgent);
        Assert.Equal(string.Empty, alert.TabId);
        Assert.Equal(string.Empty, alert.Timezone);
    }

    [Fact]
    public void TrackUrlAlertEntity_Url_CanBeSet()
    {
        // Arrange
        var alert = new TrackUrlAlertEntity();
        const string expectedUrl = "https://example.com/suspicious-page";

        // Act
        alert.Url = expectedUrl;

        // Assert
        Assert.Equal(expectedUrl, alert.Url);
    }

    [Fact]
    public void TrackUrlAlertEntity_FromUrl_CanBeSet()
    {
        // Arrange
        var alert = new TrackUrlAlertEntity();
        const string expectedFromUrl = "https://google.com/search";

        // Act
        alert.FromUrl = expectedFromUrl;

        // Assert
        Assert.Equal(expectedFromUrl, alert.FromUrl);
    }

    [Fact]
    public void TrackUrlAlertEntity_Duration_CanBeSet()
    {
        // Arrange
        var alert = new TrackUrlAlertEntity();

        // Act
        alert.Duration = 120;

        // Assert
        Assert.Equal(120, alert.Duration);
    }

    [Fact]
    public void TrackUrlAlertEntity_ScamInProgressKey_CanBeSet()
    {
        // Arrange
        var alert = new TrackUrlAlertEntity();
        const string expectedKey = "scam-key-123";

        // Act
        alert.ScamInProgressKey = expectedKey;

        // Assert
        Assert.Equal(expectedKey, alert.ScamInProgressKey);
    }

    [Fact]
    public void TrackUrlAlertEntity_IPAddress_CanBeSet()
    {
        // Arrange
        var alert = new TrackUrlAlertEntity();
        const string expectedIP = "192.168.1.100";

        // Act
        alert.IPAddress = expectedIP;

        // Assert
        Assert.Equal(expectedIP, alert.IPAddress);
    }

    [Fact]
    public void TrackUrlAlertEntity_UserAgent_CanBeSet()
    {
        // Arrange
        var alert = new TrackUrlAlertEntity();
        const string expectedUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";

        // Act
        alert.UserAgent = expectedUserAgent;

        // Assert
        Assert.Equal(expectedUserAgent, alert.UserAgent);
    }

    [Fact]
    public void TrackUrlAlertEntity_TabId_CanBeSet()
    {
        // Arrange
        var alert = new TrackUrlAlertEntity();
        const string expectedTabId = "tab-456";

        // Act
        alert.TabId = expectedTabId;

        // Assert
        Assert.Equal(expectedTabId, alert.TabId);
    }

    [Fact]
    public void TrackUrlAlertEntity_Timezone_CanBeSet()
    {
        // Arrange
        var alert = new TrackUrlAlertEntity();
        const string expectedTimezone = "America/New_York";

        // Act
        alert.Timezone = expectedTimezone;

        // Assert
        Assert.Equal(expectedTimezone, alert.Timezone);
    }

    [Fact]
    public void TrackUrlAlertEntity_AllProperties_CanBeSet()
    {
        // Arrange
        var alert = new TrackUrlAlertEntity
        {
            Url = "https://suspicious-site.com",
            FromUrl = "https://google.com",
            Duration = 300,
            ScamInProgressKey = "scam-789",
            IPAddress = "10.0.0.1",
            UserAgent = "Chrome/96.0",
            TabId = "tab-123",
            Timezone = "UTC"
        };

        // Assert
        Assert.Equal("https://suspicious-site.com", alert.Url);
        Assert.Equal("https://google.com", alert.FromUrl);
        Assert.Equal(300, alert.Duration);
        Assert.Equal("scam-789", alert.ScamInProgressKey);
        Assert.Equal("10.0.0.1", alert.IPAddress);
        Assert.Equal("Chrome/96.0", alert.UserAgent);
        Assert.Equal("tab-123", alert.TabId);
        Assert.Equal("UTC", alert.Timezone);
    }

    [Fact]
    public void TrackUrlAlertEntity_InheritedProperties_FromDeviceAlertEntity()
    {
        // Arrange
        var alert = new TrackUrlAlertEntity
        {
            AlertType = "TrackUrl",
            Priority = Priority.High,
            Timestamp = new DateTime(2026, 3, 14, 12, 0, 0),
            Token = "token-123",
            DeviceUid = "device-456",
            DeviceType = DeviceType.MobilePhone,
            OperatingSystem = OperatingSystemType.Android,
            MAC = "00:11:22:33:44:55",
            Status = AlertFlagStatus.Open
        };

        // Assert
        Assert.Equal("TrackUrl", alert.AlertType);
        Assert.Equal(Priority.High, alert.Priority);
        Assert.Equal(new DateTime(2026, 3, 14, 12, 0, 0), alert.Timestamp);
        Assert.Equal("token-123", alert.Token);
        Assert.Equal("device-456", alert.DeviceUid);
        Assert.Equal(DeviceType.MobilePhone, alert.DeviceType);
        Assert.Equal(OperatingSystemType.Android, alert.OperatingSystem);
        Assert.Equal("00:11:22:33:44:55", alert.MAC);
        Assert.Equal(AlertFlagStatus.Open, alert.Status);
    }

    [Fact]
    public void TrackUrlAlertEntity_Duration_CanBeZero()
    {
        // Arrange
        var alert = new TrackUrlAlertEntity
        {
            Duration = 0
        };

        // Assert
        Assert.Equal(0, alert.Duration);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(60)]
    [InlineData(300)]
    [InlineData(3600)]
    public void TrackUrlAlertEntity_Duration_AcceptsDifferentValues(int duration)
    {
        // Arrange & Act
        var alert = new TrackUrlAlertEntity { Duration = duration };

        // Assert
        Assert.Equal(duration, alert.Duration);
    }
}
