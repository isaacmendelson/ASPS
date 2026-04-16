using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using WebApi.Pages.WebsiteCategories;
using WebApi.Services;
using Business.Commands;
using Business.Queries;
using Common.Entities;

namespace ASPS.Tests.WebApi.Pages.WebsiteCategories;

/// <summary>
/// Unit tests for WebsiteCategories/EditModel page
/// JIRA: SCRUM-822
/// </summary>
public class EditModelTests
{
    private readonly Mock<ICQRSClient> _mockCqrsClient;
    private readonly Mock<ILogger<EditModel>> _mockLogger;
    private readonly EditModel _sut;

    public EditModelTests()
    {
        _mockCqrsClient = new Mock<ICQRSClient>();
        _mockLogger = new Mock<ILogger<EditModel>>();
        _sut = new EditModel(_mockCqrsClient.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_InitializesProperties()
    {
        // Assert
        _sut.Input.Should().NotBeNull();
        _sut.AllParents.Should().NotBeNull();
        _sut.AllParents.Should().BeEmpty();
        _sut.Category.Should().BeNull();
        _sut.ErrorMessage.Should().BeNull();
        _sut.SuccessMessage.Should().BeNull();
    }

    [Fact]
    public async Task OnGetAsync_WithoutName_RedirectsToIndex()
    {
        // Arrange
        _sut.Name = "";

        // Act
        var result = await _sut.OnGetAsync();

        // Assert
        result.Should().BeOfType<RedirectToPageResult>();
        var redirectResult = (RedirectToPageResult)result;
        redirectResult.PageName.Should().Be("Index");
    }

    [Fact]
    public async Task OnGetAsync_WithValidName_LoadsCategory_AndPreFillsForm()
    {
        // Arrange
        _sut.Name = "Test Category";

        var category = new WebsiteCategory("Test Category", "parent-123", "Manual");

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetWebsiteCategoryByNameQueryResult>(It.IsAny<GetWebsiteCategoryByNameQuery>()))
            .ReturnsAsync(new GetWebsiteCategoryByNameQueryResult
            {
                Success = true,
                Category = category
            });

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetParentCategoriesQueryResult>(It.IsAny<GetParentCategoriesQuery>()))
            .ReturnsAsync(new GetParentCategoriesQueryResult { Success = true, Parents = new() });

        // Act
        var result = await _sut.OnGetAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _sut.Category.Should().NotBeNull();
        _sut.Category!.Name.Should().Be("Test Category");
        _sut.Input.ParentId.Should().Be("parent-123");
        _sut.Input.Source.Should().Be("Manual");
    }

