using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using WebApi.Pages.Simulations;
using WebApi.Services;
using Business.Commands;
using Business.Queries;
using Common.Models;
using Common.Messaging;
using Common.Entities;
using System.Text.Json;

namespace ASPS.Tests.WebApi.Pages;

/// <summary>
/// Unit tests for Simulations/CreateModel page
/// ASPS-362: Unit Tests - Simulation feature
/// </summary>
public class SimulationsCreateModelTests
{
    private readonly Mock<ICQRSClient> _mockCqrsClient;
    private readonly Mock<ILogger<CreateModel>> _mockLogger;
    private readonly CreateModel _sut;

    public SimulationsCreateModelTests()
    {
        _mockCqrsClient = new Mock<ICQRSClient>();
        _mockLogger = new Mock<ILogger<CreateModel>>();
        _sut = new CreateModel(_mockCqrsClient.Object, _mockLogger.Object);
        
        // Setup default Page Context with authenticated user
        var claims = new List<Claim>
        {
            new Claim("sub", "test-keycloak-id")
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        
        var httpContext = new DefaultHttpContext
        {
            User = claimsPrincipal
        };
        
        _sut.PageContext = new PageContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public void Constructor_InitializesProperties()
    {
        // Assert
        _sut.Name.Should().BeEmpty();
        _sut.Description.Should().BeEmpty();
        _sut.StepsJson.Should().Be("[]");
        _sut.AvailableUsers.Should().NotBeNull();
        _sut.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task OnGetAsync_LoadsAvailableUsers()
    {
        // Arrange
        var expectedUsers = new List<SimulationUserDto>
        {
            new SimulationUserDto { UserId = "user1", FirstName = "John", LastName = "Doe" },
            new SimulationUserDto { UserId = "user2", FirstName = "Jane", LastName = "Smith" }
        };

        var queryResult = new GetSimulationUsersQueryResult
        {
            Success = true,
            Users = expectedUsers
        };

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetSimulationUsersQueryResult>(It.Is<Query>(q => q is GetSimulationUsersQuery)))
            .ReturnsAsync(queryResult);

        // Act
        await _sut.OnGetAsync();

        // Assert
        _sut.AvailableUsers.Should().HaveCount(2);
        _sut.AvailableUsers.Should().BeEquivalentTo(expectedUsers);
    }

    [Fact]
    public async Task OnPostAsync_WithValidData_CreatesSimulationAndRedirects()
    {
        // Arrange
        _sut.Name = "Test Simulation";
        _sut.Description = "Test Description";
        
        var steps = new[]
        {
            new SimulationStep
            {
                Sequence = 1,
                DelayMs = 1000,
                UserId = "user-123",
                DeviceUid = "device-456",
                AlertType = "UrlAlert",
                AlertJson = "{\"Url\":\"https://test.com\"}",
                Priority = global::Common.Enums.Priority.High
            }
        };
        _sut.StepsJson = JsonSerializer.Serialize(steps);

        CreateSimulationCommand? capturedCommand = null;
        var commandResult = new CreateSimulationCommandResult
        {
            Success = true,
            SimulationKey = new Key("Simulation", "new-sim-key")
        };

        // Mock user authentication query
        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetUserByKeycloakIdQueryResult>(It.Is<Query>(q => q is GetUserByKeycloakIdQuery)))
            .ReturnsAsync(new GetUserByKeycloakIdQueryResult 
            { 
                Success = true, 
                User = new User 
                { 
                    KeyField = "test-user-key",
                    FirstName = "Test",
                    LastName = "User",
                    Email = "test@example.com"
                }
            });

        _mockCqrsClient
            .Setup(x => x.SendCommandAsync<CreateSimulationCommandResult>(It.Is<Command>(c => c is CreateSimulationCommand)))
            .Callback<Command>(c => capturedCommand = c as CreateSimulationCommand)
            .ReturnsAsync(commandResult);

        // Mock available users query
        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetSimulationUsersQueryResult>(It.Is<Query>(q => q is GetSimulationUsersQuery)))
            .ReturnsAsync(new GetSimulationUsersQueryResult { Success = true, Users = new List<SimulationUserDto>() });

        // Act
        var result = await _sut.OnPostAsync();

        // Assert
        _mockCqrsClient.Verify(x => x.SendQueryAsync<GetUserByKeycloakIdQueryResult>(It.IsAny<Query>()), Times.Once, "GetUserByKeycloakId was not called");
        _mockCqrsClient.Verify(x => x.SendCommandAsync<CreateSimulationCommandResult>(It.IsAny<Command>()), Times.Once, "CreateSimulation was not called");
        
        if (!string.IsNullOrEmpty(_sut.ErrorMessage))
        {
            throw new Exception($"Unexpected error: {_sut.ErrorMessage}");
        }
        
        result.Should().BeOfType<RedirectToPageResult>();
        ((RedirectToPageResult)result).PageName.Should().Be("Index");
        
        capturedCommand.Should().NotBeNull();
        capturedCommand!.Name.Should().Be("Test Simulation");
        capturedCommand.Description.Should().Be("Test Description");
        capturedCommand.Steps.Should().HaveCount(1);
        capturedCommand.Steps[0].Sequence.Should().Be(1);
    }

