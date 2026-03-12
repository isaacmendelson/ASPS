using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Entities;
using Common.Models;
using FluentAssertions;
using Interface.Repositories;
using Moq;
using Xunit;

namespace ASPS.Tests.Interface;

/// <summary>
/// Tests for IUserRepository interface contract
/// </summary>
public class IUserRepositoryTests
{
    private readonly Mock<IUserRepository> _mockRepo;

    public IUserRepositoryTests()
    {
        _mockRepo = new Mock<IUserRepository>();
    }

    #region Interface Contract Tests

    [Fact]
    public void IUserRepository_InheritsFromIRepositoryOfUser()
    {
        // Assert
        typeof(IUserRepository).Should().Implement<IRepository<User>>();
    }

    [Fact]
    public async Task GetByKeycloakIdAsync_ReturnsUser_WhenExists()
    {
        // Arrange
        var keycloakId = "keycloak-123";
        var expectedUser = new User { KeyField = "user-1", FullName = "Test User" };
        _mockRepo.Setup(r => r.GetByKeycloakIdAsync(keycloakId))
                 .ReturnsAsync(expectedUser);

        // Act
        var result = await _mockRepo.Object.GetByKeycloakIdAsync(keycloakId);

        // Assert
        result.Should().NotBeNull();
        result!.FullName.Should().Be("Test User");
        _mockRepo.Verify(r => r.GetByKeycloakIdAsync(keycloakId), Times.Once);
    }

