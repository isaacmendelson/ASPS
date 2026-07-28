using Business.RealtimeAnalysis.UserDomain;
using Common.Generated.Messaging.V1;
using FluentAssertions;

namespace ASPS.Tests.Business.UserDomain;

public class AnalyzerV1ProcessClientTests
{
    [Fact]
    public async Task RunAsync_RealAnalyzerSubprocess_PreservesEchoAndReturnsStructuredError()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var analyzerDirectory = Path.Combine(
            repositoryRoot, "Analyzers", "basic-url-analyzer");
        var python = Path.Combine(analyzerDirectory, ".venv", "Scripts", "python.exe");
        var identity = new MessageIdentityV1(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "device-1", "12", "https://127.0.0.1/");

        Func<Task> action = () => new AnalyzerV1ProcessClient()
            .RunAsync(python, analyzerDirectory, identity, TimeSpan.FromSeconds(20));

        var exception = await action.Should().ThrowAsync<AnalyzerV1ProcessException>();
        exception.Which.Code.Should().Be("analyzer.analysis_failed");
        exception.Which.Response.RequestId.Should().Be(identity.RequestId);
        exception.Which.Response.CorrelationId.Should().Be(identity.CorrelationId);
        exception.Which.Response.Context.Url.Should().Be(identity.Url);
        exception.Which.Request.MessageId.Should().NotBe(identity.MessageId);
        exception.Which.Request.RequestId.Should().Be(identity.RequestId);
        exception.Which.Request.CorrelationId.Should().Be(identity.CorrelationId);
        exception.Which.Request.Context.Should().BeEquivalentTo(
            new MessageContextV1
            {
                DeviceId = identity.DeviceId,
                TabId = identity.TabId,
                Url = identity.Url
            });
    }
}
