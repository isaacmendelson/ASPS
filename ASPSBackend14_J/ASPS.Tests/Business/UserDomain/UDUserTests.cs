using Xunit;
using Moq;
using FluentAssertions;
using Business.RealtimeAnalysis.UserDomain;
using Business.Views;
using Common.Entities;
using Common.Enums;
using Common.Interfaces;
using Common.Models;
using Common.Models.Alerts;
using System;

namespace ASPS.Tests.Business.UserDomain;

public class UDUserTests
{
    #region Test Data Helpers

    private Key CreateTestKey() => new Key("User", Guid.NewGuid().ToString());

    private UserInfo CreateTestUserInfo()
    {
        var key = CreateTestKey();
        return new UserInfo(
            key,
            "keycloak-test-123",
            "Test",
            "User",
            "123 Test St",
            "TestCity",
            "TestState",
            "12345",
            "US",
            "+1234567890",
            UserRole.Self,
            false,
            DateTime.UtcNow,
            null,
            "en-US",
            0
        );
    }

    private RiskAssessment CreateTestRiskAssessment() => new RiskAssessment(
        50,       // score
        "Test",   // reason
        false,    // isHighRisk
        0.8f      // confidence (float)
    );

    private DeviceAlertView CreateTestAlert(string deviceUid = "device-001")
    {
        return new DeviceAlertView
        {
            AlertType = "Test Alert"
        };
    }

    private UserDeviceView CreateTestUserDevice(string deviceUid = "device-001")
    {
        var userKey = new Key("User", "test-user-123");
        var deviceEntity = new SmartPhone 
        { 
            DeviceUid = deviceUid,
            Make = "Apple",
            Model = "Test Device",
            UserKey = userKey
        };
        return new UserDeviceView(deviceEntity);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParams_CreatesInstance()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();

        // Act
        var sut = new UDUser(key, userInfo, riskAssessment, null, null, null, null);

        // Assert
        sut.Should().NotBeNull();
        sut.Key.Should().Be(key);
        sut.UserInfo.Should().Be(userInfo);
        sut.RiskAssessment.Should().Be(riskAssessment);
    }

    [Fact]
    public void Constructor_WithNullDevices_InitializesEmptyList()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();

        // Act
        var sut = new UDUser(key, userInfo, riskAssessment, null, null, null, null);

        // Assert
        sut.UserDevices.Should().NotBeNull();
        sut.UserDevices.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNullAlerts_InitializesEmptyList()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();

        // Act
        var sut = new UDUser(key, userInfo, riskAssessment, null, null, null, null);

        // Assert
        sut.ActiveAlerts.Should().NotBeNull();
        sut.ActiveAlerts.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNullBrowserTabs_InitializesEmptyDictionary()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();

        // Act
        var sut = new UDUser(key, userInfo, riskAssessment, null, null, null, null);

        // Assert
        sut.BrowserTabs.Should().NotBeNull();
        sut.BrowserTabs.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNullIsTargeted_DefaultsToFalse()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();

        // Act
        var sut = new UDUser(key, userInfo, riskAssessment, null, null, null, null);

        // Assert
        sut.IsTargeted.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WithIsTargeted_SetsCorrectValue(bool isTargeted)
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();

        // Act
        var sut = new UDUser(key, userInfo, riskAssessment, null, null, null, isTargeted);

