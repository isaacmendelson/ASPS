using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApi.Pages.Simulations;
using WebApi.Services;
using Business.Commands;
using Business.Queries;
using Common.Entities;
using Common.Models;
using Common.Messaging;
using System.Text.Json;

namespace ASPS.Tests.WebApi.Pages;

/// <summary>
/// Unit tests for Simulations/EditModel page
/// ASPS-362: Unit Tests - Simulation feature
/// </summary>
public class SimulationsEditModelTests
{
    private readonly Mock<ICQRSClient> _mockCqrsClient;
    private readonly Mock<ILogger<EditModel>> _mockLogger;
    private readonly EditModel _sut;

    public SimulationsEditModelTests()
    {
        _mockCqrsClient = new Mock<ICQRSClient>();
        _mockLogger = new Mock<ILogger<EditModel>>();
        _sut = new EditModel(_mockCqrsClient.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_InitializesProperties()
    {
        // Assert
        _sut.Key.Should().BeEmpty();
        _sut.Name.Should().BeEmpty();
        _sut.Description.Should().BeEmpty();
        _sut.StepsJson.Should().Be("[]");
        _sut.AvailableUsers.Should().NotBeNull();
        _sut.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task OnGetAsync_WithoutKey_RedirectsToIndex()
    {
        // Arrange
        _sut.Key = "";

        // Act
        var result = await _sut.OnGetAsync();

        // Assert
        result.Should().BeOfType<RedirectToPageResult>();
        ((RedirectToPageResult)result).PageName.Should().Be("Index");
    }

    [Fact]
    public async Task OnGetAsync_WithValidKey_LoadsSimulationData()
    {
        // Arrange
        _sut.Key = "sim-key-123";

        var steps = new[]
        {
            new SimulationStep
            {
                Sequence = 1,
                DelayMs = 2000,
                UserId = "user-789",
                DeviceUid = "device-101",
                AlertType = "UrlAlert",
                AlertJson = "{\"Url\":\"https://example.com\"}",
                Priority = global::Common.Enums.Priority.High
            }
        };

        var simulation = new Simulation("Existing Simulation", "Existing Description", "creator-key")
        {
            SimulationStepsJson = JsonSerializer.Serialize(steps)
        };

        var queryResult = new GetSimulationDetailsQueryResult
        {
            Success = true,
            Simulation = simulation,
            Steps = steps
        };

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetSimulationDetailsQueryResult>(It.IsAny<GetSimulationDetailsQuery>()))
            .ReturnsAsync(queryResult);

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetSimulationUsersQueryResult>(It.IsAny<GetSimulationUsersQuery>()))
            .ReturnsAsync(new GetSimulationUsersQueryResult { Success = true, Users = new List<SimulationUserDto>() });

        // Act
        var result = await _sut.OnGetAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _sut.Name.Should().Be("Existing Simulation");
        _sut.Description.Should().Be("Existing Description");
        _sut.StepsJson.Should().NotBeNullOrEmpty();
        _sut.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task OnGetAsync_WhenSimulationNotFound_SetsErrorMessage()
    {
        // Arrange
        _sut.Key = "non-existent-key";

        var queryResult = new GetSimulationDetailsQueryResult
        {
            Success = false,
            Message = "Simulation not found"
        };

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetSimulationDetailsQueryResult>(It.IsAny<GetSimulationDetailsQuery>()))
            .ReturnsAsync(queryResult);

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetSimulationUsersQueryResult>(It.IsAny<GetSimulationUsersQuery>()))
            .ReturnsAsync(new GetSimulationUsersQueryResult { Success = true, Users = new List<SimulationUserDto>() });

        // Act
        var result = await _sut.OnGetAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _sut.ErrorMessage.Should().Be("Simulation not found");
    }

    [Fact]
    public async Task OnPostAsync_WithValidData_UpdatesSimulationAndRedirects()
    {
        // Arrange
        _sut.Key = "sim-key-123";
        _sut.Name = "Updated Simulation";
        _sut.Description = "Updated Description";
        
        var steps = new[]
        {
            new SimulationStep
            {
                Sequence = 1,
                DelayMs = 3000,
                UserId = "user-999",
                DeviceUid = "device-888",
                AlertType = "RemoteAccessAlert",
                AlertJson = "{\"IPAddress\":\"192.168.1.1\"}",
                Priority = global::Common.Enums.Priority.Critical
            }
        };
        _sut.StepsJson = JsonSerializer.Serialize(steps);

        UpdateSimulationCommand? capturedCommand = null;
        var commandResult = new UpdateSimulationCommandResult { Success = true };

        _mockCqrsClient
            .Setup(x => x.SendCommandAsync<UpdateSimulationCommandResult>(It.IsAny<Command>()))
            .Callback<Command>(c => capturedCommand = c as UpdateSimulationCommand)
            .ReturnsAsync(commandResult);

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetSimulationUsersQueryResult>(It.IsAny<Query>()))
            .ReturnsAsync(new GetSimulationUsersQueryResult { Success = true, Users = new List<SimulationUserDto>() });