    [Fact]
    public async Task GetByKeycloakIdAsync_ReturnsNull_WhenNotExists()
    {
        // Arrange
        var keycloakId = "nonexistent";
        _mockRepo.Setup(r => r.GetByKeycloakIdAsync(keycloakId))
                 .ReturnsAsync((User?)null);

        // Act
        var result = await _mockRepo.Object.GetByKeycloakIdAsync(keycloakId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserWithDetailsAsync_ReturnsUserWithKey()
    {
        // Arrange
        var key = new Key("User", "user-123");
        var expectedUser = new User { KeyField = "user-123", FullName = "Detailed User" };
        _mockRepo.Setup(r => r.GetUserWithDetailsAsync(key))
                 .ReturnsAsync(expectedUser);

        // Act
        var result = await _mockRepo.Object.GetUserWithDetailsAsync(key);

        // Assert
        result.Should().NotBeNull();
        result!.FullName.Should().Be("Detailed User");
    }

    [Fact]
    public async Task GetActiveUsersAsync_ReturnsActiveUsers()
    {
        // Arrange
        var users = new List<User>
        {
            new User { KeyField = "user-1", FullName = "User 1" },
            new User { KeyField = "user-2", FullName = "User 2" }
        };
        _mockRepo.Setup(r => r.GetActiveUsersAsync())
                 .ReturnsAsync(users);

        // Act
        var result = await _mockRepo.Object.GetActiveUsersAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(u => u.FullName == "User 1");
    }

    #endregion
}

/// <summary>
/// Tests for IUserDeviceRepository interface contract
/// </summary>
public class IUserDeviceRepositoryTests
{
    private readonly Mock<IUserDeviceRepository> _mockRepo;

    public IUserDeviceRepositoryTests()
    {
        _mockRepo = new Mock<IUserDeviceRepository>();
    }

    #region Interface Contract Tests

    [Fact]
    public void IUserDeviceRepository_InheritsFromIRepositoryOfUserDevice()
    {
        // Assert
        typeof(IUserDeviceRepository).Should().Implement<IRepository<UserDevice>>();
    }

    [Fact]
    public async Task GetByUserKeyAsync_ReturnsDevicesForUser()
    {
        // Arrange
        var userKey = new Key("User", "user-123");
        var devices = new List<UserDevice>
        {
            new UserDevice { DeviceUid = "device-1" },
            new UserDevice { DeviceUid = "device-2" }
        };
        _mockRepo.Setup(r => r.GetByUserKeyAsync(userKey))
                 .ReturnsAsync(devices);

        // Act
        var result = await _mockRepo.Object.GetByUserKeyAsync(userKey);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(d => d.DeviceUid == "device-1");
    }

    [Fact]
    public async Task GetByDeviceUidAsync_ReturnsDevice_WhenExists()
    {
        // Arrange
        var deviceUid = "device-abc";
        var device = new UserDevice { DeviceUid = deviceUid };
        _mockRepo.Setup(r => r.GetByDeviceUidAsync(deviceUid))
                 .ReturnsAsync(device);

        // Act
        var result = await _mockRepo.Object.GetByDeviceUidAsync(deviceUid);

        // Assert
        result.Should().NotBeNull();
        result!.DeviceUid.Should().Be(deviceUid);
    }

    [Fact]
    public async Task GetMonitoredDevicesAsync_ReturnsMonitoredDevices()
    {
        // Arrange
        var devices = new List<UserDevice>
        {
            new UserDevice { DeviceUid = "monitored-1" }
        };
        _mockRepo.Setup(r => r.GetMonitoredDevicesAsync())
                 .ReturnsAsync(devices);

        // Act
        var result = await _mockRepo.Object.GetMonitoredDevicesAsync();

        // Assert
        result.Should().HaveCount(1);
    }

    #endregion
}

/// <summary>
/// Tests for IUserAccountRepository interface contract
/// </summary>
public class IUserAccountRepositoryTests
{
    private readonly Mock<IUserAccountRepository> _mockRepo;

    public IUserAccountRepositoryTests()
    {
        _mockRepo = new Mock<IUserAccountRepository>();
    }

    #region Interface Contract Tests

    [Fact]
    public void IUserAccountRepository_InheritsFromIRepositoryOfUserAccount()
    {
        // Assert
        typeof(IUserAccountRepository).Should().Implement<IRepository<UserAccount>>();
    }

    [Fact]
    public async Task GetByUserKeyAsync_ReturnsAccountsForUser()
    {
        // Arrange
        var userKey = new Key("User", "user-123");
        var accounts = new List<UserAccount>
        {
            new UserAccount { UserName = "account1" }
        };
        _mockRepo.Setup(r => r.GetByUserKeyAsync(userKey))
                 .ReturnsAsync(accounts);

        // Act
        var result = await _mockRepo.Object.GetByUserKeyAsync(userKey);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByUserNameAsync_ReturnsAccount_WhenExists()
    {
        // Arrange
        var userName = "testuser";
        var account = new UserAccount { UserName = userName };
        _mockRepo.Setup(r => r.GetByUserNameAsync(userName))
                 .ReturnsAsync(account);

        // Act
        var result = await _mockRepo.Object.GetByUserNameAsync(userName);

        // Assert
        result.Should().NotBeNull();
        result!.UserName.Should().Be(userName);
    }

    #endregion
}

/// <summary>
/// Tests for IDeviceAlertRepository interface contract
/// </summary>
public class IDeviceAlertRepositoryTests
{
    private readonly Mock<IDeviceAlertRepository> _mockRepo;

    public IDeviceAlertRepositoryTests()
    {
        _mockRepo = new Mock<IDeviceAlertRepository>();
    }

    #region Interface Contract Tests

    [Fact]
    public void IDeviceAlertRepository_InheritsFromIRepositoryOfDeviceAlertEntity()
    {
        // Assert
        typeof(IDeviceAlertRepository).Should().Implement<IRepository<DeviceAlertEntity>>();
    }

    [Fact]
    public async Task GetAlertsByDeviceUidAsync_ReturnsAlertsForDevice()
    {
        // Arrange
        var deviceUid = "device-123";
        var alerts = new List<DeviceAlertEntity>
        {
            new DeviceAlertEntity { DeviceUid = deviceUid }
        };
        _mockRepo.Setup(r => r.GetAlertsByDeviceUidAsync(deviceUid))
                 .ReturnsAsync(alerts);

        // Act
        var result = await _mockRepo.Object.GetAlertsByDeviceUidAsync(deviceUid);

        // Assert
        result.Should().HaveCount(1);
        result.First().DeviceUid.Should().Be(deviceUid);
    }

    [Fact]
    public async Task GetAlertsByUserKeyAsync_ReturnsAlertsForUser()
    {
        // Arrange
        var userKey = new Key("User", "user-123");
        var alerts = new List<DeviceAlertEntity>
        {
            new DeviceAlertEntity { DeviceUid = "device-1" }
        };
        _mockRepo.Setup(r => r.GetAlertsByUserKeyAsync(userKey))
                 .ReturnsAsync(alerts);

        // Act
        var result = await _mockRepo.Object.GetAlertsByUserKeyAsync(userKey);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetRecentAlertsAsync_ReturnsAlertsWithinTimeSpan()
    {
        // Arrange
        var timeSpan = TimeSpan.FromHours(24);
        var alerts = new List<DeviceAlertEntity>
        {
            new DeviceAlertEntity { DeviceUid = "device-1" }
        };
        _mockRepo.Setup(r => r.GetRecentAlertsAsync(timeSpan))
                 .ReturnsAsync(alerts);

        // Act
        var result = await _mockRepo.Object.GetRecentAlertsAsync(timeSpan);

        // Assert
        result.Should().HaveCount(1);
    }

    #endregion
}

/// <summary>
/// Tests for IAnalysisResultRepository interface contract
/// </summary>
public class IAnalysisResultRepositoryTests
{
    private readonly Mock<IAnalysisResultRepository> _mockRepo;

    public IAnalysisResultRepositoryTests()
    {
        _mockRepo = new Mock<IAnalysisResultRepository>();
    }

    #region Interface Contract Tests

    [Fact]
    public void IAnalysisResultRepository_InheritsFromIRepositoryOfAnalysisResultContainer()
    {
        // Assert
        typeof(IAnalysisResultRepository).Should().Implement<IRepository<AnalysisResultContainer>>();
    }

    [Fact]
    public async Task GetByUserKeyAsync_ReturnsResultsForUser()
    {
        // Arrange
        var userKey = new Key("User", "user-123");
        var results = new List<AnalysisResultContainer>
        {
            new AnalysisResultContainer("result-1", "user-123", "Test", DateTime.UtcNow, "{}", false, null)
        };
        _mockRepo.Setup(r => r.GetByUserKeyAsync(userKey))
                 .ReturnsAsync(results);

        // Act
        var result = await _mockRepo.Object.GetByUserKeyAsync(userKey);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetLatestAsync_ReturnsLatestResult()
    {
        // Arrange
        var userKey = new Key("User", "user-123");
        var latest = new AnalysisResultContainer("result-latest", "user-123", "Test", DateTime.UtcNow, "{}", false, null);
        _mockRepo.Setup(r => r.GetLatestAsync(userKey))
                 .ReturnsAsync(latest);

        // Act
        var result = await _mockRepo.Object.GetLatestAsync(userKey);

        // Assert
        result.Should().NotBeNull();
        result!.Discriminator.Should().Be("Test");
    }

    #endregion
}

/// <summary>
/// Tests for IAlertFlagRepository interface contract
/// </summary>
public class IAlertFlagRepositoryTests
{
    private readonly Mock<IAlertFlagRepository> _mockRepo;

    public IAlertFlagRepositoryTests()
    {
        _mockRepo = new Mock<IAlertFlagRepository>();
    }

    #region Interface Contract Tests

    [Fact]
    public async Task AddAsync_ReturnsAddedFlag()
    {
        // Arrange
        var flag = new AlertFlag { Key = 1, UserKey = 123 };
        _mockRepo.Setup(r => r.AddAsync(flag))
                 .ReturnsAsync(flag);

        // Act
        var result = await _mockRepo.Object.AddAsync(flag);

        // Assert
        result.Should().NotBeNull();
        result.Key.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFlag()
    {
        // Arrange
        var flag = new AlertFlag { Key = 1, UserKey = 123 };
        _mockRepo.Setup(r => r.UpdateAsync(flag))
                 .Returns(Task.CompletedTask);

        // Act
        await _mockRepo.Object.UpdateAsync(flag);

        // Assert
        _mockRepo.Verify(r => r.UpdateAsync(flag), Times.Once);
    }

    [Fact]
    public async Task GetOpenFlagsByUserAsync_ReturnsOpenFlags()
    {
        // Arrange
        var userKey = 123;
        var flags = new List<AlertFlag>
        {
            new AlertFlag { Key = 1, UserKey = userKey }
        };
        _mockRepo.Setup(r => r.GetOpenFlagsByUserAsync(userKey))
                 .ReturnsAsync(flags);

        // Act
        var result = await _mockRepo.Object.GetOpenFlagsByUserAsync(userKey);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task CloseFlag_ClosesFlag()
    {
        // Arrange
        var flagKey = 1;
        _mockRepo.Setup(r => r.CloseFlag(flagKey))
                 .Returns(Task.CompletedTask);

        // Act
        await _mockRepo.Object.CloseFlag(flagKey);

        // Assert
        _mockRepo.Verify(r => r.CloseFlag(flagKey), Times.Once);
    }

    #endregion
}

/// <summary>
/// Tests for ISafeDomainRepository interface contract
/// </summary>
public class ISafeDomainRepositoryTests
{
    private readonly Mock<ISafeDomainRepository> _mockRepo;

    public ISafeDomainRepositoryTests()
    {
        _mockRepo = new Mock<ISafeDomainRepository>();
    }

    #region Interface Contract Tests

    [Fact]
    public async Task GetAllActiveAsync_ReturnsActiveDomains()
    {
        // Arrange
        var domains = new List<SafeDomain>
        {
            new SafeDomain { Domain = "google.com" }
        };
        _mockRepo.Setup(r => r.GetAllActiveAsync())
                 .ReturnsAsync(domains);

        // Act
        var result = await _mockRepo.Object.GetAllActiveAsync();

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task IsSafeDomainAsync_ReturnsTrue_WhenDomainIsSafe()
    {
        // Arrange
        var domain = "google.com";
        _mockRepo.Setup(r => r.IsSafeDomainAsync(domain))
                 .ReturnsAsync(true);

        // Act
        var result = await _mockRepo.Object.IsSafeDomainAsync(domain);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsSafeDomainAsync_ReturnsFalse_WhenDomainIsNotSafe()
    {
        // Arrange
        var domain = "malicious.com";
        _mockRepo.Setup(r => r.IsSafeDomainAsync(domain))
                 .ReturnsAsync(false);

        // Act
        var result = await _mockRepo.Object.IsSafeDomainAsync(domain);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
