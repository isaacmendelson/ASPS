using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebApi.Controllers;
using WebApi.Services;
using Business.Queries;
using Common.Models;
using Common.Entities;

namespace ASPS.Tests.WebApi.Controllers;

/// <summary>
/// Unit tests for SimulationsApiController
/// Tests autocomplete endpoints for simulation user/device selection
/// </summary>
public class SimulationsApiControllerTests
{
    // Dependencies
    private readonly Mock<ICQRSClient> _cqrsClientMock;
    private readonly Mock<ILogger<SimulationsApiController>> _loggerMock;
    
    // System Under Test
    private readonly SimulationsApiController _sut;

    public SimulationsApiControllerTests()
    {
        // Setup mocks
        _cqrsClientMock = new Mock<ICQRSClient>();
        _loggerMock = new Mock<ILogger<SimulationsApiController>>();
        
        // Create instance
        _sut = new SimulationsApiController(_cqrsClientMock.Object, _loggerMock.Object);
    }

    #region SearchUsers Tests

    [Fact]
    public async Task SearchUsers_WhenSuccessful_ReturnsOkWithUsers()
    {
        // Arrange
        var users = new List<User>
        {
            new User
            {
                KeycloakUserId = Guid.NewGuid().ToString(),
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com",
                Phone = "123456789"
            },
            new User
            {
                KeycloakUserId = Guid.NewGuid().ToString(),
                FirstName = "Jane",
                LastName = "Smith",
                Email = "jane.smith@example.com",
                Phone = "987654321"
            }
        };

        var queryResult = new GetSimulationUsersQueryResult
        {
            Success = true,
            Users = users
        };

        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetSimulationUsersQueryResult>(It.Is<GetSimulationUsersQuery>(q => q.SearchText == "john")))
            .ReturnsAsync(queryResult);

