using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Business.Handlers;
using Business.Queries;
using Common.Entities;
using Common.Enums;
using Common.Models;
using Interface.Repositories;

namespace ASPS.Tests.Business.Handlers;

/// <summary>
/// Unit tests for ASPS-647 paged query handler methods on AdminQueryHandlers.
/// </summary>
public class AdminQueryHandlersPaged647Tests
{
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IUserDeviceRepository> _deviceRepoMock;
    private readonly Mock<IDeviceAlertRepository> _alertRepoMock;
    private readonly Mock<IKnownPhishingWebsiteRepository> _phishingMock;
    private readonly Mock<ITrackedDomainRepository> _trackedDomainMock;
    private readonly Mock<IAnalysisResultRepository> _analysisRepoMock;
    private readonly Mock<ILogger<AdminQueryHandlers>> _loggerMock;
    private readonly AdminQueryHandlers _sut;

    public AdminQueryHandlersPaged647Tests()
    {
        _userRepoMock = new Mock<IUserRepository>();
        _deviceRepoMock = new Mock<IUserDeviceRepository>();
        _alertRepoMock = new Mock<IDeviceAlertRepository>();
        _phishingMock = new Mock<IKnownPhishingWebsiteRepository>();
        _trackedDomainMock = new Mock<ITrackedDomainRepository>();
        _analysisRepoMock = new Mock<IAnalysisResultRepository>();
        _loggerMock = new Mock<ILogger<AdminQueryHandlers>>();

        _sut = new AdminQueryHandlers(
            _userRepoMock.Object,
            _deviceRepoMock.Object,
            _alertRepoMock.Object,
            _phishingMock.Object,
            _trackedDomainMock.Object,
            _analysisRepoMock.Object,
            _loggerMock.Object);
    }

    // =========================================================================
    // GetAllDevicesPagedQuery
    // =========================================================================

