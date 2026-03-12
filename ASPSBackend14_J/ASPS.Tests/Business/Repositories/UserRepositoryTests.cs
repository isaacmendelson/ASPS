using Business.Data.EF;
using Business.Data.EF.Repositories;
using Common.Entities;
using Common.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASPS.Tests.Business.Repositories;

public class UserRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly UserRepository _repository;

    public UserRepositoryTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _repository = new UserRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GetByKeycloakIdAsync Tests

    [Fact]
    public async Task GetByKeycloakIdAsync_WithValidId_ReturnsUser()
    {
        // Arrange
        var keycloakId = "keycloak-123";
        var user = new User 
        { 
            FirstName = "John", 
            LastName = "Doe", 
            Email = "john@example.com", 
            KeycloakUserId = keycloakId 
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByKeycloakIdAsync(keycloakId);

        // Assert
        result.Should().NotBeNull();
        result!.KeycloakUserId.Should().Be(keycloakId);
        result.FirstName.Should().Be("John");
    }

    [Fact]
    public async Task GetByKeycloakIdAsync_WithDeletedUser_ReturnsNull()
    {
        // Arrange
        var keycloakId = "keycloak-deleted";
        var user = new User 
        { 
            FirstName = "Deleted", 
            LastName = "User", 
            Email = "deleted@example.com", 
            KeycloakUserId = keycloakId,
            IsDeleted = true
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByKeycloakIdAsync(keycloakId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByKeycloakIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByKeycloakIdAsync("non-existent-id");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetUserWithDetailsAsync Tests

    [Fact]
    public async Task GetUserWithDetailsAsync_WithValidKey_ReturnsUser()
    {
        // Arrange
        var user = new User 
        { 
            FirstName = "Jane", 
            LastName = "Doe", 
            Email = "jane@example.com", 
            KeycloakUserId = "keycloak-456" 
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetUserWithDetailsAsync(user.Key);

        // Assert
        result.Should().NotBeNull();
        result!.FirstName.Should().Be("Jane");
        result.Email.Should().Be("jane@example.com");
    }

    [Fact]
    public async Task GetUserWithDetailsAsync_WithDeletedUser_ReturnsNull()
    {
        // Arrange
        var user = new User 
        { 
            FirstName = "Deleted", 
            LastName = "User", 
            Email = "deleted@example.com", 
            KeycloakUserId = "keycloak-789",
            IsDeleted = true
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetUserWithDetailsAsync(user.Key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserWithDetailsAsync_WithNonExistentKey_ReturnsNull()
    {
        // Arrange
        var fakeKey = new Key("User", "non-existent");

        // Act
        var result = await _repository.GetUserWithDetailsAsync(fakeKey);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetActiveUsersAsync Tests

    [Fact]
    public async Task GetActiveUsersAsync_ReturnsOnlyActiveUsers()
    {
        // Arrange
        var active1 = new User 
        { 
            FirstName = "Active", 
            LastName = "One", 
            Email = "active1@example.com", 
            KeycloakUserId = "kc-1" 
        };
        var active2 = new User 
        { 
            FirstName = "Active", 
            LastName = "Two", 
            Email = "active2@example.com", 
            KeycloakUserId = "kc-2" 
        };
        var disabled = new User 
        { 
            FirstName = "Disabled", 
            LastName = "User", 
            Email = "disabled@example.com", 
            KeycloakUserId = "kc-3",
            IsDisabled = true
        };
        var deleted = new User 
        { 
            FirstName = "Deleted", 
            LastName = "User", 
            Email = "deleted@example.com", 
            KeycloakUserId = "kc-4",
            IsDeleted = true
        };

        _context.Users.AddRange(active1, active2, disabled, deleted);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetActiveUsersAsync();

        // Assert
        var users = result.ToList();
        users.Should().HaveCount(2);
        users.Should().Contain(u => u.KeycloakUserId == "kc-1");
        users.Should().Contain(u => u.KeycloakUserId == "kc-2");
        users.Should().NotContain(u => u.IsDeleted || u.IsDisabled);
    }

    [Fact]
    public async Task GetActiveUsersAsync_WithEmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var result = await _repository.GetActiveUsersAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveUsersAsync_FiltersDeletedAndDisabled()
    {
        // Arrange
        var active = new User 
        { 
            FirstName = "Active", 
            LastName = "User", 
            Email = "active@example.com", 
            KeycloakUserId = "kc-active" 
        };
        var disabled = new User 
        { 
            FirstName = "Disabled", 
            LastName = "User", 
            Email = "disabled@example.com", 
            KeycloakUserId = "kc-disabled",
            IsDisabled = true
        };
        var deleted = new User 
        { 
            FirstName = "Deleted", 
            LastName = "User", 
            Email = "deleted@example.com", 
            KeycloakUserId = "kc-deleted",
            IsDeleted = true
        };

        _context.Users.AddRange(active, disabled, deleted);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetActiveUsersAsync();

        // Assert
        var users = result.ToList();
        users.Should().HaveCount(1);
        users[0].KeycloakUserId.Should().Be("kc-active");
    }

    #endregion

    #region Edge Cases

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByKeycloakIdAsync_WithNullOrEmptyId_ReturnsNull(string? keycloakId)
    {
        // Act
        var result = await _repository.GetByKeycloakIdAsync(keycloakId!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task MultipleUsers_WithSameEmail_CanExist()
    {
        // Arrange
        var user1 = new User 
        { 
            FirstName = "User", 
            LastName = "One", 
            Email = "same@example.com", 
            KeycloakUserId = "kc-1" 
        };
        var user2 = new User 
        { 
            FirstName = "User", 
            LastName = "Two", 
            Email = "same@example.com", 
            KeycloakUserId = "kc-2" 
        };

        _context.Users.AddRange(user1, user2);
        await _context.SaveChangesAsync();

        // Act
        var activeUsers = await _repository.GetActiveUsersAsync();

        // Assert
        activeUsers.Should().HaveCount(2);
    }

    #endregion
}
