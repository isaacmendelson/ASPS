using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Business.Messaging.Transport.NetMQ;
using Common.Messaging;
using WebApi.Services;

namespace ASPS.Tests.WebApi.Services;

/// <summary>
/// ASPS-687 (Messaging Refactoring Phase 4): NetMQCqrsClientAdapter bridges
/// Business.Messaging.Transport.NetMQ.NetMQCqrsClient (ICqrsClient) to the legacy
/// WebApi.Services.ICQRSClient contract, so existing WebApi consumers work unmodified.
/// </summary>
public class NetMQCqrsClientAdapterTests
{
    [Fact]
    public void ImplementsLegacyICQRSClient()
    {
        var inner = new NetMQCqrsClient("tcp://localhost:60002", NullLogger<NetMQCqrsClient>.Instance);
        var sut = new NetMQCqrsClientAdapter(inner);

        sut.Should().BeAssignableTo<ICQRSClient>();
    }

    [Fact]
    public void Constructor_WithNullInner_Throws()
    {
        Action act = () => new NetMQCqrsClientAdapter(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SendQueryAsync_DelegatesToInner_FailsClosedWithoutSecurityConfiguration()
    {
        var inner = new NetMQCqrsClient("tcp://localhost:65529", NullLogger<NetMQCqrsClient>.Instance);
        ICQRSClient sut = new NetMQCqrsClientAdapter(inner);

        var result = await sut.SendQueryAsync<TestQueryResult>(new TestQuery());

        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("CURVE");
    }

    [Fact]
    public async Task SendCommandAsync_DelegatesToInner_FailsClosedWithoutSecurityConfiguration()
    {
        var inner = new NetMQCqrsClient("tcp://localhost:65528", NullLogger<NetMQCqrsClient>.Instance);
        ICQRSClient sut = new NetMQCqrsClientAdapter(inner);

        var result = await sut.SendCommandAsync<TestCommandResult>(new TestCommand());

        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("CURVE");
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var inner = new NetMQCqrsClient("tcp://localhost:60003", NullLogger<NetMQCqrsClient>.Instance);
        var sut = new NetMQCqrsClientAdapter(inner);

        Action act = () => sut.Dispose();

        act.Should().NotThrow();
    }

    private class TestQuery : Query { }
    private class TestQueryResult : QueryResult { }
    private class TestCommand : Command { }
    private class TestCommandResult : CommandResult { }
}
