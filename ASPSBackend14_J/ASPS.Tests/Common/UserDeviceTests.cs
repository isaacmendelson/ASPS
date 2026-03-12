using Xunit;
using FluentAssertions;
using Common.Entities;
using Common.Enums;
using Common.Models;

namespace ASPS.Tests.Common
{
    /// <summary>
    /// Unit tests for UserDevice abstract class using SmartPhone as concrete implementation
    /// ASPS-189: Unit Tests for UDDevice (UserDevice)
    /// </summary>
    public class UserDeviceTests
    {
        #region Constructor and Initialization Tests

        [Fact]
        public void Constructor_CreatesSmartPhoneInstance()
        {
            // Act
            var device = new SmartPhone();

            // Assert
            device.Should().NotBeNull();
            device.Should().BeAssignableTo<UserDevice>();
            device.Should().Be("SmartPhone");
        }

        [Fact]
        public void Constructor_InitializesWithDefaultValues()
        {
            // Act
            var device = new SmartPhone();

            // Assert
            device.DeviceUid.Should().Be(string.Empty);
            device.UserKeyField.Should().BeNull();
            device.DeviceType.Should().Be(default(DeviceType));
            device.MonitoringStatus.Should().Be(default(DeviceMonitoringStatus));
        }

        #endregion

        #region Property Tests

        [Fact]
        public void DeviceUid_CanBeSetAndRetrieved()
        {
            // Arrange
            var device = new SmartPhone();
            var deviceUid = "DEVICE-12345";

            // Act
            device.DeviceUid = deviceUid;

            // Assert
            device.DeviceUid.Should().Be("DEVICE-12345");
        }

        [Fact]
        public void DeviceType_CanBeSetAndRetrieved()
        {
            // Arrange
            var device = new SmartPhone();

            // Act
            device.DeviceType = DeviceType.MobilePhone;

            // Assert
            device.DeviceType.Should().Be(DeviceType.MobilePhone);
        }

        [Fact]
        public void OperatingSystem_CanBeSetAndRetrieved()
        {
            // Arrange
            var device = new SmartPhone();

            // Act
            device.OperatingSystem = OperatingSystemType.Android;

            // Assert
            device.OperatingSystem.Should().Be(OperatingSystemType.Android);
        }

        [Fact]
        public void PhoneNumber_CanBeSetAndRetrieved()
        {
            // Arrange
            var device = new SmartPhone();
            var phoneNumber = "+1234567890";

            // Act
            device.PhoneNumber = phoneNumber;

            // Assert
            device.PhoneNumber.Should().Be("+1234567890");
        }

        [Fact]
        public void MAC_CanBeSetAndRetrieved()
        {
            // Arrange
            var device = new SmartPhone();
            var mac = "00:1B:44:11:3A:B7";

            // Act
            device.MAC = mac;

            // Assert
            device.MAC.Should().Be("00:1B:44:11:3A:B7");
        }

        [Fact]
        public void IMEI_CanBeSetAndRetrieved()
        {
            // Arrange
            var device = new SmartPhone();
            var imei = "123456789012345";

            // Act
            device.IMEI = imei;

            // Assert
            device.IMEI.Should().Be("123456789012345");
        }

        [Fact]
        public void Make_CanBeSetAndRetrieved()
        {
            // Arrange
            var device = new SmartPhone();

            // Act
            device.Make = "Samsung";

            // Assert
            device.Make.Should().Be("Samsung");
        }

        [Fact]
        public void Model_CanBeSetAndRetrieved()
        {
            // Arrange
            var device = new SmartPhone();

            // Act
            device.Model = "Galaxy S21";

            // Assert
            device.Model.Should().Be("Galaxy S21");
        }

        [Fact]
        public void MonitoringStatus_CanBeSetAndRetrieved()
        {
            // Arrange
            var device = new SmartPhone();

            // Act
            device.MonitoringStatus = DeviceMonitoringStatus.Enabled;

            // Assert
            device.MonitoringStatus.Should().Be(DeviceMonitoringStatus.Enabled);
        }

        #endregion

        #region UserKey Property Tests

        [Fact]
        public void UserKey_WhenUserKeyFieldIsNull_ReturnsNull()
        {
            // Arrange
            var device = new SmartPhone();
            device.UserKeyField = null;

            // Act
            var userKey = device.UserKey;

            // Assert
            userKey.Should().BeNull();
        }

        [Fact]
        public void UserKey_WhenUserKeyFieldIsEmpty_ReturnsNull()
        {
            // Arrange
            var device = new SmartPhone();
            device.UserKeyField = string.Empty;

            // Act
            var userKey = device.UserKey;

            // Assert
            userKey.Should().BeNull();
        }

        [Fact]
        public void UserKey_WhenUserKeyFieldIsSet_ReturnsKey()
        {
            // Arrange
            var device = new SmartPhone();
            device.UserKeyField = "user123";

            // Act
            var userKey = device.UserKey;

            // Assert
            userKey.Should().NotBeNull();
            userKey!.Type.Should().Be("User");
            userKey.Value.Should().Be("user123");
        }

        [Fact]
        public void UserKey_SetterUpdatesUserKeyField()
        {
            // Arrange
            var device = new SmartPhone();
            var key = new Key("User", "user456");

            // Act
            device.UserKey = key;

            // Assert
            device.UserKeyField.Should().Be("user456");
        }

