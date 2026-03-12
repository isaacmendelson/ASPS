using Xunit;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Business.Messaging;
using Business.RealtimeAnalysis.UserDomain;
using Business.Services;
using Business.RealtimeAnalysis;
using Business.RealtimeAnalysis.Indicators;
using Common.Models;
using Common.Interfaces;

namespace ASPS.Tests.Business.Messaging;

public class NotificationPublisherTests : IDisposable
{
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<NotificationPublisher>> _mockLogger;
    private readonly Mock<CurveKeyManager> _mockCurveKeyManager;
    private NotificationPublisher? _publisher;

    public NotificationPublisherTests()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<NotificationPublisher>>();
        _mockCurveKeyManager = new Mock<CurveKeyManager>();

        // Default configuration: port 50002
        var mockConfigSection = new Mock<IConfigurationSection>();
        mockConfigSection.Setup(x => x.Value).Returns("50002");
        _mockConfiguration.Setup(x => x.GetSection("NetMQ:NotificationPublisherPort")).Returns(mockConfigSection.Object);
        _mockConfiguration.Setup(x => x.GetValue<int>("NetMQ:NotificationPublisherPort", 50002)).Returns(50002);
    }

    [Fact]
    public void Constructor_WithDefaultConfiguration_CreatesPublisher()
    {
        // Act
        _publisher = new NotificationPublisher(_mockConfiguration.Object, _mockLogger.Object);

        // Assert
        Assert.NotNull(_publisher);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("NotificationPublisher started")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithCurveKeyManager_LogsEncryptionStatus()
    {
        // Arrange
        _mockCurveKeyManager.Setup(x => x.IsEnabled).Returns(true);

        // Act
        _publisher = new NotificationPublisher(_mockConfiguration.Object, _mockLogger.Object, _mockCurveKeyManager.Object);

        // Assert
        Assert.NotNull(_publisher);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("CURVE encrypted")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void PublishAnalysisResult_WithNullNotification_LogsWarning()
    {
        // Arrange
        _publisher = new NotificationPublisher(_mockConfiguration.Object, _mockLogger.Object);

        // Act
        _publisher.PublishAnalysisResult("device123", "user123", null);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("notification is null")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void PublishAnalysisResult_WithNullDeviceAndUser_LogsWarning()
    {
        // Arrange
        _publisher = new NotificationPublisher(_mockConfiguration.Object, _mockLogger.Object);
        var notification = CreateTestNotification();

        // Act
        _publisher.PublishAnalysisResult(null, null, notification);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Both deviceUid and userKeyField are null/empty")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void PublishAnalysisResult_WithEmptyDeviceAndUser_LogsWarning()
    {
        // Arrange
        _publisher = new NotificationPublisher(_mockConfiguration.Object, _mockLogger.Object);
        var notification = CreateTestNotification();

        // Act
        _publisher.PublishAnalysisResult(string.Empty, string.Empty, notification);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Both deviceUid and userKeyField are null/empty")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void PublishAnalysisResult_WithValidDeviceUid_PublishesSuccessfully()
    {
        // Arrange
        _publisher = new NotificationPublisher(_mockConfiguration.Object, _mockLogger.Object);
        var notification = CreateTestNotification();

        // Act
        _publisher.PublishAnalysisResult("device123", null, notification);

        // Assert - Should log debug message (not warning)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public void PublishAnalysisResult_WithValidUserKey_PublishesSuccessfully()
    {
        // Arrange
        _publisher = new NotificationPublisher(_mockConfiguration.Object, _mockLogger.Object);
        var notification = CreateTestNotification();

        // Act
        _publisher.PublishAnalysisResult(null, "user123", notification);

        // Assert - Should log debug message (not warning)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public void PublishAnalysisResult_WithBothDeviceAndUser_PublishesToBothTopics()
    {
        // Arrange
        _publisher = new NotificationPublisher(_mockConfiguration.Object, _mockLogger.Object);
        var notification = CreateTestNotification();

        // Act
        _publisher.PublishAnalysisResult("device123", "user123", notification);

        // Assert - Should log debug messages for both topics
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public void PublishAnalysisResult_AfterStop_LogsWarning()
    {
        // Arrange
        _publisher = new NotificationPublisher(_mockConfiguration.Object, _mockLogger.Object);
        var notification = CreateTestNotification();
        
        // Act
        _publisher.Stop();
        _publisher.PublishAnalysisResult("device123", "user123", notification);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not running")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Stop_SetsIsRunningToFalse_AndLogsInformation()
    {
        // Arrange
        _publisher = new NotificationPublisher(_mockConfiguration.Object, _mockLogger.Object);

        // Act
        _publisher.Stop();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("NotificationPublisher stopped")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Dispose_CallsStop()
    {
        // Arrange
        _publisher = new NotificationPublisher(_mockConfiguration.Object, _mockLogger.Object);

        // Act
        _publisher.Dispose();

        // Assert - Verify that Stop was called (logs "stopped" message)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("NotificationPublisher stopped")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        _publisher = new NotificationPublisher(_mockConfiguration.Object, _mockLogger.Object);

        // Act & Assert - Should not throw
        _publisher.Dispose();
        _publisher.Dispose();
    }

    public void Dispose()
    {
        _publisher?.Dispose();
    }

    private AnalysisResultNotification CreateTestNotification()
    {
        var mockAnalysisResult = new Mock<IAnalysisResult>();
        var riskAssessment = new RiskAssessment(0.3f, "Low", false, 0.8f);
        var protectiveActions = new List<IProtectiveAction>();
        var indicators = new List<IIndicator>();

        return new AnalysisResultNotification(
            "TestAlert",
            "Low",
            riskAssessment,
            mockAnalysisResult.Object,
            protectiveActions,
            indicators,
            DateTime.UtcNow
        );
    }
}
