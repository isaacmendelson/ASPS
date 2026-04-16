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
using Interface.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ASPS.Tests.Business.UserDomain;

public class UDAnalysisManagerTests
{
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<ILogger<UDAnalysisManager>> _mockLogger;
    private readonly Mock<ILogger<UDAnalysis>> _mockAnalysisLogger;
    private readonly Mock<ILogger<UDUserAnalyzer>> _mockUserAnalyzerLogger;
    private readonly Mock<ILogger<UDUrlAnalyzer>> _mockUrlAnalyzerLogger;
    private readonly Mock<ILogger<UDRemoteAccessAnalyzer>> _mockRemoteAnalyzerLogger;
    private readonly Mock<ILogger<UDPhishingAnalyzer>> _mockPhishingAnalyzerLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly ASView _mockASView;
    private readonly Mock<IKnownPhishingWebsiteRepository> _mockPhishingRepo;
    private readonly Mock<ISafeDomainRepository> _mockSafeDomainRepo;
    private readonly Mock<IWebsiteCategoryRepository> _mockWebsiteCategoryRepo;
    private readonly UDUser _testUser;
    private readonly UDUserAnalyzer _userAnalyzer;

    public UDAnalysisManagerTests()
    {
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLogger = new Mock<ILogger<UDAnalysisManager>>();
        _mockAnalysisLogger = new Mock<ILogger<UDAnalysis>>();
        _mockUserAnalyzerLogger = new Mock<ILogger<UDUserAnalyzer>>();
        _mockUrlAnalyzerLogger = new Mock<ILogger<UDUrlAnalyzer>>();
        _mockRemoteAnalyzerLogger = new Mock<ILogger<UDRemoteAccessAnalyzer>>();
        _mockPhishingAnalyzerLogger = new Mock<ILogger<UDPhishingAnalyzer>>();

        // Setup for CreateLogger<T>() - uses typeof(T).FullName
        _mockLoggerFactory.Setup(f => f.CreateLogger(It.Is<string>(s => s.EndsWith("UDAnalysisManager"))))
            .Returns(_mockLogger.Object);
        _mockLoggerFactory.Setup(f => f.CreateLogger(It.Is<string>(s => s.EndsWith("UDAnalysis") && !s.Contains("Manager"))))
            .Returns(_mockAnalysisLogger.Object);
        _mockLoggerFactory.Setup(f => f.CreateLogger(It.Is<string>(s => s.EndsWith("UDUserAnalyzer"))))
            .Returns(_mockUserAnalyzerLogger.Object);
        _mockLoggerFactory.Setup(f => f.CreateLogger(It.Is<string>(s => s.EndsWith("UDUrlAnalyzer"))))
            .Returns(_mockUrlAnalyzerLogger.Object);
        _mockLoggerFactory.Setup(f => f.CreateLogger(It.Is<string>(s => s.EndsWith("UDRemoteAccessAnalyzer"))))
            .Returns(_mockRemoteAnalyzerLogger.Object);
        _mockLoggerFactory.Setup(f => f.CreateLogger(It.Is<string>(s => s.EndsWith("UDPhishingAnalyzer"))))
            .Returns(_mockPhishingAnalyzerLogger.Object);
        _mockLoggerFactory.Setup(f => f.CreateLogger(It.Is<string>(s => s.EndsWith("UDTrackUrlAnalyzer"))))
            .Returns(Mock.Of<ILogger<UDTrackUrlAnalyzer>>());

        _mockConfiguration = new Mock<IConfiguration>();
        SetupConfiguration();

        _mockPhishingRepo = new Mock<IKnownPhishingWebsiteRepository>();
        _mockSafeDomainRepo = new Mock<ISafeDomainRepository>();
        _mockWebsiteCategoryRepo = new Mock<IWebsiteCategoryRepository>();

        _testUser = CreateMockUser();
        _mockASView = CreateMockASView();

        _userAnalyzer = CreateMockUserAnalyzer();

    }

    [Fact]
    public void Constructor_ShouldInitializeAnalysisManager()
    {
        // Act
        var manager = CreateManager();

        // Assert
        Assert.NotNull(manager);
        Assert.NotNull(manager.UDUser);
        Assert.NotNull(manager.Analysis);
        Assert.Equal(_testUser.Key, manager.UDUser.Key);
    }

