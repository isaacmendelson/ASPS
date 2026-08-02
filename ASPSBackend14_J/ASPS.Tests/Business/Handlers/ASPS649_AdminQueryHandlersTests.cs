using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Business.Handlers;
using Business.Queries;
using Common.Entities;
using Common.Models;
using Interface.Repositories;
using System.Text.Json;

namespace ASPS.Tests.Business.Handlers;

/// <summary>
/// ASPS-649 — Unit tests for ASPS649QueryHandlers:
///   - HandleAsync(GetAllSimulationsPagedQuery)
///   - HandleAsync(GetSimulationByKeyFieldQuery)
///   - HandleAsync(GetAllRoadmapsPagedQuery)
/// </summary>
public class ASPS649_AdminQueryHandlersTests
{
    private readonly Mock<ISimulationRepository> _simRepoMock = new();
    private readonly Mock<IRoadmapRepository> _roadmapRepoMock = new();
    private readonly Mock<ILogger<ASPS649QueryHandlers>> _loggerMock = new();

    private ASPS649QueryHandlers CreateSut() =>
        new ASPS649QueryHandlers(
            _simRepoMock.Object,
            _roadmapRepoMock.Object,
            _loggerMock.Object);

    // =========================================================================
    // GetAllSimulationsPagedQuery
    // =========================================================================

