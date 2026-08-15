using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Business.Messaging.Abstractions;
using Business.Messaging.Transport.NetMQ;
using Common.Messaging;

namespace ASPS.Tests.Business.Messaging;

/// <summary>
/// ASPS-687 (Messaging Refactoring Phase 4): NetMQCqrsClient replaces WebApi.Services.CQRSClient
/// as the canonical transport implementation, living in Business (not WebApi) so Business-hosted
/// callers can depend on it too, and implementing the transport-agnostic
/// Business.Messaging.Abstractions.ICqrsClient (adds CancellationToken support). Business cannot
/// reference WebApi, so the legacy WebApi.Services.ICQRSClient contract is bridged separately by
/// WebApi.Services.NetMQCqrsClientAdapter — see NetMQCqrsClientAdapterTests.
/// </summary>
public class NetMQCqrsClientTests
{
    private readonly Mock<ILogger<NetMQCqrsClient>> _loggerMock;
    private readonly string _endpoint = "tcp://localhost:60001";

    public NetMQCqrsClientTests()
    {
        _loggerMock = new Mock<ILogger<NetMQCqrsClient>>();
    }

    #region Constructor / interface tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        var sut = new NetMQCqrsClient(_endpoint, _loggerMock.Object);

        sut.Should().NotBeNull();
    }

    [Fact]
    public void ImplementsExpectedInterfaces()
    {
        var sut = new NetMQCqrsClient(_endpoint, _loggerMock.Object);

        sut.Should().BeAssignableTo<ICqrsClient>();
        sut.Should().BeAssignableTo<IDisposable>();
    }

    #endregion

    #region SendQueryAsync Tests

    [Fact]
    public async Task SendQueryAsync_WithoutSecurityConfiguration_FailsClosed()
    {
        var sut = new NetMQCqrsClient("tcp://localhost:65533", _loggerMock.Object);
        var query = new TestQuery();

        var result = await sut.SendQueryAsync<TestQueryResult>(query);

        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("CURVE");
    }

    [Fact]
    public async Task SendQueryAsync_WithCancellationToken_WithoutSecurityConfiguration_FailsClosed()
    {
        var sut = new NetMQCqrsClient("tcp://localhost:65532", _loggerMock.Object);
        var query = new TestQuery();

        var result = await sut.SendQueryAsync<TestQueryResult>(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("CURVE");
    }

    [Fact]
    public async Task SendQueryAsync_WithAlreadyCanceledToken_ThrowsOperationCanceled()
    {
        var sut = new NetMQCqrsClient("tcp://localhost:65530", _loggerMock.Object);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.SendQueryAsync<TestQueryResult>(new TestQuery(), cts.Token));
    }

    #endregion

    #region SendCommandAsync Tests

    [Fact]
    public async Task SendCommandAsync_WithoutSecurityConfiguration_FailsClosed()
    {
        var sut = new NetMQCqrsClient("tcp://localhost:55002", _loggerMock.Object);
        var command = new TestCommand();

        var result = await sut.SendCommandAsync<TestCommandResult>(command);

        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("CURVE");
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var sut = new NetMQCqrsClient(_endpoint, _loggerMock.Object);

        Action act = () => sut.Dispose();

        act.Should().NotThrow();
    }

    #endregion

    // Test helper classes
    private class TestQuery : Query { }
    private class TestQueryResult : QueryResult { }
    private class TestCommand : Command { }
    private class TestCommandResult : CommandResult { }
}