        // Act
        var result = await _sut.SearchUsers("john");

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var responseUsers = okResult!.Value as IEnumerable<User>;
        responseUsers.Should().NotBeNull();
        responseUsers.Should().HaveCount(2);
        responseUsers.Should().ContainEquivalentOf(users[0]);
    }

    [Fact]
    public async Task SearchUsers_WithNullSearch_ReturnsAllUsers()
    {
        // Arrange
        var users = new List<User>
        {
            new User { KeycloakUserId = "user1", FirstName = "User", LastName = "One", Email = "user1@test.com" },
            new User { KeycloakUserId = "user2", FirstName = "User", LastName = "Two", Email = "user2@test.com" }
        };

        var queryResult = new GetSimulationUsersQueryResult
        {
            Success = true,
            Users = users
        };

        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetSimulationUsersQueryResult>(It.Is<GetSimulationUsersQuery>(q => q.SearchText == null)))
            .ReturnsAsync(queryResult);

        // Act
        var result = await _sut.SearchUsers(null);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var responseUsers = okResult!.Value as IEnumerable<User>;
        responseUsers.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchUsers_WhenQueryFails_ReturnsBadRequest()
    {
        // Arrange
        var queryResult = new GetSimulationUsersQueryResult
        {
            Success = false,
            Message = "Database connection failed"
        };

        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetSimulationUsersQueryResult>(It.IsAny<GetSimulationUsersQuery>()))
            .ReturnsAsync(queryResult);

        // Act
        var result = await _sut.SearchUsers("test");

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest.Should().NotBeNull();
        badRequest!.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchUsers_WhenExceptionThrown_Returns500()
    {
        // Arrange
        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetSimulationUsersQueryResult>(It.IsAny<GetSimulationUsersQuery>()))
            .ThrowsAsync(new Exception("Network error"));

        // Act
        var result = await _sut.SearchUsers("test");

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
    }

    #endregion

    #region SearchDevices Tests

    [Fact]
    public async Task SearchDevices_WhenSuccessful_ReturnsOkWithDevices()
    {
        // Arrange
        var devices = new List<Device>
        {
            new Device
            {
                DeviceUID = "device-001",
                OperatingSystemName = "Android",
                DeviceType = "Smartphone",
                MACAddress = "AA:BB:CC:DD:EE:FF"
            },
            new Device
            {
                DeviceUID = "device-002",
                OperatingSystemName = "iOS",
                DeviceType = "Smartphone",
                MACAddress = "11:22:33:44:55:66"
            }
        };

        var queryResult = new GetSimulationDevicesQueryResult
        {
            Success = true,
            Devices = devices
        };

        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetSimulationDevicesQueryResult>(It.Is<GetSimulationDevicesQuery>(
                q => q.SearchText == "device" && q.UserKey == null)))
            .ReturnsAsync(queryResult);

        // Act
        var result = await _sut.SearchDevices("device", null);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var responseDevices = okResult!.Value as IEnumerable<Device>;
        responseDevices.Should().NotBeNull();
        responseDevices.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchDevices_WithUserFilter_FiltersDevicesByUser()
    {
        // Arrange
        var userKeyField = "user-123";
        var devices = new List<Device>
        {
            new Device { DeviceUID = "device-001", OperatingSystemName = "Android" }
        };

        var queryResult = new GetSimulationDevicesQueryResult
        {
            Success = true,
            Devices = devices
        };

        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetSimulationDevicesQueryResult>(It.Is<GetSimulationDevicesQuery>(
                q => q.UserKey != null && q.UserKey.KeyField == userKeyField)))
            .ReturnsAsync(queryResult);

        // Act
        var result = await _sut.SearchDevices(null, userKeyField);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var responseDevices = okResult!.Value as IEnumerable<Device>;
        responseDevices.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchDevices_WhenQueryFails_ReturnsBadRequest()
    {
        // Arrange
        var queryResult = new GetSimulationDevicesQueryResult
        {
            Success = false,
            Message = "No devices found"
        };

        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetSimulationDevicesQueryResult>(It.IsAny<GetSimulationDevicesQuery>()))
            .ReturnsAsync(queryResult);

        // Act
        var result = await _sut.SearchDevices("test", null);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SearchDevices_WhenExceptionThrown_Returns500()
    {
        // Arrange
        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetSimulationDevicesQueryResult>(It.IsAny<GetSimulationDevicesQuery>()))
            .ThrowsAsync(new Exception("CQRS gateway timeout"));

        // Act
        var result = await _sut.SearchDevices("test", null);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
    }

    #endregion

    #region GetUserDevices Tests

    [Fact]
    public async Task GetUserDevices_WhenSuccessful_ReturnsOkWithDevices()
    {
        // Arrange
        var userKeyField = "user-123";
        var devices = new List<Device>
        {
            new Device
            {
                DeviceUID = "device-001",
                OperatingSystemName = "Android",
                DeviceType = "Smartphone"
            },
            new Device
            {
                DeviceUID = "device-002",
                OperatingSystemName = "iOS",
                DeviceType = "Tablet"
            }
        };

        var queryResult = new GetSimulationUserDevicesQueryResult
        {
            Success = true,
            Devices = devices
        };

        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetSimulationUserDevicesQueryResult>(It.Is<GetSimulationUserDevicesQuery>(
                q => q.UserKey.KeyField == userKeyField)))
            .ReturnsAsync(queryResult);

        // Act
        var result = await _sut.GetUserDevices(userKeyField);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var responseDevices = okResult!.Value as IEnumerable<Device>;
        responseDevices.Should().NotBeNull();
        responseDevices.Should().HaveCount(2);
        responseDevices.Should().ContainEquivalentOf(devices[0]);
    }

    [Fact]
    public async Task GetUserDevices_WithEmptyResult_ReturnsEmptyList()
    {
        // Arrange
        var userKeyField = "user-no-devices";
        var queryResult = new GetSimulationUserDevicesQueryResult
        {
            Success = true,
            Devices = new List<Device>()
        };

        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetSimulationUserDevicesQueryResult>(It.Is<GetSimulationUserDevicesQuery>(
                q => q.UserKey.KeyField == userKeyField)))
            .ReturnsAsync(queryResult);

        // Act
        var result = await _sut.GetUserDevices(userKeyField);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var responseDevices = okResult!.Value as IEnumerable<Device>;
        responseDevices.Should().NotBeNull();
        responseDevices.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserDevices_WhenQueryFails_ReturnsBadRequest()
    {
        // Arrange
        var queryResult = new GetSimulationUserDevicesQueryResult
        {
            Success = false,
            Message = "User not found"
        };

        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetSimulationUserDevicesQueryResult>(It.IsAny<GetSimulationUserDevicesQuery>()))
            .ReturnsAsync(queryResult);

        // Act
        var result = await _sut.GetUserDevices("invalid-user");

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest.Should().NotBeNull();
    }

    [Fact]
    public async Task GetUserDevices_WhenExceptionThrown_Returns500()
    {
        // Arrange
        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetSimulationUserDevicesQueryResult>(It.IsAny<GetSimulationUserDevicesQuery>()))
            .ThrowsAsync(new Exception("Database connection lost"));

        // Act
        var result = await _sut.GetUserDevices("user-123");

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
    }

    #endregion

    #region Integration Scenarios

    [Fact]
    public async Task SearchUsers_ThenGetDevices_ReturnsConsistentData()
    {
        // Arrange - First search for users
        var userKeyField = "user-123";
        var users = new List<User>
        {
            new User { KeycloakUserId = userKeyField, FirstName = "Test", LastName = "User", Email = "test@test.com" }
        };

        var userQueryResult = new GetSimulationUsersQueryResult
        {
            Success = true,
            Users = users
        };

        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetSimulationUsersQueryResult>(It.IsAny<GetSimulationUsersQuery>()))
            .ReturnsAsync(userQueryResult);

        // Arrange - Then get devices for that user
        var devices = new List<Device>
        {
            new Device { DeviceUID = "device-001", OperatingSystemName = "Android" }
        };

        var deviceQueryResult = new GetSimulationUserDevicesQueryResult
        {
            Success = true,
            Devices = devices
        };

        _cqrsClientMock
            .Setup(c => c.SendQueryAsync<GetSimulationUserDevicesQueryResult>(It.IsAny<GetSimulationUserDevicesQuery>()))
            .ReturnsAsync(deviceQueryResult);

        // Act
        var userResult = await _sut.SearchUsers("test");
        var deviceResult = await _sut.GetUserDevices(userKeyField);

        // Assert
        userResult.Should().BeOfType<OkObjectResult>();
        deviceResult.Should().BeOfType<OkObjectResult>();
        
        var userOk = userResult as OkObjectResult;
        var deviceOk = deviceResult as OkObjectResult;
        
        var returnedUsers = userOk!.Value as IEnumerable<User>;
        var returnedDevices = deviceOk!.Value as IEnumerable<Device>;
        
        returnedUsers.Should().HaveCount(1);
        returnedDevices.Should().HaveCount(1);
    }

    #endregion
}
