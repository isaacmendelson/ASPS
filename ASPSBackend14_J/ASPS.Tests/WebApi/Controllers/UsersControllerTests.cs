using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebApi.Controllers;
using WebApi.DTOs;
using WebApi.Services;
using Common.Models;
using Common.Entities;
using Common.Enums;
using Business.Commands;
using Business.Queries;

namespace ASPS.Tests.WebApi.Controllers;

public class UsersControllerTests
{
    // Dependencies
    private readonly Mock<INetMQClientService> _netMQClientMock;
    private readonly Mock<ILogger<UsersController>> _loggerMock;
    
    // System Under Test
    private readonly UsersController _sut;

    public UsersControllerTests()
    {
        // Setup mocks
        _netMQClientMock = new Mock<INetMQClientService>();
        _loggerMock = new Mock<ILogger<UsersController>>();
        
        // Create instance
        _sut = new UsersController(_netMQClientMock.Object, _loggerMock.Object);
    }

    #region GetAll Tests

    [Fact]
    public async Task GetAll_WhenSuccessful_ReturnsOkWithUsers()
    {
        // Arrange
        var users = new List<User>
        {
            new User
            {
                KeycloakUserId = Guid.NewGuid().ToString(),
                FirstName = "John",
                LastName = "Doe",
                Email = "user1@example.com",
                Role = UserRole.Self,
                DateCreated = DateTime.UtcNow
            },
            new User
            {
                KeycloakUserId = Guid.NewGuid().ToString(),
                FirstName = "Jane",
                LastName = "Smith",
                Email = "user2@example.com",
                Role = UserRole.Guardian,
                DateCreated = DateTime.UtcNow
            }
        };

        var queryResult = new GetAllUsersQueryResult
        {
            Success = true,
            Users = users
        };

        _netMQClientMock
            .Setup(c => c.SendQueryAsync<GetAllUsersQuery, GetAllUsersQueryResult>(It.IsAny<GetAllUsersQuery>()))
            .ReturnsAsync(queryResult);

        // Act
        var result = await _sut.GetAll();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var responseUsers = okResult!.Value as IEnumerable<UserResponse>;
        responseUsers.Should().HaveCount(2);
        responseUsers!.First().FirstName.Should().Be("John");
    }

    [Fact]
    public async Task GetAll_WhenQueryFails_ReturnsBadRequest()
    {
        // Arrange
        var queryResult = new GetAllUsersQueryResult
        {
            Success = false,
            Message = "Database error",
            Users = new List<User>()
        };

        _netMQClientMock
            .Setup(c => c.SendQueryAsync<GetAllUsersQuery, GetAllUsersQueryResult>(It.IsAny<GetAllUsersQuery>()))
            .ReturnsAsync(queryResult);

        // Act
        var result = await _sut.GetAll();

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region GetByKey Tests

    [Fact]
    public async Task GetByKey_WithValidKey_ReturnsOkWithUser()
    {
        // Arrange
        var user = new User
        {
            KeycloakUserId = Guid.NewGuid().ToString(),
            FirstName = "John",
            LastName = "Doe",
            Email = "test@example.com",
            Role = UserRole.Self,
            DateCreated = DateTime.UtcNow
        };

        var queryResult = new GetUserByKeyQueryResult
        {
            Success = true,
            User = user
        };

        _netMQClientMock
            .Setup(c => c.SendQueryAsync<GetUserByKeyQuery, GetUserByKeyQueryResult>(It.IsAny<GetUserByKeyQuery>()))
            .ReturnsAsync(queryResult);

        // Act
        var result = await _sut.GetByKey("email", "test@example.com");

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as UserResponse;
        response.Should().NotBeNull();
        response!.FirstName.Should().Be("John");
    }

    [Fact]
    public async Task GetByKey_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        var queryResult = new GetUserByKeyQueryResult
        {
            Success = false,
            User = null,
            Message = "User not found"
        };

        _netMQClientMock
            .Setup(c => c.SendQueryAsync<GetUserByKeyQuery, GetUserByKeyQueryResult>(It.IsAny<GetUserByKeyQuery>()))
            .ReturnsAsync(queryResult);

        // Act
        var result = await _sut.GetByKey("email", "notfound@example.com");

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region GetDetails Tests

    [Fact]
    public async Task GetDetails_WithValidKey_ReturnsOkWithDetails()
    {
        // Arrange
        var user = new User
        {
            KeycloakUserId = Guid.NewGuid().ToString(),
            FirstName = "John",
            LastName = "Doe",
            Email = "test@example.com",
            Role = UserRole.Self,
            DateCreated = DateTime.UtcNow
        };

        var userQueryResult = new GetUserDetailsQueryResult
        {
            Success = true,
            User = user
        };

        var devicesQueryResult = new GetUserDevicesQueryResult
        {
            Success = true,
            Devices = new List<UserDevice>
            {
                new SmartPhone
                {
                    DeviceType = DeviceType.MobilePhone,
                    DeviceUid = "device-123",
                    MonitoringStatus = DeviceMonitoringStatus.Enabled,
                    DateCreated = DateTime.UtcNow
                }
            }
        };

        var accountsQueryResult = new GetUserAccountsQueryResult
        {
            Success = true,
            Accounts = new List<UserAccount>
            {
                new UserAccount
                {
                    AccountType = AccountType.Email,
                    UserName = "johndoe",
                    LoginUrl = "https://gmail.com",
                    DateCreated = DateTime.UtcNow
                }
            }
        };

        _netMQClientMock
            .Setup(c => c.SendQueryAsync<GetUserDetailsQuery, GetUserDetailsQueryResult>(It.IsAny<GetUserDetailsQuery>()))
            .ReturnsAsync(userQueryResult);

        _netMQClientMock
            .Setup(c => c.SendQueryAsync<GetUserDevicesQuery, GetUserDevicesQueryResult>(It.IsAny<GetUserDevicesQuery>()))
            .ReturnsAsync(devicesQueryResult);

        _netMQClientMock
            .Setup(c => c.SendQueryAsync<GetUserAccountsQuery, GetUserAccountsQueryResult>(It.IsAny<GetUserAccountsQuery>()))
            .ReturnsAsync(accountsQueryResult);

        // Act
        var result = await _sut.GetDetails("email", "test@example.com");

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as UserDetailsResponse;
        response.Should().NotBeNull();
        response!.FirstName.Should().Be("John");
        response.Devices.Should().HaveCount(1);
        response.Accounts.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetDetails_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        var queryResult = new GetUserDetailsQueryResult
        {
            Success = false,
            User = null,
            Message = "User not found"
        };

        _netMQClientMock
            .Setup(c => c.SendQueryAsync<GetUserDetailsQuery, GetUserDetailsQueryResult>(It.IsAny<GetUserDetailsQuery>()))
            .ReturnsAsync(queryResult);

        // Act
        var result = await _sut.GetDetails("email", "notfound@example.com");

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region Create Tests

    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreatedAtAction()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            KeycloakUserId = Guid.NewGuid().ToString(),
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Role = UserRole.Self
        };

        var commandResult = new CreateUserCommandResult
        {
            Success = true,
            UserKey = new Key("email", "john@example.com", "default"),
            Message = "User created successfully"
        };

        _netMQClientMock
            .Setup(c => c.SendCommandAsync<CreateUserCommand, CreateUserCommandResult>(It.IsAny<CreateUserCommand>()))
            .ReturnsAsync(commandResult);

        // Act
        var result = await _sut.Create(request);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result.Result as CreatedAtActionResult;
        createdResult!.ActionName.Should().Be(nameof(UsersController.GetByKey));
    }