        // Act
        var result = await _sut.OnPostAsync();

        // Assert
        result.Should().BeOfType<RedirectToPageResult>();
        ((RedirectToPageResult)result).PageName.Should().Be("Index");
        
        capturedCommand.Should().NotBeNull();
        capturedCommand!.SimulationKey.Value.Should().Be("sim-key-123");
        capturedCommand.Name.Should().Be("Updated Simulation");
        capturedCommand.Description.Should().Be("Updated Description");
        capturedCommand.Steps.Should().HaveCount(1);
    }

    [Fact]
    public async Task OnPostAsync_WithEmptyName_SetsErrorAndReturnsPage()
    {
        // Arrange
        _sut.Key = "sim-key-123";
        _sut.Name = "";
        _sut.Description = "Some Description";
        _sut.StepsJson = "[]";

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetSimulationUsersQueryResult>(It.IsAny<GetSimulationUsersQuery>()))
            .ReturnsAsync(new GetSimulationUsersQueryResult { Success = true, Users = new List<SimulationUserDto>() });

        // Act
        var result = await _sut.OnPostAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _sut.ErrorMessage.Should().Contain("name is required");
        
        _mockCqrsClient.Verify(
            x => x.SendCommandAsync<UpdateSimulationCommandResult>(It.IsAny<UpdateSimulationCommand>()),
            Times.Never);
    }

    [Fact]
    public async Task OnPostAsync_WithInvalidStepsJson_SetsErrorAndReturnsPage()
    {
        // Arrange
        _sut.Key = "sim-key-123";
        _sut.Name = "Valid Name";
        _sut.Description = "Valid Description";
        _sut.StepsJson = "not valid json {{{{";

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetSimulationUsersQueryResult>(It.IsAny<GetSimulationUsersQuery>()))
            .ReturnsAsync(new GetSimulationUsersQueryResult { Success = true, Users = new List<SimulationUserDto>() });

        // Act
        var result = await _sut.OnPostAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _sut.ErrorMessage.Should().Contain("Invalid steps JSON");
        
        _mockCqrsClient.Verify(
            x => x.SendCommandAsync<UpdateSimulationCommandResult>(It.IsAny<UpdateSimulationCommand>()),
            Times.Never);
    }

    [Fact]
    public async Task OnPostAsync_WhenCommandFails_SetsErrorAndReturnsPage()
    {
        // Arrange
        _sut.Key = "sim-key-123";
        _sut.Name = "Valid Simulation";
        _sut.Description = "Valid Description";
        _sut.StepsJson = "[]";

        var commandResult = new UpdateSimulationCommandResult
        {
            Success = false,
            Message = "Update failed due to database error"
        };

        _mockCqrsClient
            .Setup(x => x.SendCommandAsync<UpdateSimulationCommandResult>(It.IsAny<UpdateSimulationCommand>()))
            .ReturnsAsync(commandResult);

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetSimulationUsersQueryResult>(It.IsAny<GetSimulationUsersQuery>()))
            .ReturnsAsync(new GetSimulationUsersQueryResult { Success = true, Users = new List<SimulationUserDto>() });

        // Act
        var result = await _sut.OnPostAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _sut.ErrorMessage.Should().Be("Update failed due to database error");
    }

    [Fact]
    public async Task OnPostAsync_LoadsAvailableUsersOnError()
    {
        // Arrange
        _sut.Key = "sim-key-123";
        _sut.Name = ""; // Invalid - will trigger error

        var expectedUsers = new List<SimulationUserDto>
        {
            new SimulationUserDto { UserId = "user1", FirstName = "Test", LastName = "User" }
        };

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetSimulationUsersQueryResult>(It.IsAny<GetSimulationUsersQuery>()))
            .ReturnsAsync(new GetSimulationUsersQueryResult { Success = true, Users = expectedUsers });

        // Act
        var result = await _sut.OnPostAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _sut.AvailableUsers.Should().HaveCount(1);
        _sut.AvailableUsers.Should().BeEquivalentTo(expectedUsers);
    }
}
