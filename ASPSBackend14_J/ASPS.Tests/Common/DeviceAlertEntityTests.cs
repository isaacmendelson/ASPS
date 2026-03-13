using Xunit;
using FluentAssertions;
using Common.Entities;
using Common.Enums;
using Common.Models;
using Common.Models.Alerts;
using System;

namespace ASPS.Tests.Common
{
    /// <summary>
    /// Unit tests for DeviceAlertEntity abstract class using RemoteAccessAlertEntity as concrete implementation
    /// ASPS-190: Unit Tests for UDDeviceAlert (DeviceAlertEntity)
    /// </summary>
    public class DeviceAlertEntityTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_CreatesRemoteAccessAlertEntity()
        {
            // Act
            var alert = new RemoteAccessAlertEntity();

            // Assert
            alert.Should().NotBeNull();
            alert.Should().BeAssignableTo<DeviceAlertEntity>();
            alert.TypeName.Should().Be("RemoteAccessAlert");
        }

        [Fact]
        public void Constructor_InitializesWithDefaultValues()
        {
            // Act
            var alert = new RemoteAccessAlertEntity();

            // Assert
            alert.Token.Should().Be(string.Empty);
            alert.DeviceUid.Should().Be(string.Empty);
            alert.MAC.Should().Be(string.Empty);
            alert.Status.Should().Be(AlertFlagStatus.Unknown);
            alert.ConnectionUrl.Should().Be(string.Empty);
        }

        [Fact]
        public void Constructor_InitializesTimestampToNow()
        {
            // Arrange
            var before = DateTime.UtcNow.AddSeconds(-1);

            // Act
            var alert = new RemoteAccessAlertEntity();

            // Assert
            var after = DateTime.UtcNow.AddSeconds(1);
            alert.Timestamp.Should().BeAfter(before);
            alert.Timestamp.Should().BeBefore(after);
        }

        #endregion

        #region Property Tests

        [Fact]
        public void AlertType_CanBeSetAndRetrieved()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();

            // Act
            alert.AlertType = "RemoteAccess";

