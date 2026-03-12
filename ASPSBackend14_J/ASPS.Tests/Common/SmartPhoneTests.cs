using Common.Entities;
using Common.Enums;
using Common.Models;
using Xunit;

namespace ASPS.Tests.Common;

public class SmartPhoneTests
{
    [Fact]
    public void SmartPhone_TypeName_ReturnsCorrectValue()
    {
        // Arrange & Act
        var smartPhone = new SmartPhone();

        // Assert
        Assert.Equal("SmartPhone", smartPhone.TypeName);
    }

    [Fact]
    public void SmartPhone_InheritsFromUserDevice()
    {
        // Arrange & Act
        var smartPhone = new SmartPhone();

        // Assert
        Assert.IsAssignableFrom<UserDevice>(smartPhone);
    }

    [Fact]
    public void SmartPhone_CanSetInheritedProperties()
    {
        // Arrange
        var smartPhone = new SmartPhone
        {
            DeviceUid = "phone-12345",
            PhoneNumber = "+1234567890",
            DeviceType = DeviceType.MobilePhone,
            OperatingSystem = OperatingSystemType.Android,
            Make = "Samsung",
            Model = "Galaxy S21",
            MonitoringStatus = DeviceMonitoringStatus.Enabled
        };

        // Assert
        Assert.Equal("phone-12345", smartPhone.DeviceUid);
        Assert.Equal("+1234567890", smartPhone.PhoneNumber);
        Assert.Equal(DeviceType.MobilePhone, smartPhone.DeviceType);
        Assert.Equal(OperatingSystemType.Android, smartPhone.OperatingSystem);
        Assert.Equal("Samsung", smartPhone.Make);
        Assert.Equal("Galaxy S21", smartPhone.Model);
        Assert.Equal(DeviceMonitoringStatus.Enabled, smartPhone.MonitoringStatus);
    }

    [Fact]
    public void SmartPhone_PhoneNumber_IsNullableFromBaseClass()
    {
        // Arrange & Act
        var smartPhone = new SmartPhone();

        // Assert
        Assert.Null(smartPhone.PhoneNumber);
    }

    [Fact]
    public void SmartPhone_UserKey_CanBeSet()
    {
        // Arrange
        var smartPhone = new SmartPhone();
        var userKey = new Key("User", "user-123");

        // Act
        smartPhone.UserKey = userKey;

        // Assert
        Assert.NotNull(smartPhone.UserKey);
        Assert.Equal("User", smartPhone.UserKey.Type);
        Assert.Equal("user-123", smartPhone.UserKey.Value);
        Assert.Equal("user-123", smartPhone.UserKeyField);
    }

    [Fact]
    public void SmartPhone_UserKey_WhenNull_ReturnsNull()
    {
        // Arrange
        var smartPhone = new SmartPhone
        {
            UserKeyField = null
        };

        // Assert
        Assert.Null(smartPhone.UserKey);
    }

    [Fact]
    public void SmartPhone_IMEI_CanBeSet()
    {
        // Arrange
        var smartPhone = new SmartPhone();
        const string expectedIMEI = "123456789012345";

        // Act
        smartPhone.IMEI = expectedIMEI;

        // Assert
        Assert.Equal(expectedIMEI, smartPhone.IMEI);
    }

    [Fact]
    public void SmartPhone_MAC_CanBeSet()
    {
        // Arrange
        var smartPhone = new SmartPhone();
        const string expectedMAC = "00:11:22:33:44:55";

        // Act
        smartPhone.MAC = expectedMAC;

        // Assert
        Assert.Equal(expectedMAC, smartPhone.MAC);
    }

    [Fact]
    public void SmartPhone_DeviceUid_DefaultsToEmptyString()
    {
        // Arrange & Act
        var smartPhone = new SmartPhone();

        // Assert
        Assert.Equal(string.Empty, smartPhone.DeviceUid);
    }

    [Theory]
    [InlineData(OperatingSystemType.Android)]
    [InlineData(OperatingSystemType.IOS)]
    public void SmartPhone_OperatingSystem_AcceptsValidValues(OperatingSystemType os)
    {
        // Arrange & Act
        var smartPhone = new SmartPhone { OperatingSystem = os };

        // Assert
        Assert.Equal(os, smartPhone.OperatingSystem);
    }

    [Fact]
    public void SmartPhone_Tag_GeneratesCorrectly_WithDeviceUid()
    {
        // Arrange
        var smartPhone = new SmartPhone
        {
            KeyField = "SmartPhone-001",
            DeviceUid = "phone-abc-123"
        };

        // Act
        var tag = smartPhone.Tag;

        // Assert
        Assert.NotNull(tag);
        Assert.Equal("phone-abc-123", tag.Name);
        Assert.Equal("SmartPhone", tag.Type);
    }

    [Fact]
    public void SmartPhone_Tag_GeneratesCorrectly_WithMakeAndModel()
    {
        // Arrange
        var smartPhone = new SmartPhone
        {
            KeyField = "SmartPhone-002",
            DeviceUid = "",
            Make = "Apple",
            Model = "iPhone 13"
        };

        // Act
        var tag = smartPhone.Tag;

        // Assert
        Assert.NotNull(tag);
        Assert.Equal("Apple iPhone 13", tag.Name);
        Assert.Equal("SmartPhone", tag.Type);
    }

    [Fact]
    public void SmartPhone_Tag_FallsBackToUnknownDevice()
    {
        // Arrange
        var smartPhone = new SmartPhone
        {
            KeyField = "SmartPhone-003",
            DeviceUid = "",
            Make = null,
            Model = null
        };

        // Act
        var tag = smartPhone.Tag;

        // Assert
        Assert.NotNull(tag);
        Assert.Equal("Unknown Device", tag.Name);
        Assert.Equal("SmartPhone", tag.Type);
    }
}