    [Fact]
    public void Constructor_ShouldRegisterEventHandlers()
    {
        // Arrange
        var eventHandlers = new List<IDomainEventHandler>();

        // Act
        var manager = CreateManager(eventHandlers);

        // Assert - Manager should log successful creation
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("UDAnalysisManager created")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Start_ShouldStartAnalysisManager()
    {
        // Arrange
        var manager = CreateManager();

        // Act
        manager.Start();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("UDAnalysisManager started")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Stop_ShouldStopAnalysisManager()
    {
        // Arrange
        var manager = CreateManager();
        manager.Start();

        // Act
        manager.Stop();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("UDAnalysisManager stopped")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Start_ShouldInitializeOnlyOnce()
    {
        // Arrange
        var manager = CreateManager();

        // Act
        manager.Start();
        manager.Start(); // Second start

        // Assert - Should only log initialized once
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("initialized")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once); // Only once even if Start() called twice
    }

    [Fact]
    public void GetHandleableEvents_ShouldReturnCorrectEventTypes()
    {
        // Arrange
        var manager = CreateManager();

        // Act
        var events = manager.GetHandleableEvents();

        // Assert
        Assert.Contains(typeof(DeviceAlertReceived), events);
        Assert.Contains(typeof(AnalysisResultAdded), events);
        Assert.Equal(2, events.Length);
    }

    [Fact]
    public async Task Handle_WithDeviceAlertReceived_ShouldProcessAlert()
    {
        // Arrange
        var manager = CreateManager();
        manager.Start();

        var alert = new UrlAlert
        {
            AlertId = Guid.NewGuid().ToString(),
            Url = "https://test.com",
            Trackers = Array.Empty<Key>(),
            IFrameDomains = Array.Empty<string>(),
            AlertType = "Url"
        };

        var alertEvent = new DeviceAlertReceived(
            alert,
            Priority.High,
            "device-123",
            DateTime.UtcNow,
            DateTime.UtcNow,
            "alert-key-123"
        );

        // Act
        await manager.Handle(alertEvent);

        // Assert - Alert should be added to analysis
        Assert.Single(manager.Analysis.ActiveDeviceAlerts);
    }

    [Fact]
    public async Task Handle_WithAnalysisResultReceived_ShouldComplete()
    {
        // Arrange
        var manager = CreateManager();
        manager.Start();

        var analysisEvent = new AnalysisResultReceived
        {
            UserKeyField = _testUser.Key.Value,
            DeviceAlertKeyField = "alert-key-123",
            DeviceUid = "device-123",
            AnalyzerResults = new Dictionary<string, Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>>(),
            Severity = Severity.Low,
            AnalysisTimestamp = DateTime.UtcNow,
            AlertType = "Url"
        };

        // Act & Assert - Should complete without exception
        await manager.Handle(analysisEvent);
    }

    [Fact]
    public async Task Handle_WhenNotRunning_ShouldNotProcess()
    {
        // Arrange
        var manager = CreateManager();
        // Don't call Start()

        var alert = new UrlAlert
        {
            AlertId = Guid.NewGuid().ToString(),
            Url = "https://test.com",
            Trackers = Array.Empty<Key>(),
            IFrameDomains = Array.Empty<string>(),
            AlertType = "Url"
        };

        var alertEvent = new DeviceAlertReceived(
            alert,
            Priority.High,
            "device-123",
            DateTime.UtcNow,
            DateTime.UtcNow,
            "alert-key-123"
        );

        // Act
        await manager.Handle(alertEvent);

        // Assert - No alerts should be added since manager is not running
        Assert.Empty(manager.Analysis.ActiveDeviceAlerts);
    }

    [Fact]
    public void UDUser_ShouldReturnCorrectUser()
    {
        // Arrange
        var manager = CreateManager();

        // Act
        var user = manager.UDUser;

        // Assert
        Assert.Equal(_testUser.Key, user.Key);
    }

    [Fact]
    public void Analysis_ShouldReturnAnalysisInstance()
    {
        // Arrange
        var manager = CreateManager();

        // Act
        var analysis = manager.Analysis;

        // Assert
        Assert.NotNull(analysis);
    }

    // Helper methods
    private UDAnalysisManager CreateManager(List<IDomainEventHandler>? eventHandlers = null)
    {
        eventHandlers ??= new List<IDomainEventHandler>();

        return new UDAnalysisManager(
            _testUser,
            _userAnalyzer,
            _mockLoggerFactory.Object,

            _mockASView,
            _mockConfiguration.Object,
            eventHandlers,
            _mockPhishingRepo.Object,
            _mockSafeDomainRepo.Object,
            _mockWebsiteCategoryRepo.Object
        );
    }

    private UDUser CreateMockUser()
    {
        var userKey = new Key("User", "test-user-manager-123");
        var userInfo = new UserInfo(
            userKey,
            "keycloak-manager-123",
            "Test",
            "Manager",
            "789 Manager St",
            "ManagerCity",
            "ManagerState",
            "78901",
            "US",
            "+1122334455",
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

    private UDUserAnalyzer CreateMockUserAnalyzer()
    {
        return new UDUserAnalyzer(_testUser, _mockASView,10,180, _mockLoggerFactory.Object);
    }
    private ASView CreateMockASView()
    {
        var services = new ServiceCollection();
        var mockLogger = new Mock<ILogger<ASView>>();
        services.AddSingleton(mockLogger.Object);
        var serviceProvider = services.BuildServiceProvider();

        var view = new ASView(serviceProvider, mockLogger.Object, _mockConfiguration.Object);

        return view;
    }

    private void SetupConfiguration()
    {
        var expirySection = new Mock<IConfigurationSection>();
        expirySection.Setup(s => s.Value).Returns("30");

        var deletionSection = new Mock<IConfigurationSection>();
        deletionSection.Setup(s => s.Value).Returns("90");

        var pythonPathSection = new Mock<IConfigurationSection>();
        pythonPathSection.Setup(s => s.Value).Returns("python");

        var analyzersFolderSection = new Mock<IConfigurationSection>();
        analyzersFolderSection.Setup(s => s.Value).Returns("/tmp/analyzers");

        _mockConfiguration.Setup(c => c.GetSection("Analysis:DeviceAlertExpiryDays"))
            .Returns(expirySection.Object);
        _mockConfiguration.Setup(c => c.GetSection("Analysis:DeviceAlertDeletionDays"))
            .Returns(deletionSection.Object);
        _mockConfiguration.Setup(c => c.GetSection("Python:ExecutablePath"))
            .Returns(pythonPathSection.Object);
        _mockConfiguration.Setup(c => c.GetSection("Python:AnalyzersFolderPath"))
            .Returns(analyzersFolderSection.Object);

        // For indexer access (used by GetValue extension method)
        _mockConfiguration.Setup(c => c["Analysis:DeviceAlertExpiryDays"]).Returns("30");
        _mockConfiguration.Setup(c => c["Analysis:DeviceAlertDeletionDays"]).Returns("90");
        _mockConfiguration.Setup(c => c["Python:ExecutablePath"]).Returns("python");
        _mockConfiguration.Setup(c => c["Python:AnalyzersFolderPath"]).Returns("/tmp/analyzers");
        
        // TrackUrl sections
        var riskThresholdSection = new Mock<IConfigurationSection>();
        riskThresholdSection.Setup(s => s.Value).Returns("40");
        _mockConfiguration.Setup(c => c.GetSection("TrackUrl:RiskThresholdToEnableTracking"))
            .Returns(riskThresholdSection.Object);
        
        var trackingDurationSection = new Mock<IConfigurationSection>();
        trackingDurationSection.Setup(s => s.Value).Returns("3000");
        _mockConfiguration.Setup(c => c.GetSection("TrackUrl:TrackingDurationMinutes"))
            .Returns(trackingDurationSection.Object);
        
        // Analysis severity sections
        var criticalSection = new Mock<IConfigurationSection>();
        criticalSection.Setup(s => s.Value).Returns("80");
        _mockConfiguration.Setup(c => c.GetSection("Analysis:SeverityScoreThresholdCritical"))
            .Returns(criticalSection.Object);
        
        var highSection = new Mock<IConfigurationSection>();
        highSection.Setup(s => s.Value).Returns("80");
        _mockConfiguration.Setup(c => c.GetSection("Analysis:SeverityScoreThresholdHigh"))
            .Returns(highSection.Object);
        
        var mediumSection = new Mock<IConfigurationSection>();
        mediumSection.Setup(s => s.Value).Returns("80");
        _mockConfiguration.Setup(c => c.GetSection("Analysis:SeverityScoreThresholdMedium"))
            .Returns(mediumSection.Object);
    }
}
