using Xunit;
using FluentAssertions;
using Business.RealtimeAnalysis.UserDomain;

namespace ASPS.Tests.Business.UserDomain;

/// <summary>
/// Unit tests for OtpInterceptionDetector (ASPS-368)
/// </summary>
public class OtpInterceptionDetectorTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithDefaults_InitializesCorrectly()
    {
        // Act
        var sut = new OtpInterceptionDetector();

        // Assert
        sut.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithCustomValues_InitializesCorrectly()
    {
        // Act
        var sut = new OtpInterceptionDetector(
            maxCorrelationWindowSeconds: 120,
            minConfidenceThreshold: 0.8
        );

        // Assert
        sut.Should().NotBeNull();
    }

    #endregion

    #region DetectInterception Tests - No Detection Cases

    [Fact]
    public void DetectInterception_WithTimeBeyondWindow_ReturnsNull()
    {
        // Arrange
        var sut = new OtpInterceptionDetector(maxCorrelationWindowSeconds: 60);
        var smsTime = DateTime.UtcNow.AddSeconds(-70);
        var browserTime = DateTime.UtcNow;

        // Act
        var result = sut.DetectInterception(
            userKeyField: "user-123",
            mobileDeviceUid: "mobile-001",
            browserDeviceUid: "browser-001",
            otpCode: "123456",
            smsReceivedTimestamp: smsTime,
            browserInputTimestamp: browserTime,
            remoteAccessApp: "TeamViewer",
            targetUrl: "https://bank.com/login",
            isRemoteAccessActive: true
        );

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void DetectInterception_WithLowConfidence_ReturnsNull()
    {
        // Arrange
        var sut = new OtpInterceptionDetector(
            maxCorrelationWindowSeconds: 60,
            minConfidenceThreshold: 0.9
        );
        var smsTime = DateTime.UtcNow.AddSeconds(-50);
        var browserTime = DateTime.UtcNow;

        // Act - Time diff = 50s, no remote access, no OTP code
        var result = sut.DetectInterception(
            userKeyField: "user-123",
            mobileDeviceUid: "mobile-001",
            browserDeviceUid: "browser-001",
            otpCode: "",
            smsReceivedTimestamp: smsTime,
            browserInputTimestamp: browserTime,
            remoteAccessApp: "",
            targetUrl: "https://bank.com/login",
            isRemoteAccessActive: false
        );

        // Assert - Only time proximity (0.2) = 0.2 < 0.9 threshold
        result.Should().BeNull();
    }

    #endregion

    #region DetectInterception Tests - Detection Cases

    [Fact]
    public void DetectInterception_WithHighConfidence_ReturnsEvent()
    {
        // Arrange
        var sut = new OtpInterceptionDetector();
        var smsTime = DateTime.UtcNow.AddSeconds(-5);
        var browserTime = DateTime.UtcNow;

        // Act - Time diff = 5s, remote access active, has OTP
        // Confidence = 0.5 (time) + 0.3 (remote) + 0.2 (OTP) = 1.0
        var result = sut.DetectInterception(
            userKeyField: "user-123",
            mobileDeviceUid: "mobile-001",
            browserDeviceUid: "browser-001",
            otpCode: "123456",
            smsReceivedTimestamp: smsTime,
            browserInputTimestamp: browserTime,
            remoteAccessApp: "TeamViewer",
            targetUrl: "https://bank.com/login",
            isRemoteAccessActive: true
        );

        // Assert
        result.Should().NotBeNull();
        result!.UserKeyField.Should().Be("user-123");
        result.MobileDeviceUid.Should().Be("mobile-001");
        result.BrowserDeviceUid.Should().Be("browser-001");
        result.OtpCode.Should().Be("123456");
        result.RemoteAccessApp.Should().Be("TeamViewer");
        result.TargetUrl.Should().Be("https://bank.com/login");
        result.CorrelationConfidence.Should().BeGreaterThanOrEqualTo(0.7);
    }

    [Fact]
    public void DetectInterception_WithVeryHighConfidence_SetsIsBlocked()
    {
        // Arrange
        var sut = new OtpInterceptionDetector();
        var smsTime = DateTime.UtcNow.AddSeconds(-3);
        var browserTime = DateTime.UtcNow;

        // Act - Very high confidence (>= 0.85) + remote access = should block
        var result = sut.DetectInterception(
            userKeyField: "user-123",
            mobileDeviceUid: "mobile-001",
            browserDeviceUid: "browser-001",
            otpCode: "654321",
            smsReceivedTimestamp: smsTime,
            browserInputTimestamp: browserTime,
            remoteAccessApp: "AnyDesk",
            targetUrl: "https://bank.com/verify",
            isRemoteAccessActive: true
        );

        // Assert
        result.Should().NotBeNull();
        result!.IsBlocked.Should().BeTrue();
        result.CorrelationConfidence.Should().BeGreaterThanOrEqualTo(0.85);
    }

    [Fact]
    public void DetectInterception_WithModerateConfidence_DoesNotBlock()
    {
        // Arrange
        var sut = new OtpInterceptionDetector();
        var smsTime = DateTime.UtcNow.AddSeconds(-25);
        var browserTime = DateTime.UtcNow;

        // Act - Time diff = 25s (0.3) + remote (0.3) = 0.6, no OTP code
        // Confidence = 0.6 < 0.85, should not block
        var result = sut.DetectInterception(
            userKeyField: "user-123",
            mobileDeviceUid: "mobile-001",
            browserDeviceUid: "browser-001",
            otpCode: "",
            smsReceivedTimestamp: smsTime,
            browserInputTimestamp: browserTime,
            remoteAccessApp: "TeamViewer",
            targetUrl: "https://bank.com/login",
            isRemoteAccessActive: true
        );

        // Assert
        result.Should().BeNull(); // Below default 0.7 threshold
    }

    #endregion

    #region Time Proximity Tests

    [Fact]
    public void DetectInterception_Within10Seconds_HighTimeProximity()
    {
        // Arrange
        var sut = new OtpInterceptionDetector();
        var smsTime = DateTime.UtcNow.AddSeconds(-8);
        var browserTime = DateTime.UtcNow;

        // Act - Time diff = 8s, remote access, has OTP
        // Confidence = 0.5 (time <=10s) + 0.3 (remote) + 0.2 (OTP) = 1.0
        var result = sut.DetectInterception(
            userKeyField: "user-123",
            mobileDeviceUid: "mobile-001",
            browserDeviceUid: "browser-001",
            otpCode: "999888",
            smsReceivedTimestamp: smsTime,
            browserInputTimestamp: browserTime,
            remoteAccessApp: "TeamViewer",
            targetUrl: "https://bank.com/login",
            isRemoteAccessActive: true
        );

        // Assert
        result.Should().NotBeNull();
        result!.CorrelationConfidence.Should().Be(1.0);
    }

    [Fact]
    public void DetectInterception_Between10And30Seconds_MediumTimeProximity()
    {
        // Arrange
        var sut = new OtpInterceptionDetector();
        var smsTime = DateTime.UtcNow.AddSeconds(-20);
        var browserTime = DateTime.UtcNow;

        // Act - Time diff = 20s, remote access, has OTP
        // Confidence = 0.3 (10<time<=30) + 0.3 (remote) + 0.2 (OTP) = 0.8
        var result = sut.DetectInterception(
            userKeyField: "user-123",
            mobileDeviceUid: "mobile-001",
            browserDeviceUid: "browser-001",
            otpCode: "777666",
            smsReceivedTimestamp: smsTime,
            browserInputTimestamp: browserTime,
            remoteAccessApp: "TeamViewer",
            targetUrl: "https://bank.com/login",
            isRemoteAccessActive: true
        );

        // Assert
        result.Should().NotBeNull();
        result!.CorrelationConfidence.Should().Be(0.8);
    }

    [Fact]
    public void DetectInterception_Between30And60Seconds_LowTimeProximity()
    {
        // Arrange
        var sut = new OtpInterceptionDetector();
        var smsTime = DateTime.UtcNow.AddSeconds(-45);
        var browserTime = DateTime.UtcNow;

        // Act - Time diff = 45s, remote access, has OTP
        // Confidence = 0.2 (30<time<=60) + 0.3 (remote) + 0.2 (OTP) = 0.7
        var result = sut.DetectInterception(
            userKeyField: "user-123",
            mobileDeviceUid: "mobile-001",
            browserDeviceUid: "browser-001",
            otpCode: "555444",
            smsReceivedTimestamp: smsTime,
            browserInputTimestamp: browserTime,
            remoteAccessApp: "TeamViewer",
            targetUrl: "https://bank.com/login",
            isRemoteAccessActive: true
        );

        // Assert
        result.Should().NotBeNull();
        result!.CorrelationConfidence.Should().Be(0.7);
    }

    #endregion

    #region Remote Access Factor Tests

    [Fact]
    public void DetectInterception_WithoutRemoteAccess_LowerConfidence()
    {
        // Arrange
        var sut = new OtpInterceptionDetector();
        var smsTime = DateTime.UtcNow.AddSeconds(-5);
        var browserTime = DateTime.UtcNow;

        // Act - Time diff = 5s, NO remote access, has OTP
        // Confidence = 0.5 (time) + 0.0 (no remote) + 0.2 (OTP) = 0.7
        var result = sut.DetectInterception(
            userKeyField: "user-123",
            mobileDeviceUid: "mobile-001",
            browserDeviceUid: "browser-001",
            otpCode: "111222",
            smsReceivedTimestamp: smsTime,
            browserInputTimestamp: browserTime,
            remoteAccessApp: "",
            targetUrl: "https://bank.com/login",
            isRemoteAccessActive: false
        );

        // Assert
        result.Should().NotBeNull();
        result!.CorrelationConfidence.Should().Be(0.7);
        result.IsBlocked.Should().BeFalse(); // No block without remote access
    }

    #endregion

    #region OTP Code Factor Tests

    [Fact]
    public void DetectInterception_WithoutOtpCode_LowerConfidence()
    {
        // Arrange
        var sut = new OtpInterceptionDetector();
        var smsTime = DateTime.UtcNow.AddSeconds(-5);
        var browserTime = DateTime.UtcNow;

        // Act - Time diff = 5s, remote access, NO OTP code
        // Confidence = 0.5 (time) + 0.3 (remote) + 0.0 (no OTP) = 0.8
        var result = sut.DetectInterception(
            userKeyField: "user-123",
            mobileDeviceUid: "mobile-001",
            browserDeviceUid: "browser-001",
            otpCode: "",
            smsReceivedTimestamp: smsTime,
            browserInputTimestamp: browserTime,
            remoteAccessApp: "TeamViewer",
            targetUrl: "https://bank.com/login",
            isRemoteAccessActive: true
        );

        // Assert
        result.Should().NotBeNull();
        result!.CorrelationConfidence.Should().Be(0.8);
    }

    #endregion

    #region Timestamp Tests

    [Fact]
    public void DetectInterception_StoresCorrectTimestamps()
    {
        // Arrange
        var sut = new OtpInterceptionDetector();
        var smsTime = new DateTime(2026, 3, 26, 10, 0, 0, DateTimeKind.Utc);
        var browserTime = new DateTime(2026, 3, 26, 10, 0, 5, DateTimeKind.Utc);

        // Act
        var result = sut.DetectInterception(
            userKeyField: "user-123",
            mobileDeviceUid: "mobile-001",
            browserDeviceUid: "browser-001",
            otpCode: "123456",
            smsReceivedTimestamp: smsTime,
            browserInputTimestamp: browserTime,
            remoteAccessApp: "TeamViewer",
            targetUrl: "https://bank.com/login",
            isRemoteAccessActive: true
        );

        // Assert
        result.Should().NotBeNull();
        result!.SmsReceivedTimestamp.Should().Be(smsTime);
        result.BrowserInputTimestamp.Should().Be(browserTime);
    }

    [Fact]
    public void DetectInterception_WithReverseTimestamps_HandlesAbsoluteValue()
    {
        // Arrange - Browser input BEFORE SMS (should still work with absolute value)
        var sut = new OtpInterceptionDetector();
        var smsTime = DateTime.UtcNow;
        var browserTime = DateTime.UtcNow.AddSeconds(-5);

        // Act
        var result = sut.DetectInterception(
            userKeyField: "user-123",
            mobileDeviceUid: "mobile-001",
            browserDeviceUid: "browser-001",
            otpCode: "123456",
            smsReceivedTimestamp: smsTime,
            browserInputTimestamp: browserTime,
            remoteAccessApp: "TeamViewer",
            targetUrl: "https://bank.com/login",
            isRemoteAccessActive: true
        );

        // Assert - Should still detect (time difference is absolute)
        result.Should().NotBeNull();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void DetectInterception_FullScenario_BankingScamAttempt()
    {
        // Arrange
        var sut = new OtpInterceptionDetector();
        var smsTime = new DateTime(2026, 3, 26, 14, 30, 0, DateTimeKind.Utc);
        var browserTime = new DateTime(2026, 3, 26, 14, 30, 3, DateTimeKind.Utc);

        // Act - Realistic banking scam scenario
        var result = sut.DetectInterception(
            userKeyField: "user-elderly-victim",
            mobileDeviceUid: "samsung-galaxy-s21",
            browserDeviceUid: "windows-laptop-001",
            otpCode: "847392",
            smsReceivedTimestamp: smsTime,
            browserInputTimestamp: browserTime,
            remoteAccessApp: "TeamViewer",
            targetUrl: "https://bank-hapoalim.co.il/authenticate",
            isRemoteAccessActive: true
        );

        // Assert
        result.Should().NotBeNull();
        result!.EventType.Should().Be("OtpInterceptionTriggered");
        result.UserKeyField.Should().Be("user-elderly-victim");
        result.OtpCode.Should().Be("847392");
        result.IsBlocked.Should().BeTrue(); // High confidence + remote access
        result.CorrelationConfidence.Should().BeGreaterThanOrEqualTo(0.85);
    }

    #endregion
}
