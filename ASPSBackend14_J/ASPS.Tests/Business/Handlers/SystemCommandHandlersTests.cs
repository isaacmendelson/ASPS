using Xunit;
using Moq;
using FluentAssertions;
using Business.Handlers;
using Business.Commands;
using Business.Views;
using System.Threading.Tasks;

namespace ASPS.Tests.Business.Handlers;

/// <summary>
/// Unit tests for SystemCommandHandlers
/// ASPS-398: Fixed deadlock in ReInitialize command by making it async
/// </summary>
public class SystemCommandHandlersTests
{
    private readonly Mock<ASView> _asViewMock;
    private readonly SystemCommandHandlers _sut;

    public SystemCommandHandlersTests()
    {
        var serviceProviderMock = new Mock<IServiceProvider>();
        var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<ASView>>();
        var configurationMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        
        _asViewMock = new Mock<ASView>(serviceProviderMock.Object, loggerMock.Object, configurationMock.Object);
        _sut = new SystemCommandHandlers(_asViewMock.Object);
    }

    #region ReInitializeASViewCommand Tests

    [Fact]
    public async Task HandleAsync_ReInitializeASViewCommand_CallsReInitializeAsync()
    {
        // Arrange
        var command = new ReInitializeASViewCommand();
        
        _asViewMock
            .Setup(x => x.ReInitializeAsync())
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var result = await _sut.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Be("ASView re-initialized successfully!");
        
        _asViewMock.Verify(x => x.ReInitializeAsync(), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ReInitializeASViewCommand_ReturnsErrorWhenExceptionThrown()
    {
        // Arrange
        var command = new ReInitializeASViewCommand();
        var exceptionMessage = "Database connection failed";
        
        _asViewMock
            .Setup(x => x.ReInitializeAsync())
            .ThrowsAsync(new System.Exception(exceptionMessage));

        // Act
        var result = await _sut.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain(exceptionMessage);
        result.Message.Should().StartWith("Error re-initializing ASView:");
    }

    [Fact]
    public async Task HandleAsync_ReInitializeASViewCommand_HandlesTimeoutGracefully()
    {
        // Arrange
        var command = new ReInitializeASViewCommand();
        
        _asViewMock
            .Setup(x => x.ReInitializeAsync())
            .ThrowsAsync(new System.TimeoutException("Operation timed out after 10s"));

        // Act
        var result = await _sut.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Operation timed out");
    }

    [Fact]
    public async Task HandleAsync_ReInitializeASViewCommand_ReturnsSuccessMessage()
    {
        // Arrange
        var command = new ReInitializeASViewCommand();
        
        _asViewMock
            .Setup(x => x.ReInitializeAsync())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.HandleAsync(command);

        // Assert
        result.Message.Should().NotBeNullOrEmpty();
        result.Message.Should().Contain("successfully");
    }

    #endregion
}
