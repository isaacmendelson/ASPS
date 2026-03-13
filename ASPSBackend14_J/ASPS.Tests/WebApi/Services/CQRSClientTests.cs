using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using WebApi.Services;
using Common.Messaging;

namespace ASPS.Tests.WebApi.Services;

public class CQRSClientTests
{
    private readonly Mock<ILogger<CQRSClient>> _loggerMock;
    private readonly string _endpoint = "tcp://localhost:60000";

    public CQRSClientTests()
    {
        _loggerMock = new Mock<ILogger<CQRSClient>>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var sut = new CQRSClient(_endpoint, _loggerMock.Object);

        // Assert
        sut.Should().NotBeNull();
    }

    // Removed: CurveKeyManager does not exist in current codebase

    #endregion

    #region SendQueryAsync Tests

    [Fact]
    public async Task SendQueryAsync_WithTimeout_ReturnsErrorResult()
    {
        // Arrange
        var sut = new CQRSClient("tcp://localhost:65534", _loggerMock.Object);
        var query = new TestQuery();

        // Act
        var result = await sut.SendQueryAsync<TestQueryResult>(query);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("timeout"); // no server is listening
    }

    #endregion

    #region SendCommandAsync Tests

    [Fact]
    public async Task SendCommandAsync_WithTimeout_ReturnsErrorResult()
    {
        // Arrange
        var sut = new CQRSClient("tcp://localhost:55001", _loggerMock.Object);
        var command = new TestCommand();

        // Act
        var result = await sut.SendCommandAsync<TestCommandResult>(command);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("timeout"); // no server is listening
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var sut = new CQRSClient(_endpoint, _loggerMock.Object);

        // Act
        Action act = () => sut.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    // Test helper classes
    private class TestQuery : Query { }
    private class TestQueryResult : QueryResult { }
    private class TestCommand : Command { }
    private class TestCommandResult : CommandResult { }
}