    [Fact]
    public async Task OnGetAsync_WhenCategoryNotFound_SetsErrorMessage()
    {
        // Arrange
        _sut.Name = "Nonexistent Category";

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetWebsiteCategoryByNameQueryResult>(It.IsAny<GetWebsiteCategoryByNameQuery>()))
            .ReturnsAsync(new GetWebsiteCategoryByNameQueryResult
            {
                Success = false,
                Category = null,
                Message = "Category not found"
            });

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetParentCategoriesQueryResult>(It.IsAny<GetParentCategoriesQuery>()))
            .ReturnsAsync(new GetParentCategoriesQueryResult { Success = true, Parents = new() });

        // Act
        var result = await _sut.OnGetAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _sut.ErrorMessage.Should().Be("Category not found.");
        _sut.Category.Should().BeNull();
    }

    [Fact]
    public async Task OnPostAsync_WithValidData_UpdatesCategory_AndRedirects()
    {
        // Arrange
        _sut.Name = "Existing Category";
        _sut.Input = new EditModel.InputModel
        {
            ParentId = "new-parent-456",
            Source = "Updated Source"
        };

        _mockCqrsClient
            .Setup(x => x.SendCommandAsync<UpdateWebsiteCategoryCommandResult>(It.IsAny<UpdateWebsiteCategoryCommand>()))
            .ReturnsAsync(new UpdateWebsiteCategoryCommandResult { Success = true });

        // Act
        var result = await _sut.OnPostAsync();

        // Assert
        result.Should().BeOfType<RedirectToPageResult>();
        var redirectResult = (RedirectToPageResult)result;
        redirectResult.PageName.Should().Be("Index");
    }

    [Fact]
    public async Task OnPostAsync_SendsCorrectCommand()
    {
        // Arrange
        _sut.Name = "Category To Update";
        _sut.Input = new EditModel.InputModel
        {
            ParentId = "parent-789",
            Source = "  API  "
        };

        UpdateWebsiteCategoryCommand? capturedCommand = null;

        _mockCqrsClient
            .Setup(x => x.SendCommandAsync<UpdateWebsiteCategoryCommandResult>(It.IsAny<UpdateWebsiteCategoryCommand>()))
            .Callback<UpdateWebsiteCategoryCommand>(cmd => capturedCommand = cmd)
            .ReturnsAsync(new UpdateWebsiteCategoryCommandResult { Success = true });

        // Act
        await _sut.OnPostAsync();

        // Assert
        capturedCommand.Should().NotBeNull();
        capturedCommand!.Name.Should().Be("Category To Update");
        capturedCommand.ParentId.Should().Be("parent-789");
        capturedCommand.Source.Should().Be("API"); // Trimmed
    }

    [Fact]
    public async Task OnPostAsync_WithEmptySource_SendsNullSource()
    {
        // Arrange
        _sut.Name = "Category";
        _sut.Input = new EditModel.InputModel
        {
            ParentId = null,
            Source = "  "
        };

        UpdateWebsiteCategoryCommand? capturedCommand = null;

        _mockCqrsClient
            .Setup(x => x.SendCommandAsync<UpdateWebsiteCategoryCommandResult>(It.IsAny<UpdateWebsiteCategoryCommand>()))
            .Callback<UpdateWebsiteCategoryCommand>(cmd => capturedCommand = cmd)
            .ReturnsAsync(new UpdateWebsiteCategoryCommandResult { Success = true });

        // Act
        await _sut.OnPostAsync();

        // Assert
        capturedCommand.Should().NotBeNull();
        capturedCommand!.Source.Should().BeNull();
        capturedCommand.ParentId.Should().BeNull();
    }

    [Fact]
    public async Task OnPostAsync_WhenCommandFails_SetsErrorMessage_AndReturnsPage()
    {
        // Arrange
        _sut.Name = "Category";
        _sut.Input = new EditModel.InputModel { ParentId = "parent-123" };

        var category = new WebsiteCategory("Category", "parent-123", "Manual");

        _mockCqrsClient
            .Setup(x => x.SendCommandAsync<UpdateWebsiteCategoryCommandResult>(It.IsAny<UpdateWebsiteCategoryCommand>()))
            .ReturnsAsync(new UpdateWebsiteCategoryCommandResult
            {
                Success = false,
                Message = "Update failed"
            });

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetWebsiteCategoryByNameQueryResult>(It.IsAny<GetWebsiteCategoryByNameQuery>()))
            .ReturnsAsync(new GetWebsiteCategoryByNameQueryResult { Success = true, Category = category });

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetParentCategoriesQueryResult>(It.IsAny<GetParentCategoriesQuery>()))
            .ReturnsAsync(new GetParentCategoriesQueryResult { Success = true, Parents = new() });

        // Act
        var result = await _sut.OnPostAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _sut.ErrorMessage.Should().Be("Update failed");
    }

    [Fact]
    public async Task OnPostAsync_WhenExceptionThrown_SetsErrorMessage_AndReturnsPage()
    {
        // Arrange
        _sut.Name = "Category";
        _sut.Input = new EditModel.InputModel { ParentId = "parent-123" };

        var category = new WebsiteCategory("Category", "parent-123", "Manual");

        _mockCqrsClient
            .Setup(x => x.SendCommandAsync<UpdateWebsiteCategoryCommandResult>(It.IsAny<UpdateWebsiteCategoryCommand>()))
            .ThrowsAsync(new Exception("Network error"));

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetWebsiteCategoryByNameQueryResult>(It.IsAny<GetWebsiteCategoryByNameQuery>()))
            .ReturnsAsync(new GetWebsiteCategoryByNameQueryResult { Success = true, Category = category });

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetParentCategoriesQueryResult>(It.IsAny<GetParentCategoriesQuery>()))
            .ReturnsAsync(new GetParentCategoriesQueryResult { Success = true, Parents = new() });

        // Act
        var result = await _sut.OnPostAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _sut.ErrorMessage.Should().Contain("Network error");
    }

    [Fact]
    public async Task OnPostAsync_InvalidModelState_DoesNotSendCommand_ReturnsPage()
    {
        // Arrange
        _sut.Name = "Category";
        _sut.ModelState.AddModelError("Input.Source", "Invalid source");

        var category = new WebsiteCategory("Category", "parent-123", "Manual");

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetWebsiteCategoryByNameQueryResult>(It.IsAny<GetWebsiteCategoryByNameQuery>()))
            .ReturnsAsync(new GetWebsiteCategoryByNameQueryResult { Success = true, Category = category });

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetParentCategoriesQueryResult>(It.IsAny<GetParentCategoriesQuery>()))
            .ReturnsAsync(new GetParentCategoriesQueryResult { Success = true, Parents = new() });

        // Act
        var result = await _sut.OnPostAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _mockCqrsClient.Verify(
            x => x.SendCommandAsync<UpdateWebsiteCategoryCommandResult>(It.IsAny<UpdateWebsiteCategoryCommand>()),
            Times.Never
        );
    }
}
