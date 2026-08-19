using System.Net;
using System.Net.WebSockets;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebApi.Services;

namespace ASPS.Tests.WebApi;

/// <summary>
/// ASPS-723: E2E integration tests for the /ws/agent WebSocket gateway.
/// Uses WebApplicationFactory to exercise the full ASP.NET Core middleware
/// pipeline with test doubles for the ZMQ backend interfaces.
/// </summary>
public class AgentWebSocketE2ETests : IDisposable
{
    private readonly List<IDisposable> _disposables = new();

    public void Dispose()
    {
        foreach (var d in _disposables)
            d.Dispose();
    }

    // -----------------------------------------------------------------------
    // Phase 1: No mock needed (middleware handles before backend call)
    // -----------------------------------------------------------------------

    /// <summary>1. HTTP GET to /ws/agent (not a WebSocket upgrade) returns 400.</summary>
    [Fact]
    public async Task HttpGet_NotWebSocketUpgrade_Returns400()
    {
        var factory = CreateFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/ws/agent");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>2. WS upgrade without asps-agent-v1 subprotocol is rejected.</summary>
    [Fact]
    public async Task WebSocketUpgrade_WithoutSubprotocol_IsRejected()
    {
        var factory = CreateFactory();
        var wsClient = factory.Server.CreateWebSocketClient();
        var act = () => wsClient.ConnectAsync(
            new Uri("ws://localhost/ws/agent"), CancellationToken.None);
        await act.Should().ThrowAsync<Exception>();
    }

    /// <summary>3. Gateway disabled (AgentGateway:Enabled=false) returns 503.</summary>
    [Fact]
    public async Task GatewayDisabled_Returns503()
    {
        var factory = CreateFactory(gatewayEnabled: false);
        var client = factory.CreateClient();
        var response = await client.GetAsync("/ws/agent");
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    /// <summary>4. Connection limit exceeded (limiter returns false) is rejected.</summary>
    [Fact]
    public async Task ConnectionLimitExceeded_IsRejected()
    {
        var limiterMock = new Mock<IAgentConnectionLimiter>();
        limiterMock
            .Setup(l => l.TryAcquireConnectionSlot(It.IsAny<string>()))
            .Returns(false);
        var factory = CreateFactory(limiterMock: limiterMock);
        var wsClient = factory.Server.CreateWebSocketClient();
        wsClient.SubProtocols.Add("asps-agent-v1");
        var act = () => wsClient.ConnectAsync(
            new Uri("ws://localhost/ws/agent"), CancellationToken.None);
        await act.Should().ThrowAsync<Exception>();
    }

    /// <summary>5. WS upgrade with correct subprotocol: 101 + connection open.</summary>
    [Fact]
    public async Task CorrectSubprotocol_SuccessfulConnection()
    {
        var factory = CreateFactory();
        var ws = await ConnectAgentAsync(factory);
        ws.State.Should().Be(WebSocketState.Open);
        ws.SubProtocol.Should().Be("asps-agent-v1");
        await CloseClientAsync(ws);
    }

    /// <summary>6. Send invalid JSON: receive error frame + connection closed.</summary>
    [Fact]
    public async Task InvalidJson_ErrorFrameAndClose()
    {
        var factory = CreateFactory();
        var ws = await ConnectAgentAsync(factory);
        await SendTextAsync(ws, "{broken json!!");
        var error = await ReceiveJsonFrameAsync(ws);
        error.Should().NotBeNull("server should send an error frame before closing");
        error!["frame"]!.ToString().Should().Be("error");
        error["code"]!.ToString().Should().Be(AgentErrorCode.InvalidFrame);
        await AssertServerClosedAsync(ws);
    }

    /// <summary>
    /// 7. Send unauthenticated request (not RequestToken/RefreshToken/RegisterDevice):
    ///    receive auth.not_authenticated error frame. Connection stays open.
    /// </summary>
    [Fact]
    public async Task UnauthenticatedRequest_ReturnsNotAuthenticatedError()
    {
        var factory = CreateFactory();
        var ws = await ConnectAgentAsync(factory);
        await SendRequestFrameAsync(ws, "req-unauth-1", new JObject
        {
            ["AlertType"] = "UrlAlert",
            ["Url"] = "http://evil.example"
        });
        var error = await ReceiveJsonFrameAsync(ws);
        error!["frame"]!.ToString().Should().Be("error");
        error["code"]!.ToString().Should().Be(AgentErrorCode.NotAuthenticated);
        error["id"]!.Type.Should().Be(JTokenType.Null);
        ws.State.Should().Be(WebSocketState.Open);
        await CloseClientAsync(ws);
    }

    /// <summary>8. Send oversized frame: error frame (FrameTooLarge) + connection closed.</summary>
    [Fact]
    public async Task OversizedFrame_ErrorAndClose()
    {
        var factory = CreateFactory(maxFrameSize: 256);
        var ws = await ConnectAgentAsync(factory);
        var oversizedFrame = BuildRequestFrame("req-big", new JObject
        {
            ["data"] = new string('X', 500)
        });
        Encoding.UTF8.GetByteCount(oversizedFrame).Should().BeGreaterThan(256,
            "test setup: frame must actually exceed the configured MaxFrameSize");
        await SendTextAsync(ws, oversizedFrame);
        var error = await ReceiveJsonFrameAsync(ws);
        error.Should().NotBeNull("server should send FrameTooLarge error before closing");
        error!["frame"]!.ToString().Should().Be("error");
        error["code"]!.ToString().Should().Be(AgentErrorCode.FrameTooLarge);
        await AssertServerClosedAsync(ws);
    }

    // -----------------------------------------------------------------------
    // Phase 2: With mock IAgentBackendGateway
    // -----------------------------------------------------------------------

    /// <summary>
    /// 9. Send RequestToken, mock returns TokenCreated: verify response frame
    ///    and verify authenticated state (subsequent non-auth request succeeds).
    /// </summary>
    [Fact]
    public async Task RequestToken_Success_ReturnsResponseAndAuthenticates()
    {
        var gatewayMock = new Mock<IAgentBackendGateway>();
        gatewayMock
            .Setup(g => g.ForwardRequestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentBackendResult.Ok("{\"status\":\"TokenCreated\",\"token\":\"tok-e2e-9\",\"deviceUid\":\"PC-E2E-001\",\"expiration\":\"2026-12-31T23:59:59Z\"}"));

        var factory = CreateFactory(gatewayMock: gatewayMock);
        var ws = await ConnectAgentAsync(factory);

        await SendRequestFrameAsync(ws, "req-auth-9", new JObject
        {
            ["MessageType"] = "RequestToken",
            ["DeviceUid"] = "PC-E2E-001",
            ["Email"] = "test@example.com"
        });

        var resp = await ReceiveJsonFrameAsync(ws);
        resp!["frame"]!.ToString().Should().Be("response");
        resp["id"]!.ToString().Should().Be("req-auth-9");
        resp["payload"]!["status"]!.ToString().Should().Be("TokenCreated");
        resp["payload"]!["token"]!.ToString().Should().Be("tok-e2e-9");
        resp["payload"]!["deviceUid"]!.ToString().Should().Be("PC-E2E-001");

        // Verify authenticated: non-auth request should succeed
        await SendRequestFrameAsync(ws, "req-after-auth", new JObject
        {
            ["Token"] = "tok-e2e-9",
            ["AlertType"] = "UrlAlert"
        });

        var postAuthResp = await ReceiveJsonFrameAsync(ws);
        postAuthResp!["frame"]!.ToString().Should().Be("response",
            "after authentication, non-auth requests should produce a response frame, not an error");

        await CloseClientAsync(ws);
    }

    /// <summary>
    /// 10. After auth, send alert: response echoes correct correlation id.
    /// </summary>
    [Fact]
    public async Task AuthenticatedAlert_ResponseEchoesCorrelationId()
    {
        var gatewayMock = new Mock<IAgentBackendGateway>();
        gatewayMock
            .SetupSequence(g => g.ForwardRequestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentBackendResult.Ok("{\"status\":\"TokenCreated\",\"token\":\"tok-e2e-10\",\"deviceUid\":\"PC-E2E-010\"}"))
            .ReturnsAsync(AgentBackendResult.Ok("{\"status\":\"AlertProcessed\",\"alertId\":\"alert-42\"}"));

        var factory = CreateFactory(gatewayMock: gatewayMock);
        var ws = await ConnectAgentAsync(factory);

        // Authenticate first
        await SendRequestFrameAsync(ws, "req-auth-10", new JObject
        {
            ["MessageType"] = "RequestToken",
            ["DeviceUid"] = "PC-E2E-010"
        });
        await ReceiveJsonFrameAsync(ws); // consume auth response

        // Send alert with a specific correlation id
        var correlationId = "corr-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        await SendRequestFrameAsync(ws, correlationId, new JObject
        {
            ["Token"] = "tok-e2e-10",
            ["AlertType"] = "UrlAlert",
            ["Url"] = "http://phishing.example"
        });

        var alertResp = await ReceiveJsonFrameAsync(ws);
        alertResp!["frame"]!.ToString().Should().Be("response");
        alertResp["id"]!.ToString().Should().Be(correlationId,
            "the response frame id must echo the correlation id from the request");
        alertResp["payload"]!["alertId"]!.ToString().Should().Be("alert-42");

        await CloseClientAsync(ws);
    }

    /// <summary>
    /// 11. After auth, send subscribe, push notification through captured
    ///     callback: client receives the notification frame.
    /// </summary>
    [Fact]
    public async Task Subscribe_PushNotification_ClientReceivesNotificationFrame()
    {
        var subscribeCalled = new TaskCompletionSource<Func<string, string, Task>>();
        var subscriptionMock = new Mock<IAgentNotificationSubscription>();
        subscriptionMock.SetupGet(s => s.DeviceUid).Returns("PC-E2E-011");

        var gatewayMock = new Mock<IAgentBackendGateway>();
        gatewayMock
            .Setup(g => g.ForwardRequestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentBackendResult.Ok("{\"status\":\"TokenCreated\",\"token\":\"tok-e2e-11\",\"deviceUid\":\"PC-E2E-011\"}"));
        gatewayMock
            .Setup(g => g.Subscribe("PC-E2E-011", It.IsAny<Func<string, string, Task>>()))
            .Callback<string, Func<string, string, Task>>((_, cb) => subscribeCalled.TrySetResult(cb))
            .Returns(subscriptionMock.Object);

        var factory = CreateFactory(gatewayMock: gatewayMock);
        var ws = await ConnectAgentAsync(factory);

        // 1. Authenticate
        await SendRequestFrameAsync(ws, "req-auth-11", new JObject
        {
            ["MessageType"] = "RequestToken",
            ["DeviceUid"] = "PC-E2E-011"
        });
        var authResp = await ReceiveJsonFrameAsync(ws);
        authResp!["payload"]!["status"]!.ToString().Should().Be("TokenCreated");

        // 2. Subscribe
        await SendTextAsync(ws, new JObject
        {
            ["frame"] = "subscribe",
            ["deviceUid"] = "PC-E2E-011"
        }.ToString(Formatting.None));

        // 3. Wait for server-side subscribe to complete
        var pushCallback = await subscribeCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // 4. Push notification through the captured callback
        await pushCallback("device:PC-E2E-011", "{\"alertId\":\"notif-777\",\"severity\":\"high\"}");

        // 5. Client receives the notification frame
        var notif = await ReceiveJsonFrameAsync(ws);
        notif!["frame"]!.ToString().Should().Be("notification");
        notif["topic"]!.ToString().Should().Be("device:PC-E2E-011");
        notif["payload"]!["alertId"]!.ToString().Should().Be("notif-777");
        notif["payload"]!["severity"]!.ToString().Should().Be("high");

        await CloseClientAsync(ws);
    }

    /// <summary>
    /// 12. Send request, mock returns timeout: verify error frame with
    ///     gateway.timeout and the request id echoed (request-level error).
    /// </summary>
    [Fact]
    public async Task RequestTimeout_ReturnsTimeoutErrorWithRequestId()
    {
        var gatewayMock = new Mock<IAgentBackendGateway>();
        gatewayMock
            .SetupSequence(g => g.ForwardRequestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentBackendResult.Ok("{\"status\":\"TokenCreated\",\"token\":\"tok-e2e-12\",\"deviceUid\":\"PC-E2E-012\"}"))
            .ReturnsAsync(AgentBackendResult.Timeout());

        var factory = CreateFactory(gatewayMock: gatewayMock);
        var ws = await ConnectAgentAsync(factory);

        // Authenticate first
        await SendRequestFrameAsync(ws, "req-auth-12", new JObject
        {
            ["MessageType"] = "RequestToken",
            ["DeviceUid"] = "PC-E2E-012"
        });
        var authResp = await ReceiveJsonFrameAsync(ws);
        authResp!["payload"]!["status"]!.ToString().Should().Be("TokenCreated");

        // Send request that will timeout
        var requestId = "req-will-timeout";
        await SendRequestFrameAsync(ws, requestId, new JObject
        {
            ["Token"] = "tok-e2e-12",
            ["AlertType"] = "UrlAlert"
        });

        var error = await ReceiveJsonFrameAsync(ws);
        error!["frame"]!.ToString().Should().Be("error");
        error["code"]!.ToString().Should().Be(AgentErrorCode.Timeout);
        // gateway.timeout is request-level: id echoes the request per protocol section 9
        error["id"]!.ToString().Should().Be(requestId,
            "gateway.timeout is a request-level error so id must echo the request");

        // Connection should remain open after a timeout error
        ws.State.Should().Be(WebSocketState.Open);

        await CloseClientAsync(ws);
    }

    // -----------------------------------------------------------------------
    // Factory
    // -----------------------------------------------------------------------

    private WebApplicationFactory<Program> CreateFactory(
        bool gatewayEnabled = true,
        int? maxFrameSize = null,
        Mock<IAgentBackendGateway>? gatewayMock = null,
        Mock<IAgentConnectionLimiter>? limiterMock = null)
    {
        var gw = gatewayMock ?? new Mock<IAgentBackendGateway>();
        var lim = limiterMock ?? CreateDefaultLimiter();

        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    var overrides = new Dictionary<string, string?>
                    {
                        ["Security:CurveEnabled"] = "false",
                        ["AgentGateway:Enabled"] = gatewayEnabled.ToString(),
                        ["Keycloak:ClientId"] = "",
                    };
                    if (maxFrameSize.HasValue)
                        overrides["AgentGateway:MaxFrameSize"] = maxFrameSize.Value.ToString();

                    config.AddInMemoryCollection(overrides);
                });

