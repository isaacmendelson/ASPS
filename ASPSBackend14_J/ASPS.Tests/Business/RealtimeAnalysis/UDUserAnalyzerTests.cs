using Business.RealtimeAnalysis.UserDomain;
using Business.Views;
using Common.Entities;
using Common.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ASPS.Tests.Business.RealtimeAnalysis;

public class UDUserAnalyzerTests
{
    private UDUserAnalyzer CreateSut()
    {
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        var loggerMock = new Mock<ILogger<UDUserAnalyzer>>();
        loggerFactoryMock.Setup(x => x.CreateLogger<UDUserAnalyzer>())
            .Returns(loggerMock.Object);

        var mockServiceProvider = new Mock<System.IServiceProvider>();
        var mockASViewLogger = new Mock<ILogger<ASView>>();
        var asViewMock = new Mock<ASView>(mockServiceProvider.Object, mockASViewLogger.Object);

        var testKey = new Key("test", "user", "123");
        var testUser = new UDUser(
            key: testKey,
            userInfo: new UserInfo(testKey, "test-keycloak", "Test", "User", "test@example.com", "", null, null, ""),
            riskAssessment: new RiskAssessment(0, "Safe", false, 1),
            userDevices: null,
            activeAlerts: null,
            browserTabs: null,
            isTaregted: false
        );

        return new UDUserAnalyzer(
            testUser,
            asViewMock.Object,
            alertExpiryDays: 30,
            alertDeletionDays: 90,
            loggerFactoryMock.Object
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
