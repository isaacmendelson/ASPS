using Business.Data.EF;
using Business.Data.EF.Repositories;
using Common.Entities;
using Common.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASPS.Tests.Business.Repositories;

public class UserAccountRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly UserAccountRepository _repository;
    private readonly User _testUser;

    public UserAccountRepositoryTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _repository = new UserAccountRepository(_context);

        // Create test user
        _testUser = new User { KeyField = Guid.NewGuid().ToString(), FirstName = "Test", LastName = "User", Email = "test@example.com", KeycloakUserId = "kc-test" };
        _context.Users.Add(_testUser);
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GetByUserKeyAsync Tests

    [Fact]
    public async Task GetByUserKeyAsync_WithValidUserKey_ReturnsAccounts()
    {
        // Arrange
        var account1 = new UserAccount { KeyField = Guid.NewGuid().ToString(), UserName = "user@gmail.com", UserKey = _testUser.Key };
        var account2 = new UserAccount { KeyField = Guid.NewGuid().ToString(), UserName = "user@outlook.com", UserKey = _testUser.Key };
        var otherUser = new User { KeyField = Guid.NewGuid().ToString(), FirstName = "Other", LastName = "User", Email = "other@example.com", KeycloakUserId = "kc-other" };
        _context.Users.Add(otherUser);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var account3 = new UserAccount { KeyField = Guid.NewGuid().ToString(), UserName = "other@yahoo.com", UserKey = otherUser.Key };

        _context.UserAccounts.AddRange(account1, account2, account3);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _repository.GetByUserKeyAsync(_testUser.Key);

        // Assert
        var accounts = result.ToList();
        accounts.Should().HaveCount(2);
        accounts.Should().Contain(a => a.UserName == "user@gmail.com");
        accounts.Should().Contain(a => a.UserName == "user@outlook.com");
        accounts.Should().NotContain(a => a.UserName == "other@yahoo.com");
    }

    [Fact]
    public async Task GetByUserKeyAsync_ExcludesDeletedAccounts()
    {
        // Arrange
        var active = new UserAccount { KeyField = Guid.NewGuid().ToString(), UserName = "active@example.com", UserKey = _testUser.Key };
        var deleted = new UserAccount { KeyField = Guid.NewGuid().ToString(), UserName = "deleted@example.com", UserKey = _testUser.Key, IsDeleted = true };

        _context.UserAccounts.AddRange(active, deleted);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _repository.GetByUserKeyAsync(_testUser.Key);

        // Assert
        var accounts = result.ToList();
        accounts.Should().HaveCount(1);
        accounts[0].UserName.Should().Be("active@example.com");
    }

    [Fact]
    public async Task GetByUserKeyAsync_WithNoAccounts_ReturnsEmpty()
    {
        // Act
        var result = await _repository.GetByUserKeyAsync(_testUser.Key);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetByUserNameAsync Tests

    [Fact]
    public async Task GetByUserNameAsync_WithValidUserName_ReturnsAccount()
    {
        // Arrange
        var userName = "john.doe@example.com";
        var account = new UserAccount { UserName = userName, UserKey = _testUser.Key };
        _context.UserAccounts.Add(account);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByUserNameAsync(userName);

        // Assert
        result.Should().NotBeNull();
        result!.UserName.Should().Be(userName);
    }

    [Fact]
    public async Task GetByUserNameAsync_WithDeletedAccount_ReturnsNull()
    {
        // Arrange
        var userName = "deleted@example.com";
        var account = new UserAccount { UserName = userName, UserKey = _testUser.Key, IsDeleted = true };
        _context.UserAccounts.Add(account);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByUserNameAsync(userName);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserNameAsync_WithNonExistentUserName_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByUserNameAsync("non-existent@example.com");

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByUserNameAsync_WithNullOrEmptyUserName_ReturnsNull(string? userName)
    {
        // Act
        var result = await _repository.GetByUserNameAsync(userName!);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task MultipleAccounts_CanExistForSameUser()
    {
        // Arrange
        var account1 = new UserAccount { KeyField = Guid.NewGuid().ToString(), UserName = "account1@example.com", UserKey = _testUser.Key };
        var account2 = new UserAccount { KeyField = Guid.NewGuid().ToString(), UserName = "account2@example.com", UserKey = _testUser.Key };
        var account3 = new UserAccount { KeyField = Guid.NewGuid().ToString(), UserName = "account3@example.com", UserKey = _testUser.Key };

        _context.UserAccounts.AddRange(account1, account2, account3);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _repository.GetByUserKeyAsync(_testUser.Key);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetByUserNameAsync_IsCaseSensitive()
    {
        // Arrange
        var account = new UserAccount { UserName = "Test@Example.com", UserKey = _testUser.Key };
        _context.UserAccounts.Add(account);
        await _context.SaveChangesAsync();

        // Act
        var result1 = await _repository.GetByUserNameAsync("Test@Example.com");
        var result2 = await _repository.GetByUserNameAsync("test@example.com");

        // Assert
        result1.Should().NotBeNull();
        // Note: Actual behavior depends on DB collation
        // In-memory DB is case-sensitive by default
        result2.Should().BeNull();
    }

    [Fact]
    public async Task DifferentUsers_CanHaveSameUserName()
    {
        // Arrange
        var user2 = new User { KeyField = Guid.NewGuid().ToString(), FirstName = "User", LastName = "Two", Email = "user2@example.com", KeycloakUserId = "kc-2" };
        _context.Users.Add(user2);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        
        var account1 = new UserAccount { KeyField = Guid.NewGuid().ToString(), UserName = "shared@example.com", UserKey = _testUser.Key };
        var account2 = new UserAccount { KeyField = Guid.NewGuid().ToString(), UserName = "shared@example.com", UserKey = user2.Key };

        _context.UserAccounts.AddRange(account1, account2);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _repository.GetByUserNameAsync("shared@example.com");

        // Assert
        // Should return the first match (behavior depends on implementation)
        result.Should().NotBeNull();
    }

    #endregion
}
