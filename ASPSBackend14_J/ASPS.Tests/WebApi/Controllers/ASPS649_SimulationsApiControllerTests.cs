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
/// ASPS-649 — Unit tests for SimulationsApiController CRUD + run endpoints.
/// </summary>
public class ASPS649_SimulationsApiControllerTests
{
    private readonly Mock<ICQRSClient> _cqrsMock = new();
    private readonly Mock<ILogger<SimulationsApiController>> _loggerMock = new();

    private SimulationsApiController CreateSut() =>
        new SimulationsApiController(_cqrsMock.Object, _loggerMock.Object);

    // =========================================================================
    // GET /api/simulations — paged list
    // =========================================================================

    [Fact]
    public async Task GetAll_ReturnsOkWithPagedResult()
    {
        var pagedResult = new PagedResult<SimulationDto>
        {
            Items = new List<SimulationDto> { new SimulationDto { Name = "Sim A" } },
            TotalCount = 1,
            Page = 1,
            PageSize = 25
        };

        _cqrsMock.Setup(c => c.SendQueryAsync<GetAllSimulationsPagedQueryResult>(It.IsAny<Query>()))
            .ReturnsAsync(new GetAllSimulationsPagedQueryResult { Success = true, Result = pagedResult });

        var result = await CreateSut().GetAll(new PagedRequest());

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<PagedResult<SimulationDto>>()
            .Which.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAll_NormalizesRequest_ClampsPageSizeTo100()
    {
        GetAllSimulationsPagedQuery? captured = null;
        _cqrsMock.Setup(c => c.SendQueryAsync<GetAllSimulationsPagedQueryResult>(It.IsAny<Query>()))
            .Callback<Query>(q => captured = (GetAllSimulationsPagedQuery)q)
            .ReturnsAsync(new GetAllSimulationsPagedQueryResult
            {
                Success = true,
                Result = new PagedResult<SimulationDto>()
            });

        await CreateSut().GetAll(new PagedRequest { Page = 0, PageSize = 9999 });

        captured!.Page.Should().Be(1);
        captured.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task GetAll_WhenFailure_ReturnsBadRequest()
    {
        _cqrsMock.Setup(c => c.SendQueryAsync<GetAllSimulationsPagedQueryResult>(It.IsAny<Query>()))
            .ReturnsAsync(new GetAllSimulationsPagedQueryResult { Success = false, Message = "Error" });

        var result = await CreateSut().GetAll(new PagedRequest());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetAll_WhenException_Returns500()
    {
        _cqrsMock.Setup(c => c.SendQueryAsync<GetAllSimulationsPagedQueryResult>(It.IsAny<Query>()))
            .ThrowsAsync(new Exception("timeout"));

        var result = await CreateSut().GetAll(new PagedRequest());

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(500);
    }

    // =========================================================================
    // GET /api/simulations/{keyField}
    // =========================================================================

    [Fact]
    public async Task GetByKey_WhenFound_ReturnsOk()
    {
        var dto = new SimulationDto { Name = "Sim 1", Key = new Key("Simulation", "key-1") };
        _cqrsMock.Setup(c => c.SendQueryAsync<GetSimulationByKeyFieldQueryResult>(It.IsAny<Query>()))
            .ReturnsAsync(new GetSimulationByKeyFieldQueryResult { Success = true, Simulation = dto });

        var result = await CreateSut().GetByKey("key-1");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(dto);
    }

    [Fact]
    public async Task GetByKey_WhenNotFound_ReturnsNotFound()
    {
        _cqrsMock.Setup(c => c.SendQueryAsync<GetSimulationByKeyFieldQueryResult>(It.IsAny<Query>()))
            .ReturnsAsync(new GetSimulationByKeyFieldQueryResult { Success = false, Simulation = null, Message = "Not found" });

        var result = await CreateSut().GetByKey("missing");

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // =========================================================================
    // POST /api/simulations
    // =========================================================================

    [Fact]
    public async Task Create_WhenSuccess_ReturnsCreated()
    {
        _cqrsMock.Setup(c => c.SendCommandAsync<CreateSimulationCommandResult>(It.IsAny<Command>()))
            .ReturnsAsync(new CreateSimulationCommandResult
            {
                Success = true,
                Message = "Created",
                SimulationKey = new Key("Simulation", "new-key")
            });

        var result = await CreateSut().Create(new CreateSimulationRequest
        {
            Name = "Test Sim",
            Description = "desc",
            CreatorKeyField = "user-1"
        });

        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task Create_WhenFailure_ReturnsBadRequest()
    {
        _cqrsMock.Setup(c => c.SendCommandAsync<CreateSimulationCommandResult>(It.IsAny<Command>()))
            .ReturnsAsync(new CreateSimulationCommandResult { Success = false, Message = "Creator not found" });

        var result = await CreateSut().Create(new CreateSimulationRequest());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // =========================================================================
    // PUT /api/simulations/{keyField}
    // =========================================================================

    [Fact]
    public async Task Update_WhenSuccess_ReturnsOk()
    {
        _cqrsMock.Setup(c => c.SendCommandAsync<UpdateSimulationCommandResult>(It.IsAny<Command>()))
            .ReturnsAsync(new UpdateSimulationCommandResult { Success = true, Message = "Updated" });

        var result = await CreateSut().Update("key-1", new UpdateSimulationRequest { Name = "New Name" });

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Update_WhenFailure_ReturnsBadRequest()
    {
        _cqrsMock.Setup(c => c.SendCommandAsync<UpdateSimulationCommandResult>(It.IsAny<Command>()))
            .ReturnsAsync(new UpdateSimulationCommandResult { Success = false, Message = "Not found" });

        var result = await CreateSut().Update("missing", new UpdateSimulationRequest());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // =========================================================================
    // DELETE /api/simulations/{keyField}
    // =========================================================================

    [Fact]
    public async Task Delete_WhenSuccess_ReturnsOk()
    {
        _cqrsMock.Setup(c => c.SendCommandAsync<DeleteSimulationCommandResult>(It.IsAny<Command>()))
            .ReturnsAsync(new DeleteSimulationCommandResult { Success = true, Message = "Deleted" });

        var result = await CreateSut().Delete("key-1");

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Delete_WhenFailure_ReturnsBadRequest()
    {
        _cqrsMock.Setup(c => c.SendCommandAsync<DeleteSimulationCommandResult>(It.IsAny<Command>()))
            .ReturnsAsync(new DeleteSimulationCommandResult { Success = false, Message = "Error" });

        var result = await CreateSut().Delete("bad-key");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // =========================================================================
    // POST /api/simulations/{keyField}/run
    // =========================================================================

    [Fact]
    public async Task Run_WhenSuccess_ReturnsOkWithStepCounts()
    {
        _cqrsMock.Setup(c => c.SendCommandAsync<RunSimulationCommandResult>(It.IsAny<Command>()))
            .ReturnsAsync(new RunSimulationCommandResult
            {
                Success = true,
                Message = "Queued",
                TotalSteps = 3,
                ExecutedSteps = 0,
                StartedAt = DateTime.UtcNow
            });

        var result = await CreateSut().Run("key-1", new RunSimulationRequest { RequestorKeyField = "user-1" });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value!;
        body.GetType().GetProperty("totalSteps")!.GetValue(body).Should().Be(3);
    }

    [Fact]
    public async Task Run_WhenSimulationHasNoSteps_ReturnsBadRequest()
    {
        _cqrsMock.Setup(c => c.SendCommandAsync<RunSimulationCommandResult>(It.IsAny<Command>()))
            .ReturnsAsync(new RunSimulationCommandResult { Success = false, Message = "Simulation has no steps" });

        var result = await CreateSut().Run("empty-sim", null);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
