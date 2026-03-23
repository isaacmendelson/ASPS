using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using WebApi.Pages.Simulations;
using WebApi.Services;
using Business.Queries;
using Business.Commands;
using Common.Entities;
using Common.Models;
using Common.Messaging;

namespace ASPS.Tests.WebApi.Pages;

/// <summary>
/// Unit tests for Simulations/IndexModel page
/// ASPS-362: Unit Tests - Simulation feature
/// </summary>
public class SimulationsIndexModelTests
{
    private readonly Mock<ICQRSClient> _mockCqrsClient;
    private readonly Mock<ILogger<IndexModel>> _mockLogger;
    private readonly IndexModel _sut;

    public SimulationsIndexModelTests()
    {
        _mockCqrsClient = new Mock<ICQRSClient>();
        _mockLogger = new Mock<ILogger<IndexModel>>();
        _sut = new IndexModel(_mockCqrsClient.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_InitializesProperties()
    {
        // Assert
        _sut.Simulations.Should().NotBeNull();
        _sut.Simulations.Should().BeEmpty();
        _sut.ErrorMessage.Should().BeNull();
        _sut.SuccessMessage.Should().BeNull();
    }

    [Fact]
    public async Task OnGetAsync_WithoutSearch_LoadsAllSimulations()
    {
        // Arrange
        var expectedSimulations = new List<Simulation>
        {
            new Simulation("Test Sim 1", "Description 1", "user-key-1"),
            new Simulation("Test Sim 2", "Description 2", "user-key-2")
        };

        var queryResult = new GetSimulationsQueryResult
        {
            Success = true,
            Simulations = expectedSimulations
        };

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetSimulationsQueryResult>(It.IsAny<GetSimulationsQuery>()))
            .ReturnsAsync(queryResult);

        // Act
        await _sut.OnGetAsync();

        // Assert
        _sut.Simulations.Should().HaveCount(2);
        _sut.Simulations.Should().BeEquivalentTo(expectedSimulations);
        _sut.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task OnGetAsync_WithSearch_PassesSearchTextToQuery()
    {
        // Arrange
        _sut.Search = "phishing";
        GetSimulationsQuery? capturedQuery = null;

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetSimulationsQueryResult>(It.IsAny<Query>()))
            .Callback<Query>(q => capturedQuery = q as GetSimulationsQuery)
            .ReturnsAsync(new GetSimulationsQueryResult { Success = true, Simulations = new List<Simulation>() });

        // Act
        await _sut.OnGetAsync();

        // Assert
        capturedQuery.Should().NotBeNull();
        capturedQuery!.SearchText.Should().Be("phishing");
    }

    [Fact]
    public async Task OnGetAsync_WhenQueryFails_SetsErrorMessage()
    {
        // Arrange
        var queryResult = new GetSimulationsQueryResult
        {
            Success = false,
            Message = "Database connection failed"
        };

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetSimulationsQueryResult>(It.IsAny<GetSimulationsQuery>()))
            .ReturnsAsync(queryResult);

        // Act
        await _sut.OnGetAsync();

        // Assert
        _sut.Simulations.Should().BeEmpty();
        _sut.ErrorMessage.Should().Be("Database connection failed");
    }

    [Fact]
    public async Task OnPostDeleteAsync_WithValidKey_DeletesSimulationAndRedirects()
    {
        // Arrange
        var simulationKey = "test-sim-key-123";
        DeleteSimulationCommand? capturedCommand = null;

        var commandResult = new DeleteSimulationCommandResult { Success = true };

        _mockCqrsClient
            .Setup(x => x.SendCommandAsync<DeleteSimulationCommandResult>(It.IsAny<Command>()))
            .Callback<Command>(c => capturedCommand = c as DeleteSimulationCommand)
            .ReturnsAsync(commandResult);

        // Act
        var result = await _sut.OnPostDeleteAsync(simulationKey);

        // Assert
        result.Should().BeOfType<RedirectToPageResult>();
        capturedCommand.Should().NotBeNull();
        capturedCommand!.SimulationKey.Value.Should().Be(simulationKey);
        _sut.SuccessMessage.Should().Contain("deleted successfully");
    }

    [Fact]
    public async Task OnPostDeleteAsync_WhenCommandFails_SetsErrorMessage()
    {
        // Arrange
        var simulationKey = "test-sim-key-123";
        var commandResult = new DeleteSimulationCommandResult
        {
            Success = false,
            Message = "Simulation not found"
        };

        _mockCqrsClient
            .Setup(x => x.SendCommandAsync<DeleteSimulationCommandResult>(It.IsAny<DeleteSimulationCommand>()))
            .ReturnsAsync(commandResult);

        // Act
        var result = await _sut.OnPostDeleteAsync(simulationKey);

        // Assert
        result.Should().BeOfType<RedirectToPageResult>();
        _sut.ErrorMessage.Should().Be("Simulation not found");
        _sut.SuccessMessage.Should().BeNull();
    }

    [Fact]
    public async Task OnPostRunAsync_WithValidKey_RunsSimulationAndRedirects()
    {
        // Arrange
        var simulationKey = "test-sim-key-123";
        RunSimulationCommand? capturedCommand = null;

        var commandResult = new RunSimulationCommandResult
        {
            Success = true,
            TotalSteps = 5,
            ExecutedSteps = 5
        };

        _mockCqrsClient
            .Setup(x => x.SendCommandAsync<RunSimulationCommandResult>(It.IsAny<Command>()))
            .Callback<Command>(c => capturedCommand = c as RunSimulationCommand)
            .ReturnsAsync(commandResult);

        // Act
        var result = await _sut.OnPostRunAsync(simulationKey);

        // Assert
        result.Should().BeOfType<RedirectToPageResult>();
        capturedCommand.Should().NotBeNull();
        capturedCommand!.SimulationKey.Value.Should().Be(simulationKey);
        _sut.SuccessMessage.Should().Contain("5/5 steps executed");
    }

    [Fact]
    public async Task OnPostRunAsync_WhenCommandFails_SetsErrorMessage()
    {
        // Arrange
        var simulationKey = "test-sim-key-123";
        var commandResult = new RunSimulationCommandResult
        {
            Success = false,
            Message = "Simulation runner is unavailable"
        };

        _mockCqrsClient
            .Setup(x => x.SendCommandAsync<RunSimulationCommandResult>(It.IsAny<RunSimulationCommand>()))
            .ReturnsAsync(commandResult);

        // Act
        var result = await _sut.OnPostRunAsync(simulationKey);

        // Assert
        result.Should().BeOfType<RedirectToPageResult>();
        _sut.ErrorMessage.Should().Be("Simulation runner is unavailable");
        _sut.SuccessMessage.Should().BeNull();
    }
}
