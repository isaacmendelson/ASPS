using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Business.Data.EF.Repositories;
using Business.Data.EF;
using Common.Entities;

namespace ASPS.Tests.Business;

public class KnownPhishingWebsiteRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<ILogger<KnownPhishingWebsiteRepository>> _loggerMock;
    private readonly KnownPhishingWebsiteRepository _repository;

    public KnownPhishingWebsiteRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _loggerMock = new Mock<ILogger<KnownPhishingWebsiteRepository>>();
        _repository = new KnownPhishingWebsiteRepository(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WhenWebsiteExists_ReturnsWebsite()
    {
        // Arrange
        var website = new KnownPhishingWebsite("http://phishing.com", "TestSource");
        _context.KnownPhishingWebsites.Add(website);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(website.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(website.Id);
        result.Url.Should().Be(website.Url);
    }

    [Fact]
    public async Task GetByIdAsync_WhenWebsiteDoesNotExist_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WhenWebsiteIsDeleted_ReturnsNull()
    {
        // Arrange
        var website = new KnownPhishingWebsite("http://deleted.com", "TestSource");
        _context.KnownPhishingWebsites.Add(website);
        await _context.SaveChangesAsync();
        
        // Soft delete
        await _repository.DeleteAsync(website.Id);

        // Act
        var result = await _repository.GetByIdAsync(website.Id);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetAllActiveAsync Tests

    [Fact]
    public async Task GetAllActiveAsync_ReturnsOnlyActiveWebsites()
    {
        // Arrange
        var activeWebsite1 = new KnownPhishingWebsite("http://active1.com");
        var activeWebsite2 = new KnownPhishingWebsite("http://active2.com");
        var deletedWebsite = new KnownPhishingWebsite("http://deleted.com");

        _context.KnownPhishingWebsites.AddRange(activeWebsite1, activeWebsite2, deletedWebsite);
        await _context.SaveChangesAsync();
        
        await _repository.DeleteAsync(deletedWebsite.Id);

        // Act
        var result = await _repository.GetAllActiveAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(w => w.Url == "http://active1.com");
        result.Should().Contain(w => w.Url == "http://active2.com");
        result.Should().NotContain(w => w.Url == "http://deleted.com");
    }

    [Fact]
    public async Task GetAllActiveAsync_WhenNoWebsites_ReturnsEmptyList()
    {
        // Act
        var result = await _repository.GetAllActiveAsync();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetByUrlAsync Tests

    [Fact]
    public async Task GetByUrlAsync_WhenUrlExists_ReturnsWebsite()
    {
        // Arrange
        var url = "http://phishing.com/page";
        var website = new KnownPhishingWebsite(url);
        _context.KnownPhishingWebsites.Add(website);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByUrlAsync(url);

        // Assert
        result.Should().NotBeNull();
        result!.Url.Should().Be(url);
    }

    [Fact]
    public async Task GetByUrlAsync_WhenUrlDoesNotExist_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByUrlAsync("http://nonexistent.com");

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByUrlAsync_WhenUrlIsNullOrWhitespace_ReturnsNull(string? url)
    {
        // Act
        var result = await _repository.GetByUrlAsync(url!);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetByDomainAsync Tests

    [Fact]
    public async Task GetByDomainAsync_WhenDomainExists_ReturnsWebsites()
    {
        // Arrange
        var website1 = new KnownPhishingWebsite("http://phishing.com/page1");
        var website2 = new KnownPhishingWebsite("http://phishing.com/page2");
        _context.KnownPhishingWebsites.AddRange(website1, website2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByDomainAsync("phishing.com");

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByDomainAsync_NormalizesDomain()
    {
        // Arrange
        var website = new KnownPhishingWebsite("http://phishing.com");
        _context.KnownPhishingWebsites.Add(website);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByDomainAsync("PHISHING.COM");

        // Assert
        result.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByDomainAsync_WhenDomainIsNullOrWhitespace_ReturnsEmpty(string? domain)
    {
        // Act
        var result = await _repository.GetByDomainAsync(domain!);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region IsPhishingUrlAsync Tests

    [Fact]
    public async Task IsPhishingUrlAsync_WhenUrlExists_ReturnsTrue()
    {
        // Arrange
        var url = "http://phishing.com";
        var website = new KnownPhishingWebsite(url);
        _context.KnownPhishingWebsites.Add(website);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.IsPhishingUrlAsync(url);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsPhishingUrlAsync_WhenUrlDoesNotExist_ReturnsFalse()
    {
        // Act
        var result = await _repository.IsPhishingUrlAsync("http://safe.com");

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task IsPhishingUrlAsync_WhenUrlIsNullOrWhitespace_ReturnsFalse(string? url)
    {
        // Act
        var result = await _repository.IsPhishingUrlAsync(url!);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsPhishingDomainAsync Tests

    [Fact]
    public async Task IsPhishingDomainAsync_WhenDomainExists_ReturnsTrue()
    {
        // Arrange
        var website = new KnownPhishingWebsite("http://phishing.com");
        _context.KnownPhishingWebsites.Add(website);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.IsPhishingDomainAsync("phishing.com");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsPhishingDomainAsync_NormalizesDomain()
    {
        // Arrange
        var website = new KnownPhishingWebsite("http://phishing.com");
        _context.KnownPhishingWebsites.Add(website);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.IsPhishingDomainAsync("PHISHING.COM");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsPhishingDomainAsync_WhenDomainDoesNotExist_ReturnsFalse()
    {
        // Act
        var result = await _repository.IsPhishingDomainAsync("safe.com");

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task IsPhishingDomainAsync_WhenDomainIsNullOrWhitespace_ReturnsFalse(string? domain)
    {
        // Act
        var result = await _repository.IsPhishingDomainAsync(domain!);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_AddsWebsiteToDatabase()
    {
        // Arrange
        var website = new KnownPhishingWebsite("http://new-phishing.com");

        // Act
        var result = await _repository.AddAsync(website);

        // Assert
        result.Should().BeGreaterThan(0);
        website.Id.Should().Be(result);
        _context.KnownPhishingWebsites.Should().Contain(website);
    }

    #endregion

    #region AddRangeAsync Tests

    [Fact]
    public async Task AddRangeAsync_AddsMultipleWebsitesToDatabase()
    {
        // Arrange
        var websites = new[]
        {
            new KnownPhishingWebsite("http://phishing1.com"),
            new KnownPhishingWebsite("http://phishing2.com"),
            new KnownPhishingWebsite("http://phishing3.com")
        };

        // Act
        var result = await _repository.AddRangeAsync(websites);

        // Assert
        result.Should().Be(3);
        _context.KnownPhishingWebsites.Should().HaveCount(3);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_UpdatesWebsiteInDatabase()
    {
        // Arrange
        var website = new KnownPhishingWebsite("http://old-url.com");
        _context.KnownPhishingWebsites.Add(website);
        await _context.SaveChangesAsync();

        // Act
        // Note: Cannot change URL directly (read-only), but we can test Update method
        await _repository.UpdateAsync(website);

        // Assert
        var updated = await _context.KnownPhishingWebsites.FindAsync(website.Id);
        updated.Should().NotBeNull();
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_SoftDeletesWebsite()
    {
        // Arrange
        var website = new KnownPhishingWebsite("http://to-delete.com");
        _context.KnownPhishingWebsites.Add(website);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(website.Id);

        // Assert
        var deleted = await _context.KnownPhishingWebsites.FindAsync(website.Id);
        deleted!.DateDeleted.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_WhenWebsiteDoesNotExist_DoesNotThrow()
    {
        // Act
        Func<Task> act = async () => await _repository.DeleteAsync(999);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region GetCountAsync Tests

    [Fact]
    public async Task GetCountAsync_ReturnsCountOfActiveWebsites()
    {
        // Arrange
        var active1 = new KnownPhishingWebsite("http://active1.com");
        var active2 = new KnownPhishingWebsite("http://active2.com");
        var deleted = new KnownPhishingWebsite("http://deleted.com");

        _context.KnownPhishingWebsites.AddRange(active1, active2, deleted);
        await _context.SaveChangesAsync();
        
        await _repository.DeleteAsync(deleted.Id);

        // Act
        var result = await _repository.GetCountAsync();

        // Assert
        result.Should().Be(2);
    }

    [Fact]
    public async Task GetCountAsync_WhenNoWebsites_ReturnsZero()
    {
        // Act
        var result = await _repository.GetCountAsync();

        // Assert
        result.Should().Be(0);
    }

    #endregion
}