    [Fact]
    public async Task OnPostAsync_WithEmptyName_SetsErrorAndReturnsPage()
    {
        // Arrange
        _sut.Name = "";
        _sut.Description = "Test Description";
        _sut.StepsJson = "[]";

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetSimulationUsersQueryResult>(It.Is<Query>(q => q is GetSimulationUsersQuery)))
            .ReturnsAsync(new GetSimulationUsersQueryResult { Success = true, Users = new List<SimulationUserDto>() });

        // Act
        var result = await _sut.OnPostAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _sut.ErrorMessage.Should().Contain("name is required");
        
        _mockCqrsClient.Verify(
            x => x.SendCommandAsync<CreateSimulationCommandResult>(It.Is<Command>(c => c is CreateSimulationCommand)),
            Times.Never);
    }

    [Fact]
    public async Task OnPostAsync_WithInvalidStepsJson_SetsErrorAndReturnsPage()
    {
        // Arrange
        _sut.Name = "Test Simulation";
        _sut.Description = "Test Description";
        _sut.StepsJson = "{ invalid json [[[";

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetSimulationUsersQueryResult>(It.Is<Query>(q => q is GetSimulationUsersQuery)))
            .ReturnsAsync(new GetSimulationUsersQueryResult { Success = true, Users = new List<SimulationUserDto>() });

        // Act
        var result = await _sut.OnPostAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _sut.ErrorMessage.Should().Contain("Invalid steps JSON");
        
        _mockCqrsClient.Verify(
            x => x.SendCommandAsync<CreateSimulationCommandResult>(It.Is<Command>(c => c is CreateSimulationCommand)),
            Times.Never);
    }

    [Fact]
    public async Task OnPostAsync_WhenCommandFails_SetsErrorAndReturnsPage()
    {
        // Arrange
        _sut.Name = "Test Simulation";
        _sut.Description = "Test Description";
        _sut.StepsJson = "[]";

        var commandResult = new CreateSimulationCommandResult
        {
            Success = false,
            Message = "Failed to create simulation"
        };

        // Mock user authentication query
        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetUserByKeycloakIdQueryResult>(It.Is<Query>(q => q is GetUserByKeycloakIdQuery)))
            .ReturnsAsync(new GetUserByKeycloakIdQueryResult 
            { 
                Success = true, 
                User = new User 
                { 
                    KeyField = "test-user-key",
                    FirstName = "Test",
                    LastName = "User",
                    Email = "test@example.com"
                }
            });

        _mockCqrsClient
            .Setup(x => x.SendCommandAsync<CreateSimulationCommandResult>(It.Is<Command>(c => c is CreateSimulationCommand)))
            .ReturnsAsync(commandResult);

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetSimulationUsersQueryResult>(It.Is<Query>(q => q is GetSimulationUsersQuery)))
            .ReturnsAsync(new GetSimulationUsersQueryResult { Success = true, Users = new List<SimulationUserDto>() });

        // Act
        var result = await _sut.OnPostAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _sut.ErrorMessage.Should().Be("Failed to create simulation");
    }

    [Fact]
    public async Task OnPostAsync_WithEmptyStepsJson_CreatesSimulationWithNoSteps()
    {
        // Arrange
        _sut.Name = "Test Simulation";
        _sut.Description = "Test Description";
        _sut.StepsJson = "[]";

        CreateSimulationCommand? capturedCommand = null;
        var commandResult = new CreateSimulationCommandResult
        {
            Success = true,
            SimulationKey = new Key("Simulation", "new-sim-key")
        };

        // Mock user authentication query
        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetUserByKeycloakIdQueryResult>(It.Is<Query>(q => q is GetUserByKeycloakIdQuery)))
            .ReturnsAsync(new GetUserByKeycloakIdQueryResult 
            { 
                Success = true, 
                User = new User 
                { 
                    KeyField = "test-user-key",
                    FirstName = "Test",
                    LastName = "User",
                    Email = "test@example.com"
                }
            });

        _mockCqrsClient
            .Setup(x => x.SendCommandAsync<CreateSimulationCommandResult>(It.Is<Command>(c => c is CreateSimulationCommand)))
            .Callback<Command>(c => capturedCommand = c as CreateSimulationCommand)
            .ReturnsAsync(commandResult);

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetSimulationUsersQueryResult>(It.Is<Query>(q => q is GetSimulationUsersQuery)))
            .ReturnsAsync(new GetSimulationUsersQueryResult { Success = true, Users = new List<SimulationUserDto>() });

        // Act
        var result = await _sut.OnPostAsync();

        // Assert
        result.Should().BeOfType<RedirectToPageResult>();
        capturedCommand.Should().NotBeNull();
        capturedCommand!.Steps.Should().BeEmpty();
    }
}
