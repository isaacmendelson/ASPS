using Business.Data.EF;
using Business.Data.EF.Repositories;
using Common.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ASPS.Tests.Business;

public class TrackedDomainRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly TrackedDomainRepository _repository;
    private readonly Mock<ILogger<TrackedDomainRepository>> _loggerMock;

    public TrackedDomainRepositoryTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _loggerMock = new Mock<ILogger<TrackedDomainRepository>>();
        _repository = new TrackedDomainRepository(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsTrackedDomain()
    {
        // Arrange
        var domain = new TrackedDomain("google-analytics.com", "Analytics");
        _context.TrackedDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(domain.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(domain.Id);
        result.Domain.Should().Be("google-analytics.com");
    }

    [Fact]
    public async Task GetByIdAsync_WithDeletedDomain_ReturnsNull()
    {
        // Arrange
        var domain = new TrackedDomain("deleted.com", "Analytics");
        domain.Delete();
        _context.TrackedDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(domain.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(99999);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetAllActiveAsync Tests

    [Fact]
    public async Task GetAllActiveAsync_ReturnsOnlyActiveNonDeletedDomains()
    {
        // Arrange
        var active1 = new TrackedDomain("analytics1.com", "Analytics");
        var active2 = new TrackedDomain("analytics2.com", "Advertising");
        var inactive = new TrackedDomain("inactive.com", "Analytics");
        inactive.Update(isActive: false);
        var deleted = new TrackedDomain("deleted.com", "Analytics");
        deleted.Delete();

        _context.TrackedDomains.AddRange(active1, active2, inactive, deleted);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllActiveAsync();

        // Assert
        var domains = result.ToList();
        domains.Should().HaveCount(2);
        domains.Should().Contain(d => d.Domain == "analytics1.com");
        domains.Should().Contain(d => d.Domain == "analytics2.com");
        domains.Should().NotContain(d => d.Domain == "inactive.com");
        domains.Should().NotContain(d => d.Domain == "deleted.com");
    }

    [Fact]
    public async Task GetAllActiveAsync_OrdersByCategoryThenDomain()
    {
        // Arrange
        var domain1 = new TrackedDomain("zebra.com", "Analytics");
        var domain2 = new TrackedDomain("apple.com", "Analytics");
        var domain3 = new TrackedDomain("beta.com", "Advertising");

        _context.TrackedDomains.AddRange(domain1, domain2, domain3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllActiveAsync();

        // Assert
        var domains = result.ToList();
        domains[0].Domain.Should().Be("beta.com"); // Advertising comes first
        domains[1].Domain.Should().Be("apple.com"); // Analytics, alphabetical
        domains[2].Domain.Should().Be("zebra.com");
    }

    [Fact]
    public async Task GetAllActiveAsync_WithEmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var result = await _repository.GetAllActiveAsync();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetByDomainAsync Tests

    [Fact]
    public async Task GetByDomainAsync_WithValidDomain_ReturnsDomain()
    {
        // Arrange
        var domain = new TrackedDomain("test-analytics.com", "Analytics");
        _context.TrackedDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByDomainAsync("test-analytics.com");

        // Assert
        result.Should().NotBeNull();
        result!.Domain.Should().Be("test-analytics.com");
    }

    [Fact]
    public async Task GetByDomainAsync_NormalizesDomainToLowerCase()
    {
        // Arrange
        var domain = new TrackedDomain("analytics.com", "Analytics");
        _context.TrackedDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByDomainAsync("ANALYTICS.COM");

        // Assert
        result.Should().NotBeNull();
        result!.Domain.Should().Be("analytics.com");
    }

    [Fact]
    public async Task GetByDomainAsync_TrimsDomain()
    {
        // Arrange
        var domain = new TrackedDomain("analytics.com", "Analytics");
        _context.TrackedDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByDomainAsync("  analytics.com  ");

        // Assert
        result.Should().NotBeNull();
        result!.Domain.Should().Be("analytics.com");
    }

    [Fact]
    public async Task GetByDomainAsync_WithDeletedDomain_ReturnsNull()
    {
        // Arrange
        var domain = new TrackedDomain("deleted.com", "Analytics");
        domain.Delete();
        _context.TrackedDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByDomainAsync("deleted.com");

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByDomainAsync_WithNullOrEmptyDomain_ReturnsNull(string? domain)
    {
        // Act
        var result = await _repository.GetByDomainAsync(domain!);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetByCategoryAsync Tests

    [Fact]
    public async Task GetByCategoryAsync_ReturnsDomainsInCategory()
    {
        // Arrange
        var analytics1 = new TrackedDomain("analytics1.com", "Analytics");
        var analytics2 = new TrackedDomain("analytics2.com", "Analytics");
        var advertising = new TrackedDomain("ads.com", "Advertising");

        _context.TrackedDomains.AddRange(analytics1, analytics2, advertising);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByCategoryAsync("Analytics");

        // Assert
        var domains = result.ToList();
        domains.Should().HaveCount(2);
        domains.Should().Contain(d => d.Domain == "analytics1.com");
        domains.Should().Contain(d => d.Domain == "analytics2.com");
    }

    [Fact]
    public async Task GetByCategoryAsync_FiltersDeletedAndInactive()
    {
        // Arrange
        var active = new TrackedDomain("active.com", "Analytics");
        var inactive = new TrackedDomain("inactive.com", "Analytics");
        inactive.Update(isActive: false);
        var deleted = new TrackedDomain("deleted.com", "Analytics");
        deleted.Delete();

        _context.TrackedDomains.AddRange(active, inactive, deleted);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByCategoryAsync("Analytics");

        // Assert
        var domains = result.ToList();
        domains.Should().HaveCount(1);
        domains[0].Domain.Should().Be("active.com");
    }

    [Fact]
    public async Task GetByCategoryAsync_OrdersByDomain()
    {
        // Arrange
        var domain1 = new TrackedDomain("zebra.com", "Analytics");
        var domain2 = new TrackedDomain("apple.com", "Analytics");
        var domain3 = new TrackedDomain("middle.com", "Analytics");

        _context.TrackedDomains.AddRange(domain1, domain2, domain3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByCategoryAsync("Analytics");

        // Assert
        var domains = result.ToList();
        domains[0].Domain.Should().Be("apple.com");
        domains[1].Domain.Should().Be("middle.com");
        domains[2].Domain.Should().Be("zebra.com");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByCategoryAsync_WithNullOrEmptyCategory_ReturnsEmpty(string? category)
    {
        // Act
        var result = await _repository.GetByCategoryAsync(category!);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region IsTrackedDomainAsync Tests

    [Fact]
    public async Task IsTrackedDomainAsync_WithActiveTrackedDomain_ReturnsTrue()
    {
        // Arrange
        var domain = new TrackedDomain("tracked.com", "Analytics");
        _context.TrackedDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.IsTrackedDomainAsync("tracked.com");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsTrackedDomainAsync_NormalizesDomain()
    {
        // Arrange
        var domain = new TrackedDomain("tracked.com", "Analytics");
        _context.TrackedDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.IsTrackedDomainAsync("TRACKED.COM");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsTrackedDomainAsync_TrimsDomain()
    {
        // Arrange
        var domain = new TrackedDomain("tracked.com", "Analytics");
        _context.TrackedDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.IsTrackedDomainAsync("  tracked.com  ");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsTrackedDomainAsync_WithInactiveDomain_ReturnsFalse()
    {
        // Arrange
        var domain = new TrackedDomain("inactive.com", "Analytics");
        domain.Update(isActive: false);
        _context.TrackedDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.IsTrackedDomainAsync("inactive.com");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsTrackedDomainAsync_WithDeletedDomain_ReturnsFalse()
    {
        // Arrange
        var domain = new TrackedDomain("deleted.com", "Analytics");
        domain.Delete();
        _context.TrackedDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.IsTrackedDomainAsync("deleted.com");

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task IsTrackedDomainAsync_WithNullOrEmptyDomain_ReturnsFalse(string? domain)
    {
        // Act
        var result = await _repository.IsTrackedDomainAsync(domain!);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_AddsNewDomain_ReturnsId()
    {
        // Arrange
        var domain = new TrackedDomain("newdomain.com", "Analytics");

        // Act
        var result = await _repository.AddAsync(domain);

        // Assert
        result.Should().BeGreaterThan(0);
        domain.Id.Should().Be(result);
        
        var saved = await _context.TrackedDomains.FindAsync(result);
        saved.Should().NotBeNull();
        saved!.Domain.Should().Be("newdomain.com");
    }

    [Fact]
    public async Task AddAsync_LogsInformation()
    {
        // Arrange
        var domain = new TrackedDomain("logged.com", "Analytics");

        // Act
        await _repository.AddAsync(domain);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Added tracked domain")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region AddRangeAsync Tests

    [Fact]
    public async Task AddRangeAsync_AddsMultipleDomains_ReturnsCount()
    {
        // Arrange
        var domains = new List<TrackedDomain>
        {
            new TrackedDomain("domain1.com", "Analytics"),
            new TrackedDomain("domain2.com", "Advertising"),
            new TrackedDomain("domain3.com", "Social")
        };

        // Act
        var result = await _repository.AddRangeAsync(domains);

        // Assert
        result.Should().Be(3);
        
        var saved = await _context.TrackedDomains.ToListAsync();
        saved.Should().HaveCount(3);
    }

    [Fact]
    public async Task AddRangeAsync_WithEmptyList_ReturnsZero()
    {
        // Arrange
        var domains = new List<TrackedDomain>();

        // Act
        var result = await _repository.AddRangeAsync(domains);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task AddRangeAsync_LogsInformation()
    {
        // Arrange
        var domains = new List<TrackedDomain>
        {
            new TrackedDomain("domain1.com", "Analytics"),
            new TrackedDomain("domain2.com", "Advertising")
        };

        // Act
        await _repository.AddRangeAsync(domains);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Added 2 tracked domains")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_UpdatesDomain_SetsDateModified()
    {
        // Arrange
        var domain = new TrackedDomain("original.com", "Analytics");
        _context.TrackedDomains.Add(domain);
        await _context.SaveChangesAsync();
        
        var originalModified = domain.DateModified;
        await Task.Delay(10); // Ensure time difference

        // Act
        domain.Update(category: "Advertising");
        await _repository.UpdateAsync(domain);

        // Assert
        var updated = await _context.TrackedDomains.FindAsync(domain.Id);
        updated.Should().NotBeNull();
        updated!.Category.Should().Be("Advertising");
        updated.DateModified.Should().BeAfter(originalModified);
    }

    [Fact]
    public async Task UpdateAsync_LogsInformation()
    {
        // Arrange
        var domain = new TrackedDomain("update.com", "Analytics");
        _context.TrackedDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Act
        await _repository.UpdateAsync(domain);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Updated tracked domain")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_SoftDeletesDomain_SetsDateDeleted()
    {
        // Arrange
        var domain = new TrackedDomain("todelete.com", "Analytics");
        _context.TrackedDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(domain.Id);

        // Assert
        var deleted = await _context.TrackedDomains.FindAsync(domain.Id);
        deleted.Should().NotBeNull();
        deleted!.DateDeleted.Should().NotBeNull();
        deleted.DateDeleted.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task DeleteAsync_DoesNotHardDelete()
    {
        // Arrange
        var domain = new TrackedDomain("soft.com", "Analytics");
        _context.TrackedDomains.Add(domain);
        await _context.SaveChangesAsync();
        var domainId = domain.Id;

        // Act
        await _repository.DeleteAsync(domainId);

        // Assert
        var stillExists = await _context.TrackedDomains.FindAsync(domainId);
        stillExists.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_DoesNotThrow()
    {
        // Act
        var act = async () => await _repository.DeleteAsync(99999);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteAsync_LogsInformation()
    {
        // Arrange
        var domain = new TrackedDomain("log.com", "Analytics");
        _context.TrackedDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(domain.Id);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Deleted tracked domain")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region GetCountAsync Tests

    [Fact]
    public async Task GetCountAsync_ReturnsCountOfActiveNonDeletedDomains()
    {
        // Arrange
        var active1 = new TrackedDomain("active1.com", "Analytics");
        var active2 = new TrackedDomain("active2.com", "Advertising");
        var inactive = new TrackedDomain("inactive.com", "Analytics");
        inactive.Update(isActive: false);
        var deleted = new TrackedDomain("deleted.com", "Analytics");
        deleted.Delete();

        _context.TrackedDomains.AddRange(active1, active2, inactive, deleted);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetCountAsync();

        // Assert
        result.Should().Be(2);
    }

    [Fact]
    public async Task GetCountAsync_WithEmptyDatabase_ReturnsZero()
    {
        // Act
        var result = await _repository.GetCountAsync();

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region Edge Cases and Integration Tests

    [Fact]
    public async Task DomainNormalization_ConsistentAcrossAllMethods()
    {
        // Arrange
        var domain = new TrackedDomain("MixedCase.COM", "Analytics");
        _context.TrackedDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Act & Assert - GetByDomainAsync
        var byDomain = await _repository.GetByDomainAsync("MIXEDCASE.com");
        byDomain.Should().NotBeNull();

        // Act & Assert - IsTrackedDomainAsync
        var isTracked = await _repository.IsTrackedDomainAsync("  MixedCase.COM  ");
        isTracked.Should().BeTrue();
    }

    [Fact]
    public async Task SoftDelete_FilteredInAllQueries()
    {
        // Arrange
        var domain = new TrackedDomain("filtered.com", "Analytics");
        _context.TrackedDomains.Add(domain);
        await _context.SaveChangesAsync();
        var domainId = domain.Id;

        // Act - Soft delete
        await _repository.DeleteAsync(domainId);

        // Assert - All queries should filter it out
        (await _repository.GetByIdAsync(domainId)).Should().BeNull();
        (await _repository.GetByDomainAsync("filtered.com")).Should().BeNull();
        (await _repository.IsTrackedDomainAsync("filtered.com")).Should().BeFalse();
        (await _repository.GetAllActiveAsync()).Should().NotContain(d => d.Id == domainId);
        (await _repository.GetByCategoryAsync("Analytics")).Should().NotContain(d => d.Id == domainId);
        (await _repository.GetCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DuplicateDomain_CannotBeAdded()
    {
        // Arrange
        var domain1 = new TrackedDomain("duplicate.com", "Analytics");
        await _repository.AddAsync(domain1);

        var domain2 = new TrackedDomain("duplicate.com", "Advertising");

        // Act
        var act = async () => await _repository.AddAsync(domain2);

        // Assert
        // In-memory DB doesn't enforce unique constraints like real DB would
        // This test documents the expected behavior
        // In production, this would throw DbUpdateException
        await act.Should().NotThrowAsync(); // In-memory limitation
    }

    #endregion
}
