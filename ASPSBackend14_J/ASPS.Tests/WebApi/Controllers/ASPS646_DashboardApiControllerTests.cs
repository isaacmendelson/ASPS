using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using WebApi.Controllers;
using WebApi.Services;
using Business.Queries;
using Common.Messaging;

namespace ASPS.Tests.WebApi.Controllers;

/// <summary>
/// ASPS-646 — Unit tests for DashboardApiController.
/// Tests input validation and response shaping; CQRS calls are mocked.
/// </summary>
public class ASPS646_DashboardApiControllerTests
{
    private readonly Mock<ICQRSClient> _cqrsMock = new();
    private readonly Mock<ILogger<DashboardApiController>> _loggerMock = new();

    private DashboardApiController CreateSut() =>
        new DashboardApiController(_cqrsMock.Object, _loggerMock.Object);

    [Fact]
    public async Task GetSummary_WhenSuccess_ReturnsOkWithKpiFields()
    {
        // Arrange
        _cqrsMock.Setup(c => c.SendQueryAsync<GetDashboardSummaryQueryResult>(It.IsAny<Query>()))
            .ReturnsAsync(new GetDashboardSummaryQueryResult
            {
                Success = true,
                TotalUsers = 42,
                TotalDevices = 17,
                ActiveAlerts24h = 5,
                AnalysisResultsCount = 100
            });

        var sut = CreateSut();

        // Act
        var actionResult = await sut.GetSummary();

        // Assert
        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value!;

        // Verify the anonymous object has the expected shape via reflection
        var type = body.GetType();
        type.GetProperty("totalUsers")!.GetValue(body).Should().Be(42);
        type.GetProperty("totalDevices")!.GetValue(body).Should().Be(17);
        type.GetProperty("activeAlerts24h")!.GetValue(body).Should().Be(5);
        type.GetProperty("analysisResultsCount")!.GetValue(body).Should().Be(100);
    }

    [Fact]
    public async Task GetSummary_WhenCqrsReturnsFailure_ReturnsBadRequest()
    {
        _cqrsMock.Setup(c => c.SendQueryAsync<GetDashboardSummaryQueryResult>(It.IsAny<Query>()))
            .ReturnsAsync(new GetDashboardSummaryQueryResult
            {
                Success = false,
                Message = "Backend unavailable"
            });

        var sut = CreateSut();

        var actionResult = await sut.GetSummary();

        actionResult.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetSummary_WhenCqrsThrows_Returns500()
    {
        _cqrsMock.Setup(c => c.SendQueryAsync<GetDashboardSummaryQueryResult>(It.IsAny<Query>()))
            .ThrowsAsync(new Exception("Network error"));

        var sut = CreateSut();

        var actionResult = await sut.GetSummary();

        var status = actionResult.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(500);
    }
}
