using Xunit;
using FluentAssertions;
using Common.Models;
using Common.Enums;

namespace ASPS.Tests.Common
{
    public class DeviceInfoTests
    {
        #region Constructor Tests

        [Fact]
        public void DefaultConstructor_CreatesInstance()
        {
            // Act
            var result = new DeviceInfo();

            // Assert
            result.Should().NotBeNull();
            result.Key.Should().NotBeNull();
            result.DeviceUid.Should().Be(string.Empty);
        }

        [Fact]
        public void Constructor_WithAllParams_CreatesInstance()
        {
            // Arrange
            var key = new Key();
            var deviceUid = "device-123";
            var version = "1.0.0";
            var ip = "192.168.1.1";
            var userAgent = "Mozilla/5.0";
            var timezone = 3;
            var os = OperatingSystemType.Windows;
            var mac = "00:11:22:33:44:55";
            var userKey = new Key();

            // Act
            var result = new DeviceInfo(key, deviceUid, version, ip, userAgent, timezone, os, mac, userKey);

            // Assert
            result.Should().NotBeNull();
            result.Key.Should().Be(key);
            result.DeviceUid.Should().Be(deviceUid);
            result.AggregateVersion.Should().Be(version);
            result.IP.Should().Be(ip);
            result.UserAgent.Should().Be(userAgent);
            result.Timezone.Should().Be(timezone);
            result.OperatingSystem.Should().Be(os);
            result.MACAddress.Should().Be(mac);
            result.UserKey.Should().Be(userKey);
        }

        [Fact]
        public void Constructor_WithoutUserKey_AcceptsNull()
        {
            // Arrange
            var key = new Key();

            // Act
            var result = new DeviceInfo(key, "device-123", "1.0.0", "192.168.1.1", "Mozilla", 3, 
                OperatingSystemType.Windows, "00:11:22:33:44:55");

            // Assert
            result.UserKey.Should().BeNull();
        }

        #endregion

        #region DeviceType Tests

        [Theory]
        [InlineData(OperatingSystemType.Windows, DeviceType.PersonalComputer)]
        [InlineData(OperatingSystemType.MacOS, DeviceType.PersonalComputer)]
        [InlineData(OperatingSystemType.Linux, DeviceType.PersonalComputer)]
        [InlineData(OperatingSystemType.Android, DeviceType.MobilePhone)]
        [InlineData(OperatingSystemType.IOS, DeviceType.MobilePhone)]
        public void DeviceType_WithKnownOS_ReturnsCorrectType(OperatingSystemType os, DeviceType expectedType)
        {
            // Arrange
            var deviceInfo = new DeviceInfo
            {
                OperatingSystem = os
            };

            // Act
            var result = deviceInfo.DeviceType;

            // Assert
            result.Should().Be(expectedType);
        }

        [Fact]
        public void DeviceType_WithUnknownOS_ReturnsUnknown()
        {
            // Arrange
            var deviceInfo = new DeviceInfo
            {
                OperatingSystem = (OperatingSystemType)999
            };

            // Act
            var result = deviceInfo.DeviceType;

            // Assert
            result.Should().Be(DeviceType.Unknown);
        }

        #endregion

        #region Property Tests

        [Fact]
        public void Properties_CanBeSetAndRead()
        {
            // Arrange
            var deviceInfo = new DeviceInfo();
            var key = new Key();
            var userKey = new Key();

            // Act
            deviceInfo.Key = key;
            deviceInfo.DeviceUid = "test-uid";
            deviceInfo.AggregateVersion = "2.0.0";
            deviceInfo.IP = "10.0.0.1";
            deviceInfo.UserAgent = "Chrome/90.0";
            deviceInfo.Timezone = -5;
            deviceInfo.OperatingSystem = OperatingSystemType.MacOS;
            deviceInfo.MACAddress = "AA:BB:CC:DD:EE:FF";
            deviceInfo.UserKey = userKey;

            // Assert
            deviceInfo.Key.Should().Be(key);
            deviceInfo.DeviceUid.Should().Be("test-uid");
            deviceInfo.AggregateVersion.Should().Be("2.0.0");
            deviceInfo.IP.Should().Be("10.0.0.1");
            deviceInfo.UserAgent.Should().Be("Chrome/90.0");
            deviceInfo.Timezone.Should().Be(-5);
            deviceInfo.OperatingSystem.Should().Be(OperatingSystemType.MacOS);
            deviceInfo.MACAddress.Should().Be("AA:BB:CC:DD:EE:FF");
            deviceInfo.UserKey.Should().Be(userKey);
        }

        #endregion
    }
}