                builder.ConfigureServices(services =>
                {
                    // Remove all hosted services to prevent constructing
                    // AgentGatewayService (CURVE keys), SimulationRunner, etc.
                    var hostedDescriptors = services
                        .Where(d => d.ServiceType == typeof(IHostedService))
                        .ToList();
                    foreach (var d in hostedDescriptors)
                        services.Remove(d);

                    // Replace agent gateway interfaces with test doubles
                    services.RemoveAll<AgentGatewayService>();
                    services.RemoveAll<IAgentBackendGateway>();
                    services.RemoveAll<IAgentConnectionLimiter>();
                    services.AddSingleton(gw.Object);
                    services.AddSingleton(lim.Object);

                    // Replace CQRS client to prevent NetMQ socket creation
                    services.RemoveAll<ICQRSClient>();
                    services.AddSingleton(new Mock<ICQRSClient>().Object);
                });
            });

        _disposables.Add(factory);
        return factory;
    }

    private static Mock<IAgentConnectionLimiter> CreateDefaultLimiter()
    {
        var mock = new Mock<IAgentConnectionLimiter>();
        mock.Setup(l => l.TryAcquireConnectionSlot(It.IsAny<string>())).Returns(true);
        return mock;
    }

    // -----------------------------------------------------------------------
    // WebSocket helpers
    // -----------------------------------------------------------------------

    private static async Task<WebSocket> ConnectAgentAsync(WebApplicationFactory<Program> factory)
    {
        var wsClient = factory.Server.CreateWebSocketClient();
        wsClient.SubProtocols.Add("asps-agent-v1");
        return await wsClient.ConnectAsync(
            new Uri("ws://localhost/ws/agent"), CancellationToken.None);
    }

    private static string BuildRequestFrame(string id, JObject payload) =>
        new JObject
        {
            ["frame"] = "request",
            ["id"] = id,
            ["payload"] = payload
        }.ToString(Formatting.None);

    private static Task SendRequestFrameAsync(WebSocket ws, string id, JObject payload) =>
        SendTextAsync(ws, BuildRequestFrame(id, payload));

    private static async Task SendTextAsync(WebSocket ws, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await ws.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            CancellationToken.None);
    }

    private static async Task<JObject?> ReceiveJsonFrameAsync(WebSocket ws, int timeoutMs = 5000)
    {
        var text = await ReceiveTextAsync(ws, timeoutMs);
        return text is null ? null : JObject.Parse(text);
    }

    private static async Task<string?> ReceiveTextAsync(WebSocket ws, int timeoutMs = 5000)
    {
        var buffer = new byte[65536];
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(
                    new ArraySegment<byte>(buffer), cts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                    return null;
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            return Encoding.UTF8.GetString(ms.ToArray());
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                $"No WebSocket message received within {timeoutMs}ms");
        }
    }

    /// <summary>
    /// Asserts that the server closes the WebSocket connection. Drains any
    /// remaining text frames until a Close frame is received or the connection
    /// is aborted.
    /// </summary>
    private static async Task AssertServerClosedAsync(WebSocket ws, int timeoutMs = 5000)
    {
        var buffer = new byte[256];
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            while (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                var result = await ws.ReceiveAsync(
                    new ArraySegment<byte>(buffer), cts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                    return;
            }
        }
        catch (WebSocketException)
        {
            return; // Connection aborted -- pass
        }
        catch (OperationCanceledException)
        {
            Assert.Fail("Server did not close the WebSocket connection within the timeout");
        }
    }

    private static async Task CloseClientAsync(WebSocket ws)
    {
        if (ws.State == WebSocketState.Open)
        {
            using var cts = new CancellationTokenSource(3000);
            try
            {
                await ws.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, "test done", cts.Token);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}
