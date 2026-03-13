using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Business.Data.EF;
using Business.Data.EF.Repositories;
using Common.Entities;

namespace ASPS.Tests.Business.Repositories;

public class SafeDomainRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<ILogger<SafeDomainRepository>> _loggerMock;
    private readonly SafeDomainRepository _sut;

    public SafeDomainRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _loggerMock = new Mock<ILogger<SafeDomainRepository>>();
        _sut = new SafeDomainRepository(_context, _loggerMock.Object);
    }

    #region GetAllActiveAsync Tests

    [Fact]
    public async Task GetAllActiveAsync_ReturnsNonDeletedDomains()
    {
        // Arrange
        await _context.SafeDomains.AddRangeAsync(
            new SafeDomain { Domain = "google.com", IsDeleted = false },
            new SafeDomain { Domain = "deleted.com", IsDeleted = true },
            new SafeDomain { Domain = "microsoft.com", IsDeleted = false }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllActiveAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(d => !d.IsDeleted);
    }

    [Fact]
    public async Task GetAllActiveAsync_WhenNoDomains_ReturnsEmptyList()
    {
        // Act
        var result = await _sut.GetAllActiveAsync();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region IsSafeDomainAsync Tests

    [Fact]
    public async Task IsSafeDomainAsync_WhenDomainIsSafe_ReturnsTrue()
    {
        // Arrange
        await _context.SafeDomains.AddAsync(
            new SafeDomain { Domain = "google.com", IsDeleted = false }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.IsSafeDomainAsync("google.com");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsSafeDomainAsync_IsCaseInsensitive()
    {
        // Arrange
        await _context.SafeDomains.AddAsync(
            new SafeDomain { Domain = "google.com", IsDeleted = false }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.IsSafeDomainAsync("GOOGLE.COM");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsSafeDomainAsync_WhenDomainNotFound_ReturnsFalse()
    {
        // Act
        var result = await _sut.IsSafeDomainAsync("unknown.com");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsSafeDomainAsync_WhenDomainDeleted_ReturnsFalse()
    {
        // Arrange
        await _context.SafeDomains.AddAsync(
            new SafeDomain { Domain = "deleted.com", IsDeleted = true }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.IsSafeDomainAsync("deleted.com");

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task IsSafeDomainAsync_WithNullOrWhitespace_ReturnsFalse(string domain)
    {
        // Act
        var result = await _sut.IsSafeDomainAsync(domain);

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
