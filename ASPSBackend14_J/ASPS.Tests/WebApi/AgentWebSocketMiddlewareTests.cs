using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WebApi.Middleware;
using WebApi.Services;
using Xunit;

namespace ASPS.Tests.WebApi;

/// <summary>
/// ASPS-723 — AgentWebSocketMiddleware must resolve its dependencies through
/// interfaces (IAgentBackendGateway / IAgentConnectionLimiter / IOptions&lt;AgentGatewayOptions&gt;)
/// so it can be exercised with test doubles in E2E/integration tests, without
/// requiring the real sealed AgentGatewayService (which pulls in CurveKeyManager
/// and reads CURVE key material from disk).
/// </summary>
public class AgentWebSocketMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ResolvesDependenciesViaInterfaces_NotConcreteSealedGatewayService()
    {
        // Arrange: a DI container that provides only test doubles for the agent
        // gateway seams — no real AgentGatewayService/CurveKeyManager registered.
        // This mirrors an E2E/integration test host.
        var services = new ServiceCollection();
        var gatewayMock = new Mock<IAgentBackendGateway>();
        var limiterMock = new Mock<IAgentConnectionLimiter>();
        limiterMock.Setup(l => l.TryAcquireConnectionSlot(It.IsAny<string>())).Returns(true);
        services.AddSingleton(gatewayMock.Object);
        services.AddSingleton(limiterMock.Object);
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Path = "/ws/agent";

        var middleware = new AgentWebSocketMiddleware(
            _ => Task.CompletedTask,
            NullLogger<AgentWebSocketMiddleware>.Instance,
            Options.Create(new AgentGatewayOptions { Enabled = true }));

        // Act
        var act = async () => await middleware.InvokeAsync(context);

        // Assert: must not throw trying to resolve a concrete sealed class that
        // was never registered — only the interfaces were provided.
        await act.Should().NotThrowAsync(
            "the middleware must depend on IAgentBackendGateway/IAgentConnectionLimiter, " +
            "not the concrete sealed AgentGatewayService, so test doubles can be injected");

        // Not a real WebSocket upgrade request, so it is rejected with 400 —
        // proves the middleware actually ran past dependency resolution.
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}
