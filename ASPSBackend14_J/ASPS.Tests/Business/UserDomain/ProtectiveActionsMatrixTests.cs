using Xunit;
using FluentAssertions;
using Business.RealtimeAnalysis.UserDomain;

namespace ASPS.Tests.Business.UserDomain;

/// <summary>
/// Unit tests for ProtectiveActionsMatrix (ASPS-372)
/// </summary>
public class ProtectiveActionsMatrixTests
{
    #region Risk Range 0-20: Passive Monitoring

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(20)]
    public void DetermineActions_RiskScore0To20_ReturnsLogOnly(double riskScore)
    {
        // Arrange
        var sut = new ProtectiveActionsMatrix();

        // Act
        var result = sut.DetermineActions(riskScore);

        // Assert
        result.Should().HaveFlag(ProtectiveActionFlags.LogEvent);
        result.Should().NotHaveFlag(ProtectiveActionFlags.WarningBanner);
        result.Should().NotHaveFlag(ProtectiveActionFlags.PushNotification);
    }

    [Fact]
    public void DetermineActions_RiskScore0_ReturnsNone()
    {
        // Arrange
        var sut = new ProtectiveActionsMatrix();

        // Act
        var result = sut.DetermineActions(0);

        // Assert
        result.Should().Be(ProtectiveActionFlags.None);
    }

    #endregion

    #region Risk Range 21-40: Warning Banner

    [Theory]
    [InlineData(21)]
    [InlineData(30)]
    [InlineData(40)]
    public void DetermineActions_RiskScore21To40_ReturnsWarningBanner(double riskScore)
    {
        // Arrange
        var sut = new ProtectiveActionsMatrix();

        // Act
        var result = sut.DetermineActions(riskScore);

        // Assert
        result.Should().HaveFlag(ProtectiveActionFlags.LogEvent);
        result.Should().HaveFlag(ProtectiveActionFlags.WarningBanner);
        result.Should().NotHaveFlag(ProtectiveActionFlags.PushNotification);
        result.Should().NotHaveFlag(ProtectiveActionFlags.ModalDialog);
    }

    #endregion

    #region Risk Range 41-60: Push + Modal + Detailed Tracking

    [Theory]
    [InlineData(41)]
    [InlineData(50)]
    [InlineData(60)]
    public void DetermineActions_RiskScore41To60_ReturnsPushModalTracking(double riskScore)
    {
        // Arrange
        var sut = new ProtectiveActionsMatrix();

        // Act
        var result = sut.DetermineActions(riskScore);

        // Assert
        result.Should().HaveFlag(ProtectiveActionFlags.LogEvent);
        result.Should().HaveFlag(ProtectiveActionFlags.WarningBanner);
        result.Should().HaveFlag(ProtectiveActionFlags.PushNotification);
        result.Should().HaveFlag(ProtectiveActionFlags.ModalDialog);
        result.Should().HaveFlag(ProtectiveActionFlags.DetailedTracking);
        result.Should().HaveFlag(ProtectiveActionFlags.AlertGuardian);
        result.Should().NotHaveFlag(ProtectiveActionFlags.BlockPage);
    }

    #endregion

    #region Risk Range 61-80: Block Page + Disconnect Remote Access + SMS

    [Theory]
    [InlineData(61)]
    [InlineData(70)]
    [InlineData(80)]
    public void DetermineActions_RiskScore61To80_ReturnsBlockPageAndSMS(double riskScore)
    {
        // Arrange
        var sut = new ProtectiveActionsMatrix();

        // Act
        var result = sut.DetermineActions(riskScore);

        // Assert
        result.Should().HaveFlag(ProtectiveActionFlags.LogEvent);
        result.Should().HaveFlag(ProtectiveActionFlags.WarningBanner);
        result.Should().HaveFlag(ProtectiveActionFlags.PushNotification);
        result.Should().HaveFlag(ProtectiveActionFlags.ModalDialog);
        result.Should().HaveFlag(ProtectiveActionFlags.DetailedTracking);
        result.Should().HaveFlag(ProtectiveActionFlags.BlockPage);
        result.Should().HaveFlag(ProtectiveActionFlags.SmsEmergencyContact);
        result.Should().HaveFlag(ProtectiveActionFlags.AlertGuardian);
        result.Should().NotHaveFlag(ProtectiveActionFlags.CrossPlatformLock);
    }

    [Theory]
    [InlineData(61)]
    [InlineData(70)]
    [InlineData(80)]
    public void DetermineActions_RiskScore61To80_WithRemoteAccess_DisconnectsRemoteAccess(double riskScore)
    {
        // Arrange
        var sut = new ProtectiveActionsMatrix();

        // Act
        var result = sut.DetermineActions(riskScore, hasRemoteAccess: true);

        // Assert
        result.Should().HaveFlag(ProtectiveActionFlags.DisconnectRemoteAccess);
    }

    [Theory]
    [InlineData(61)]
    [InlineData(70)]
    [InlineData(80)]
    public void DetermineActions_RiskScore61To80_WithoutRemoteAccess_DoesNotDisconnect(double riskScore)
    {
        // Arrange
        var sut = new ProtectiveActionsMatrix();

        // Act
        var result = sut.DetermineActions(riskScore, hasRemoteAccess: false);

        // Assert
        result.Should().NotHaveFlag(ProtectiveActionFlags.DisconnectRemoteAccess);
    }

    #endregion

    #region Risk Range 81-100: Cross-Platform Lock + Black Screen + Lock Browser

    [Theory]
    [InlineData(81)]
    [InlineData(90)]
    [InlineData(100)]
    public void DetermineActions_RiskScore81To100_ReturnsMaximumProtection(double riskScore)
    {
        // Arrange
        var sut = new ProtectiveActionsMatrix();

        // Act
        var result = sut.DetermineActions(riskScore);

        // Assert
        result.Should().HaveFlag(ProtectiveActionFlags.LogEvent);
        result.Should().HaveFlag(ProtectiveActionFlags.WarningBanner);
        result.Should().HaveFlag(ProtectiveActionFlags.PushNotification);
        result.Should().HaveFlag(ProtectiveActionFlags.ModalDialog);
        result.Should().HaveFlag(ProtectiveActionFlags.DetailedTracking);
        result.Should().HaveFlag(ProtectiveActionFlags.BlockPage);
        result.Should().HaveFlag(ProtectiveActionFlags.SmsEmergencyContact);
        result.Should().HaveFlag(ProtectiveActionFlags.AlertGuardian);
        result.Should().HaveFlag(ProtectiveActionFlags.CrossPlatformLock);
        result.Should().HaveFlag(ProtectiveActionFlags.LockBrowser);
    }

    [Theory]
    [InlineData(81)]
    [InlineData(90)]
    [InlineData(100)]
    public void DetermineActions_RiskScore81To100_WithRemoteAccess_ActivatesBlackScreen(double riskScore)
    {
        // Arrange
        var sut = new ProtectiveActionsMatrix();

        // Act
        var result = sut.DetermineActions(riskScore, hasRemoteAccess: true);

        // Assert
        result.Should().HaveFlag(ProtectiveActionFlags.BlackScreen);
        result.Should().HaveFlag(ProtectiveActionFlags.DisconnectRemoteAccess);
    }

    [Theory]
    [InlineData(81)]
    [InlineData(90)]
    [InlineData(100)]
    public void DetermineActions_RiskScore81To100_WithoutRemoteAccess_NoBlackScreen(double riskScore)
    {
        // Arrange
        var sut = new ProtectiveActionsMatrix();

        // Act
        var result = sut.DetermineActions(riskScore, hasRemoteAccess: false);

        // Assert
        result.Should().NotHaveFlag(ProtectiveActionFlags.BlackScreen);
        result.Should().NotHaveFlag(ProtectiveActionFlags.DisconnectRemoteAccess);
    }

    #endregion

    #region ShouldTakeAction Tests

    [Fact]
    public void ShouldTakeAction_WithMatchingFlag_ReturnsTrue()
    {
        // Arrange
        var sut = new ProtectiveActionsMatrix();
        var actions = ProtectiveActionFlags.LogEvent | ProtectiveActionFlags.WarningBanner;

        // Act
        var result = sut.ShouldTakeAction(actions, ProtectiveActionFlags.WarningBanner);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldTakeAction_WithoutMatchingFlag_ReturnsFalse()
    {
        // Arrange
        var sut = new ProtectiveActionsMatrix();
        var actions = ProtectiveActionFlags.LogEvent | ProtectiveActionFlags.WarningBanner;

        // Act
        var result = sut.ShouldTakeAction(actions, ProtectiveActionFlags.BlockPage);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region DescribeActions Tests

    [Fact]
    public void DescribeActions_WithNoActions_ReturnsEmpty()
    {
        // Arrange
        var sut = new ProtectiveActionsMatrix();

        // Act
        var result = sut.DescribeActions(ProtectiveActionFlags.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void DescribeActions_WithSingleAction_ReturnsOneDescription()
    {
        // Arrange
        var sut = new ProtectiveActionsMatrix();

        // Act
        var result = sut.DescribeActions(ProtectiveActionFlags.WarningBanner);

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain("Display warning banner");
    }

    [Fact]
    public void DescribeActions_WithMultipleActions_ReturnsAllDescriptions()
    {
        // Arrange
        var sut = new ProtectiveActionsMatrix();
        var actions = ProtectiveActionFlags.LogEvent | 
                     ProtectiveActionFlags.WarningBanner | 
                     ProtectiveActionFlags.PushNotification;

        // Act
        var result = sut.DescribeActions(actions);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain("Log event for analysis");
        result.Should().Contain("Display warning banner");
        result.Should().Contain("Send push notification");
    }

    [Fact]
    public void DescribeActions_WithMaximumProtection_ReturnsAllRelevantDescriptions()
    {
        // Arrange
        var sut = new ProtectiveActionsMatrix();
        var actions = sut.DetermineActions(100, hasRemoteAccess: true);

        // Act
        var result = sut.DescribeActions(actions);

        // Assert
        result.Should().Contain("Lock all user devices");
        result.Should().Contain("Activate black screen protection");
        result.Should().Contain("Lock browser completely");
        result.Should().Contain("Disconnect remote access session");
    }

    #endregion

    #region GetSeverity Tests

    [Fact]
    public void GetSeverity_WithNoActions_ReturnsInfo()
    {
        // Arrange
        var sut = new ProtectiveActionsMatrix();

        // Act
        var result = sut.GetSeverity(ProtectiveActionFlags.None);

        // Assert
        result.Should().Be(RiskSeverity.Info);
    }

    [Fact]
    public void GetSeverity_WithWarningBanner_ReturnsLow()
    {
        // Arrange
        var sut = new ProtectiveActionsMatrix();

        // Act
        var result = sut.GetSeverity(ProtectiveActionFlags.WarningBanner);

        // Assert
        result.Should().Be(RiskSeverity.Low);
    }

    [Fact]
    public void GetSeverity_WithModalDialog_ReturnsMedium()
    {
        // Arrange
        var sut = new ProtectiveActionsMatrix();

        // Act
        var result = sut.GetSeverity(ProtectiveActionFlags.ModalDialog);

        // Assert
        result.Should().Be(RiskSeverity.Medium);
    }

    [Fact]
    public void GetSeverity_WithBlockPage_ReturnsHigh()
    {
        // Arrange
        var sut = new ProtectiveActionsMatrix();

        // Act
        var result = sut.GetSeverity(ProtectiveActionFlags.BlockPage);

        // Assert
        result.Should().Be(RiskSeverity.High);
    }

    [Fact]
    public void GetSeverity_WithCrossPlatformLock_ReturnsCritical()
    {
        // Arrange
        var sut = new ProtectiveActionsMatrix();

        // Act
        var result = sut.GetSeverity(ProtectiveActionFlags.CrossPlatformLock);

        // Assert
        result.Should().Be(RiskSeverity.Critical);
    }

    [Fact]
    public void GetSeverity_WithLockBrowser_ReturnsCritical()
    {
        // Arrange
        var sut = new ProtectiveActionsMatrix();

        // Act
        var result = sut.GetSeverity(ProtectiveActionFlags.LockBrowser);

        // Assert
        result.Should().Be(RiskSeverity.Critical);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void ProtectiveActionsMatrix_ElderlyVictimScenario_HighRisk()
    {
        // Arrange
        var sut = new ProtectiveActionsMatrix();
        var riskScore = 92.0; // Very high risk
        var hasRemoteAccess = true;
        var isTargeted = true;

        // Act
        var actions = sut.DetermineActions(riskScore, hasRemoteAccess, isTargeted);
        var descriptions = sut.DescribeActions(actions);
        var severity = sut.GetSeverity(actions);

        // Assert - Critical severity
        severity.Should().Be(RiskSeverity.Critical);

        // Assert - All maximum protection actions
        sut.ShouldTakeAction(actions, ProtectiveActionFlags.CrossPlatformLock).Should().BeTrue();
        sut.ShouldTakeAction(actions, ProtectiveActionFlags.LockBrowser).Should().BeTrue();
        sut.ShouldTakeAction(actions, ProtectiveActionFlags.BlackScreen).Should().BeTrue();
        sut.ShouldTakeAction(actions, ProtectiveActionFlags.DisconnectRemoteAccess).Should().BeTrue();
        sut.ShouldTakeAction(actions, ProtectiveActionFlags.SmsEmergencyContact).Should().BeTrue();
        sut.ShouldTakeAction(actions, ProtectiveActionFlags.AlertGuardian).Should().BeTrue();

        // Assert - Comprehensive descriptions
        descriptions.Should().Contain("Lock all user devices");
        descriptions.Should().Contain("Activate black screen protection");
        descriptions.Should().Contain("Lock browser completely");
    }

    [Fact]
    public void ProtectiveActionsMatrix_MediumRiskScenario_ModerateProtection()
    {
        // Arrange
        var sut = new ProtectiveActionsMatrix();
        var riskScore = 55.0;

        // Act
        var actions = sut.DetermineActions(riskScore);
        var severity = sut.GetSeverity(actions);

        // Assert
        severity.Should().Be(RiskSeverity.Medium);
        sut.ShouldTakeAction(actions, ProtectiveActionFlags.ModalDialog).Should().BeTrue();
        sut.ShouldTakeAction(actions, ProtectiveActionFlags.PushNotification).Should().BeTrue();
        sut.ShouldTakeAction(actions, ProtectiveActionFlags.DetailedTracking).Should().BeTrue();
        sut.ShouldTakeAction(actions, ProtectiveActionFlags.BlockPage).Should().BeFalse();
    }

    #endregion
}
