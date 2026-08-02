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

/// <summary>
/// ASPS-646 — Angular Admin: Dashboard + Users API
/// Unit tests for the three new AdminQueryHandlers methods:
///   - HandleAsync(GetDashboardSummaryQuery)
///   - HandleAsync(GetAllUsersPagedQuery)
///   - HandleAsync(GetUserAlertsByKeyQuery)
/// </summary>
public class ASPS646_AdminQueryHandlersTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IUserDeviceRepository> _deviceRepoMock = new();
    private readonly Mock<IDeviceAlertRepository> _alertRepoMock = new();
    private readonly Mock<IKnownPhishingWebsiteRepository> _phishingRepoMock = new();
    private readonly Mock<ITrackedDomainRepository> _trackedDomainRepoMock = new();
    private readonly Mock<IAnalysisResultRepository> _analysisRepoMock = new();
    private readonly Mock<ILogger<AdminQueryHandlers>> _loggerMock = new();

    private AdminQueryHandlers CreateSut() => new AdminQueryHandlers(
        _userRepoMock.Object,
        _deviceRepoMock.Object,
        _alertRepoMock.Object,
        _phishingRepoMock.Object,
        _trackedDomainRepoMock.Object,
        _analysisRepoMock.Object,
        _loggerMock.Object);

    // =========================================================================
    // GetDashboardSummaryQuery
    // =========================================================================

    [Fact]
    public async Task DashboardSummary_ReturnsCorrectKpiCounts()
    {
        // Arrange
        var users = new List<User> { new User(), new User(), new User() };
        var devices = new List<UserDevice> { new PersonalComputer(), new SmartPhone() };
        var alerts = new List<DeviceAlertEntity>
        {
            new UrlAlertEntity { Timestamp = DateTime.UtcNow.AddHours(-1) },   // within 24h
            new UrlAlertEntity { Timestamp = DateTime.UtcNow.AddHours(-48) }   // older
        };
        var analysis = new List<AnalysisResultContainer>
        {
            new AnalysisResultContainer("key1", "userA", "PhishingAnalysis", DateTime.UtcNow, null, false, null)
        };

        _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(users);
        _deviceRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(devices);
        _alertRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(alerts);
        _analysisRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(analysis);

        var sut = CreateSut();

        // Act
        var result = await sut.HandleAsync(new GetDashboardSummaryQuery());

        // Assert
        result.Success.Should().BeTrue();
        result.TotalUsers.Should().Be(3);
        result.TotalDevices.Should().Be(2);
        result.ActiveAlerts24h.Should().Be(1, "only 1 alert is within the last 24h");
        result.AnalysisResultsCount.Should().Be(1);
    }

    [Fact]
    public async Task DashboardSummary_WhenException_ReturnsFailure()
    {
        _userRepoMock.Setup(r => r.GetAllAsync()).ThrowsAsync(new Exception("DB down"));
        var sut = CreateSut();

        var result = await sut.HandleAsync(new GetDashboardSummaryQuery());

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Error");
    }

    [Fact]
    public async Task DashboardSummary_NoAlerts_ActiveAlerts24hIsZero()
    {
        _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());
        _deviceRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<UserDevice>());
        _alertRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<DeviceAlertEntity>());
        _analysisRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AnalysisResultContainer>());

        var sut = CreateSut();

        var result = await sut.HandleAsync(new GetDashboardSummaryQuery());

        result.Success.Should().BeTrue();
        result.ActiveAlerts24h.Should().Be(0);
        result.TotalUsers.Should().Be(0);
        result.TotalDevices.Should().Be(0);
    }

    // =========================================================================
    // GetAllUsersPagedQuery
    // =========================================================================

    [Fact]
    public async Task GetAllUsersPaged_DefaultPaging_ReturnsFirstPage()
    {
        // Arrange — 30 users, page 1 of 25
        var users = Enumerable.Range(1, 30).Select(i => new User
        {
            KeyField = $"user{i:D2}",
            FirstName = $"First{i:D2}",
            LastName = $"Last{i:D2}",
            Email = $"user{i:D2}@test.com"
        }).ToList();

        _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(users);
        _deviceRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<UserDevice>());

        var query = new GetAllUsersPagedQuery { Page = 1, PageSize = 25 };
        var sut = CreateSut();

        // Act
        var result = await sut.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Result.Items.Should().HaveCount(25);
        result.Result.TotalCount.Should().Be(30);
        result.Result.Page.Should().Be(1);
        result.Result.PageSize.Should().Be(25);
    }

    [Fact]
    public async Task GetAllUsersPaged_SecondPage_ReturnsRemainder()
    {
        var users = Enumerable.Range(1, 30).Select(i => new User
        {
            KeyField = $"user{i:D2}",
            LastName = $"Last{i:D2}"
        }).ToList();

        _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(users);
        _deviceRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<UserDevice>());

        var query = new GetAllUsersPagedQuery { Page = 2, PageSize = 25 };
        var sut = CreateSut();

        var result = await sut.HandleAsync(query);

        result.Success.Should().BeTrue();
        result.Result.Items.Should().HaveCount(5);
        result.Result.TotalCount.Should().Be(30);
    }

    [Fact]
    public async Task GetAllUsersPaged_SearchByFirstName_FiltersCorrectly()
    {
        var users = new List<User>
        {
            new User { KeyField = "u1", FirstName = "Alice", LastName = "Smith", Email = "alice@test.com" },
            new User { KeyField = "u2", FirstName = "Bob", LastName = "Jones", Email = "bob@test.com" },
            new User { KeyField = "u3", FirstName = "Charlie", LastName = "Brown", Email = "charlie@test.com" }
        };

        _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(users);
        _deviceRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<UserDevice>());

        var query = new GetAllUsersPagedQuery { Page = 1, PageSize = 25, Search = "ali" };
        var sut = CreateSut();

        var result = await sut.HandleAsync(query);

        result.Success.Should().BeTrue();
        result.Result.Items.Should().HaveCount(1);
        result.Result.Items[0].User.FirstName.Should().Be("Alice");
    }

    [Fact]
    public async Task GetAllUsersPaged_SearchByEmail_FiltersCorrectly()
    {
        var users = new List<User>
        {
            new User { KeyField = "u1", FirstName = "Alice", Email = "alice@company.com" },
            new User { KeyField = "u2", FirstName = "Bob", Email = "bob@other.org" }
        };

        _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(users);
        _deviceRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<UserDevice>());

        var query = new GetAllUsersPagedQuery { Page = 1, PageSize = 25, Search = "company.com" };
        var sut = CreateSut();

        var result = await sut.HandleAsync(query);

        result.Success.Should().BeTrue();
        result.Result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAllUsersPaged_IncludesDeviceCount()
    {
        var user1 = new User { KeyField = "u1", LastName = "Smith" };
        var user2 = new User { KeyField = "u2", LastName = "Jones" };
        var devices = new List<UserDevice>
        {
            new PersonalComputer { KeyField = "d1", UserKeyField = "u1" },
            new SmartPhone { KeyField = "d2", UserKeyField = "u1" },
            new SmartPhone { KeyField = "d3", UserKeyField = "u2" }
        };

        _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User> { user1, user2 });
        _deviceRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(devices);

        var query = new GetAllUsersPagedQuery
        {
            Page = 1, PageSize = 25,
            SortBy = "lastname", SortDirection = "asc"
        };
        var sut = CreateSut();

        var result = await sut.HandleAsync(query);

        result.Success.Should().BeTrue();
        var smithRow = result.Result.Items.First(u => u.User.KeyField == "u1");
        var jonesRow = result.Result.Items.First(u => u.User.KeyField == "u2");
        smithRow.DeviceCount.Should().Be(2);
        jonesRow.DeviceCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAllUsersPaged_SortByLastNameDesc_OrdersCorrectly()
    {
        var users = new List<User>
        {
            new User { KeyField = "u1", LastName = "Adams" },
            new User { KeyField = "u2", LastName = "Zelda" },
            new User { KeyField = "u3", LastName = "Morgan" }
        };

        _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(users);
        _deviceRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<UserDevice>());

        var query = new GetAllUsersPagedQuery { Page = 1, PageSize = 25, SortBy = "lastname", SortDirection = "desc" };
        var sut = CreateSut();

        var result = await sut.HandleAsync(query);

        result.Success.Should().BeTrue();
        result.Result.Items[0].User.LastName.Should().Be("Zelda");
        result.Result.Items[1].User.LastName.Should().Be("Morgan");
        result.Result.Items[2].User.LastName.Should().Be("Adams");
    }

    [Fact]
    public async Task GetAllUsersPaged_WhenException_ReturnsFailure()
    {
        _userRepoMock.Setup(r => r.GetAllAsync()).ThrowsAsync(new Exception("DB error"));
        var sut = CreateSut();

        var result = await sut.HandleAsync(new GetAllUsersPagedQuery());

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Error");
    }

    // =========================================================================
    // GetUserAlertsByKeyQuery
    // =========================================================================

    [Fact]
    public async Task GetUserAlerts_ReturnsOnlyAlertsForUserDevices()
    {
        var userKey = new Key("User", "userA");
        var devices = new List<UserDevice>
        {
            new PersonalComputer { KeyField = "dev1", UserKeyField = "userA", DeviceUid = "uid-A1" },
            new SmartPhone { KeyField = "dev2", UserKeyField = "userA", DeviceUid = "uid-A2" },
            new PersonalComputer { KeyField = "dev3", UserKeyField = "userB", DeviceUid = "uid-B1" }
        };
        var alerts = new List<DeviceAlertEntity>
        {
            new UrlAlertEntity { DeviceUid = "uid-A1", AlertType = "Phishing", Timestamp = DateTime.UtcNow.AddHours(-1) },
            new UrlAlertEntity { DeviceUid = "uid-A2", AlertType = "Scam", Timestamp = DateTime.UtcNow.AddHours(-2) },
            new UrlAlertEntity { DeviceUid = "uid-B1", AlertType = "OtherUser", Timestamp = DateTime.UtcNow.AddHours(-3) }
        };

        _deviceRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(devices);
        _alertRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(alerts);

        var query = new GetUserAlertsByKeyQuery
        {
            UserKey = userKey,
            Page = 1,
            PageSize = 25
        };
        var sut = CreateSut();

        var result = await sut.HandleAsync(query);

        result.Success.Should().BeTrue();
        result.Result.TotalCount.Should().Be(2, "only alerts from userA's devices");
        result.Result.Items.Should().NotContain(a => a.AlertType == "OtherUser");
    }

    [Fact]
    public async Task GetUserAlerts_SearchFiltersAlertType()
    {
        var userKey = new Key("User", "userA");
        var devices = new List<UserDevice>
        {
            new PersonalComputer { KeyField = "dev1", UserKeyField = "userA", DeviceUid = "uid-A1" }
        };
        var alerts = new List<DeviceAlertEntity>
        {
            new UrlAlertEntity { DeviceUid = "uid-A1", AlertType = "Phishing", Timestamp = DateTime.UtcNow.AddHours(-1) },
            new UrlAlertEntity { DeviceUid = "uid-A1", AlertType = "Scam", Timestamp = DateTime.UtcNow.AddHours(-2) }
        };

        _deviceRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(devices);
        _alertRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(alerts);

        var query = new GetUserAlertsByKeyQuery
        {
            UserKey = userKey,
            Page = 1, PageSize = 25,
            Search = "phish"
        };
        var sut = CreateSut();

        var result = await sut.HandleAsync(query);

        result.Success.Should().BeTrue();
        result.Result.TotalCount.Should().Be(1);
        result.Result.Items[0].AlertType.Should().Be("Phishing");
    }

    [Fact]
    public async Task GetUserAlerts_DefaultSortIsTimestampDescending()
    {
        var userKey = new Key("User", "userA");
        var devices = new List<UserDevice>
        {
            new PersonalComputer { KeyField = "dev1", UserKeyField = "userA", DeviceUid = "uid-A1" }
        };
        var older = new UrlAlertEntity { DeviceUid = "uid-A1", AlertType = "Old", Timestamp = DateTime.UtcNow.AddHours(-10) };
        var newer = new UrlAlertEntity { DeviceUid = "uid-A1", AlertType = "New", Timestamp = DateTime.UtcNow.AddHours(-1) };
        var alerts = new List<DeviceAlertEntity> { older, newer };

        _deviceRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(devices);
        _alertRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(alerts);

        var query = new GetUserAlertsByKeyQuery { UserKey = userKey, Page = 1, PageSize = 25 };
        var sut = CreateSut();

        var result = await sut.HandleAsync(query);

        result.Success.Should().BeTrue();
        result.Result.Items[0].AlertType.Should().Be("New", "most recent first");
        result.Result.Items[1].AlertType.Should().Be("Old");
    }

    [Fact]
    public async Task GetUserAlerts_UserWithNoDevices_ReturnsEmptyPaged()
    {
        var userKey = new Key("User", "userX");
        _deviceRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<UserDevice>());
        _alertRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<DeviceAlertEntity>());

        var query = new GetUserAlertsByKeyQuery { UserKey = userKey, Page = 1, PageSize = 25 };
        var sut = CreateSut();

        var result = await sut.HandleAsync(query);

        result.Success.Should().BeTrue();
        result.Result.TotalCount.Should().Be(0);
        result.Result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserAlerts_WhenException_ReturnsFailure()
    {
        _deviceRepoMock.Setup(r => r.GetAllAsync()).ThrowsAsync(new Exception("DB error"));
        var sut = CreateSut();

        var result = await sut.HandleAsync(new GetUserAlertsByKeyQuery
        {
            UserKey = new Key("User", "x"),
            Page = 1, PageSize = 25
        });

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Error");
    }
}
