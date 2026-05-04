using Common.Enums;
using Business.RealtimeAnalysis.UserDomain;
using Business.Views;
using Common.Entities;
using Common.Models;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Business.RealtimeAnalysis.ProtectivActions;

namespace ASPS.Tests.Business.RealtimeAnalysis;

public class UDUserAnalyzerTests
{
    private  Mock<ProtectiveActionsFactory> _mockProtectiveActionsFactory;

    private UDUserAnalyzer CreateSut()
    {
        // Use NullLoggerFactory instead of mocking extension methods
        var loggerFactory = NullLoggerFactory.Instance;

        var services = new ServiceCollection();
        var mockASViewLogger = new Mock<ILogger<ASView>>();
        services.AddSingleton(mockASViewLogger.Object);
        var serviceProvider = services.BuildServiceProvider();
        var mockConfiguration = new Mock<IConfiguration>();
        var asView = new ASView(serviceProvider, mockASViewLogger.Object, mockConfiguration.Object);
        this._mockProtectiveActionsFactory = new Mock<ProtectiveActionsFactory>();
        var testKey = new Key("test", "user", "123");
        var testUser = new UDUser(
            key: testKey,
            userInfo: new UserInfo(testKey, "test-keycloak", "Test", "User", "", "", "", "", "", "1234567890", UserRole.Self, false, DateTime.UtcNow, null, null, null),
            riskAssessment: new RiskAssessment(0, "Safe", false, 1),
            userDevices: null,
            activeAlerts: null,
            browserTabs: null,
            isTaregted: false
        );

        return new UDUserAnalyzer(
            testUser,
            asView,
            alertExpiryDays: 30,
            alertDeletionDays: 90,
            loggerFactory,
            this._mockProtectiveActionsFactory.Object
        );
    }

    [Fact]
    public void Constructor_WithValidParams_CreatesInstance()
    {
        // Act
        var sut = CreateSut();

        // Assert
        sut.Should().NotBeNull();
        sut.Name.Should().Be("UDUserAnalyzer");
    }

    [Fact]
    public void HandleTrackUrlAnalysisResultReceived_Method_Exists()
    {
        // Arrange
        var sut = CreateSut();

        // Act - Verify method exists via reflection
        var method = sut.GetType().GetMethod("HandleTrackUrlAnalysisResultReceived", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Assert
        method.Should().NotBeNull("HandleTrackUrlAnalysisResultReceived method should exist");
        method!.ReturnType.Should().Be(typeof(void));
        method.GetParameters().Should().HaveCount(2);
    }
}
