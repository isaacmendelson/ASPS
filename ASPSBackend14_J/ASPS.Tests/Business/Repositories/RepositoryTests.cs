using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Business.Data.EF;
using Business.Data.EF.Repositories;
using Common.Entities;
using Common.Models;

namespace ASPS.Tests.Business.Repositories;

public class RepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Repository<User> _sut;

    public RepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _sut = new Repository<User>(_context);
    }

    #region GetByKeyAsync Tests

    [Fact]
    public async Task GetByKeyAsync_WhenEntityExists_ReturnsEntity()
    {
        // Arrange
        var keyField = Guid.NewGuid().ToString();
        var user = new User
        {
            KeyField = keyField,
            FirstName = "John",
            LastName = "Doe",
            IsDeleted = false
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var key = new Key("User", keyField, "default");

        // Act
        var result = await _sut.GetByKeyAsync(key);

        // Assert
        result.Should().NotBeNull();
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
    }

    [Fact]
    public async Task GetByKeyAsync_WhenEntityNotFound_ReturnsNull()
    {
        // Arrange
        var key = new Key("User", "99999", "default");

        // Act
        var result = await _sut.GetByKeyAsync(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByKeyAsync_WhenEntityDeleted_ReturnsNull()
    {
        // Arrange
        var keyField = Guid.NewGuid().ToString();
        var user = new User
        {
            KeyField = keyField,
            FirstName = "Deleted",
            IsDeleted = true
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var key = new Key("User", keyField, "default");

        // Act
        var result = await _sut.GetByKeyAsync(key);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsAllNonDeletedEntities()
    {
        // Arrange
        await _context.Users.AddRangeAsync(
            new User { KeyField = Guid.NewGuid().ToString(), FirstName = "User1", IsDeleted = false },
            new User { KeyField = Guid.NewGuid().ToString(), FirstName = "User2", IsDeleted = false },
            new User { KeyField = Guid.NewGuid().ToString(), FirstName = "Deleted", IsDeleted = true }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(u => !u.IsDeleted);
    }

    [Fact]
    public async Task GetAllAsync_WhenNoEntities_ReturnsEmptyList()
    {
        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_AddsEntityAndSetsDateCreated()
    {
        // Arrange
        var user = new User
        {
            KeyField = Guid.NewGuid().ToString(),
            FirstName = "New",
            LastName = "User"
        };

        // Act
        var result = await _sut.AddAsync(user);

        // Assert
        result.Should().NotBeNull();
        result.DateCreated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        
        var saved = await _context.Users.FirstOrDefaultAsync(u => u.KeyField == user.KeyField);
        saved.Should().NotBeNull();
        saved.FirstName.Should().Be("New");
    }

    [Fact]
    public async Task AddAsync_ReturnsAddedEntity()
    {
        // Arrange
        var user = new User
        {
            KeyField = Guid.NewGuid().ToString(),
            FirstName = "Test"
        };

        // Act
        var result = await _sut.AddAsync(user);

        // Assert
        result.Should().BeSameAs(user);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_UpdatesEntityAndSetsDateModified()
    {
        // Arrange
        var user = new User
        {
            KeyField = Guid.NewGuid().ToString(),
            FirstName = "Original",
            DateCreated = DateTime.UtcNow.AddDays(-1)
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Detach to simulate fresh load
        _context.Entry(user).State = EntityState.Detached;

        // Load fresh
        var toUpdate = await _context.Users.FirstAsync(u => u.KeyField == user.KeyField);
        toUpdate.FirstName = "Updated";

        // Act
        await _sut.UpdateAsync(toUpdate);

        // Assert
        var updated = await _context.Users.FirstAsync(u => u.KeyField == user.KeyField);
        updated.FirstName.Should().Be("Updated");
        updated.DateModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_SoftDeletesEntity()
    {
        // Arrange
        var keyField = Guid.NewGuid().ToString();
        var user = new User
        {
            KeyField = keyField,
            FirstName = "ToDelete",
            IsDeleted = false
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var key = new Key("User", keyField, "default");

        // Act
        await _sut.DeleteAsync(key);

        // Assert
        var deleted = await _context.Users.FirstAsync(u => u.KeyField == keyField);
        deleted.IsDeleted.Should().BeTrue();
        deleted.DateDeleted.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DeleteAsync_WhenEntityNotFound_DoesNotThrow()
    {
        // Arrange
        var key = new Key("User", "99999", "default");

        // Act
        Func<Task> act = async () => await _sut.DeleteAsync(key);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region ExistsAsync Tests

    [Fact]
    public async Task ExistsAsync_WhenEntityExists_ReturnsTrue()
    {
        // Arrange
        var keyField = Guid.NewGuid().ToString();
        var user = new User
        {
            KeyField = keyField,
            FirstName = "Exists",
            IsDeleted = false
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var key = new Key("User", keyField, "default");

        // Act
        var result = await _sut.ExistsAsync(key);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenEntityNotFound_ReturnsFalse()
    {
        // Arrange
        var key = new Key("User", "99999", "default");

        // Act
        var result = await _sut.ExistsAsync(key);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_WhenEntityDeleted_ReturnsFalse()
    {
        // Arrange
        var keyField = Guid.NewGuid().ToString();
        var user = new User
        {
            KeyField = keyField,
            FirstName = "Deleted",
            IsDeleted = true
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var key = new Key("User", keyField, "default");

        // Act
        var result = await _sut.ExistsAsync(key);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
