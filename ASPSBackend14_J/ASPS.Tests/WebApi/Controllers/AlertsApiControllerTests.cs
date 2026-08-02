using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Business.Queries;
using Common.Enums;
using Common.Messaging;
using Common.Models;
using WebApi.Controllers;
using WebApi.Services;

namespace ASPS.Tests.WebApi.Controllers;

/// <summary>
/// Unit tests for AlertsApiController (ASPS-647).
/// </summary>
public class AlertsApiControllerTests
{
    private readonly Mock<ICQRSClient> _cqrsClientMock;
    private readonly Mock<ILogger<AlertsApiController>> _loggerMock;
    private readonly AlertsApiController _controller;

    public AlertsApiControllerTests()
    {
        _cqrsClientMock = new Mock<ICQRSClient>();
        _loggerMock = new Mock<ILogger<AlertsApiController>>();
        _controller = new AlertsApiController(_cqrsClientMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetAll_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var pagedResult = new GetAllAlertsPagedQueryResult
        {
            Success = true,
            Result = new PagedResult<AlertDto>
            {
                Items = new List<AlertDto>
                {
                    new AlertDto { AlertType = "UrlAlert", Priority = Priority.High }
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 25
            }
        };

        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetAllAlertsPagedQueryResult>(It.IsAny<GetAllAlertsPagedQuery>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAll(new PagedRequest(), from: null, to: null, severity: null);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var paged = ok.Value as PagedResult<AlertDto>;
        paged.Should().NotBeNull();
        paged!.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAll_WithFromFilter_PassesFilterToQuery()
    {
        // Arrange
        var fromDate = DateTime.UtcNow.AddDays(-1);
        GetAllAlertsPagedQuery? capturedQuery = null;

        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetAllAlertsPagedQueryResult>(It.IsAny<GetAllAlertsPagedQuery>()))
            .Callback<Query>(q => capturedQuery = (GetAllAlertsPagedQuery)q)
            .ReturnsAsync(new GetAllAlertsPagedQueryResult
            {
                Success = true,
                Result = new PagedResult<AlertDto>()
            });

        // Act
        await _controller.GetAll(new PagedRequest(), from: fromDate, to: null, severity: null);

        // Assert
        capturedQuery.Should().NotBeNull();
        capturedQuery!.From.Should().BeCloseTo(fromDate, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetAll_WithSeverityFilter_PassesFilterToQuery()
    {
        // Arrange
        GetAllAlertsPagedQuery? capturedQuery = null;

        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetAllAlertsPagedQueryResult>(It.IsAny<GetAllAlertsPagedQuery>()))
            .Callback<Query>(q => capturedQuery = (GetAllAlertsPagedQuery)q)
            .ReturnsAsync(new GetAllAlertsPagedQueryResult
            {
                Success = true,
                Result = new PagedResult<AlertDto>()
            });

        // Act
        await _controller.GetAll(new PagedRequest(), from: null, to: null, severity: "High");

        // Assert
        capturedQuery!.Severity.Should().Be("High");
    }

    [Fact]
    public async Task GetAll_WhenQueryFails_ReturnsBadRequest()
    {
        // Arrange
        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetAllAlertsPagedQueryResult>(It.IsAny<GetAllAlertsPagedQuery>()))
            .ReturnsAsync(new GetAllAlertsPagedQueryResult { Success = false, Message = "Error" });

        // Act
        var result = await _controller.GetAll(new PagedRequest(), null, null, null);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetByKey_WhenAlertFound_ReturnsOk()
    {
        // Arrange
        var alertDto = new AlertDto { AlertType = "UrlAlert", Priority = Priority.Medium };

        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetAlertDetailQueryResult>(It.IsAny<GetAlertDetailQuery>()))
            .ReturnsAsync(new GetAlertDetailQueryResult { Success = true, Alert = alertDto });

        // Act
        var result = await _controller.GetByKey("DeviceAlertEntity", "alert-1");

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        ok.Value.Should().BeOfType<AlertDto>();
    }

    [Fact]
    public async Task GetByKey_WhenAlertNotFound_ReturnsNotFound()
    {
        // Arrange
        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetAlertDetailQueryResult>(It.IsAny<GetAlertDetailQuery>()))
            .ReturnsAsync(new GetAlertDetailQueryResult { Success = false, Alert = null, Message = "Not found" });

        // Act
        var result = await _controller.GetByKey("DeviceAlertEntity", "missing");

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetAnalysis_WhenAnalysisExists_ReturnsOk()
    {
        // Arrange
        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetAnalysisResultByAlertKeyQueryResult>(It.IsAny<GetAnalysisResultByAlertKeyQuery>()))
            .ReturnsAsync(new GetAnalysisResultByAlertKeyQueryResult
            {
                Success = true,
                AnalysisResult = null  // no analysis is valid
            });

        // Act
        var result = await _controller.GetAnalysis("DeviceAlertEntity", "alert-1");

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetAnalysis_WhenExceptionThrown_Returns500()
    {
        // Arrange
        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetAnalysisResultByAlertKeyQueryResult>(It.IsAny<GetAnalysisResultByAlertKeyQuery>()))
            .ThrowsAsync(new Exception("Network error"));

        // Act
        var result = await _controller.GetAnalysis("DeviceAlertEntity", "alert-1");

        // Assert
        result.Should().BeOfType<ObjectResult>();
        ((ObjectResult)result).StatusCode.Should().Be(500);
    }
}