    [Fact]
    public async Task Create_WhenCommandFails_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            KeycloakUserId = Guid.NewGuid().ToString(),
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Role = UserRole.Self
        };

        var commandResult = new CreateUserCommandResult
        {
            Success = false,
            Message = "User already exists"
        };

        _netMQClientMock
            .Setup(c => c.SendCommandAsync<CreateUserCommand, CreateUserCommandResult>(It.IsAny<CreateUserCommand>()))
            .ReturnsAsync(commandResult);

        // Act
        var result = await _sut.Create(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Update_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new UpdateUserRequest
        {
            FirstName = "John",
            LastName = "Updated",
            Address = "123 Main St",
            City = "New York",
            PhoneNumber = "+1234567890"
        };

        var commandResult = new UpdateUserCommandResult
        {
            Success = true,
            Message = "User updated successfully"
        };

        _netMQClientMock
            .Setup(c => c.SendCommandAsync<UpdateUserCommand, UpdateUserCommandResult>(It.IsAny<UpdateUserCommand>()))
            .ReturnsAsync(commandResult);

        // Act
        var result = await _sut.Update("email", "test@example.com", request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _netMQClientMock.Verify(c => c.SendCommandAsync<UpdateUserCommand, UpdateUserCommandResult>(
            It.Is<UpdateUserCommand>(cmd => 
                cmd.UserKey.Type == "email" && 
                cmd.UserKey.Value == "test@example.com" &&
                cmd.FirstName == "John" &&
                cmd.LastName == "Updated"
            )), Times.Once);
    }

    [Fact]
    public async Task Update_WhenCommandFails_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateUserRequest
        {
            FirstName = "John",
            LastName = "Updated"
        };

        var commandResult = new UpdateUserCommandResult
        {
            Success = false,
            Message = "User not found"
        };

        _netMQClientMock
            .Setup(c => c.SendCommandAsync<UpdateUserCommand, UpdateUserCommandResult>(It.IsAny<UpdateUserCommand>()))
            .ReturnsAsync(commandResult);

        // Act
        var result = await _sut.Update("email", "test@example.com", request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task Delete_WithValidKey_ReturnsOk()
    {
        // Arrange
        var commandResult = new DeleteUserCommandResult
        {
            Success = true,
            Message = "User deleted successfully"
        };

        _netMQClientMock
            .Setup(c => c.SendCommandAsync<DeleteUserCommand, DeleteUserCommandResult>(It.IsAny<DeleteUserCommand>()))
            .ReturnsAsync(commandResult);

        // Act
        var result = await _sut.Delete("email", "test@example.com");

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Delete_WhenCommandFails_ReturnsBadRequest()
    {
        // Arrange
        var commandResult = new DeleteUserCommandResult
        {
            Success = false,
            Message = "User not found"
        };

        _netMQClientMock
            .Setup(c => c.SendCommandAsync<DeleteUserCommand, DeleteUserCommandResult>(It.IsAny<DeleteUserCommand>()))
            .ReturnsAsync(commandResult);

        // Act
        var result = await _sut.Delete("email", "test@example.com");

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Delete_WhenExceptionThrown_ReturnsInternalServerError()
    {
        // Arrange
        _netMQClientMock
            .Setup(c => c.SendCommandAsync<DeleteUserCommand, DeleteUserCommandResult>(It.IsAny<DeleteUserCommand>()))
            .ThrowsAsync(new Exception("Network error"));

        // Act
        var result = await _sut.Delete("email", "test@example.com");

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
    }

    #endregion
}
