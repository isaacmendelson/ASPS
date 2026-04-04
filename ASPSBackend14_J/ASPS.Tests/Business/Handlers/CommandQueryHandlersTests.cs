using Xunit;
using Moq;
using FluentAssertions;
using Business.Handlers;
using Business.Commands;
using Business.Queries;
using Business.Views;
using Common.Entities;
using Common.Enums;
using Common.Interfaces;
using Common.Models;
using Interface.Repositories;
using Business.DomainEvents;

namespace ASPS.Tests.Business.Handlers;

public class CommandQueryHandlersTests
{
    #region UserCommandHandlers Tests

    public class UserCommandHandlersTests
    {
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly Mock<ASView> _asViewMock;
        private readonly UserCommandHandlers _sut;

        public UserCommandHandlersTests()
        {
            _userRepositoryMock = new Mock<IUserRepository>();
            
            // Create proper mocks for ASView dependencies
            var serviceProviderMock = new Mock<IServiceProvider>();
            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<ASView>>();
            
            _asViewMock = new Mock<ASView>(serviceProviderMock.Object, loggerMock.Object);
            _sut = new UserCommandHandlers(_userRepositoryMock.Object, _asViewMock.Object);
        }

        [Fact]
        public async Task HandleAsync_CreateUser_WithValidCommand_ReturnsSuccess()
        {
            // Arrange
            var command = new CreateUserCommand
            {
                KeycloakUserId = "kc-123",
                FirstName = "John",
                LastName = "Doe",
                Role = UserRole.Self
            };

            var createdUser = new User();
            _userRepositoryMock.Setup(r => r.AddAsync(It.IsAny<User>())).ReturnsAsync(createdUser);

            // Act
            var result = await _sut.HandleAsync(command);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue($"Error: {result.Message}");
            _asViewMock.Verify(v => v.Handle(It.IsAny<IDomainEvent>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_CreateUser_WhenException_ReturnsFailure()
        {
            // Arrange
            var command = new CreateUserCommand
            {
                FirstName = "Test",
                LastName = "User",
                Role = UserRole.Self
            };

            _userRepositoryMock.Setup(r => r.AddAsync(It.IsAny<User>()))
                .ThrowsAsync(new Exception("DB error"));

            // Act
            var result = await _sut.HandleAsync(command);

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Error creating user");
        }

        [Fact]
        public async Task HandleAsync_UpdateUser_WithValidCommand_ReturnsSuccess()
        {
            // Arrange
            var existingUser = new User { FirstName = "Old" };
            _userRepositoryMock.Setup(r => r.GetByKeyAsync(It.IsAny<Key>())).ReturnsAsync(existingUser);

            var command = new UpdateUserCommand
            {
                UserKey = new Key("User", "user-123"),
                FirstName = "New",
                LastName = "Name",
                Address = "123 Main St",
                City = "NYC",
                PhoneNumber = "555-0100"
            };

            // Act
            var result = await _sut.HandleAsync(command);

            // Assert
            result.Success.Should().BeTrue();
            existingUser.FirstName.Should().Be("New");
            existingUser.LastName.Should().Be("Name");
            _userRepositoryMock.Verify(r => r.UpdateAsync(existingUser), Times.Once);
            _asViewMock.Verify(v => v.Handle(It.IsAny<IDomainEvent>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_UpdateUser_WhenUserNotFound_ReturnsFailure()
        {
            // Arrange
            _userRepositoryMock.Setup(r => r.GetByKeyAsync(It.IsAny<Key>())).ReturnsAsync((User)null);

            var command = new UpdateUserCommand { UserKey = new Key("User", "missing") };

            // Act
            var result = await _sut.HandleAsync(command);

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Be("User not found");
        }

        [Fact]
        public async Task HandleAsync_DeleteUser_WithValidKey_ReturnsSuccess()
        {
            // Arrange
            var command = new DeleteUserCommand { UserKey = new Key("User", "user-123") };

            // Act
            var result = await _sut.HandleAsync(command);

            // Assert
            result.Success.Should().BeTrue();
            _userRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Key>()), Times.Once);
            _asViewMock.Verify(v => v.Handle(It.IsAny<IDomainEvent>()), Times.Once);
        }
    }

    #endregion

    #region UserQueryHandlers Tests

    public class UserQueryHandlersTests
    {
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly UserQueryHandlers _sut;

        public UserQueryHandlersTests()
        {
            _userRepositoryMock = new Mock<IUserRepository>();
            _sut = new UserQueryHandlers(_userRepositoryMock.Object);
        }

        [Fact]
        public async Task HandleAsync_GetAllUsers_ReturnsAllUsers()
        {
            // Arrange
            var users = new List<User>
            {
                new User { FirstName = "John" },
                new User { FirstName = "Jane" }
            };

            _userRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(users);

            var query = new GetAllUsersQuery();

            // Act
            var result = await _sut.HandleAsync(query);

            // Assert
            result.Success.Should().BeTrue();
            result.Users.Should().HaveCount(2);
            result.Users[0].FirstName.Should().Be("John");
        }

        [Fact]
        public async Task HandleAsync_GetAllUsers_WhenException_ReturnsFailure()
        {
            // Arrange
            _userRepositoryMock.Setup(r => r.GetAllAsync()).ThrowsAsync(new Exception("DB error"));

            var query = new GetAllUsersQuery();

            // Act
            var result = await _sut.HandleAsync(query);

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Error");
        }

        [Fact]
        public async Task HandleAsync_GetUserByKey_WhenUserExists_ReturnsUser()
        {
            // Arrange
            var user = new User { FirstName = "John" };  // Key is auto-assigned
            _userRepositoryMock.Setup(r => r.GetByKeyAsync(It.IsAny<Key>())).ReturnsAsync(user);

            var query = new GetUserByKeyQuery { UserKey = new Key("User", "user-123") };

            // Act
            var result = await _sut.HandleAsync(query);

            // Assert
            result.Success.Should().BeTrue();
            result.User.Should().NotBeNull();
            result.User.FirstName.Should().Be("John");
            result.Message.Should().Be("User found");
        }

        [Fact]
        public async Task HandleAsync_GetUserByKey_WhenUserNotFound_ReturnsNull()
        {
            // Arrange
            _userRepositoryMock.Setup(r => r.GetByKeyAsync(It.IsAny<Key>())).ReturnsAsync((User)null);

            var query = new GetUserByKeyQuery { UserKey = new Key("User", "missing") };

            // Act
            var result = await _sut.HandleAsync(query);

            // Assert
            result.Success.Should().BeTrue();
            result.User.Should().BeNull();
            result.Message.Should().Be("User not found");
        }

        [Fact]
        public async Task HandleAsync_GetUserDetails_ReturnsUserWithDetails()
        {
            // Arrange
            var user = new User();  // Key is auto-assigned
            _userRepositoryMock.Setup(r => r.GetUserWithDetailsAsync(It.IsAny<Key>())).ReturnsAsync(user);

            var query = new GetUserDetailsQuery { UserKey = new Key("User", "user-123") };

            // Act
            var result = await _sut.HandleAsync(query);

            // Assert
            result.Success.Should().BeTrue();
            result.User.Should().NotBeNull();
        }

        [Fact]
        public async Task HandleAsync_GetUserDevices_ReturnsEmptyList()
        {
            // Arrange
            var query = new GetUserDevicesQuery { UserKey = new Key("User", "user-123") };

            // Act
            var result = await _sut.HandleAsync(query);

            // Assert
            result.Success.Should().BeTrue();
            result.Devices.Should().BeEmpty();
        }

        [Fact]
        public async Task HandleAsync_GetUserAccounts_ReturnsEmptyList()
        {
            // Arrange
            var query = new GetUserAccountsQuery { UserKey = new Key("User", "user-123") };

            // Act
            var result = await _sut.HandleAsync(query);

            // Assert
            result.Success.Should().BeTrue();
            result.Accounts.Should().BeEmpty();
        }
    }

    #endregion

    #region UserDeviceCommandHandlers Tests

    public class UserDeviceCommandHandlersTests
    {
        private readonly Mock<IUserDeviceRepository> _deviceRepositoryMock;
        private readonly UserDeviceCommandHandlers _sut;

        public UserDeviceCommandHandlersTests()
        {
            _deviceRepositoryMock = new Mock<IUserDeviceRepository>();
            _sut = new UserDeviceCommandHandlers(_deviceRepositoryMock.Object);
        }

        [Fact]
        public async Task HandleAsync_CreateUserDevice_WithPersonalComputer_ReturnsSuccess()
        {
            // Arrange
            var command = new CreateUserDeviceCommand
            {
                UserKey = new Key("User", "user-123"),
                DeviceUid = "device-uid",
                DeviceType = DeviceType.PersonalComputer,
                OperatingSystem = OperatingSystemType.Windows,
                Make = "Dell",
                Model = "Latitude"
            };

            var createdDevice = new PersonalComputer();
            _deviceRepositoryMock.Setup(r => r.AddAsync(It.IsAny<UserDevice>())).ReturnsAsync(createdDevice);

            // Act
            var result = await _sut.HandleAsync(command);

            // Assert
            result.Success.Should().BeTrue();
            _deviceRepositoryMock.Verify(r => r.AddAsync(It.IsAny<PersonalComputer>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_CreateUserDevice_WithSmartPhone_ReturnsSuccess()
        {
            // Arrange
            var command = new CreateUserDeviceCommand
            {
                UserKey = new Key("User", "user-123"),
                DeviceUid = "device-uid",
                DeviceType = DeviceType.MobilePhone,
                PhoneNumber = "555-0100",
                OperatingSystem = OperatingSystemType.IOS,
                Make = "Apple",
                Model = "iPhone 14"
            };

            var createdDevice = new SmartPhone();
            _deviceRepositoryMock.Setup(r => r.AddAsync(It.IsAny<UserDevice>())).ReturnsAsync(createdDevice);

            // Act
            var result = await _sut.HandleAsync(command);

            // Assert
            result.Success.Should().BeTrue();
            _deviceRepositoryMock.Verify(r => r.AddAsync(It.IsAny<SmartPhone>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_CreateUserDevice_WhenException_ReturnsFailure()
        {
            // Arrange
            var command = new CreateUserDeviceCommand
            {
                UserKey = new Key("User", "user-123"),
                DeviceType = DeviceType.PersonalComputer
            };

            _deviceRepositoryMock.Setup(r => r.AddAsync(It.IsAny<UserDevice>()))
                .ThrowsAsync(new Exception("DB error"));

            // Act
            var result = await _sut.HandleAsync(command);

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Error");
        }

        [Fact]
        public async Task HandleAsync_UpdateUserDevice_WithValidDevice_ReturnsSuccess()
        {
            // Arrange
            var existingDevice = new PersonalComputer();
            _deviceRepositoryMock.Setup(r => r.GetByKeyAsync(It.IsAny<Key>())).ReturnsAsync(existingDevice);

            var command = new UpdateUserDeviceCommand
            {
                DeviceKey = new Key("Device", "device-123"),
                MonitoringStatus = DeviceMonitoringStatus.Enabled
            };

            // Act
            var result = await _sut.HandleAsync(command);

            // Assert
            result.Success.Should().BeTrue();
            existingDevice.MonitoringStatus.Should().Be(DeviceMonitoringStatus.Enabled);
            _deviceRepositoryMock.Verify(r => r.UpdateAsync(existingDevice), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_UpdateUserDevice_WhenDeviceNotFound_ReturnsFailure()
        {
            // Arrange
            _deviceRepositoryMock.Setup(r => r.GetByKeyAsync(It.IsAny<Key>())).ReturnsAsync((UserDevice)null);

            var command = new UpdateUserDeviceCommand { DeviceKey = new Key("Device", "missing") };

            // Act
            var result = await _sut.HandleAsync(command);

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Be("Device not found");
        }

        [Fact]
        public async Task HandleAsync_DeleteUserDevice_WithValidKey_ReturnsSuccess()
        {
            // Arrange
            var command = new DeleteUserDeviceCommand { DeviceKey = new Key("Device", "device-123") };

            // Act
            var result = await _sut.HandleAsync(command);

            // Assert
            result.Success.Should().BeTrue();
            _deviceRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Key>()), Times.Once);
        }
    }

    #endregion
}
