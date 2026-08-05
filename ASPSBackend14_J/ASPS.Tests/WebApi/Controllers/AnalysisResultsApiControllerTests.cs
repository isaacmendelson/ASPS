using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Business.Queries;
using Common.Messaging;
using Common.Models;
using WebApi.Controllers;
using WebApi.Services;

namespace ASPS.Tests.WebApi.Controllers;

/// <summary>
/// Unit tests for AnalysisResultsApiController (ASPS-647).
/// </summary>
public class AnalysisResultsApiControllerTests
{
    private readonly Mock<ICQRSClient> _cqrsClientMock;
    private readonly Mock<ILogger<AnalysisResultsApiController>> _loggerMock;
    private readonly AnalysisResultsApiController _controller;

    public AnalysisResultsApiControllerTests()
    {
        _cqrsClientMock = new Mock<ICQRSClient>();
        _loggerMock = new Mock<ILogger<AnalysisResultsApiController>>();
        _controller = new AnalysisResultsApiController(_cqrsClientMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetAll_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var pagedResult = new GetAllAnalysisResultsPagedQueryResult
        {
            Success = true,
            Result = new PagedResult<AnalysisResultDto>
            {
                Items = new List<AnalysisResultDto>
                {
                    new AnalysisResultDto { Discriminator = "UrlAnalysis" }
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 25
            }
        };

        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetAllAnalysisResultsPagedQueryResult>(It.IsAny<GetAllAnalysisResultsPagedQuery>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAll(new PagedRequest(), from: null, to: null, isFromCache: null, hasError: null, deviceType: null);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var paged = ok.Value as PagedResult<AnalysisResultDto>;
        paged!.TotalCount.Should().Be(1);
        paged.Items[0].Discriminator.Should().Be("UrlAnalysis");
    }

    [Fact]
    public async Task GetAll_WhenQueryFails_ReturnsBadRequest()
    {
        // Arrange
        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetAllAnalysisResultsPagedQueryResult>(It.IsAny<GetAllAnalysisResultsPagedQuery>()))
            .ReturnsAsync(new GetAllAnalysisResultsPagedQueryResult { Success = false, Message = "Error" });

        // Act
        var result = await _controller.GetAll(new PagedRequest(), from: null, to: null, isFromCache: null, hasError: null, deviceType: null);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetAll_PassesNormalizedPagingToQuery()
    {
        // Arrange
        GetAllAnalysisResultsPagedQuery? captured = null;

        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetAllAnalysisResultsPagedQueryResult>(It.IsAny<GetAllAnalysisResultsPagedQuery>()))
            .Callback<Query>(q => captured = (GetAllAnalysisResultsPagedQuery)q)
            .ReturnsAsync(new GetAllAnalysisResultsPagedQueryResult
            {
                Success = true,
                Result = new PagedResult<AnalysisResultDto>()
            });

        // Act: pass an out-of-range page (0) — should be normalized to 1
        await _controller.GetAll(new PagedRequest { Page = 0, PageSize = 200 }, from: null, to: null, isFromCache: null, hasError: null, deviceType: null);

        // Assert
        captured!.Page.Should().Be(1);
        captured.PageSize.Should().Be(100); // clamped to max
    }

    [Fact]
    public async Task GetByKey_WhenFound_ReturnsOk()
    {
        // Arrange
        var dto = new AnalysisResultDto { Discriminator = "UrlAnalysis", JsonValue = "{}" };

        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetAnalysisResultDetailQueryResult>(It.IsAny<GetAnalysisResultDetailQuery>()))
            .ReturnsAsync(new GetAnalysisResultDetailQueryResult { Success = true, AnalysisResult = dto });

        // Act
        var result = await _controller.GetByKey("AnalysisResultContainer", "ar-1");

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        ok.Value.Should().BeOfType<AnalysisResultDto>();
        ((AnalysisResultDto)ok.Value!).JsonValue.Should().Be("{}");
    }

    [Fact]
    public async Task GetByKey_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetAnalysisResultDetailQueryResult>(It.IsAny<GetAnalysisResultDetailQuery>()))
            .ReturnsAsync(new GetAnalysisResultDetailQueryResult { Success = false, AnalysisResult = null });

        // Act
        var result = await _controller.GetByKey("AnalysisResultContainer", "missing");

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetByKey_WhenExceptionThrown_Returns500()
    {
        // Arrange
        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetAnalysisResultDetailQueryResult>(It.IsAny<GetAnalysisResultDetailQuery>()))
            .ThrowsAsync(new Exception("CQRS timeout"));

        // Act
        var result = await _controller.GetByKey("AnalysisResultContainer", "ar-1");

        // Assert
        result.Should().BeOfType<ObjectResult>();
        ((ObjectResult)result).StatusCode.Should().Be(500);
    }
}