    [Fact]
    public async Task HandleAsync_GetAllDevicesPagedQuery_ReturnsPagedResult()
    {
        // Arrange
        var user = new User { KeyField = "user-1", FirstName = "Alice", LastName = "Smith" };

        var device = new PersonalComputer
        {
            KeyField = "dev-1",
            UserKeyField = "user-1",
            DeviceUid = "DEV-001",
            DeviceType = DeviceType.PersonalComputer,
            MonitoringStatus = DeviceMonitoringStatus.Enabled
        };

        _deviceRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<UserDevice> { device });
        _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User> { user });

        var query = new GetAllDevicesPagedQuery { Page = 1, PageSize = 25 };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Result.Items.Should().HaveCount(1);
        result.Result.TotalCount.Should().Be(1);
        result.Result.Page.Should().Be(1);
        result.Result.Items[0].DeviceUid.Should().Be("DEV-001");
        result.Result.Items[0].UserName.Should().Be("Alice Smith");
    }

    [Fact]
    public async Task HandleAsync_GetAllDevicesPagedQuery_SearchFiltersDeviceUid()
    {
        // Arrange
        var dev1 = new PersonalComputer { KeyField = "d1", DeviceUid = "DEV-ALPHA" };
        var dev2 = new PersonalComputer { KeyField = "d2", DeviceUid = "DEV-BETA" };

        _deviceRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<UserDevice> { dev1, dev2 });
        _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());

        var query = new GetAllDevicesPagedQuery { Page = 1, PageSize = 25, Search = "ALPHA" };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Result.Items.Should().HaveCount(1);
        result.Result.Items[0].DeviceUid.Should().Be("DEV-ALPHA");
    }

    [Fact]
    public async Task HandleAsync_GetAllDevicesPagedQuery_StatusFilterWorks()
    {
        // Arrange
        var dev1 = new PersonalComputer { KeyField = "d1", DeviceUid = "DEV-1", MonitoringStatus = DeviceMonitoringStatus.Enabled };
        var dev2 = new PersonalComputer { KeyField = "d2", DeviceUid = "DEV-2", MonitoringStatus = DeviceMonitoringStatus.Disabled };

        _deviceRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<UserDevice> { dev1, dev2 });
        _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());

        var query = new GetAllDevicesPagedQuery { Page = 1, PageSize = 25, Status = "Enabled" };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Result.Items.Should().HaveCount(1);
        result.Result.Items[0].DeviceUid.Should().Be("DEV-1");
    }

    [Fact]
    public async Task HandleAsync_GetAllDevicesPagedQuery_PagingWorks()
    {
        // Arrange
        var devices = Enumerable.Range(1, 10)
            .Select(i => (UserDevice)new PersonalComputer { KeyField = $"d{i}", DeviceUid = $"DEV-{i:D2}" })
            .ToList();

        _deviceRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(devices);
        _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());

        var query = new GetAllDevicesPagedQuery { Page = 2, PageSize = 3 };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Result.TotalCount.Should().Be(10);
        result.Result.Items.Should().HaveCount(3);
        result.Result.Page.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_GetAllDevicesPagedQuery_WhenRepositoryThrows_ReturnsFailure()
    {
        // Arrange
        _deviceRepoMock.Setup(r => r.GetAllAsync()).ThrowsAsync(new Exception("DB error"));
        _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());

        // Act
        var result = await _sut.HandleAsync(new GetAllDevicesPagedQuery());

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Error");
    }

    // =========================================================================
    // GetAllAlertsPagedQuery
    // =========================================================================

    [Fact]
    public async Task HandleAsync_GetAllAlertsPagedQuery_ReturnsAllAlerts()
    {
        // Arrange
        var alerts = new List<DeviceAlertEntity>
        {
            new UrlAlertEntity { KeyField = "a1", AlertType = "UrlAlert", Priority = Priority.High, Timestamp = DateTime.UtcNow.AddHours(-1) },
            new RemoteAccessAlertEntity { KeyField = "a2", AlertType = "RemoteAccessAlert", Priority = Priority.Low, Timestamp = DateTime.UtcNow.AddHours(-2) }
        };

        _alertRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(alerts);
        _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());

        // Act
        var result = await _sut.HandleAsync(new GetAllAlertsPagedQuery { Page = 1, PageSize = 25 });

        // Assert
        result.Success.Should().BeTrue();
        result.Result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_GetAllAlertsPagedQuery_TimeRangeFilterWorks()
    {
        // Arrange
        var cutoff = DateTime.UtcNow.AddHours(-5);
        var oldAlert = new UrlAlertEntity { KeyField = "a1", AlertType = "UrlAlert", Timestamp = DateTime.UtcNow.AddHours(-10) };
        var newAlert = new UrlAlertEntity { KeyField = "a2", AlertType = "UrlAlert", Timestamp = DateTime.UtcNow.AddHours(-1) };

        _alertRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<DeviceAlertEntity> { oldAlert, newAlert });
        _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());

        var query = new GetAllAlertsPagedQuery { Page = 1, PageSize = 25, From = cutoff };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Result.Items.Should().HaveCount(1);
        result.Result.Items[0].AlertType.Should().Be("UrlAlert");
    }

    [Fact]
    public async Task HandleAsync_GetAllAlertsPagedQuery_SeverityFilterWorks()
    {
        // Arrange
        var highAlert = new UrlAlertEntity { KeyField = "a1", AlertType = "UrlAlert", Priority = Priority.High };
        var lowAlert = new UrlAlertEntity { KeyField = "a2", AlertType = "UrlAlert", Priority = Priority.Low };

        _alertRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<DeviceAlertEntity> { highAlert, lowAlert });
        _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());

        var query = new GetAllAlertsPagedQuery { Page = 1, PageSize = 25, Severity = "High" };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Result.Items.Should().HaveCount(1);
        result.Result.Items[0].Priority.Should().Be(Priority.High);
    }

    [Fact]
    public async Task HandleAsync_GetAllAlertsPagedQuery_SearchByAlertType()
    {
        // Arrange
        var a1 = new UrlAlertEntity { KeyField = "a1", AlertType = "UrlAlert" };
        var a2 = new RemoteAccessAlertEntity { KeyField = "a2", AlertType = "RemoteAccessAlert" };

        _alertRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<DeviceAlertEntity> { a1, a2 });
        _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());

        var query = new GetAllAlertsPagedQuery { Page = 1, PageSize = 25, Search = "Remote" };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Result.Items.Should().HaveCount(1);
        result.Result.Items[0].AlertType.Should().Be("RemoteAccessAlert");
    }

    // =========================================================================
    // GetDeviceAlertsPagedQuery
    // =========================================================================

    [Fact]
    public async Task HandleAsync_GetDeviceAlertsPagedQuery_ReturnsAlertsForDevice()
    {
        // Arrange
        var targetDeviceKey = "device-key-abc";
        var a1 = new UrlAlertEntity { KeyField = "a1", AlertType = "UrlAlert", DeviceKeyField = targetDeviceKey };
        var a2 = new UrlAlertEntity { KeyField = "a2", AlertType = "UrlAlert", DeviceKeyField = "other-device" };

        _alertRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<DeviceAlertEntity> { a1, a2 });
        _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());

        var query = new GetDeviceAlertsPagedQuery
        {
            DeviceKeyField = targetDeviceKey,
            Page = 1,
            PageSize = 25
        };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Result.Items.Should().HaveCount(1);
    }

    // =========================================================================
    // GetAlertDetailQuery
    // =========================================================================

    [Fact]
    public async Task HandleAsync_GetAlertDetailQuery_ReturnsMappedDto()
    {
        // Arrange
        var alertKey = new Key("RemoteAccessAlertEntity", "alert-001");
        var alert = new RemoteAccessAlertEntity
        {
            KeyField = "alert-001",
            AlertType = "RemoteAccessAlert",
            Priority = Priority.High,
            DeviceUid = "DEV-XYZ",
            Timestamp = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)
        };

        _alertRepoMock.Setup(r => r.GetByKeyAsync(It.IsAny<Key>())).ReturnsAsync(alert);
        _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());

        // Act
        var result = await _sut.HandleAsync(new GetAlertDetailQuery { AlertKey = alertKey });

        // Assert
        result.Success.Should().BeTrue();
        result.Alert.Should().NotBeNull();
        result.Alert!.AlertType.Should().Be("RemoteAccessAlert");
        result.Alert.Priority.Should().Be(Priority.High);
        result.Alert.DeviceUid.Should().Be("DEV-XYZ");
    }

    [Fact]
    public async Task HandleAsync_GetAlertDetailQuery_WhenNotFound_ReturnsFailure()
    {
        // Arrange
        _alertRepoMock.Setup(r => r.GetByKeyAsync(It.IsAny<Key>())).ReturnsAsync((DeviceAlertEntity?)null);

        // Act
        var result = await _sut.HandleAsync(new GetAlertDetailQuery { AlertKey = new Key("DeviceAlertEntity", "missing") });

        // Assert
        result.Success.Should().BeFalse();
        result.Alert.Should().BeNull();
    }

    // =========================================================================
    // GetAllAnalysisResultsPagedQuery
    // =========================================================================

    [Fact]
    public async Task HandleAsync_GetAllAnalysisResultsPagedQuery_ReturnsPagedResult()
    {
        // Arrange
        var r1 = new UrlAnalysisResultContainer(
            url: "https://example.com",
            keyField: "ar-1",
            userKeyField: "user-1",
            discriminator: "UrlAnalysis",
            timestamp: DateTime.UtcNow,
            jsonValue: "{}",
            hasError: false,
            errorMessage: null);

        _analysisRepoMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<AnalysisResultContainer> { r1 });

        var query = new GetAllAnalysisResultsPagedQuery { Page = 1, PageSize = 25 };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Result.TotalCount.Should().Be(1);
        result.Result.Items.Should().HaveCount(1);
        result.Result.Items[0].Discriminator.Should().Be("UrlAnalysis");
        result.Result.Items[0].Url.Should().Be("https://example.com");
    }

    [Fact]
    public async Task HandleAsync_GetAllAnalysisResultsPagedQuery_SearchByDiscriminator()
    {
        // Arrange
        var r1 = new AnalysisResultContainer("ar-1", "user-1", "UrlAnalysis", DateTime.UtcNow, null, false, null);
        var r2 = new AnalysisResultContainer("ar-2", "user-1", "RemoteAccessAnalysis", DateTime.UtcNow, null, false, null);

        _analysisRepoMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<AnalysisResultContainer> { r1, r2 });

        var query = new GetAllAnalysisResultsPagedQuery { Page = 1, PageSize = 25, Search = "RemoteAccess" };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Result.Items.Should().HaveCount(1);
        result.Result.Items[0].Discriminator.Should().Be("RemoteAccessAnalysis");
    }

    // =========================================================================
    // GetAnalysisResultDetailQuery
    // =========================================================================

    [Fact]
    public async Task HandleAsync_GetAnalysisResultDetailQuery_ReturnsMappedDto()
    {
        // Arrange
        var ar = new AnalysisResultContainer("ar-1", "user-1", "UrlAnalysis", DateTime.UtcNow, "{}", false, null);
        _analysisRepoMock.Setup(r => r.GetByKeyAsync(It.IsAny<Key>())).ReturnsAsync(ar);

        // Act
        var result = await _sut.HandleAsync(new GetAnalysisResultDetailQuery { AnalysisKey = new Key("AnalysisResultContainer", "ar-1") });

        // Assert
        result.Success.Should().BeTrue();
        result.AnalysisResult.Should().NotBeNull();
        result.AnalysisResult!.Discriminator.Should().Be("UrlAnalysis");
        result.AnalysisResult.JsonValue.Should().Be("{}");
    }

    [Fact]
    public async Task HandleAsync_GetAnalysisResultDetailQuery_WhenNotFound_ReturnsFailure()
    {
        // Arrange
        _analysisRepoMock.Setup(r => r.GetByKeyAsync(It.IsAny<Key>())).ReturnsAsync((AnalysisResultContainer?)null);

        // Act
        var result = await _sut.HandleAsync(new GetAnalysisResultDetailQuery { AnalysisKey = new Key("AnalysisResultContainer", "missing") });

        // Assert
        result.Success.Should().BeFalse();
        result.AnalysisResult.Should().BeNull();
    }
}
