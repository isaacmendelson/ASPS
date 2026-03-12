using Business.Data.EF;
using Business.Data.EF.Repositories;
using Common.Entities;
using Common.Enums;
using Common.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASPS.Tests.Business;

/// <summary>
/// Unit tests for the generic IRepository<T> implementation (Repository<T>)
/// Tests use SmartPhone as a concrete entity for testing generic repository functionality
/// </summary>
public class IRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Repository<SmartPhone> _repository;

    public IRepositoryTests()
    {
        // Setup in-memory database with unique name per test instance
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _repository = new Repository<SmartPhone>(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    // Helper method to create a SmartPhone for testing
    private SmartPhone CreateSmartPhone(string deviceUid, string phoneNumber = "+1234567890")
    {
        return new SmartPhone
        {
            UserKeyField = Guid.NewGuid().ToString(),
            DeviceUid = deviceUid,
            DeviceType = DeviceType.MobilePhone,
            OperatingSystem = OperatingSystemType.IOS,
            PhoneNumber = phoneNumber,
            Make = "Apple",
            Model = "iPhone",
            MonitoringStatus = DeviceMonitoringStatus.Enabled
        };
    }

    #region GetByKeyAsync Tests

    [Fact]
    public async Task GetByKeyAsync_WithValidKey_ReturnsEntity()
    {
        // Arrange
        var device = CreateSmartPhone("test-device-123");
        _context.UserDevices.Add(device);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByKeyAsync(device.Key);

        // Assert
        result.Should().NotBeNull();
        result!.Key.Should().Be(device.Key);
        result.DeviceUid.Should().Be("test-device-123");
    }

    [Fact]
    public async Task GetByKeyAsync_WithDeletedEntity_ReturnsNull()
    {
        // Arrange
        var device = CreateSmartPhone("deleted-device");
        device.IsDeleted = true;
        device.DateDeleted = DateTime.UtcNow;
        _context.UserDevices.Add(device);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByKeyAsync(device.Key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByKeyAsync_WithNonExistentKey_ReturnsNull()
    {
        // Arrange
        var nonExistentKey = new Key("SmartPhone", Guid.NewGuid().ToString());

        // Act
        var result = await _repository.GetByKeyAsync(nonExistentKey);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_WithMultipleEntities_ReturnsAllNonDeleted()
    {
        // Arrange
        var device1 = CreateSmartPhone("device-1", "+11111111111");
        var device2 = CreateSmartPhone("device-2", "+22222222222");
        var device3 = CreateSmartPhone("device-3", "+33333333333");
        device3.IsDeleted = true; // Deleted device
        
        _context.UserDevices.AddRange(device1, device2, device3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        var devices = result.ToList();
        devices.Should().HaveCount(2);
        devices.Should().Contain(d => d.DeviceUid == "device-1");
        devices.Should().Contain(d => d.DeviceUid == "device-2");
        devices.Should().NotContain(d => d.DeviceUid == "device-3");
    }

    [Fact]
    public async Task GetAllAsync_WithEmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_WithOnlyDeletedEntities_ReturnsEmptyList()
    {
        // Arrange
        var device1 = CreateSmartPhone("device-1");
        var device2 = CreateSmartPhone("device-2");
        device1.IsDeleted = true;
        device2.IsDeleted = true;
        
        _context.UserDevices.AddRange(device1, device2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_WithValidEntity_AddsToDatabase()
    {
        // Arrange
        var device = CreateSmartPhone("new-device-123");

        // Act
        var result = await _repository.AddAsync(device);

        // Assert
        result.Should().NotBeNull();
        result.DeviceUid.Should().Be("new-device-123");
        result.DateCreated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        
        // Verify it's in the database
        var dbDevices = await _context.UserDevices.OfType<SmartPhone>().ToListAsync();
        dbDevices.Should().HaveCount(1);
        dbDevices[0].DeviceUid.Should().Be("new-device-123");
    }

    [Fact]
    public async Task AddAsync_SetsDateCreated()
    {
        // Arrange
        var device = CreateSmartPhone("test-device");
        var beforeAdd = DateTime.UtcNow;

        // Act
        await _repository.AddAsync(device);

        // Assert
        device.DateCreated.Should().BeOnOrAfter(beforeAdd);
        device.DateCreated.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WithValidEntity_UpdatesInDatabase()
    {
        // Arrange
        var device = CreateSmartPhone("update-device");
        await _repository.AddAsync(device);
        
        // Modify the device
        device.Model = "iPhone 15 Pro";
        device.Make = "Apple Inc.";

        // Act
        await _repository.UpdateAsync(device);

        // Assert
        device.DateModified.Should().NotBeNull();
        device.DateModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        
        // Verify in database
        var dbDevices = await _context.UserDevices.OfType<SmartPhone>().ToListAsync();
        var dbDevice = dbDevices.FirstOrDefault(d => d.DeviceUid == "update-device");
        dbDevice.Should().NotBeNull();
        dbDevice!.Model.Should().Be("iPhone 15 Pro");
        dbDevice.Make.Should().Be("Apple Inc.");
    }

    [Fact]
    public async Task UpdateAsync_SetsDateModified()
    {
        // Arrange
        var device = CreateSmartPhone("test-device");
        await _repository.AddAsync(device);
        var beforeUpdate = DateTime.UtcNow;

        // Act
        await _repository.UpdateAsync(device);

        // Assert
        device.DateModified.Should().NotBeNull();
        device.DateModified.Should().BeOnOrAfter(beforeUpdate);
        device.DateModified.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_WithExistingEntity_SoftDeletesEntity()
    {
        // Arrange
        var device = CreateSmartPhone("delete-device");
        await _repository.AddAsync(device);
        var beforeDelete = DateTime.UtcNow;

        // Act
        await _repository.DeleteAsync(device.Key);

        // Assert
        var dbDevices = await _context.UserDevices.IgnoreQueryFilters().OfType<SmartPhone>().ToListAsync();
        var dbDevice = dbDevices.FirstOrDefault(d => d.DeviceUid == "delete-device");
        dbDevice.Should().NotBeNull();
        dbDevice!.IsDeleted.Should().BeTrue();
        dbDevice.DateDeleted.Should().NotBeNull();
        dbDevice.DateDeleted.Should().BeOnOrAfter(beforeDelete);
        dbDevice.DateDeleted.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentKey_DoesNotThrow()
    {
        // Arrange
        var nonExistentKey = new Key("SmartPhone", Guid.NewGuid().ToString());

        // Act
        Func<Task> act = async () => await _repository.DeleteAsync(nonExistentKey);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteAsync_WithAlreadyDeletedEntity_DoesNotThrow()
    {
        // Arrange
        var device = CreateSmartPhone("already-deleted");
        device.IsDeleted = true;
        await _repository.AddAsync(device);

        // Act
        Func<Task> act = async () => await _repository.DeleteAsync(device.Key);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region ExistsAsync Tests

    [Fact]
    public async Task ExistsAsync_WithExistingEntity_ReturnsTrue()
    {
        // Arrange
        var device = CreateSmartPhone("exists-device");
        await _repository.AddAsync(device);

        // Act
        var result = await _repository.ExistsAsync(device.Key);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistentKey_ReturnsFalse()
    {
        // Arrange
        var nonExistentKey = new Key("SmartPhone", Guid.NewGuid().ToString());

        // Act
        var result = await _repository.ExistsAsync(nonExistentKey);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_WithDeletedEntity_ReturnsFalse()
    {
        // Arrange
        var device = CreateSmartPhone("deleted-exists");
        device.IsDeleted = true;
        await _repository.AddAsync(device);

        // Act
        var result = await _repository.ExistsAsync(device.Key);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