    [Fact]
    public async Task GetAllSimulationsPaged_ReturnsPagedResult()
    {
        var simulations = new List<Simulation>
        {
            new Simulation("Sim A", "Desc A", "creator-1") { KeyField = "key-1" },
            new Simulation("Sim B", "Desc B", "creator-2") { KeyField = "key-2" }
        };

        _simRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(simulations);

        var result = await CreateSut().HandleAsync(new GetAllSimulationsPagedQuery
        {
            Page = 1,
            PageSize = 25
        });

        result.Success.Should().BeTrue();
        result.Result.TotalCount.Should().Be(2);
        result.Result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllSimulationsPaged_ExcludesDeletedSimulations()
    {
        var simulations = new List<Simulation>
        {
            new Simulation("Active", "desc", "creator-1") { KeyField = "key-1", IsDeleted = false },
            new Simulation("Deleted", "desc", "creator-2") { KeyField = "key-2", IsDeleted = true }
        };

        _simRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(simulations);

        var result = await CreateSut().HandleAsync(new GetAllSimulationsPagedQuery());

        result.Success.Should().BeTrue();
        result.Result.TotalCount.Should().Be(1);
        result.Result.Items.Single().Name.Should().Be("Active");
    }

    [Fact]
    public async Task GetAllSimulationsPaged_FiltersOnSearch()
    {
        var simulations = new List<Simulation>
        {
            new Simulation("Phishing Test", "desc", "c1") { KeyField = "k1" },
            new Simulation("Remote Access Test", "desc", "c2") { KeyField = "k2" }
        };

        _simRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(simulations);

        var result = await CreateSut().HandleAsync(new GetAllSimulationsPagedQuery
        {
            Search = "Phishing"
        });

        result.Success.Should().BeTrue();
        result.Result.TotalCount.Should().Be(1);
        result.Result.Items.Single().Name.Should().Be("Phishing Test");
    }

    [Fact]
    public async Task GetAllSimulationsPaged_PaginatesCorrectly()
    {
        var simulations = Enumerable.Range(1, 10)
            .Select(i => new Simulation($"Sim {i}", "", "c") { KeyField = $"k{i}" })
            .ToList();

        _simRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(simulations);

        var result = await CreateSut().HandleAsync(new GetAllSimulationsPagedQuery
        {
            Page = 2,
            PageSize = 3
        });

        result.Success.Should().BeTrue();
        result.Result.TotalCount.Should().Be(10);
        result.Result.Page.Should().Be(2);
        result.Result.PageSize.Should().Be(3);
        result.Result.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAllSimulationsPaged_DeserializesSteps()
    {
        var steps = new List<SimulationStep>
        {
            new SimulationStep { AlertType = "UrlAlert" }
        };
        var stepsJson = JsonSerializer.Serialize(steps);

        var sim = new Simulation("Sim With Steps", "desc", "c1")
        {
            KeyField = "k1",
            SimulationStepsJson = stepsJson
        };

        _simRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Simulation> { sim });

        var result = await CreateSut().HandleAsync(new GetAllSimulationsPagedQuery());

        result.Success.Should().BeTrue();
        result.Result.Items.Single().Steps.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAllSimulationsPaged_WhenRepositoryThrows_ReturnsFailure()
    {
        _simRepoMock.Setup(r => r.GetAllAsync()).ThrowsAsync(new Exception("DB error"));

        var result = await CreateSut().HandleAsync(new GetAllSimulationsPagedQuery());

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("DB error");
    }

    // =========================================================================
    // GetSimulationByKeyFieldQuery
    // =========================================================================

    [Fact]
    public async Task GetSimulationByKeyField_WhenFound_ReturnsDto()
    {
        var sim = new Simulation("My Sim", "desc", "creator-1") { KeyField = "key-abc" };
        _simRepoMock.Setup(r => r.GetByKeyAsync(It.IsAny<Key>())).ReturnsAsync(sim);

        var result = await CreateSut().HandleAsync(new GetSimulationByKeyFieldQuery { KeyField = "key-abc" });

        result.Success.Should().BeTrue();
        result.Simulation.Should().NotBeNull();
        result.Simulation!.Name.Should().Be("My Sim");
    }

    [Fact]
    public async Task GetSimulationByKeyField_WhenNotFound_ReturnsFailure()
    {
        _simRepoMock.Setup(r => r.GetByKeyAsync(It.IsAny<Key>())).ReturnsAsync((Simulation?)null);

        var result = await CreateSut().HandleAsync(new GetSimulationByKeyFieldQuery { KeyField = "missing" });

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task GetSimulationByKeyField_WhenDeleted_ReturnsFailure()
    {
        var sim = new Simulation("Deleted Sim", "desc", "c") { KeyField = "del-key", IsDeleted = true };
        _simRepoMock.Setup(r => r.GetByKeyAsync(It.IsAny<Key>())).ReturnsAsync(sim);

        var result = await CreateSut().HandleAsync(new GetSimulationByKeyFieldQuery { KeyField = "del-key" });

        result.Success.Should().BeFalse();
    }

    // =========================================================================
    // GetAllRoadmapsPagedQuery
    // =========================================================================

    [Fact]
    public async Task GetAllRoadmapsPaged_ReturnsPagedResult()
    {
        var roadmaps = new List<Roadmap>
        {
            new Roadmap { Id = 1, Name = "Roadmap A", DateCreated = DateTime.UtcNow, Version = 1 },
            new Roadmap { Id = 2, Name = "Roadmap B", DateCreated = DateTime.UtcNow, Version = 1 }
        };

        _roadmapRepoMock.Setup(r => r.ListAsync(false)).ReturnsAsync(roadmaps);

        var result = await CreateSut().HandleAsync(new GetAllRoadmapsPagedQuery());

        result.Success.Should().BeTrue();
        result.Result.TotalCount.Should().Be(2);
        result.Result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllRoadmapsPaged_IncludeArchived_PassedToRepository()
    {
        _roadmapRepoMock.Setup(r => r.ListAsync(true)).ReturnsAsync(new List<Roadmap>());

        await CreateSut().HandleAsync(new GetAllRoadmapsPagedQuery { IncludeArchived = true });

        _roadmapRepoMock.Verify(r => r.ListAsync(true), Times.Once);
    }

    [Fact]
    public async Task GetAllRoadmapsPaged_FiltersOnSearch()
    {
        var roadmaps = new List<Roadmap>
        {
            new Roadmap { Id = 1, Name = "ASPS Roadmap", DateCreated = DateTime.UtcNow, Version = 1 },
            new Roadmap { Id = 2, Name = "Other Roadmap", DateCreated = DateTime.UtcNow, Version = 1 }
        };

        _roadmapRepoMock.Setup(r => r.ListAsync(false)).ReturnsAsync(roadmaps);

        var result = await CreateSut().HandleAsync(new GetAllRoadmapsPagedQuery { Search = "ASPS" });

        result.Success.Should().BeTrue();
        result.Result.TotalCount.Should().Be(1);
        result.Result.Items.Single().Name.Should().Be("ASPS Roadmap");
    }

    [Fact]
    public async Task GetAllRoadmapsPaged_PaginatesCorrectly()
    {
        var roadmaps = Enumerable.Range(1, 8)
            .Select(i => new Roadmap { Id = i, Name = $"Roadmap {i}", DateCreated = DateTime.UtcNow, Version = 1 })
            .ToList();

        _roadmapRepoMock.Setup(r => r.ListAsync(false)).ReturnsAsync(roadmaps);

        var result = await CreateSut().HandleAsync(new GetAllRoadmapsPagedQuery { Page = 2, PageSize = 3 });

        result.Success.Should().BeTrue();
        result.Result.TotalCount.Should().Be(8);
        result.Result.Page.Should().Be(2);
        result.Result.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAllRoadmapsPaged_WhenRepositoryThrows_ReturnsFailure()
    {
        _roadmapRepoMock.Setup(r => r.ListAsync(It.IsAny<bool>())).ThrowsAsync(new Exception("DB error"));

        var result = await CreateSut().HandleAsync(new GetAllRoadmapsPagedQuery());

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("DB error");
    }

    [Fact]
    public async Task GetAllRoadmapsPaged_MapsDtoFields()
    {
        var roadmap = new Roadmap
        {
            Id = 7,
            Name = "Test Roadmap",
            Description = "A description",
            Version = 3,
            DateCreated = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LastUpdatedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedBy = "alice",
            LastUpdatedBy = "bob",
            IsArchived = false
        };

        _roadmapRepoMock.Setup(r => r.ListAsync(false)).ReturnsAsync(new List<Roadmap> { roadmap });

        var result = await CreateSut().HandleAsync(new GetAllRoadmapsPagedQuery());

        var dto = result.Result.Items.Single();
        dto.Id.Should().Be(7);
        dto.Name.Should().Be("Test Roadmap");
        dto.Description.Should().Be("A description");
        dto.Version.Should().Be(3);
        dto.CreatedBy.Should().Be("alice");
        dto.LastUpdatedBy.Should().Be("bob");
        dto.IsArchived.Should().BeFalse();
    }
}
