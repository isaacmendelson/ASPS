using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using WebApi.Pages.DeviceAlerts;
using WebApi.Services;
using Business.Queries;
using Common.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ASPS.Tests.WebApi.Pages.DeviceAlerts;

public class IndexModelTests
{
    private readonly Mock<ICQRSClient> _cqrsClientMock;
    private readonly Mock<ILogger<IndexModel>> _loggerMock;
    private readonly IndexModel _sut;

    public IndexModelTests()
    {
        _cqrsClientMock = new Mock<ICQRSClient>();
        _loggerMock = new Mock<ILogger<IndexModel>>();
        _sut = new IndexModel(_cqrsClientMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        // Assert
        _sut.AlertsWithInfo.Should().NotBeNull();
        _sut.AlertsWithInfo.Should().BeEmpty();
        _sut.TimeFilter.Should().Be(24);
        _sut.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task OnGetAsync_WithValidAlerts_LoadsAlertsSuccessfully()
    {
        // Arrange
        var alerts = new List<DeviceAlertEntity>
        {
            new TrackUrlAlertEntity
            {
                KeyField = "alert1",
                Timestamp = DateTime.UtcNow,
                AlertType = "TrackUrlAlert",
                Url = "https://example.com/page",
                FromUrl = "https://example.com/home",
                Duration = 45,
                UserKeyField = "user1",
                DeviceKeyField = "device1",
                DeviceUid = "device-uid-123"
            }
        };

        var queryResult = new GetRecentAlertsQueryResult
        {
            Success = true,
            Alerts = alerts
        };

        _cqrsClientMock
            .Setup(x => x.SendQueryAsync<GetRecentAlertsQueryResult>(It.IsAny<GetRecentAlertsQuery>()))
            .ReturnsAsync(queryResult);

        // Mock user query
        _cqrsClientMock
            .Setup(x => x.SendQueryAsync<GetUserByKeyQueryResult>(It.IsAny<GetUserByKeyQuery>()))
            .ReturnsAsync(new GetUserByKeyQueryResult 
            { 
                Success = true, 
                User = new User 
                { 
                    FirstName = "John", 
                    LastName = "Doe" 
                } 
            });

        // Act
        await _sut.OnGetAsync(24);

        // Assert
        _sut.AlertsWithInfo.Should().HaveCount(1);
        _sut.AlertsWithInfo[0].Alert.Should().BeOfType<TrackUrlAlertEntity>();
        _sut.TimeFilter.Should().Be(24);
        _sut.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task OnGetAsync_WithTrackUrlAlert_LoadsAllRequiredFields()
    {
        // Arrange
        var trackAlert = new TrackUrlAlertEntity
        {
            KeyField = "alert1",
            Timestamp = DateTime.UtcNow,
            AlertType = "TrackUrlAlert",
            Url = "https://example.com/product",
            FromUrl = "https://example.com/category",
            Duration = 120,
            UserKeyField = "user1",
            DeviceKeyField = "device1",
            DeviceUid = "device-uid-456"
        };

        var queryResult = new GetRecentAlertsQueryResult
        {
            Success = true,
            Alerts = new List<DeviceAlertEntity> { trackAlert }
        };

        _cqrsClientMock
            .Setup(x => x.SendQueryAsync<GetRecentAlertsQueryResult>(It.IsAny<GetRecentAlertsQuery>()))
            .ReturnsAsync(queryResult);

        _cqrsClientMock
            .Setup(x => x.SendQueryAsync<GetUserByKeyQueryResult>(It.IsAny<GetUserByKeyQuery>()))
            .ReturnsAsync(new GetUserByKeyQueryResult 
            { 
                Success = true, 
                User = new User 
                { 
                    FirstName = "Jane", 
                    LastName = "Smith" 
                } 
            });

        // Act
        await _sut.OnGetAsync(null);

        // Assert
        _sut.AlertsWithInfo.Should().HaveCount(1);
        var loadedAlert = _sut.AlertsWithInfo[0].Alert as TrackUrlAlertEntity;
        loadedAlert.Should().NotBeNull();
        loadedAlert!.Url.Should().Be("https://example.com/product");
        loadedAlert.FromUrl.Should().Be("https://example.com/category");
        loadedAlert.Duration.Should().Be(120);
        _sut.AlertsWithInfo[0].UserName.Should().Be("Jane Smith");
    }

    [Fact]
    public async Task OnGetAsync_WithFailedQuery_SetsErrorMessage()
    {
        // Arrange
        var queryResult = new GetRecentAlertsQueryResult
        {
            Success = false,
            Message = "Failed to retrieve alerts"
        };

        _cqrsClientMock
            .Setup(x => x.SendQueryAsync<GetRecentAlertsQueryResult>(It.IsAny<GetRecentAlertsQuery>()))
            .ReturnsAsync(queryResult);

        // Act
        await _sut.OnGetAsync(24);

        // Assert
        _sut.ErrorMessage.Should().Be("Failed to retrieve alerts");
        _sut.AlertsWithInfo.Should().BeEmpty();
    }

    [Fact]
    public async Task OnGetAsync_WithSearchFilter_PassesSearchToQuery()
    {
        // Arrange
        _sut.Search = "test-search";
        GetRecentAlertsQuery? capturedQuery = null;

        _cqrsClientMock
            .Setup(x => x.SendQueryAsync<GetRecentAlertsQueryResult>(It.IsAny<GetRecentAlertsQuery>()))
            .Callback<GetRecentAlertsQuery>(q => capturedQuery = q)
            .ReturnsAsync(new GetRecentAlertsQueryResult { Success = true, Alerts = new List<DeviceAlertEntity>() });

        // Act
        await _sut.OnGetAsync(24);

        // Assert
        capturedQuery.Should().NotBeNull();
        capturedQuery!.Search.Should().Be("test-search");
    }

    [Fact]
    public async Task OnGetAsync_UsesDefaultTimeFilter_WhenHoursIsNull()
    {
        // Arrange
        _cqrsClientMock
            .Setup(x => x.SendQueryAsync<GetRecentAlertsQueryResult>(It.IsAny<GetRecentAlertsQuery>()))
            .ReturnsAsync(new GetRecentAlertsQueryResult { Success = true, Alerts = new List<DeviceAlertEntity>() });

        // Act
        await _sut.OnGetAsync(null);

        // Assert
        _sut.TimeFilter.Should().Be(24);
    }
}
