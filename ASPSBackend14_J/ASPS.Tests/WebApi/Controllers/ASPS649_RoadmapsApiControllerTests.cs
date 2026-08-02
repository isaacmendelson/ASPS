using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebApi.Controllers;
using WebApi.Services;
using Business.Commands;
using Business.Queries;
using Common.Messaging;
using Common.Models;

namespace ASPS.Tests.WebApi.Controllers;

/// <summary>
/// ASPS-649 — Unit tests for RoadmapsApiController.
/// </summary>
public class ASPS649_RoadmapsApiControllerTests
{
    private readonly Mock<ICQRSClient> _cqrsMock = new();
    private readonly Mock<ILogger<RoadmapsApiController>> _loggerMock = new();

    private RoadmapsApiController CreateSut() =>
        new RoadmapsApiController(_cqrsMock.Object, _loggerMock.Object);

    // =========================================================================
    // GET /api/roadmaps
    // =========================================================================

    [Fact]
    public async Task GetAll_ReturnsOkWithPagedResult()
    {
        var pagedResult = new PagedResult<RoadmapDto>
        {
            Items = new List<RoadmapDto> { new RoadmapDto { Id = 1, Name = "Master Roadmap" } },
            TotalCount = 1,
            Page = 1,
            PageSize = 25
        };

        _cqrsMock.Setup(c => c.SendQueryAsync<GetAllRoadmapsPagedQueryResult>(It.IsAny<Query>()))
            .ReturnsAsync(new GetAllRoadmapsPagedQueryResult { Success = true, Result = pagedResult });

        var result = await CreateSut().GetAll(new PagedRequest());

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<PagedResult<RoadmapDto>>()
            .Which.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAll_NormalizesRequest_ClampsPageSizeTo100()
    {
        GetAllRoadmapsPagedQuery? captured = null;
        _cqrsMock.Setup(c => c.SendQueryAsync<GetAllRoadmapsPagedQueryResult>(It.IsAny<Query>()))
            .Callback<Query>(q => captured = (GetAllRoadmapsPagedQuery)q)
            .ReturnsAsync(new GetAllRoadmapsPagedQueryResult
            {
                Success = true,
                Result = new PagedResult<RoadmapDto>()
            });

        await CreateSut().GetAll(new PagedRequest { Page = 0, PageSize = 9999 });

        captured!.Page.Should().Be(1);
        captured.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task GetAll_DefaultExcludesArchived()
    {
        GetAllRoadmapsPagedQuery? captured = null;
        _cqrsMock.Setup(c => c.SendQueryAsync<GetAllRoadmapsPagedQueryResult>(It.IsAny<Query>()))
            .Callback<Query>(q => captured = (GetAllRoadmapsPagedQuery)q)
            .ReturnsAsync(new GetAllRoadmapsPagedQueryResult
            {
                Success = true,
                Result = new PagedResult<RoadmapDto>()
            });

        await CreateSut().GetAll(new PagedRequest());

        captured!.IncludeArchived.Should().BeFalse();
    }

    [Fact]
    public async Task GetAll_IncludeArchivedTrue_PassesToQuery()
    {
        GetAllRoadmapsPagedQuery? captured = null;
        _cqrsMock.Setup(c => c.SendQueryAsync<GetAllRoadmapsPagedQueryResult>(It.IsAny<Query>()))
            .Callback<Query>(q => captured = (GetAllRoadmapsPagedQuery)q)
            .ReturnsAsync(new GetAllRoadmapsPagedQueryResult
            {
                Success = true,
                Result = new PagedResult<RoadmapDto>()
            });

        await CreateSut().GetAll(new PagedRequest(), includeArchived: true);

        captured!.IncludeArchived.Should().BeTrue();
    }

    [Fact]
    public async Task GetAll_WhenFailure_ReturnsBadRequest()
    {
        _cqrsMock.Setup(c => c.SendQueryAsync<GetAllRoadmapsPagedQueryResult>(It.IsAny<Query>()))
            .ReturnsAsync(new GetAllRoadmapsPagedQueryResult { Success = false, Message = "Error" });

        var result = await CreateSut().GetAll(new PagedRequest());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetAll_WhenException_Returns500()
    {
        _cqrsMock.Setup(c => c.SendQueryAsync<GetAllRoadmapsPagedQueryResult>(It.IsAny<Query>()))
            .ThrowsAsync(new Exception("timeout"));

        var result = await CreateSut().GetAll(new PagedRequest());

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(500);
    }

    // =========================================================================
    // POST /api/roadmaps
    // =========================================================================

    [Fact]
    public async Task Create_WhenSuccess_Returns201WithId()
    {
        _cqrsMock.Setup(c => c.SendCommandAsync<CreateRoadmapCommandResult>(It.IsAny<Command>()))
            .ReturnsAsync(new CreateRoadmapCommandResult { Success = true, RoadmapId = 42 });

        var result = await CreateSut().Create(new CreateRoadmapRequest { Name = "New Roadmap" });

        var created = result.Should().BeOfType<ObjectResult>().Subject;
        created.StatusCode.Should().Be(201);
        var body = created.Value!;
        body.GetType().GetProperty("id")!.GetValue(body).Should().Be(42);
    }

    [Fact]
    public async Task Create_WhenNameIsEmpty_ReturnsBadRequest()
    {
        var result = await CreateSut().Create(new CreateRoadmapRequest { Name = "" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_WhenCqrsFailure_ReturnsBadRequest()
    {
        _cqrsMock.Setup(c => c.SendCommandAsync<CreateRoadmapCommandResult>(It.IsAny<Command>()))
            .ReturnsAsync(new CreateRoadmapCommandResult { Success = false, Message = "Name too long" });

        var result = await CreateSut().Create(new CreateRoadmapRequest { Name = "A" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // =========================================================================
    // POST /api/roadmaps/{id}/archive
    // =========================================================================

    [Fact]
    public async Task Archive_WhenSuccess_ReturnsOk()
    {
        _cqrsMock.Setup(c => c.SendCommandAsync<ArchiveRoadmapCommandResult>(It.IsAny<Command>()))
            .ReturnsAsync(new ArchiveRoadmapCommandResult { Success = true });

        var result = await CreateSut().Archive(1);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Archive_WhenNotFound_ReturnsBadRequest()
    {
        _cqrsMock.Setup(c => c.SendCommandAsync<ArchiveRoadmapCommandResult>(It.IsAny<Command>()))
            .ReturnsAsync(new ArchiveRoadmapCommandResult { Success = false, Message = "Roadmap not found" });

        var result = await CreateSut().Archive(999);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Archive_WhenException_Returns500()
    {
        _cqrsMock.Setup(c => c.SendCommandAsync<ArchiveRoadmapCommandResult>(It.IsAny<Command>()))
            .ThrowsAsync(new Exception("timeout"));

        var result = await CreateSut().Archive(1);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(500);
    }
}