            // Assert
            alert.AlertType.Should().Be("RemoteAccess");
        }

        [Fact]
        public void Priority_CanBeSetAndRetrieved()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();

            // Act
            alert.Priority = Priority.High;

            // Assert
            alert.Priority.Should().Be(Priority.High);
        }

        [Fact]
        public void DeviceUid_CanBeSetAndRetrieved()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();

            // Act
            alert.DeviceUid = "DEVICE-123";

            // Assert
            alert.DeviceUid.Should().Be("DEVICE-123");
        }

        [Fact]
        public void DeviceType_CanBeSetAndRetrieved()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();

            // Act
            alert.DeviceType = DeviceType.PersonalComputer;

            // Assert
            alert.DeviceType.Should().Be(DeviceType.PersonalComputer);
        }

        [Fact]
        public void OperatingSystem_CanBeSetAndRetrieved()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();

            // Act
            alert.OperatingSystem = OperatingSystemType.Windows;

            // Assert
            alert.OperatingSystem.Should().Be(OperatingSystemType.Windows);
        }

        [Fact]
        public void MAC_CanBeSetAndRetrieved()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();

            // Act
            alert.MAC = "00:1A:2B:3C:4D:5E";

            // Assert
            alert.MAC.Should().Be("00:1A:2B:3C:4D:5E");
        }

        [Fact]
        public void IPAddress_CanBeSetAndRetrieved()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();

            // Act
            alert.IPAddress = "192.168.1.100";

            // Assert
            alert.IPAddress.Should().Be("192.168.1.100");
        }

        [Fact]
        public void Status_CanBeSetAndRetrieved()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();

            // Act
            alert.Status = AlertFlagStatus.Open;

            // Assert
            alert.Status.Should().Be(AlertFlagStatus.Open);
        }

        #endregion

        #region UserKey and DeviceKey Tests

        [Fact]
        public void UserKey_WhenUserKeyFieldIsNull_ReturnsNull()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();
            alert.UserKeyField = null;

            // Act
            var userKey = alert.UserKey;

            // Assert
            userKey.Should().BeNull();
        }

        [Fact]
        public void UserKey_WhenUserKeyFieldIsSet_ReturnsKey()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();
            alert.UserKeyField = "user123";

            // Act
            var userKey = alert.UserKey;

            // Assert
            userKey.Should().NotBeNull();
            userKey!.Type.Should().Be("User");
            userKey.Value.Should().Be("user123");
        }

        [Fact]
        public void UserKey_SetterUpdatesUserKeyField()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();
            var key = new Key("User", "user456");

            // Act
            alert.UserKey = key;

            // Assert
            alert.UserKeyField.Should().Be("user456");
        }

        [Fact]
        public void DeviceKey_WhenDeviceKeyFieldIsNull_ReturnsNull()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();
            alert.DeviceKeyField = null;

            // Act
            var deviceKey = alert.DeviceKey;

            // Assert
            deviceKey.Should().BeNull();
        }

        [Fact]
        public void DeviceKey_WhenDeviceKeyFieldIsSet_ReturnsKey()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();
            alert.DeviceKeyField = "device789";

            // Act
            var deviceKey = alert.DeviceKey;

            // Assert
            deviceKey.Should().NotBeNull();
            deviceKey!.Type.Should().Be("UserDevice");
            deviceKey.Value.Should().Be("device789");
        }

        [Fact]
        public void DeviceKey_SetterUpdatesDeviceKeyField()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();
            var key = new Key("UserDevice", "device999");

            // Act
            alert.DeviceKey = key;

            // Assert
            alert.DeviceKeyField.Should().Be("device999");
        }

        #endregion

        #region SetStatus Method Tests

        [Fact]
        public void SetStatus_UpdatesStatus()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();
            alert.Status = AlertFlagStatus.Unknown;

            // Act
            alert.SetStatus(AlertFlagStatus.Open);

            // Assert
            alert.Status.Should().Be(AlertFlagStatus.Open);
        }

        [Fact]
        public void SetStatus_CanChangeFromFlaggedToCleared()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();
            alert.Status = AlertFlagStatus.Open;

            // Act
            alert.SetStatus(AlertFlagStatus.Closed);

            // Assert
            alert.Status.Should().Be(AlertFlagStatus.Closed);
        }

        #endregion

        #region Tag Tests

        [Fact]
        public void Tag_ContainsAlertType()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();
            alert.AlertType = "RemoteAccess";
            alert.DeviceUid = "DEVICE-001";
            alert.Timestamp = new DateTime(2024, 3, 12, 14, 30, 0);
            alert.KeyField = "alert-key-1";

            // Act
            var tag = alert.Tag;

            // Assert
            tag.Should().NotBeNull();
            tag.Name.Should().Contain("RemoteAccess");
            tag.Name.Should().Contain("DEVICE-001");
            tag.Type.Should().Be("RemoteAccessAlert");
        }

        [Fact]
        public void Tag_ContainsTimestamp()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();
            alert.AlertType = "RemoteAccess";
            alert.DeviceUid = "DEVICE-002";
            alert.Timestamp = new DateTime(2024, 3, 12, 14, 30, 0);
            alert.KeyField = "alert-key-2";

            // Act
            var tag = alert.Tag;

            // Assert
            tag.Name.Should().Contain("2024-03-12");
        }

        [Fact]
        public void Tag_IsCached_ReturnsSameInstance()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();
            alert.AlertType = "RemoteAccess";
            alert.DeviceUid = "DEVICE-003";
            alert.KeyField = "alert-key-3";

            // Act
            var tag1 = alert.Tag;
            var tag2 = alert.Tag;

            // Assert
            tag1.Should().BeSameAs(tag2);
        }

        #endregion

        #region RemoteAccessAlertEntity Specific Tests

        [Fact]
        public void RemoteAccessAlertEntity_RemoteAccessApp_CanBeSet()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();

            // Act
            alert.RemoteAccessApp = RemoteAccessApp.TeamViewer;

            // Assert
            alert.RemoteAccessApp.Should().Be(RemoteAccessApp.TeamViewer);
        }

        [Fact]
        public void RemoteAccessAlertEntity_ConnectionUrl_CanBeSet()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();

            // Act
            alert.ConnectionUrl = "https://remote.example.com/session/12345";

            // Assert
            alert.ConnectionUrl.Should().Be("https://remote.example.com/session/12345");
        }

        [Fact]
        public void RemoteAccessAlertEntity_ConnectionStatus_CanBeSet()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();

            // Act
            alert.ConnectionStatus = ConnectionStatus.Open;

            // Assert
            alert.ConnectionStatus.Should().Be(ConnectionStatus.Open);
        }

        [Fact]
        public void RemoteAccessAlertEntity_RunningProcesses_CanBeSet()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();

            // Act
            alert.RunningProcesses = 5;

            // Assert
            alert.RunningProcesses.Should().Be(5);
        }

        [Fact]
        public void RemoteAccessAlertEntity_ConnectionsCount_CanBeSet()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();

            // Act
            alert.ConnectionsCount = 3;

            // Assert
            alert.ConnectionsCount.Should().Be(3);
        }

        [Fact]
        public void RemoteAccessAlertEntity_DeepDetectionFields_CanBeSet()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();

            // Act
            alert.RemoteOS = "Windows 10";
            alert.RemoteVersion = "21H2";
            alert.ConnectionType = "RDP";
            alert.FileTransferActive = true;
            alert.FileTransfers = 2;

            // Assert
            alert.RemoteOS.Should().Be("Windows 10");
            alert.RemoteVersion.Should().Be("21H2");
            alert.ConnectionType.Should().Be("RDP");
            alert.FileTransferActive.Should().BeTrue();
            alert.FileTransfers.Should().Be(2);
        }

        #endregion

        #region Real-World Scenario Tests

        [Fact]
        public void RemoteAccessAlert_FullScenario_TeamViewer()
        {
            // Arrange & Act
            var alert = new RemoteAccessAlertEntity
            {
                AlertType = "RemoteAccess",
                Priority = Priority.Critical,
                DeviceUid = "PC-WIN-001",
                DeviceType = DeviceType.PersonalComputer,
                OperatingSystem = OperatingSystemType.Windows,
                MAC = "AA:BB:CC:DD:EE:FF",
                IPAddress = "10.0.0.50",
                UserKeyField = "user-123",
                DeviceKeyField = "device-456",
                Status = AlertFlagStatus.Open,
                RemoteAccessApp = RemoteAccessApp.TeamViewer,
                ConnectionUrl = "https://teamviewer.com/session/xyz",
                ConnectionStatus = ConnectionStatus.Open,
                RunningProcesses = 3,
                ConnectionsCount = 1,
                FileTransferActive = true,
                FileTransfers = 2
            };

            // Assert
            alert.AlertType.Should().Be("RemoteAccess");
            alert.Priority.Should().Be(Priority.Critical);
            alert.DeviceUid.Should().Be("PC-WIN-001");
            alert.RemoteAccessApp.Should().Be(RemoteAccessApp.TeamViewer);
            alert.Status.Should().Be(AlertFlagStatus.Open);
            alert.FileTransferActive.Should().BeTrue();
        }

        [Fact]
        public void RemoteAccessAlert_AnyDesk_WithMultipleConnections()
        {
            // Arrange & Act
            var alert = new RemoteAccessAlertEntity
            {
                RemoteAccessApp = RemoteAccessApp.AnyDesk,
                ConnectionsCount = 5,
                RunningProcesses = 10,
                ConnectionStatus = ConnectionStatus.Open
            };

            // Assert
            alert.RemoteAccessApp.Should().Be(RemoteAccessApp.AnyDesk);
            alert.ConnectionsCount.Should().Be(5);
            alert.RunningProcesses.Should().BeGreaterThan(0);
        }

        [Fact]
        public void DeviceAlertEntity_WithUserAndDevice_HasKeys()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity
            {
                UserKeyField = "user-789",
                DeviceKeyField = "device-012"
            };

            // Act
            var userKey = alert.UserKey;
            var deviceKey = alert.DeviceKey;

            // Assert
            userKey.Should().NotBeNull();
            userKey!.Value.Should().Be("user-789");
            deviceKey.Should().NotBeNull();
            deviceKey!.Value.Should().Be("device-012");
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public void AlertId_ReturnsKeyField()
        {
            // Arrange
            var alert = new RemoteAccessAlertEntity();
            alert.KeyField = "alert-999";

            // Act
            var alertId = alert.AlertId;

            // Assert
            alertId.Should().Be("alert-999");
        }

        [Fact]
        public void UserKeyField_CanBeNull()
        {
            // Arrange & Act
            var alert = new RemoteAccessAlertEntity
            {
                UserKeyField = null
            };

            // Assert
            alert.UserKeyField.Should().BeNull();
            alert.UserKey.Should().BeNull();
        }

        [Fact]
        public void DeviceKeyField_CanBeNull()
        {
            // Arrange & Act
            var alert = new RemoteAccessAlertEntity
            {
                DeviceKeyField = null
            };

            // Assert
            alert.DeviceKeyField.Should().BeNull();
            alert.DeviceKey.Should().BeNull();
        }

        [Fact]
        public void ConnectionsCount_CanBeZero()
        {
            // Arrange & Act
            var alert = new RemoteAccessAlertEntity
            {
                ConnectionsCount = 0
            };

            // Assert
            alert.ConnectionsCount.Should().Be(0);
        }

        [Fact]
        public void FileTransferActive_DefaultsToFalse()
        {
            // Arrange & Act
            var alert = new RemoteAccessAlertEntity();

            // Assert
            alert.FileTransferActive.Should().BeFalse();
        }

        #endregion
    }
}
