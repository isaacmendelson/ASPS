using Business.DomainEvents;
using Business.RealtimeAnalysis;
using Business.RealtimeAnalysis.Indicators;
using Business.RealtimeAnalysis.UserDomain;
using Business.Views;
using Common.Entities;
using Common.Enums;
using Common.Interfaces;
using Common.Models;
using Common.Models.Alerts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ASPS.Tests.Business.UserDomain;

public class UDAnalysisResultTests
{
    [Fact]
    public void Constructor_ShouldInitializeAllProperties()
    {
        // Arrange
        var analysisLevel = AnalysisLevel.Device;
        var severity = Severity.High;
        var analyzerResults = new Dictionary<string, Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>>();
        var timestamp = DateTime.UtcNow;
        var mockConfig = new Mock<IConfiguration>();
        var user = CreateMockUser();

        // Act
        var result = new UDAnalysisResult(analysisLevel, severity, analyzerResults, timestamp, user, mockConfig.Object);

        // Assert
        Assert.Equal(severity, result.OverallSeverity);
        Assert.NotNull(result.AnalyzerResults);
        Assert.Equal(timestamp, result.AnalysisTimestamp);
        Assert.Equal(user, result.User);
    }

    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var mockConfig = new Mock<IConfiguration>();
        var result = new UDAnalysisResult(
            AnalysisLevel.Device,
            Severity.Low,
            new Dictionary<string, Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>>(),
            DateTime.UtcNow,
            CreateMockUser(),
            mockConfig.Object
        );

        // Act
        result.OverallSeverity = Severity.Critical;
        var newTimestamp = DateTime.UtcNow.AddHours(1);
        result.AnalysisTimestamp = newTimestamp;

        // Assert
        Assert.Equal(Severity.Critical, result.OverallSeverity);
        Assert.Equal(newTimestamp, result.AnalysisTimestamp);
    }

    private UDUser CreateMockUser()
    {
        var userKey = new Key("User", "test-user-result-123");
        var userInfo = new UserInfo(
            userKey,
            "keycloak-result-123",
            "Test",
            "Result",
            "456 Test Ave",
            "TestCity",
            "TestState",
            "54321",
            "US",
            "+9876543210",
            UserRole.Self,
            false,
            DateTime.UtcNow,
            null,
            "en-US",
            0
        );
        var riskAssessment = new RiskAssessment(0, "", false, 1);
        
        return new UDUser(userKey, userInfo, riskAssessment, null, null, null, false);
    }
}

public class UDAnalysisTests
{
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<ILogger<UDAnalysis>> _mockLogger;
    private readonly Mock<ILogger<UDUserAnalyzer>> _mockUserAnalyzerLogger;
    private readonly Mock<IndicatorFactory> _mockIndicatorFactory;
    private readonly Mock<ProtectiveActionsFactory> _mockProtectiveActionsFactory;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly UDUser _testUser;
    private readonly ASView _testASView;

    public UDAnalysisTests()
    {
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLogger = new Mock<ILogger<UDAnalysis>>();
        _mockUserAnalyzerLogger = new Mock<ILogger<UDUserAnalyzer>>();
        
        _mockLoggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(_mockLogger.Object);
        _mockLoggerFactory.Setup(f => f.CreateLogger(It.Is<string>(s => s.Contains("UDUserAnalyzer"))))
            .Returns(_mockUserAnalyzerLogger.Object);

        _mockIndicatorFactory = new Mock<IndicatorFactory>();
        _mockProtectiveActionsFactory = new Mock<ProtectiveActionsFactory>();
        _mockConfiguration = new Mock<IConfiguration>();
        //SetupConfiguration();

        _testUser = CreateMockUser();
        _testASView = CreateMockASView();
    }

    [Fact]
    public void Constructor_ShouldInitializeAnalysis()
    {
        // Act
        var analysis = CreateUDAnalysis();

        // Assert
        Assert.NotNull(analysis);
        Assert.NotNull(analysis._udUser);
        Assert.Empty(analysis.ActiveDeviceAlerts);
        Assert.Empty(analysis.ExpiredDeviceAlerts);
    }

