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
        Assert.Equal(string.Empty, alert.TrackerKeys);
        Assert.Equal(0, alert.TrackerCount);
        Assert.Equal(string.Empty, alert.TrackingType);
        Assert.Equal(string.Empty, alert.UserAgent);
    }

    [Fact]
    public void TrackUrlAlertEntity_Url_CanBeSet()
    {
        // Arrange
        var alert = new TrackUrlAlertEntity();
        const string expectedUrl = "https://example.com/track?utm_source=test&fbclid=abc123";

        // Act
        alert.Url = expectedUrl;

        // Assert
        Assert.Equal(expectedUrl, alert.Url);
    }

    [Fact]
    public void TrackUrlAlertEntity_TrackerKeys_CanBeSet()
    {
        // Arrange
        var alert = new TrackUrlAlertEntity();
        const string expectedKeys = "[\"utm_source\",\"fbclid\",\"gclid\"]";

        // Act
        alert.TrackerKeys = expectedKeys;

        // Assert
        Assert.Equal(expectedKeys, alert.TrackerKeys);
    }

    [Fact]
    public void TrackUrlAlertEntity_TrackerCount_CanBeSet()
    {
        // Arrange
        var alert = new TrackUrlAlertEntity();

        // Act
        alert.TrackerCount = 3;

        // Assert
        Assert.Equal(3, alert.TrackerCount);
    }

    [Fact]
    public void TrackUrlAlertEntity_TrackingType_CanBeSet()
    {
        // Arrange
        var alert = new TrackUrlAlertEntity();
        const string expectedType = "Analytics";

        // Act
        alert.TrackingType = expectedType;

        // Assert
        Assert.Equal(expectedType, alert.TrackingType);
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
    public void TrackUrlAlertEntity_AllProperties_CanBeSet()
    {
        // Arrange
        var alert = new TrackUrlAlertEntity
        {
            Url = "https://tracking.site.com?param=value",
            TrackerKeys = "[\"param\",\"tracker\"]",
            TrackerCount = 2,
            TrackingType = "Advertising",
            UserAgent = "Chrome/96.0"
        };

        // Assert
        Assert.Equal("https://tracking.site.com?param=value", alert.Url);
        Assert.Equal("[\"param\",\"tracker\"]", alert.TrackerKeys);
        Assert.Equal(2, alert.TrackerCount);
        Assert.Equal("Advertising", alert.TrackingType);
        Assert.Equal("Chrome/96.0", alert.UserAgent);
    }

    [Fact]
    public void TrackUrlAlertEntity_InheritedProperties_FromDeviceAlertEntity()
    {
        // Arrange
        var alert = new TrackUrlAlertEntity
        {
            AlertType = "TrackUrl",
            Priority = Priority.High,
            Timestamp = new DateTime(2026, 3, 12, 12, 0, 0),
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
        Assert.Equal(new DateTime(2026, 3, 12, 12, 0, 0), alert.Timestamp);
        Assert.Equal("token-123", alert.Token);
        Assert.Equal("device-456", alert.DeviceUid);
        Assert.Equal(DeviceType.MobilePhone, alert.DeviceType);
        Assert.Equal(OperatingSystemType.Android, alert.OperatingSystem);
        Assert.Equal("00:11:22:33:44:55", alert.MAC);
        Assert.Equal(AlertFlagStatus.Open, alert.Status);
    }

    [Theory]
    [InlineData("Analytics")]
    [InlineData("Advertising")]
    [InlineData("Social")]
    [InlineData("")]
    public void TrackUrlAlertEntity_TrackingType_AcceptsDifferentValues(string trackingType)
    {
        // Arrange & Act
        var alert = new TrackUrlAlertEntity { TrackingType = trackingType };

        // Assert
        Assert.Equal(trackingType, alert.TrackingType);
    }

    [Fact]
    public void TrackUrlAlertEntity_TrackerCount_CanBeZero()
    {
        // Arrange
        var alert = new TrackUrlAlertEntity
        {
            TrackerCount = 0
        };

        // Assert
        Assert.Equal(0, alert.TrackerCount);
    }

    [Fact]
    public void TrackUrlAlertEntity_TrackerCount_CanBeMultiple()
    {
        // Arrange
        var alert = new TrackUrlAlertEntity
        {
            TrackerCount = 10
        };

        // Assert
        Assert.Equal(10, alert.TrackerCount);
    }
}
