using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Business.Handlers;
using Business.Queries;
using Common.Entities;
using Common.Models;
using Interface.Repositories;

namespace ASPS.Tests.Business.Handlers;

public class AdminQueryHandlersTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUserDeviceRepository> _userDeviceRepositoryMock;
    private readonly Mock<IDeviceAlertRepository> _deviceAlertRepositoryMock;
    private readonly Mock<IKnownPhishingWebsiteRepository> _phishingWebsiteRepositoryMock;
    private readonly Mock<ITrackedDomainRepository> _trackedDomainRepositoryMock;
    private readonly Mock<IAnalysisResultRepository> _analysisResultRepositoryMock;
    private readonly Mock<ILogger<AdminQueryHandlers>> _loggerMock;
    private readonly AdminQueryHandlers _sut;

    public AdminQueryHandlersTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _userDeviceRepositoryMock = new Mock<IUserDeviceRepository>();
        _deviceAlertRepositoryMock = new Mock<IDeviceAlertRepository>();
        _phishingWebsiteRepositoryMock = new Mock<IKnownPhishingWebsiteRepository>();
        _trackedDomainRepositoryMock = new Mock<ITrackedDomainRepository>();
        _analysisResultRepositoryMock = new Mock<IAnalysisResultRepository>();
        _loggerMock = new Mock<ILogger<AdminQueryHandlers>>();

        _sut = new AdminQueryHandlers(
            _userRepositoryMock.Object,
            _userDeviceRepositoryMock.Object,
            _deviceAlertRepositoryMock.Object,
            _phishingWebsiteRepositoryMock.Object,
            _trackedDomainRepositoryMock.Object,
            _analysisResultRepositoryMock.Object,
            _loggerMock.Object);
    }

    #region GetDashboardStatsQuery Tests

    [Fact]
    public async Task HandleAsync_GetDashboardStatsQuery_ReturnsCorrectCounts()
    {
        // Arrange
        var users = new List<User> { new User(), new User() };
        var devices = new List<UserDevice> { new PersonalComputer(), new SmartPhone(), new PersonalComputer() };
        var alerts = new List<DeviceAlertEntity>
        {
            new UrlAlertEntity { Timestamp = DateTime.UtcNow.AddHours(-1) },
            new UrlAlertEntity { Timestamp = DateTime.UtcNow.AddHours(-48) }
        };
        var phishing = new List<KnownPhishingWebsite>();
        var mockPhish = new Mock<KnownPhishingWebsite>();
        phishing.Add(mockPhish.Object);

        _userRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(users);
        _userDeviceRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(devices);
        _deviceAlertRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(alerts);
        _phishingWebsiteRepositoryMock.Setup(r => r.GetAllActiveAsync()).ReturnsAsync(phishing);

        var query = new GetDashboardStatsQuery();

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.UsersCount.Should().Be(2);
        result.DevicesCount.Should().Be(3);
        result.AlertsCount24h.Should().Be(1);
        result.PhishingWebsitesCount.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_GetDashboardStatsQuery_WhenException_ReturnsFailure()
    {
        // Arrange
        _userRepositoryMock.Setup(r => r.GetAllAsync()).ThrowsAsync(new Exception("DB error"));
        var query = new GetDashboardStatsQuery();

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Error");
    }

    #endregion

    #region GetAllDevicesQuery Tests

    [Fact]
    public async Task HandleAsync_GetAllDevicesQuery_ReturnsAllDevices()
    {
        // Arrange
        var user = new User { KeyField = "user1", FirstName = "John", LastName = "Doe" };
        var devices = new List<UserDevice>
        {
            new SmartPhone { DeviceUid = "device1", UserKeyField = "user1", Model = "iPhone" },
            new SmartPhone { DeviceUid = "device2", UserKeyField = null, Model = "Android" }
        };

        _userRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User> { user });
        _userDeviceRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(devices);

        var query = new GetAllDevicesQuery();

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Devices.Should().HaveCount(2);
        result.Devices[0].UserName.Should().Be("John Doe");
        result.Devices[1].UserName.Should().Be("Unregistered");
    }

    [Fact]
    public async Task HandleAsync_GetAllDevicesQuery_WithSearch_FiltersResults()
    {
        // Arrange
        var devices = new List<UserDevice>
        {
            new SmartPhone { DeviceUid = "device-iphone", Model = "iPhone 12" },
            new SmartPhone { DeviceUid = "device-android", Model = "Pixel 6" }
        };

        _userRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());
        _userDeviceRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(devices);

        var query = new GetAllDevicesQuery { Search = "iphone" };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Devices.Should().HaveCount(1);
        result.Devices[0].Device.Model.Should().Be("iPhone 12");
    }

    #endregion

    #region GetAllPhishingWebsitesQuery Tests

    [Fact]
    public async Task HandleAsync_GetAllPhishingWebsitesQuery_ReturnsActiveWebsites()
    {
        // Arrange
        var websites = new List<KnownPhishingWebsite>();
        var mock1 = new Mock<KnownPhishingWebsite>();
        var mock2 = new Mock<KnownPhishingWebsite>();
        websites.Add(mock1.Object);
        websites.Add(mock2.Object);

        _phishingWebsiteRepositoryMock.Setup(r => r.GetAllActiveAsync()).ReturnsAsync(websites);

        var query = new GetAllPhishingWebsitesQuery();

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.PhishingWebsites.Should().HaveCount(2);
    }

    #endregion

    #region GetUsersWithDeviceCountsQuery Tests

    [Fact]
    public async Task HandleAsync_GetUsersWithDeviceCountsQuery_CalculatesDeviceCounts()
    {
        // Arrange
        var users = new List<User>
        {
            new User { KeyField = "user1", FirstName = "John" },
            new User { KeyField = "user2", FirstName = "Jane" }
        };

        var devices = new List<UserDevice>
        {
            new PersonalComputer { UserKeyField = "user1" },
            new SmartPhone { UserKeyField = "user1" },
            new PersonalComputer { UserKeyField = "user2" }
        };

        _userRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(users);
        _userDeviceRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(devices);

        var query = new GetUsersWithDeviceCountsQuery();

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Users.Should().HaveCount(2);
        result.Users[0].DeviceCount.Should().Be(2);
        result.Users[1].DeviceCount.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_GetUsersWithDeviceCountsQuery_WithSearch_FiltersUsers()
    {
        // Arrange
        var users = new List<User>
        {
            new User { KeyField = "user1", FirstName = "John", LastName = "Smith" },
            new User { KeyField = "user2", FirstName = "Jane", LastName = "Doe" }
        };

        _userRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(users);
        _userDeviceRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<UserDevice>());

        var query = new GetUsersWithDeviceCountsQuery { Search = "jane" };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Users.Should().HaveCount(1);
        result.Users[0].User.FirstName.Should().Be("Jane");
    }

    #endregion

    #region GetRecentAlertsQuery Tests

    [Fact]
    public async Task HandleAsync_GetRecentAlertsQuery_ReturnsAlertsWithinTimeRange()
    {
        // Arrange
        var alerts = new List<DeviceAlertEntity>
        {
            new UrlAlertEntity { Timestamp = DateTime.UtcNow.AddHours(-2), AlertType = "Phishing" },
            new RemoteAccessAlertEntity { Timestamp = DateTime.UtcNow.AddHours(-10), AlertType = "Malware" },
            new UrlAlertEntity { Timestamp = DateTime.UtcNow.AddHours(-50), AlertType = "Spam" }
        };

        _deviceAlertRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(alerts);

        var query = new GetRecentAlertsQuery { Hours = 24 };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Alerts.Should().HaveCount(2);
        result.Alerts[0].Timestamp.Should().BeAfter(DateTime.UtcNow.AddHours(-24));
    }

    [Fact]
    public async Task HandleAsync_GetRecentAlertsQuery_WithSearch_FiltersAlerts()
    {
        // Arrange
        var alerts = new List<DeviceAlertEntity>
        {
            new UrlAlertEntity { Timestamp = DateTime.UtcNow.AddHours(-1), AlertType = "Phishing" },
            new RemoteAccessAlertEntity { Timestamp = DateTime.UtcNow.AddHours(-2), AlertType = "Malware" }
        };

        _deviceAlertRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(alerts);

        var query = new GetRecentAlertsQuery { Hours = 24, Search = "phish" };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Alerts.Should().HaveCount(1);
        result.Alerts[0].AlertType.Should().Be("Phishing");
    }

    #endregion

    #region GetDevicesByUserQuery Tests

    [Fact]
    public async Task HandleAsync_GetDevicesByUserQuery_ReturnsUserDevices()
    {
        // Arrange
        var devices = new List<UserDevice>
        {
            new PersonalComputer { DeviceUid = "device1" },
            new SmartPhone { DeviceUid = "device2" }
        };

        _userDeviceRepositoryMock.Setup(r => r.GetByUserKeyAsync(It.IsAny<Key>())).ReturnsAsync(devices);

        var query = new GetDevicesByUserQuery { UserKey = new Key("User", "user-key") };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Devices.Should().HaveCount(2);
    }

    #endregion

    #region GetDeviceByKeyQuery Tests

    [Fact]
    public async Task HandleAsync_GetDeviceByKeyQuery_WhenDeviceExists_ReturnsDevice()
    {
        // Arrange
        var device = new PersonalComputer();
        _userDeviceRepositoryMock.Setup(r => r.GetByKeyAsync(It.IsAny<Key>())).ReturnsAsync(device);

        var query = new GetDeviceByKeyQuery { DeviceKey = new Key("Device", "device-key") };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Device.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_GetDeviceByKeyQuery_WhenDeviceNotFound_ReturnsFailure()
    {
        // Arrange
        _userDeviceRepositoryMock.Setup(r => r.GetByKeyAsync(It.IsAny<Key>())).ReturnsAsync((UserDevice)null);

        var query = new GetDeviceByKeyQuery { DeviceKey = new Key("Device", "missing") };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Device not found");
    }

    #endregion

    #region GetDeviceByUidQuery Tests

    [Fact]
    public async Task HandleAsync_GetDeviceByUidQuery_WhenDeviceExists_ReturnsDevice()
    {
        // Arrange
        var device = new PersonalComputer { DeviceUid = "uid-123" };
        _userDeviceRepositoryMock.Setup(r => r.GetByDeviceUidAsync("uid-123")).ReturnsAsync(device);

        var query = new GetDeviceByUidQuery { DeviceUid = "uid-123" };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Device.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_GetDeviceByUidQuery_WhenDeviceNotFound_ReturnsFailure()
    {
        // Arrange
        _userDeviceRepositoryMock.Setup(r => r.GetByDeviceUidAsync("missing")).ReturnsAsync((UserDevice)null);

        var query = new GetDeviceByUidQuery { DeviceUid = "missing" };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Device not found");
    }

    #endregion

    #region GetAlertsByDeviceQuery Tests

    [Fact]
    public async Task HandleAsync_GetAlertsByDeviceQuery_ReturnsDeviceAlerts()
    {
        // Arrange
        var alerts = new List<DeviceAlertEntity>
        {
            new UrlAlertEntity { DeviceUid = "device1", Timestamp = DateTime.UtcNow.AddHours(-1) },
            new UrlAlertEntity { DeviceUid = "device1", Timestamp = DateTime.UtcNow.AddHours(-50) },
            new RemoteAccessAlertEntity { DeviceUid = "device2", Timestamp = DateTime.UtcNow.AddHours(-1) }
        };

        _deviceAlertRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(alerts);

        var query = new GetAlertsByDeviceQuery { DeviceUid = "device1", Hours = 24 };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Alerts.Should().HaveCount(1);
        result.Alerts[0].DeviceUid.Should().Be("device1");
    }

    #endregion

    #region GetAlertByKeyQuery Tests

    [Fact]
    public async Task HandleAsync_GetAlertByKeyQuery_WhenAlertExists_ReturnsAlert()
    {
        // Arrange
        var alert = new UrlAlertEntity();
        _deviceAlertRepositoryMock.Setup(r => r.GetByKeyAsync(It.IsAny<Key>())).ReturnsAsync(alert);

        var query = new GetAlertByKeyQuery { AlertKey = new Key("Alert", "alert-key", null) };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Alert.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_GetAlertByKeyQuery_WhenAlertNotFound_ReturnsFailure()
    {
        // Arrange
        _deviceAlertRepositoryMock.Setup(r => r.GetByKeyAsync(It.IsAny<Key>())).ReturnsAsync((DeviceAlertEntity)null);

        var query = new GetAlertByKeyQuery { AlertKey = new Key("Alert", "missing", null) };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Alert not found");
    }

    #endregion

    #region GetAllAnalysisResultsQuery Tests

    [Fact]
    public async Task HandleAsync_GetAllAnalysisResultsQuery_ReturnsRecentResults()
    {
        // Arrange
        var results = new List<AnalysisResultContainer>();
        var result1 = new AnalysisResultContainer("key1", "user1", "test", DateTime.UtcNow.AddHours(-1), null, false, null);
        var result2 = new AnalysisResultContainer("key2", "user2", "test", DateTime.UtcNow.AddHours(-50), null, false, null);
        results.Add(result1);
        results.Add(result2);

        _analysisResultRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(results);

        var query = new GetAllAnalysisResultsQuery { Hours = 24 };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.AnalysisResults.Should().HaveCount(1);
    }

    #endregion

    #region GetAnalysisResultByAlertKeyQuery Tests

    [Fact]
    public async Task HandleAsync_GetAnalysisResultByAlertKeyQuery_ReturnsMatchingResult()
    {
        // Arrange
        var results = new List<AnalysisResultContainer>();
        var result1 = new AnalysisResultContainer("key1", "user1", "test", DateTime.UtcNow, null, false, null, false, "alert1");
        var result2 = new AnalysisResultContainer("key2", "user2", "test", DateTime.UtcNow, null, false, null, false, "alert2");
        results.Add(result1);
        results.Add(result2);

        _analysisResultRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(results);

        var query = new GetAnalysisResultByAlertKeyQuery { DeviceAlertKeyField = "alert1" };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.AnalysisResult.Should().NotBeNull();
        result.AnalysisResult.DeviceAlertKeyField.Should().Be("alert1");
    }

    #endregion

    #region GetAllTrackedDomainsQuery Tests

    [Fact]
    public async Task HandleAsync_GetAllTrackedDomainsQuery_ReturnsPaginatedResults()
    {
        // Arrange
        var domains = Enumerable.Range(1, 50).Select(i =>
        {
            return new TrackedDomain($"domain{i}.com", "test");
        }).ToList();

        _trackedDomainRepositoryMock.Setup(r => r.GetAllActiveAsync()).ReturnsAsync(domains);

        var query = new GetAllTrackedDomainsQuery { Page = 1, PageSize = 10 };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.TrackedDomains.Should().HaveCount(10);
        result.TotalCount.Should().Be(50);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task HandleAsync_GetAllTrackedDomainsQuery_WithCategoryFilter_FiltersResults()
    {
        // Arrange
        var domains = new List<TrackedDomain>
        {
            new TrackedDomain("domain1.com", "banking"),
            new TrackedDomain("domain2.com", "social")
        };

        _trackedDomainRepositoryMock.Setup(r => r.GetAllActiveAsync()).ReturnsAsync(domains);

        var query = new GetAllTrackedDomainsQuery { Category = "banking", Page = 1, PageSize = 10 };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.TrackedDomains.Should().HaveCount(1);
        result.TrackedDomains[0].Category.Should().Be("banking");
    }

    [Fact]
    public async Task HandleAsync_GetAllTrackedDomainsQuery_WithSearch_FiltersResults()
    {
        // Arrange
        var domains = new List<TrackedDomain>
        {
            new TrackedDomain("example.com", "test"),
            new TrackedDomain("other.com", "test")
        };

        _trackedDomainRepositoryMock.Setup(r => r.GetAllActiveAsync()).ReturnsAsync(domains);

        var query = new GetAllTrackedDomainsQuery { Search = "example", Page = 1, PageSize = 10 };

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.TrackedDomains.Should().HaveCount(1);
        result.TrackedDomains[0].Domain.Should().Be("example.com");
    }

    #endregion
}