    [Fact]
    public void Start_ShouldSetRunningState()
    {
        // Arrange
        var analysis = CreateUDAnalysis();

        // Act
        analysis.Start();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("UDAnalysis started")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Stop_ShouldStopAnalysis()
    {
        // Arrange
        var analysis = CreateUDAnalysis();
        analysis.Start();

        // Act
        analysis.Stop();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("UDAnalysis stopped")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldAddAlertToActiveList()
    {
        // Arrange
        var analysis = CreateUDAnalysis();
        var deviceAlert = CreateUrlAlert();
        var deviceUid = "device-123";
        var alertEntityKey = "alert-key-123";

        // Act
        await analysis.AnalyzeAsync(deviceAlert, deviceUid, alertEntityKey);

        // Assert
        Assert.Single(analysis.ActiveDeviceAlerts);
        Assert.Equal(deviceUid, analysis.ActiveDeviceAlerts[0].DeviceUid);
    }

    [Fact]
    public async Task AnalyzeAsync_WithMatchingAnalyzer_ShouldRunAnalysis()
    {
        // Arrange
        var mockAnalyzer = new Mock<ISpecificAnalyzer>();
        var deviceAlert = CreateUrlAlert();
        
        mockAnalyzer.Setup(a => a.CanAnalyze(It.IsAny<DeviceAlert>())).Returns(true);
        mockAnalyzer.Setup(a => a.AnalyzeAsync(
            It.IsAny<DeviceAlert>(),
            It.IsAny<List<DeviceAlert>>(),
            It.IsAny<IConfiguration>()))
            .ReturnsAsync(new AnalyzerResult(Severity.Low, "Test analysis"));

        var analyzers = new List<ISpecificAnalyzer> { mockAnalyzer.Object };
        var analysis = CreateUDAnalysis(analyzers);

        // Act
        await analysis.AnalyzeAsync(deviceAlert, "device-123", "alert-key");

        // Assert
        mockAnalyzer.Verify(a => a.AnalyzeAsync(
            It.IsAny<DeviceAlert>(),
            It.IsAny<List<DeviceAlert>>(),
            It.IsAny<IConfiguration>()), Times.Once);
    }

    [Fact]
    public async Task AnalyzeAsync_WithMultipleAlerts_ShouldMaintainHistory()
    {
        // Arrange
        var analysis = CreateUDAnalysis();
        var alert1 = CreateUrlAlert();
        var alert2 = CreateUrlAlert();

        // Act
        await analysis.AnalyzeAsync(alert1, "device-1", "key-1");
        await analysis.AnalyzeAsync(alert2, "device-2", "key-2");

        // Assert
        Assert.Equal(2, analysis.ActiveDeviceAlerts.Count);
    }

    [Fact]
    public void RegisterEventHandler_ShouldAddHandler()
    {
        // Arrange
        var analysis = CreateUDAnalysis();
        var mockHandler = new Mock<IDomainEventHandler>();
        mockHandler.Setup(h => h.GetHandleableEvents()).Returns(new[] { typeof(AnalysisResultReceived) });

        // Act
        analysis.RegisterEventHandler(mockHandler.Object);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Registered event handler")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldFireAnalysisResultEvent()
    {
        // Arrange
        var mockHandler = new Mock<IDomainEventHandler>();
        mockHandler.Setup(h => h.GetHandleableEvents())
            .Returns(new[] { typeof(AnalysisResultReceived) });
        
        bool eventHandled = false;
        mockHandler.Setup(h => h.Handle(It.IsAny<AnalysisResultReceived>()))
            .Callback(() => eventHandled = true);

        var analysis = CreateUDAnalysis();
        analysis.RegisterEventHandler(mockHandler.Object);

        var deviceAlert = CreateUrlAlert();

        // Act
        await analysis.AnalyzeAsync(deviceAlert, "device-123", "alert-key");

        // Assert
        Assert.True(eventHandled);
    }

    [Fact]
    public void ActiveDeviceAlerts_ShouldBeReadOnly()
    {
        // Arrange
        var analysis = CreateUDAnalysis();

        // Act
        var alerts = analysis.ActiveDeviceAlerts;

        // Assert
        Assert.IsAssignableFrom<IReadOnlyList<ActiveDeviceAlert>>(alerts);
    }

    [Fact]
    public void ExpiredDeviceAlerts_ShouldBeReadOnly()
    {
        // Arrange
        var analysis = CreateUDAnalysis();

        // Act
        var alerts = analysis.ExpiredDeviceAlerts;

        // Assert
        Assert.IsAssignableFrom<IReadOnlyList<ActiveDeviceAlert>>(alerts);
    }

    // Helper methods
    private UDAnalysis CreateUDAnalysis(List<ISpecificAnalyzer>? analyzers = null)
    {
        analyzers ??= new List<ISpecificAnalyzer>();

        return new UDAnalysis(
            _testUser,
            _testASView,
            analyzers,
            _mockLoggerFactory.Object,
            _mockIndicatorFactory.Object,
            _mockProtectiveActionsFactory.Object,
            _mockConfiguration.Object,
            alertExpiryDays: 30,
            alertDeletionDays: 90
        );
    }

    private UDUser CreateMockUser()
    {
        var userKey = new Key("User", "test-user-123");
        var userInfo = new UserInfo(
            userKey,
            "keycloak-123",
            "Test",
            "User",
            "123 Main St",
            "City",
            "State",
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
        var riskAssessment = new RiskAssessment(0, "", false, 1);
        
        return new UDUser(userKey, userInfo, riskAssessment, null, null, null, false);
    }

    private ASView CreateMockASView()
    {
        var services = new ServiceCollection();
        var mockLogger = new Mock<ILogger<ASView>>();
        services.AddSingleton(mockLogger.Object);
        var serviceProvider = services.BuildServiceProvider();

        return new ASView(serviceProvider, mockLogger.Object, _mockConfiguration.Object);
    }

    private UrlAlert CreateUrlAlert()
    {
        return new UrlAlert
        {
            AlertId = Guid.NewGuid().ToString(),
            Url = "https://example.com",
            Trackers = Array.Empty<Key>(),
            IFrameDomains = Array.Empty<string>(),
            AlertType = "Url"
        };
    }
}

public class AnalyzerResultTests
{
    [Fact]
    public void Constructor_WithAllParameters_ShouldInitialize()
    {
        // Arrange
        var severity = Severity.High;
        var message = "Test message";
        var indicators = new List<IIndicator>();
        var actions = new List<IProtectiveAction>();
        var details = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var result = new AnalyzerResult(severity, message, indicators, actions, details);

        // Assert
        Assert.Equal(severity, result.Severity);
        Assert.Equal(message, result.Message);
        Assert.Equal(indicators, result.Indicators);
        Assert.Equal(actions, result.ProtectiveActions);
        Assert.Equal(details, result.Details);
    }

    [Fact]
    public void Constructor_WithSeverityAndMessage_ShouldInitialize()
    {
        // Arrange
        var severity = Severity.Medium;
        var message = "Simple test";

        // Act
        var result = new AnalyzerResult(severity, message);

        // Assert
        Assert.Equal(severity, result.Severity);
        Assert.Equal(message, result.Message);
        Assert.NotNull(result.Details);
        Assert.Empty(result.Details);
    }

    [Fact]
    public void Properties_CanBeModified()
    {
        // Arrange
        var result = new AnalyzerResult(Severity.Low, "Initial");

        // Act
        result.Severity = Severity.Critical;
        result.Message = "Updated message";
        result.Details["newKey"] = "newValue";

        // Assert
        Assert.Equal(Severity.Critical, result.Severity);
        Assert.Equal("Updated message", result.Message);
        Assert.Contains("newKey", result.Details.Keys);
    }

    [Fact]
    public void Details_ShouldBeEmptyByDefault()
    {
        // Act
        var result = new AnalyzerResult(Severity.Low, "Test");

        // Assert
        Assert.NotNull(result.Details);
        Assert.Empty(result.Details);
    }
}
