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
/// Unit tests for WebsiteCategories/CreateModel page
/// JIRA: SCRUM-822
/// </summary>
public class CreateModelTests
{
    private readonly Mock<ICQRSClient> _mockCqrsClient;
    private readonly Mock<ILogger<CreateModel>> _mockLogger;
    private readonly CreateModel _sut;

    public CreateModelTests()
    {
        _mockCqrsClient = new Mock<ICQRSClient>();
        _mockLogger = new Mock<ILogger<CreateModel>>();
        _sut = new CreateModel(_mockCqrsClient.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_InitializesProperties()
    {
        // Assert
        _sut.Input.Should().NotBeNull();
        _sut.AllParents.Should().NotBeNull();
        _sut.AllParents.Should().BeEmpty();
        _sut.ErrorMessage.Should().BeNull();
        _sut.SuccessMessage.Should().BeNull();
    }

    [Fact]
    public async Task OnGetAsync_LoadsParentCategories()
    {
        // Arrange
        var expectedParents = new List<WebsiteCategory>
        {
            new WebsiteCategory("E-Commerce", "", "Manual"),
            new WebsiteCategory("Media", "", "Manual")
        };

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
        _sut.AllParents.Should().HaveCount(2);
        _sut.AllParents.Should().BeEquivalentTo(expectedParents);
    }

    [Fact]
    public async Task OnPostAsync_WithValidData_CreatesCategory_AndRedirects()
    {
        // Arrange
        _sut.Input = new CreateModel.InputModel
        {
            Name = "New Category",
            ParentId = "parent-123",
            Source = "Manual"
        };

        _mockCqrsClient
            .Setup(x => x.SendCommandAsync<CreateWebsiteCategoryCommandResult>(It.IsAny<CreateWebsiteCategoryCommand>()))
            .ReturnsAsync(new CreateWebsiteCategoryCommandResult
            {
                Success = true,
                CategoryId = 42
            });

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetParentCategoriesQueryResult>(It.IsAny<GetParentCategoriesQuery>()))
            .ReturnsAsync(new GetParentCategoriesQueryResult { Success = true, Parents = new() });

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
        _sut.Input = new CreateModel.InputModel
        {
            Name = "  Test Category  ",
            ParentId = "parent-456",
            Source = "  Import  "
        };

        CreateWebsiteCategoryCommand? capturedCommand = null;

        _mockCqrsClient
            .Setup(x => x.SendCommandAsync<CreateWebsiteCategoryCommandResult>(It.IsAny<CreateWebsiteCategoryCommand>()))
            .Callback<CreateWebsiteCategoryCommand>(cmd => capturedCommand = cmd)
            .ReturnsAsync(new CreateWebsiteCategoryCommandResult { Success = true, CategoryId = 1 });

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetParentCategoriesQueryResult>(It.IsAny<GetParentCategoriesQuery>()))
            .ReturnsAsync(new GetParentCategoriesQueryResult { Success = true, Parents = new() });

        // Act
        await _sut.OnPostAsync();

        // Assert
        capturedCommand.Should().NotBeNull();
        capturedCommand!.Name.Should().Be("Test Category"); // Trimmed
        capturedCommand.ParentId.Should().Be("parent-456");
        capturedCommand.Source.Should().Be("Import"); // Trimmed
    }

    [Fact]
    public async Task OnPostAsync_WithEmptyParentId_SendsNullParentId()
    {
        // Arrange
        _sut.Input = new CreateModel.InputModel
        {
            Name = "Top Level Category",
            ParentId = "",
            Source = null
        };

        CreateWebsiteCategoryCommand? capturedCommand = null;

        _mockCqrsClient
            .Setup(x => x.SendCommandAsync<CreateWebsiteCategoryCommandResult>(It.IsAny<CreateWebsiteCategoryCommand>()))
            .Callback<CreateWebsiteCategoryCommand>(cmd => capturedCommand = cmd)
            .ReturnsAsync(new CreateWebsiteCategoryCommandResult { Success = true, CategoryId = 1 });

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetParentCategoriesQueryResult>(It.IsAny<GetParentCategoriesQuery>()))
            .ReturnsAsync(new GetParentCategoriesQueryResult { Success = true, Parents = new() });

        // Act
        await _sut.OnPostAsync();

        // Assert
        capturedCommand.Should().NotBeNull();
        capturedCommand!.ParentId.Should().BeNull();
        capturedCommand.Source.Should().BeNull();
    }

    [Fact]
    public async Task OnPostAsync_WhenCommandFails_SetsErrorMessage_AndReturnsPage()
    {
        // Arrange
        _sut.Input = new CreateModel.InputModel
        {
            Name = "Duplicate Category"
        };

        _mockCqrsClient
            .Setup(x => x.SendCommandAsync<CreateWebsiteCategoryCommandResult>(It.IsAny<CreateWebsiteCategoryCommand>()))
            .ReturnsAsync(new CreateWebsiteCategoryCommandResult
            {
                Success = false,
                Message = "Category already exists"
            });

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetParentCategoriesQueryResult>(It.IsAny<GetParentCategoriesQuery>()))
            .ReturnsAsync(new GetParentCategoriesQueryResult { Success = true, Parents = new() });

        // Act
        var result = await _sut.OnPostAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _sut.ErrorMessage.Should().Be("Category already exists");
    }

    [Fact]
    public async Task OnPostAsync_WhenExceptionThrown_SetsErrorMessage_AndReturnsPage()
    {
        // Arrange
        _sut.Input = new CreateModel.InputModel
        {
            Name = "Error Category"
        };

        _mockCqrsClient
            .Setup(x => x.SendCommandAsync<CreateWebsiteCategoryCommandResult>(It.IsAny<CreateWebsiteCategoryCommand>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetParentCategoriesQueryResult>(It.IsAny<GetParentCategoriesQuery>()))
            .ReturnsAsync(new GetParentCategoriesQueryResult { Success = true, Parents = new() });

        // Act
        var result = await _sut.OnPostAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _sut.ErrorMessage.Should().Contain("Database connection failed");
    }

    [Fact]
    public async Task OnPostAsync_InvalidModelState_DoesNotSendCommand_ReturnsPage()
    {
        // Arrange
        _sut.ModelState.AddModelError("Input.Name", "Name is required");

        _mockCqrsClient
            .Setup(x => x.SendQueryAsync<GetParentCategoriesQueryResult>(It.IsAny<GetParentCategoriesQuery>()))
            .ReturnsAsync(new GetParentCategoriesQueryResult { Success = true, Parents = new() });

        // Act
        var result = await _sut.OnPostAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _mockCqrsClient.Verify(
            x => x.SendCommandAsync<CreateWebsiteCategoryCommandResult>(It.IsAny<CreateWebsiteCategoryCommand>()),
            Times.Never
        );
    }
}
