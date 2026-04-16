using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using WebApi.Pages.WebsiteCategories;
using WebApi.Services;
using Business.Queries;
using Common.Entities;

namespace ASPS.Tests.WebApi.Pages.WebsiteCategories;

/// <summary>
/// Unit tests for WebsiteCategories/IndexModel page
/// JIRA: SCRUM-822
/// </summary>
public class IndexModelTests
{
    private readonly Mock<ICQRSClient> _mockCqrsClient;
    private readonly Mock<ILogger<IndexModel>> _mockLogger;
    private readonly IndexModel _sut;

    public IndexModelTests()
    {
        _mockCqrsClient = new Mock<ICQRSClient>();
        _mockLogger = new Mock<ILogger<IndexModel>>();
        _sut = new IndexModel(_mockCqrsClient.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_InitializesProperties()
    {
        // Assert
        _sut.Categories.Should().NotBeNull();
        _sut.Categories.Should().BeEmpty();
        _sut.AllParents.Should().NotBeNull();
        _sut.AllParents.Should().BeEmpty();
        _sut.ErrorMessage.Should().BeNull();
        _sut.CurrentPage.Should().Be(1);
        _sut.PageSize.Should().Be(50);
    }

    [Fact]
    public async Task OnGetAsync_LoadsCategories_Successfully()
    {
        // Arrange
        var expectedCategories = new List<WebsiteCategory>
        {
            new WebsiteCategory("Shopping", "parent-1", "Manual"),
            new WebsiteCategory("News", "parent-2", "Import")
        };

        var expectedParents = new List<WebsiteCategory>
        {
            new WebsiteCategory("E-Commerce", "", "Manual"),
            new WebsiteCategory("Media", "", "Manual")
        };

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetAllWebsiteCategoriesQueryResult>(It.IsAny<GetAllWebsiteCategoriesQuery>()))
            .ReturnsAsync(new GetAllWebsiteCategoriesQueryResult
            {
                Success = true,
                Categories = expectedCategories,
                TotalCount = 2,
                PageSize = 50
            });

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetParentCategoriesQueryResult>(It.IsAny<GetParentCategoriesQuery>()))
            .ReturnsAsync(new GetParentCategoriesQueryResult
            {
                Success = true,
                Parents = expectedParents
            });

        // Act
        await _sut.OnGetAsync();

        // Assert
        _sut.Categories.Should().HaveCount(2);
        _sut.Categories.Should().BeEquivalentTo(expectedCategories);
        _sut.AllParents.Should().HaveCount(2);
        _sut.AllParents.Should().BeEquivalentTo(expectedParents);
        _sut.TotalCount.Should().Be(2);
        _sut.TotalPages.Should().Be(1);
        _sut.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task OnGetAsync_WithSearch_PassesSearchToQuery()
    {
        // Arrange
        _sut.Search = "shopping";
        GetAllWebsiteCategoriesQuery? capturedQuery = null;

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetAllWebsiteCategoriesQueryResult>(It.IsAny<GetAllWebsiteCategoriesQuery>()))
            .Callback<GetAllWebsiteCategoriesQuery>(q => capturedQuery = q)
            .ReturnsAsync(new GetAllWebsiteCategoriesQueryResult { Success = true, Categories = new(), TotalCount = 0, PageSize = 50 });

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetParentCategoriesQueryResult>(It.IsAny<GetParentCategoriesQuery>()))
            .ReturnsAsync(new GetParentCategoriesQueryResult { Success = true, Parents = new() });

        // Act
        await _sut.OnGetAsync();

