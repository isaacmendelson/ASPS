using Business.Data.EF;
using Business.Data.EF.Repositories;
using Common.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ASPS.Tests.Business.Data.EF.Repositories;

/// <summary>
/// Unit tests for WebsiteCategoryRepository
/// JIRA: SCRUM-819
/// </summary>
public class WebsiteCategoryRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly WebsiteCategoryRepository _repository;
    private readonly Mock<ILogger<WebsiteCategoryRepository>> _loggerMock;

    public WebsiteCategoryRepositoryTests()
    {
        // Setup in-memory database with unique name per test instance
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _loggerMock = new Mock<ILogger<WebsiteCategoryRepository>>();
        _repository = new WebsiteCategoryRepository(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsAllCategories()
    {
        // Arrange
        var parent = new WebsiteCategory("Parent", 0, "test")
        {
            KeyField = Guid.NewGuid().ToString()
        };
        var child1 = new WebsiteCategory("Child1", parent.KeyField, "test")
        {
            KeyField = Guid.NewGuid().ToString()
        };
        var child2 = new WebsiteCategory("Child2", parent.KeyField, "test")
        {
            KeyField = Guid.NewGuid().ToString()
        };

        _context.WebsiteCategories.AddRange(parent, child1, child2);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        var categories = result.ToList();
        categories.Should().HaveCount(3);
        categories.Should().Contain(c => c.Name == "Parent");
        categories.Should().Contain(c => c.Name == "Child1");
        categories.Should().Contain(c => c.Name == "Child2");
        categories.Should().BeInAscendingOrder(c => c.Name); // Verify OrderBy
    }

    [Fact]
    public async Task GetAllAsync_ExcludesDeletedCategories()
    {
        // Arrange
        var active = new WebsiteCategory("Active", 0, "test")
        {
            KeyField = Guid.NewGuid().ToString()
        };
        var deleted = new WebsiteCategory("Deleted", 0, "test")
        {
            KeyField = Guid.NewGuid().ToString(),
            DateDeleted = DateTime.UtcNow
        };

        _context.WebsiteCategories.AddRange(active, deleted);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        var categories = result.ToList();
        categories.Should().HaveCount(1);
        categories[0].Name.Should().Be("Active");
        categories.Should().NotContain(c => c.Name == "Deleted");
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
    public async Task GetAllAsync_IncludesParentRelationship()
    {
        // Arrange
        var parent = new WebsiteCategory("Parent", 0, "test")
        {
            KeyField = Guid.NewGuid().ToString()
        };
        var child = new WebsiteCategory("Child", parent.KeyField, "test")
        {
            KeyField = Guid.NewGuid().ToString()
        };

        _context.WebsiteCategories.AddRange(parent, child);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        var categories = result.ToList();
        var childCategory = categories.First(c => c.Name == "Child");
        childCategory.Parent.Should().NotBeNull();
        childCategory.Parent!.Name.Should().Be("Parent");
    }

    #endregion

    #region GetByNameAsync Tests

    [Fact]
    public async Task GetByNameAsync_WhenExists_ReturnsCategory()
    {
        // Arrange
        var category = new WebsiteCategory("Technology", 0, "test")
        {
            KeyField = Guid.NewGuid().ToString()
        };
        _context.WebsiteCategories.Add(category);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _repository.GetByNameAsync("Technology");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Technology");
    }

    [Fact]
    public async Task GetByNameAsync_WhenNotExists_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByNameAsync("NonExistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByNameAsync_IsCaseInsensitive()
    {
        // Arrange
        var category = new WebsiteCategory("Technology", 0, "test")
        {
            KeyField = Guid.NewGuid().ToString()
        };
        _context.WebsiteCategories.Add(category);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var resultLower = await _repository.GetByNameAsync("technology");
        var resultUpper = await _repository.GetByNameAsync("TECHNOLOGY");
        var resultMixed = await _repository.GetByNameAsync("TeCHnoLoGy");

        // Assert
        resultLower.Should().NotBeNull();
        resultUpper.Should().NotBeNull();
        resultMixed.Should().NotBeNull();
        resultLower!.Name.Should().Be("Technology");
        resultUpper!.Name.Should().Be("Technology");
        resultMixed!.Name.Should().Be("Technology");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByNameAsync_WithNullOrWhitespace_ReturnsNull(string? name)
    {
        // Act
        var result = await _repository.GetByNameAsync(name!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByNameAsync_ExcludesDeletedCategories()
    {
        // Arrange
        var deleted = new WebsiteCategory("Deleted", 0, "test")
        {
            KeyField = Guid.NewGuid().ToString(),
            DateDeleted = DateTime.UtcNow
        };
        _context.WebsiteCategories.Add(deleted);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _repository.GetByNameAsync("Deleted");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByNameAsync_IncludesParentRelationship()
    {
        // Arrange
        var parent = new WebsiteCategory("Parent", 0, "test")
        {
            KeyField = Guid.NewGuid().ToString()
        };
        var child = new WebsiteCategory("Child", parent.KeyField, "test")
        {
            KeyField = Guid.NewGuid().ToString()
        };
        _context.WebsiteCategories.AddRange(parent, child);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _repository.GetByNameAsync("Child");

        // Assert
        result.Should().NotBeNull();
        result!.Parent.Should().NotBeNull();
        result.Parent!.Name.Should().Be("Parent");
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_AddsCategory()
    {
        // Arrange
        var category = new WebsiteCategory("NewCategory", 0, "test")
        {
            KeyField = Guid.NewGuid().ToString()
        };

        // Act
        var result = await _repository.AddAsync(category);

        // Assert
        result.Should().BeGreaterThan(0); // SaveChangesAsync returns affected rows
        var saved = await _context.WebsiteCategories.FirstOrDefaultAsync(c => c.Name == "NewCategory");
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("NewCategory");
    }

    [Fact]
    public async Task AddAsync_WithNullCategory_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.AddAsync(null!));
    }

    [Fact]
    public async Task AddAsync_PersistsAllProperties()
    {
        // Arrange
        var category = new WebsiteCategory("FullCategory", 0, "api")
        {
            KeyField = Guid.NewGuid().ToString()
        };

        // Act
        await _repository.AddAsync(category);
        _context.ChangeTracker.Clear();

        // Assert
        var saved = await _context.WebsiteCategories.FirstOrDefaultAsync(c => c.Name == "FullCategory");
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("FullCategory");
        saved.Source.Should().Be("api");
        saved.DateCreated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        saved.DateDeleted.Should().BeNull();
    }

    [Fact]
    public async Task AddAsync_WithParent_CreatesHierarchy()
    {
        // Arrange
        var parent = new WebsiteCategory("ParentCategory", 0, "test")
        {
            KeyField = Guid.NewGuid().ToString()
        };
        await _context.WebsiteCategories.AddAsync(parent);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var child = new WebsiteCategory("ChildCategory", parent.KeyField, "test")
        {
            KeyField = Guid.NewGuid().ToString()
        };

        // Act
        await _repository.AddAsync(child);
        _context.ChangeTracker.Clear();

        // Assert
        var saved = await _context.WebsiteCategories
            .Include(c => c.Parent)
            .FirstOrDefaultAsync(c => c.Name == "ChildCategory");
        
        saved.Should().NotBeNull();
        saved!.ParentId.Should().Be(parent.KeyField);
    }

    #endregion

    #region ExistsAsync Tests

    [Fact]
    public async Task ExistsAsync_WhenExists_ReturnsTrue()
    {
        // Arrange
        var category = new WebsiteCategory("ExistingCategory", 0, "test")
        {
            KeyField = Guid.NewGuid().ToString()
        };
        _context.WebsiteCategories.Add(category);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _repository.ExistsAsync("ExistingCategory");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenNotExists_ReturnsFalse()
    {
        // Act
        var result = await _repository.ExistsAsync("NonExistent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_IsCaseInsensitive()
    {
        // Arrange
        var category = new WebsiteCategory("TestCategory", 0, "test")
        {
            KeyField = Guid.NewGuid().ToString()
        };
        _context.WebsiteCategories.Add(category);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var resultLower = await _repository.ExistsAsync("testcategory");
        var resultUpper = await _repository.ExistsAsync("TESTCATEGORY");
        var resultMixed = await _repository.ExistsAsync("TeStCaTeGoRy");

        // Assert
        resultLower.Should().BeTrue();
        resultUpper.Should().BeTrue();
        resultMixed.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExistsAsync_WithNullOrWhitespace_ReturnsFalse(string? name)
    {
        // Act
        var result = await _repository.ExistsAsync(name!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_ExcludesDeletedCategories()
    {
        // Arrange
        var deleted = new WebsiteCategory("DeletedCategory", 0, "test")
        {
            KeyField = Guid.NewGuid().ToString(),
            DateDeleted = DateTime.UtcNow
        };
        _context.WebsiteCategories.Add(deleted);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _repository.ExistsAsync("DeletedCategory");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_WithMultipleCategories_FindsCorrectOne()
    {
        // Arrange
        var cat1 = new WebsiteCategory("Category1", 0, "test")
        {
            KeyField = Guid.NewGuid().ToString()
        };
        var cat2 = new WebsiteCategory("Category2", 0, "test")
        {
            KeyField = Guid.NewGuid().ToString()
        };
        var cat3 = new WebsiteCategory("Category3", 0, "test")
        {
            KeyField = Guid.NewGuid().ToString()
        };
        _context.WebsiteCategories.AddRange(cat1, cat2, cat3);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var exists1 = await _repository.ExistsAsync("Category1");
        var exists2 = await _repository.ExistsAsync("Category2");
        var exists3 = await _repository.ExistsAsync("Category3");
        var existsNone = await _repository.ExistsAsync("Category4");

        // Assert
        exists1.Should().BeTrue();
        exists2.Should().BeTrue();
        exists3.Should().BeTrue();
        existsNone.Should().BeFalse();
    }

    #endregion

    #region Edge Cases & Integration Tests

    [Fact]
    public async Task MultipleOperations_WorkTogether()
    {
        // Arrange & Act
        var category = new WebsiteCategory("IntegrationTest", 0, "test")
        {
            KeyField = Guid.NewGuid().ToString()
        };

        // Add
        await _repository.AddAsync(category);
        _context.ChangeTracker.Clear();

        // Verify exists
        var exists = await _repository.ExistsAsync("IntegrationTest");

        // Get by name
        var retrieved = await _repository.GetByNameAsync("IntegrationTest");

        // Get all
        var all = await _repository.GetAllAsync();

        // Assert
        exists.Should().BeTrue();
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("IntegrationTest");
        all.Should().Contain(c => c.Name == "IntegrationTest");
    }

    [Fact]
    public async Task Hierarchy_WorksCorrectly()
    {
        // Arrange
        var root = new WebsiteCategory("Root", 0, "test")
        {
            KeyField = Guid.NewGuid().ToString()
        };
        await _repository.AddAsync(root);
        _context.ChangeTracker.Clear();

        var child = new WebsiteCategory("Child", root.KeyField, "test")
        {
            KeyField = Guid.NewGuid().ToString()
        };
        await _repository.AddAsync(child);
        _context.ChangeTracker.Clear();

        var grandchild = new WebsiteCategory("Grandchild", child.KeyField, "test")
        {
            KeyField = Guid.NewGuid().ToString()
        };
        await _repository.AddAsync(grandchild);
        _context.ChangeTracker.Clear();

        // Act
        var all = await _repository.GetAllAsync();
        var retrievedGrandchild = await _repository.GetByNameAsync("Grandchild");

        // Assert
        all.Should().HaveCount(3);
        retrievedGrandchild.Should().NotBeNull();
        retrievedGrandchild!.Parent.Should().NotBeNull();
        retrievedGrandchild.Parent!.Name.Should().Be("Child");
    }

    #endregion
}