        // Assert
        sut.IsTargeted.Should().Be(isTargeted);
    }

    [Fact]
    public void Constructor_WithDevices_StoresDevices()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();
        var devices = new List<UserDeviceView>
        {
            CreateTestUserDevice("device-001"),
            CreateTestUserDevice("device-002")
        };

        // Act
        var sut = new UDUser(key, userInfo, riskAssessment, devices, null, null, null);

        // Assert
        sut.UserDevices.Should().HaveCount(2);
        sut.UserDevices.Should().Contain(d => d.DeviceUid == "device-001");
        sut.UserDevices.Should().Contain(d => d.DeviceUid == "device-002");
    }

    [Fact]
    public void Constructor_WithAlerts_StoresAlerts()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();
        var alerts = new List<DeviceAlertView>
        {
            CreateTestAlert("device-001"),
            CreateTestAlert("device-002")
        };

        // Act
        var sut = new UDUser(key, userInfo, riskAssessment, null, alerts, null, null);

        // Assert
        sut.ActiveAlerts.Should().HaveCount(2);
    }

    #endregion

    #region IsCrossPlatformLocked Tests (ASPS-365)

    [Fact]
    public void Constructor_WithNullIsCrossPlatformLocked_DefaultsToFalse()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();

        // Act
        var sut = new UDUser(key, userInfo, riskAssessment, null, null, null, null);

        // Assert
        sut.IsCrossPlatformLocked.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WithIsCrossPlatformLocked_SetsCorrectValue(bool isLocked)
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();

        // Act
        var sut = new UDUser(key, userInfo, riskAssessment, null, null, null, null, isLocked);

        // Assert
        sut.IsCrossPlatformLocked.Should().Be(isLocked);
    }

    [Fact]
    public void Constructor_WithNullDevices_InitializesEmptyDeviceList()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();

        // Act
        var sut = new UDUser(key, userInfo, riskAssessment, null, null, null, null);

        // Assert
        sut.UserDevices.Should().NotBeNull();
        sut.UserDevices.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithDevicesList_StoresDeviceEntities()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();
        var userKey = new Key("User", "test-user-123");
        
        
        var smartphone = new SmartPhone { DeviceUid = "phone-001", Make = "Apple", Model = "iPhone 14", UserKey = userKey };
        var pc = new PersonalComputer { DeviceUid = "laptop-001", Make = "Dell", Model = "XPS 15", UserKey = userKey };
        var devices = new List<UserDeviceView>
        {
            new SmartPhoneView (smartphone),
            new PersonalComputerView(pc)
        };

        // Act
        var sut = new UDUser(key, userInfo, riskAssessment, devices, null, null, null, null);

        // Assert
        sut.UserDevices.Should().HaveCount(2);
        sut.UserDevices.Should().Contain(d => d.DeviceUid == "phone-001");
        sut.UserDevices.Should().Contain(d => d.DeviceUid == "laptop-001");
    }

    [Fact]
    public void Constructor_WithNullRiskProfile_CreatesDefaultRiskProfile()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();

        // Act
        var sut = new UDUser(key, userInfo, riskAssessment, null, null, null, null);

        // Assert
        sut.RiskProfile.Should().NotBeNull();
        sut.RiskProfile.VulnerabilityScore.Should().Be(0.0);
        sut.RiskProfile.ExposureScore.Should().Be(0.0);
        sut.RiskProfile.RiskyUrlWeight.Should().Be(1.0);
    }

    [Fact]
    public void Constructor_WithCustomRiskProfile_StoresRiskProfile()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();
        var customRiskProfile = new UserRiskProfile(
            vulnerabilityScore: 45,
            exposureScore: 67,
            riskyUrlWeight: 1.2,
            suspiciousCallWeight: 1.8,
            remoteAccessWeight: 2.5,
            scamInProgressWeight: 3.5,
            aggregationPeriodDays: 60,
            timeDecayFactor: 0.9
        );

        // Act
        var sut = new UDUser(key, userInfo, riskAssessment, null, null, null, null, false, customRiskProfile);

        // Assert
        sut.RiskProfile.Should().Be(customRiskProfile);
        sut.RiskProfile.VulnerabilityScore.Should().Be(45);
        sut.RiskProfile.ExposureScore.Should().Be(67);
    }

    #endregion

    #region SetCrossPlatformLock Tests (ASPS-365)

    [Fact]
    public void SetCrossPlatformLock_WithTrue_SetsLockToTrue()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();
        var sut = new UDUser(key, userInfo, riskAssessment, null, null, null, null, false);

        // Act
        sut.SetCrossPlatformLock(true);

        // Assert
        sut.IsCrossPlatformLocked.Should().BeTrue();
    }

    [Fact]
    public void SetCrossPlatformLock_WithFalse_SetsLockToFalse()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();
        var sut = new UDUser(key, userInfo, riskAssessment, null, null, null, null, true);

        // Act
        sut.SetCrossPlatformLock(false);

        // Assert
        sut.IsCrossPlatformLocked.Should().BeFalse();
    }

    [Fact]
    public void SetCrossPlatformLock_CalledMultipleTimes_UpdatesValue()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();
        var sut = new UDUser(key, userInfo, riskAssessment, null, null, null, null, false);

        // Act & Assert
        sut.SetCrossPlatformLock(true);
        sut.IsCrossPlatformLocked.Should().BeTrue();

        sut.SetCrossPlatformLock(false);
        sut.IsCrossPlatformLocked.Should().BeFalse();

        sut.SetCrossPlatformLock(true);
        sut.IsCrossPlatformLocked.Should().BeTrue();
    }

    [Fact]
    public void SetCrossPlatformLock_DoesNotAffectOtherProperties()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();
        var sut = new UDUser(key, userInfo, riskAssessment, null, null, null, true, false);

        // Act
        sut.SetCrossPlatformLock(true);

        // Assert
        sut.IsCrossPlatformLocked.Should().BeTrue();
        sut.IsTargeted.Should().BeTrue(); // Should remain unchanged
        sut.Key.Should().Be(key);
        sut.UserInfo.Should().Be(userInfo);
    }

    #endregion

    #region SetUserIsTargeted Tests

    [Fact]
    public void SetUserIsTargeted_WithTrue_SetsIsTargetedToTrue()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();
        var sut = new UDUser(key, userInfo, riskAssessment, null, null, null, false);

        // Act
        sut.SetUserIsTargeted(true);

        // Assert
        sut.IsTargeted.Should().BeTrue();
    }

    [Fact]
    public void SetUserIsTargeted_WithFalse_SetsIsTargetedToFalse()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();
        var sut = new UDUser(key, userInfo, riskAssessment, null, null, null, true);

        // Act
        sut.SetUserIsTargeted(false);

        // Assert
        sut.IsTargeted.Should().BeFalse();
    }

    [Fact]
    public void SetUserIsTargeted_CalledMultipleTimes_UpdatesValue()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();
        var sut = new UDUser(key, userInfo, riskAssessment, null, null, null, false);

        // Act & Assert
        sut.SetUserIsTargeted(true);
        sut.IsTargeted.Should().BeTrue();

        sut.SetUserIsTargeted(false);
        sut.IsTargeted.Should().BeFalse();

        sut.SetUserIsTargeted(true);
        sut.IsTargeted.Should().BeTrue();
    }

    #endregion

    #region AddAlert Tests

    [Fact]
    public void AddAlert_WithValidAlert_AddsToList()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();
        var sut = new UDUser(key, userInfo, riskAssessment, null, null, null, null);
        var alert = CreateTestAlert();

        // Act
        sut.AddAlert(alert);

        // Assert
        sut.ActiveAlerts.Should().HaveCount(1);
        sut.ActiveAlerts.Should().Contain(alert);
    }

    [Fact]
    public void AddAlert_WithMultipleAlerts_AddsAllToList()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();
        var sut = new UDUser(key, userInfo, riskAssessment, null, null, null, null);
        var alert1 = CreateTestAlert("device-001");
        var alert2 = CreateTestAlert("device-002");
        var alert3 = CreateTestAlert("device-003");

        // Act
        sut.AddAlert(alert1);
        sut.AddAlert(alert2);
        sut.AddAlert(alert3);

        // Assert
        sut.ActiveAlerts.Should().HaveCount(3);
        sut.ActiveAlerts.Should().Contain(alert1);
        sut.ActiveAlerts.Should().Contain(alert2);
        sut.ActiveAlerts.Should().Contain(alert3);
    }

    [Fact]
    public void AddAlert_ToExistingAlerts_PreservesOldAlerts()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();
        var existingAlert = CreateTestAlert("device-001");
        var alerts = new List<DeviceAlertView> { existingAlert };
        var sut = new UDUser(key, userInfo, riskAssessment, null, alerts, null, null);
        var newAlert = CreateTestAlert("device-002");

        // Act
        sut.AddAlert(newAlert);

        // Assert
        sut.ActiveAlerts.Should().HaveCount(2);
        sut.ActiveAlerts.Should().Contain(existingAlert);
        sut.ActiveAlerts.Should().Contain(newAlert);
    }

    #endregion

    #region ClearAlerts Tests

    [Fact]
    public void ClearAlerts_WithExistingAlerts_RemovesAll()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();
        var alerts = new List<DeviceAlertView>
        {
            CreateTestAlert("device-001"),
            CreateTestAlert("device-002"),
            CreateTestAlert("device-003")
        };
        var sut = new UDUser(key, userInfo, riskAssessment, null, alerts, null, null);

        // Act
        sut.ClearAlerts();

        // Assert
        sut.ActiveAlerts.Should().BeEmpty();
    }

    [Fact]
    public void ClearAlerts_WithNoAlerts_DoesNotThrow()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();
        var sut = new UDUser(key, userInfo, riskAssessment, null, null, null, null);

        // Act
        Action act = () => sut.ClearAlerts();

        // Assert
        act.Should().NotThrow();
        sut.ActiveAlerts.Should().BeEmpty();
    }

    [Fact]
    public void ClearAlerts_CalledMultipleTimes_RemovesAllAlerts()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();
        var alerts = new List<DeviceAlertView> { CreateTestAlert() };
        var sut = new UDUser(key, userInfo, riskAssessment, null, alerts, null, null);

        // Act
        sut.ClearAlerts();
        sut.ClearAlerts();

        // Assert
        sut.ActiveAlerts.Should().BeEmpty();
    }

    #endregion

    #region AddRemoteAccessAnalysisResult Tests

    [Fact]
    public void AddRemoteAccessAnalysisResult_WithValidData_AddsResult()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();
        var device = CreateTestUserDevice("device-001");
        var devices = new List<UserDeviceView> { device };
        var sut = new UDUser(key, userInfo, riskAssessment, devices, null, null, null);

        var result = new RemoteAccessAnalysisResult(
            remoteAccessApp: RemoteAccessApp.TeamViewer,
            runningProcesses: 1,
            connectionUrl: "https://test.example.com",
            connectionStatus: ConnectionStatus.Open,
            RemoteAccessDirection.Unknown,
            connectionsCount: 1,
            sessionStatus: 1,
            browserTabs: null,
            risk_assessment: riskAssessment
        )
        {
            Success = true,
            analyzed_at = DateTime.UtcNow
        };

        // Act
        sut.AddRemoteAccessAnalysisResult("device-001", result);

        // Assert
        sut.RemoteAccessAnalysisResults.Should().ContainKey("device-001");
        sut.RemoteAccessAnalysisResults["device-001"].Should().Contain(result);
    }

    #endregion

    #region RemoteAccessStatus Tests

    [Fact]
    public void RemoteAccessStatus_WithSuccessfulResults_ReturnsLatestPerDevice()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();
        var device = CreateTestUserDevice("device-001");
        var devices = new List<UserDeviceView> { device };
        var sut = new UDUser(key, userInfo, riskAssessment, devices, null, null, null);

        var oldResult = new RemoteAccessAnalysisResult(
            remoteAccessApp: RemoteAccessApp.TeamViewer,
            runningProcesses: 1,
            connectionUrl: "https://old.example.com",
            connectionStatus: ConnectionStatus.Open,
            RemoteAccessDirection.Unknown,
            connectionsCount: 1,
            sessionStatus: 1,
            browserTabs: null,
            risk_assessment: riskAssessment
        )
        {
            Success = true,
            analyzed_at = DateTime.UtcNow.AddHours(-2)
        };

        var newResult = new RemoteAccessAnalysisResult(
            remoteAccessApp: RemoteAccessApp.TeamViewer,
            runningProcesses: 1,
            connectionUrl: "https://new.example.com",
            connectionStatus: ConnectionStatus.Open,
            remoteAccessDirection: RemoteAccessDirection.Unknown,
            connectionsCount: 1,
            sessionStatus: 1,
            browserTabs: null,
            risk_assessment: riskAssessment
        )
        {
            Success = true,
            analyzed_at = DateTime.UtcNow
        };

        sut.AddRemoteAccessAnalysisResult("device-001", oldResult);
        sut.AddRemoteAccessAnalysisResult("device-001", newResult);

        // Act
        var status = sut.RemoteAccessStatus;

        // Assert
        status.Should().ContainKey("device-001");
        status["device-001"].Should().Be(newResult);
    }

    [Fact]
    public void RemoteAccessStatus_WithNoSuccessfulResults_ReturnsEmpty()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();
        var device = CreateTestUserDevice("device-001");
        var devices = new List<UserDeviceView> { device };
        var sut = new UDUser(key, userInfo, riskAssessment, devices, null, null, null);

        var failedResult = new RemoteAccessAnalysisResult(
            remoteAccessApp: RemoteAccessApp.TeamViewer,
            runningProcesses: 1,
            connectionUrl: "https://test.example.com",
            connectionStatus: ConnectionStatus.Closed,
            RemoteAccessDirection.Unknown,
            connectionsCount: 0,
            sessionStatus: 0,
            browserTabs: null,
            risk_assessment: null
        )
        {
            Success = false,
            analyzed_at = DateTime.UtcNow
        };

        sut.AddRemoteAccessAnalysisResult("device-001", failedResult);

        // Act
        var status = sut.RemoteAccessStatus;

        // Assert
        status.Should().NotContainKey("device-001");
    }

    #endregion

    #region UserDevices Property Tests

    [Fact]
    public void UserDevices_WhenSet_UpdatesProperty()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();
        var sut = new UDUser(key, userInfo, riskAssessment, null, null, null, null);
        var devices = new List<UserDeviceView>
        {
            CreateTestUserDevice("device-001"),
            CreateTestUserDevice("device-002")
        };

        // Act
        sut.UserDevices = devices;

        // Assert
        sut.UserDevices.Should().HaveCount(2);
        sut.UserDevices.Should().Contain(d => d.DeviceUid == "device-001");
    }

    [Fact]
    public void UserDevices_WhenNull_ReturnsEmptyList()
    {
        // Arrange
        var key = CreateTestKey();
        var userInfo = CreateTestUserInfo();
        var riskAssessment = CreateTestRiskAssessment();
        var devices = new List<UserDeviceView> { CreateTestUserDevice() };
        var sut = new UDUser(key, userInfo, riskAssessment, devices, null, null, null);

        // Act
        sut.UserDevices = null;

        // Assert
        sut.UserDevices.Should().NotBeNull();
        sut.UserDevices.Should().BeEmpty();
    }

    #endregion
}
