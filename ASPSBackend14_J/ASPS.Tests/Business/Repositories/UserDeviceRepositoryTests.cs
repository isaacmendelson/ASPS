using Business.Data.EF;
using Business.Data.EF.Repositories;
using Common.Entities;
using Common.Enums;
using Common.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASPS.Tests.Business.Repositories;

public class UserDeviceRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly UserDeviceRepository _repository;
    private readonly User _testUser;

    public UserDeviceRepositoryTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _repository = new UserDeviceRepository(_context);

        // Create test user
        _testUser = new User 
        { 
            FirstName = "Test", 
            LastName = "User", 
            Email = "test@example.com", 
            KeycloakUserId = "kc-test" 
        };
        _context.Users.Add(_testUser);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GetByUserKeyAsync Tests

    [Fact]
    public async Task GetByUserKeyAsync_WithValidUserKey_ReturnsDevices()
    {
        // Arrange
        var device1 = new SmartPhone { DeviceUid = "device-uid-1", Make = "Apple", Model = "iPhone 13", UserKey = _testUser.Key };
        var device2 = new PersonalComputer { DeviceUid = "device-uid-2", Make = "Apple", Model = "MacBook Pro", UserKey = _testUser.Key };
        var otherUser = new User 
        { 
            FirstName = "Other", 
            LastName = "User", 
            Email = "other@example.com", 
            KeycloakUserId = "kc-other" 
        };
        _context.Users.Add(otherUser);
        var device3 = new SmartPhone { DeviceUid = "device-uid-3", Make = "Apple", Model = "iPad", UserKey = otherUser.Key };

        _context.UserDevices.AddRange(device1, device2, device3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByUserKeyAsync(_testUser.Key);

        // Assert
        var devices = result.ToList();
        devices.Should().HaveCount(2);
        devices.Should().Contain(d => d.DeviceUid == "device-uid-1");
        devices.Should().Contain(d => d.DeviceUid == "device-uid-2");
        devices.Should().NotContain(d => d.DeviceUid == "device-uid-3");
    }

    [Fact]
    public async Task GetByUserKeyAsync_ExcludesDeletedDevices()
    {
        // Arrange
        var device1 = new SmartPhone { DeviceUid = "device-active", Make = "Apple", Model = "iPhone", UserKey = _testUser.Key };
        var device2 = new SmartPhone { DeviceUid = "device-deleted", Make = "Samsung", Model = "Android", UserKey = _testUser.Key, IsDeleted = true };

        _context.UserDevices.AddRange(device1, device2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByUserKeyAsync(_testUser.Key);

        // Assert
        var devices = result.ToList();
        devices.Should().HaveCount(1);
        devices[0].DeviceUid.Should().Be("device-active");
    }

    [Fact]
    public async Task GetByUserKeyAsync_WithNoDevices_ReturnsEmpty()
    {
        // Act
        var result = await _repository.GetByUserKeyAsync(_testUser.Key);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetByDeviceUidAsync Tests

    [Fact]
    public async Task GetByDeviceUidAsync_WithValidUid_ReturnsDevice()
    {
        // Arrange
        var deviceUid = "unique-device-uid";
        var device = new SmartPhone { DeviceUid = deviceUid, Make = "Test", Model = "Device", UserKey = _testUser.Key };
        _context.UserDevices.Add(device);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByDeviceUidAsync(deviceUid);

        // Assert
        result.Should().NotBeNull();
        result!.DeviceUid.Should().Be(deviceUid);
        result.Model.Should().Be("Device");
    }

    [Fact]
    public async Task GetByDeviceUidAsync_WithDeletedDevice_ReturnsNull()
    {
        // Arrange
        var deviceUid = "deleted-device";
        var device = new SmartPhone { DeviceUid = deviceUid, Make = "Deleted", Model = "Device", UserKey = _testUser.Key, IsDeleted = true };
        _context.UserDevices.Add(device);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByDeviceUidAsync(deviceUid);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByDeviceUidAsync_WithNonExistentUid_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByDeviceUidAsync("non-existent-uid");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetMonitoredDevicesAsync Tests

    [Fact]
    public async Task GetMonitoredDevicesAsync_ReturnsOnlyEnabledDevices()
    {
        // Arrange
        var enabled1 = new SmartPhone { DeviceUid = "enabled-1", Make = "Device", Model = "1", UserKey = _testUser.Key, MonitoringStatus = DeviceMonitoringStatus.Enabled };
        
        var enabled2 = new SmartPhone { DeviceUid = "enabled-2", Make = "Device", Model = "2", UserKey = _testUser.Key, MonitoringStatus = DeviceMonitoringStatus.Enabled };
        
        var disabled = new SmartPhone { DeviceUid = "disabled", Make = "Device", Model = "3", UserKey = _testUser.Key, MonitoringStatus = DeviceMonitoringStatus.Disabled };

        _context.UserDevices.AddRange(enabled1, enabled2, disabled);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetMonitoredDevicesAsync();

        // Assert
        var devices = result.ToList();
        devices.Should().HaveCount(2);
        devices.Should().Contain(d => d.DeviceUid == "enabled-1");
        devices.Should().Contain(d => d.DeviceUid == "enabled-2");
        devices.Should().NotContain(d => d.MonitoringStatus == DeviceMonitoringStatus.Disabled);
    }

    [Fact]
    public async Task GetMonitoredDevicesAsync_ExcludesDeletedDevices()
    {
        // Arrange
        var enabled = new SmartPhone { DeviceUid = "enabled", Make = "Enabled", Model = "Phone", UserKey = _testUser.Key, MonitoringStatus = DeviceMonitoringStatus.Enabled };
        
        var enabledButDeleted = new SmartPhone { DeviceUid = "deleted", Make = "Deleted", Model = "Phone", UserKey = _testUser.Key, MonitoringStatus = DeviceMonitoringStatus.Enabled, IsDeleted = true };

        _context.UserDevices.AddRange(enabled, enabledButDeleted);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetMonitoredDevicesAsync();

        // Assert
        var devices = result.ToList();
        devices.Should().HaveCount(1);
        devices[0].DeviceUid.Should().Be("enabled");
    }

    [Fact]
    public async Task GetMonitoredDevicesAsync_WithNoMonitoredDevices_ReturnsEmpty()
    {
        // Arrange
        var device = new SmartPhone { DeviceUid = "disabled", Make = "Device", Model = "Test", UserKey = _testUser.Key, MonitoringStatus = DeviceMonitoringStatus.Disabled };
        _context.UserDevices.Add(device);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetMonitoredDevicesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Edge Cases

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByDeviceUidAsync_WithNullOrEmptyUid_ReturnsNull(string? deviceUid)
    {
        // Act
        var result = await _repository.GetByDeviceUidAsync(deviceUid!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task MultipleUsers_CanHaveDevicesWithSimilarNames()
    {
        // Arrange
        var user2 = new User 
        { 
            FirstName = "User", 
            LastName = "Two", 
            Email = "user2@example.com", 
            KeycloakUserId = "kc-2" 
        };
        _context.Users.Add(user2);
        
        var device1 = new SmartPhone { DeviceUid = "uid-1", Make = "Apple", Model = "iPhone", UserKey = _testUser.Key };
        var device2 = new SmartPhone { DeviceUid = "uid-2", Make = "Apple", Model = "iPhone", UserKey = user2.Key };

        _context.UserDevices.AddRange(device1, device2);
        await _context.SaveChangesAsync();

        // Act
        var user1Devices = await _repository.GetByUserKeyAsync(_testUser.Key);
        var user2Devices = await _repository.GetByUserKeyAsync(user2.Key);

        // Assert
        user1Devices.Should().HaveCount(1);
        user2Devices.Should().HaveCount(1);
    }

    #endregion
}