        // Assert
        capturedQuery.Should().NotBeNull();
        capturedQuery!.Search.Should().Be("shopping");
    }

    [Fact]
    public async Task OnGetAsync_WithParentFilter_PassesParentIdToQuery()
    {
        // Arrange
        _sut.ParentId = "parent-123";
        GetAllWebsiteCategoriesQuery? capturedQuery = null;

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetAllWebsiteCategoriesQueryResult>(It.IsAny<GetAllWebsiteCategoriesQuery>()))
            .Callback<GetAllWebsiteCategoriesQuery>(q => capturedQuery = q)
            .ReturnsAsync(new GetAllWebsiteCategoriesQueryResult { Success = true, Categories = new(), TotalCount = 0, PageSize = 50 });

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetParentCategoriesQueryResult>(It.IsAny<GetParentCategoriesQuery>()))
            .ReturnsAsync(new GetParentCategoriesQueryResult { Success = true, Parents = new() });

        // Act
        await _sut.OnGetAsync();

        // Assert
        capturedQuery.Should().NotBeNull();
        capturedQuery!.ParentId.Should().Be("parent-123");
    }

    [Fact]
    public async Task OnGetAsync_WithPagination_CalculatesTotalPages()
    {
        // Arrange
        _sut.CurrentPage = 2;
        _sut.PageSize = 10;

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetAllWebsiteCategoriesQueryResult>(It.IsAny<GetAllWebsiteCategoriesQuery>()))
            .ReturnsAsync(new GetAllWebsiteCategoriesQueryResult
            {
                Success = true,
                Categories = new(),
                TotalCount = 25,
                PageSize = 10
            });

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetParentCategoriesQueryResult>(It.IsAny<GetParentCategoriesQuery>()))
            .ReturnsAsync(new GetParentCategoriesQueryResult { Success = true, Parents = new() });

        // Act
        await _sut.OnGetAsync();

        // Assert
        _sut.TotalPages.Should().Be(3); // Ceiling(25/10) = 3
    }

    [Fact]
    public async Task OnGetAsync_WhenQueryFails_SetsErrorMessage()
    {
        // Arrange
        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetAllWebsiteCategoriesQueryResult>(It.IsAny<GetAllWebsiteCategoriesQuery>()))
            .ReturnsAsync(new GetAllWebsiteCategoriesQueryResult
            {
                Success = false,
                Message = "Database error"
            });

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetParentCategoriesQueryResult>(It.IsAny<GetParentCategoriesQuery>()))
            .ReturnsAsync(new GetParentCategoriesQueryResult { Success = true, Parents = new() });

        // Act
        await _sut.OnGetAsync();

        // Assert
        _sut.ErrorMessage.Should().Be("Database error");
        _sut.Categories.Should().BeEmpty();
    }

    [Fact]
    public async Task OnGetAsync_WhenExceptionThrown_SetsErrorMessage()
    {
        // Arrange
        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetAllWebsiteCategoriesQueryResult>(It.IsAny<GetAllWebsiteCategoriesQuery>()))
            .ThrowsAsync(new Exception("Network error"));

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetParentCategoriesQueryResult>(It.IsAny<GetParentCategoriesQuery>()))
            .ReturnsAsync(new GetParentCategoriesQueryResult { Success = true, Parents = new() });

        // Act
        await _sut.OnGetAsync();

        // Assert
        _sut.ErrorMessage.Should().Contain("Network error");
    }

    [Fact]
    public async Task OnGetAsync_NormalizesPageNumber_WhenLessThanOne()
    {
        // Arrange
        _sut.CurrentPage = 0;

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetAllWebsiteCategoriesQueryResult>(It.IsAny<GetAllWebsiteCategoriesQuery>()))
            .ReturnsAsync(new GetAllWebsiteCategoriesQueryResult { Success = true, Categories = new(), TotalCount = 0, PageSize = 50 });

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetParentCategoriesQueryResult>(It.IsAny<GetParentCategoriesQuery>()))
            .ReturnsAsync(new GetParentCategoriesQueryResult { Success = true, Parents = new() });

        // Act
        await _sut.OnGetAsync();

        // Assert
        _sut.CurrentPage.Should().Be(1);
    }
}