        [Fact]
        public void UserKey_SetterWithNull_SetsUserKeyFieldToNull()
        {
            // Arrange
            var device = new SmartPhone();
            device.UserKeyField = "user123";

            // Act
            device.UserKey = null;

            // Assert
            device.UserKeyField.Should().BeNull();
        }

        #endregion

        #region Tag Tests

        [Fact]
        public void Tag_WithDeviceUid_UsesDeviceUidAsName()
        {
            // Arrange
            var device = new SmartPhone();
            device.DeviceUid = "DEVICE-XYZ";
            device.KeyField = "device-key-1";

            // Act
            var tag = device.Tag;

            // Assert
            tag.Should().NotBeNull();
            tag.Name.Should().Be("DEVICE-XYZ");
            tag.Should().Be("SmartPhone");
        }

        [Fact]
        public void Tag_WithoutDeviceUid_WithMakeAndModel_UsesMakeModelAsName()
        {
            // Arrange
            var device = new SmartPhone();
            device.Make = "Apple";
            device.Model = "iPhone 13";
            device.KeyField = "device-key-2";

            // Act
            var tag = device.Tag;

            // Assert
            tag.Should().NotBeNull();
            tag.Name.Should().Be("Apple iPhone 13");
            tag.Should().Be("SmartPhone");
        }

        [Fact]
        public void Tag_WithoutDeviceUid_WithoutMakeModel_UsesUnknownDevice()
        {
            // Arrange
            var device = new SmartPhone();
            device.KeyField = "device-key-3";

            // Act
            var tag = device.Tag;

            // Assert
            tag.Should().NotBeNull();
            tag.Name.Should().Be("Unknown Device");
            tag.Should().Be("SmartPhone");
        }

        [Fact]
        public void Tag_IsCached_ReturnsConsistentInstance()
        {
            // Arrange
            var device = new SmartPhone();
            device.DeviceUid = "DEVICE-ABC";
            device.KeyField = "device-key-4";

            // Act
            var tag1 = device.Tag;
            var tag2 = device.Tag;

            // Assert
            tag1.Should().BeSameAs(tag2);
        }

        #endregion

        #region Real-World Scenario Tests

        [Fact]
        public void SmartPhone_FullInitialization_HasAllProperties()
        {
            // Arrange & Act
            var device = new SmartPhone
            {
                DeviceUid = "ANDROID-001",
                DeviceType = DeviceType.MobilePhone,
                OperatingSystem = OperatingSystemType.Android,
                PhoneNumber = "+972501234567",
                MAC = "AA:BB:CC:DD:EE:FF",
                IMEI = "353879234567890",
                Make = "Google",
                Model = "Pixel 6",
                MonitoringStatus = DeviceMonitoringStatus.Enabled,
                UserKeyField = "user-789"
            };

            // Assert
            device.DeviceUid.Should().Be("ANDROID-001");
            device.DeviceType.Should().Be(DeviceType.MobilePhone);
            device.OperatingSystem.Should().Be(OperatingSystemType.Android);
            device.PhoneNumber.Should().Be("+972501234567");
            device.MAC.Should().Be("AA:BB:CC:DD:EE:FF");
            device.IMEI.Should().Be("353879234567890");
            device.Make.Should().Be("Google");
            device.Model.Should().Be("Pixel 6");
            device.MonitoringStatus.Should().Be(DeviceMonitoringStatus.Enabled);
            device.UserKeyField.Should().Be("user-789");
        }

        [Fact]
        public void SmartPhone_IOS_ConfigurationIsCorrect()
        {
            // Arrange & Act
            var device = new SmartPhone
            {
                DeviceUid = "IOS-001",
                DeviceType = DeviceType.MobilePhone,
                OperatingSystem = OperatingSystemType.IOS,
                Make = "Apple",
                Model = "iPhone 14 Pro",
                MonitoringStatus = DeviceMonitoringStatus.Enabled
            };

            // Assert
            device.OperatingSystem.Should().Be(OperatingSystemType.IOS);
            device.Make.Should().Be("Apple");
            device.Model.Should().Be("iPhone 14 Pro");
        }

        [Fact]
        public void SmartPhone_WithUser_HasUserKey()
        {
            // Arrange
            var device = new SmartPhone
            {
                DeviceUid = "DEVICE-999",
                UserKeyField = "user-123"
            };

            // Act
            var userKey = device.UserKey;

            // Assert
            userKey.Should().NotBeNull();
            userKey!.Type.Should().Be("User");
            userKey.Value.Should().Be("user-123");
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public void UserKeyField_WithEmptyString_UserKeyIsNull()
        {
            // Arrange
            var device = new SmartPhone();

            // Act
            device.UserKeyField = "";

            // Assert
            device.UserKey.Should().BeNull();
        }

        [Fact]
        public void DeviceUid_CanBeEmpty()
        {
            // Arrange & Act
            var device = new SmartPhone
            {
                DeviceUid = string.Empty
            };

            // Assert
            device.DeviceUid.Should().Be(string.Empty);
        }

        [Fact]
        public void OptionalProperties_CanBeNull()
        {
            // Arrange & Act
            var device = new SmartPhone
            {
                PhoneNumber = null,
                MAC = null,
                IMEI = null,
                BiosSerial = null,
                Make = null,
                Model = null,
                Serial = null
            };

            // Assert
            device.PhoneNumber.Should().BeNull();
            device.MAC.Should().BeNull();
            device.IMEI.Should().BeNull();
            device.BiosSerial.Should().BeNull();
            device.Make.Should().BeNull();
            device.Model.Should().BeNull();
            device.Serial.Should().BeNull();
        }

        #endregion
    }
}
