using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using WebApi.Services;

namespace ASPS.Tests.WebApi;

/// <summary>
/// ASPS-720 — /ws/agent WebSocket gateway. Covers the pure/testable seams:
/// frame parsing, error-frame generation, the auth state machine, and
/// request/response correlation by id. IO (real WebSocket + real ZMQ sockets)
/// is exercised end-to-end by ASPS-723 (QA).
/// </summary>
public class AgentGatewayTests
{
    private const int MaxFrameSize = 262144;

    // ─────────────────────────────────────────────────────────────────────────
    // Frame parsing (WS-AGENT-PROTOCOL.md section 3.1)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ValidRequestFrame_Succeeds()
    {
        var raw = """{"frame":"request","id":"aaa-111","payload":{"MessageType":"RequestToken","DeviceUid":"PC-1","Email":"a@b.com"}}""";

        var result = AgentFrameParser.Parse(raw, MaxFrameSize);

        result.Success.Should().BeTrue();
        result.Frame!.Frame.Should().Be(AgentFrameType.Request);
        result.Frame.Id.Should().Be("aaa-111");
        result.Frame.Payload!["MessageType"]!.ToString().Should().Be("RequestToken");
    }

    [Fact]
    public void Parse_ValidSubscribeFrame_Succeeds()
    {
        var raw = """{"frame":"subscribe","deviceUid":"PC-JOHN-001"}""";

        var result = AgentFrameParser.Parse(raw, MaxFrameSize);

        result.Success.Should().BeTrue();
        result.Frame!.Frame.Should().Be(AgentFrameType.Subscribe);
        result.Frame.DeviceUid.Should().Be("PC-JOHN-001");
    }

    [Fact]
    public void Parse_ValidPongFrame_Succeeds()
    {
        var raw = """{"frame":"pong","ts":"2026-08-19T10:15:30.123Z"}""";

        var result = AgentFrameParser.Parse(raw, MaxFrameSize);

        result.Success.Should().BeTrue();
        result.Frame!.Frame.Should().Be(AgentFrameType.Pong);
        result.Frame.Ts.Should().Be("2026-08-19T10:15:30.123Z");
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsInvalidFrame()
    {
        var result = AgentFrameParser.Parse("{not json", MaxFrameSize);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AgentErrorCode.InvalidFrame);
    }

    [Fact]
    public void Parse_MissingFrameField_ReturnsInvalidFrame()
    {
        var result = AgentFrameParser.Parse("""{"id":"x","payload":{}}""", MaxFrameSize);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AgentErrorCode.InvalidFrame);
    }

    [Fact]
    public void Parse_UnknownFrameType_ReturnsUnknownFrameType()
    {
        var result = AgentFrameParser.Parse("""{"frame":"teleport"}""", MaxFrameSize);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AgentErrorCode.UnknownFrameType);
    }

    [Fact]
    public void Parse_OversizedFrame_ReturnsFrameTooLarge()
    {
        var raw = "{\"frame\":\"pong\",\"ts\":\"" + new string('x', 300) + "\"}";

        var result = AgentFrameParser.Parse(raw, maxFrameSizeBytes: 32);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AgentErrorCode.FrameTooLarge);
    }

    [Fact]
    public void Parse_RequestMissingId_ReturnsInvalidFrame()
    {
        var result = AgentFrameParser.Parse("""{"frame":"request","payload":{}}""", MaxFrameSize);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AgentErrorCode.InvalidFrame);
    }

    [Fact]
    public void Parse_RequestMissingPayload_ReturnsInvalidFrame()
    {
        var result = AgentFrameParser.Parse("""{"frame":"request","id":"aaa"}""", MaxFrameSize);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AgentErrorCode.InvalidFrame);
    }

    [Fact]
    public void Parse_SubscribeMissingDeviceUid_ReturnsInvalidFrame()
    {
        var result = AgentFrameParser.Parse("""{"frame":"subscribe"}""", MaxFrameSize);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AgentErrorCode.InvalidFrame);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Error frame generation (WS-AGENT-PROTOCOL.md section 9)
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(AgentErrorCode.NotAuthenticated)]
    [InlineData(AgentErrorCode.DeviceMismatch)]
    [InlineData(AgentErrorCode.Timeout)]
    [InlineData(AgentErrorCode.BackendUnavailable)]
    [InlineData(AgentErrorCode.InvalidFrame)]
    [InlineData(AgentErrorCode.FrameTooLarge)]
    [InlineData(AgentErrorCode.RateLimited)]
    [InlineData(AgentErrorCode.UnknownFrameType)]
    [InlineData(AgentErrorCode.SubscribeFailed)]
    public void BuildErrorFrame_EveryErrorCode_ProducesWellFormedFrame(string code)
    {
        var json = AgentFrameBuilder.BuildErrorFrame("req-1", code, "boom");

        var obj = JObject.Parse(json);
        obj["frame"]!.ToString().Should().Be(AgentFrameType.Error);
        obj["id"]!.ToString().Should().Be("req-1");
        obj["code"]!.ToString().Should().Be(code);
        obj["message"]!.ToString().Should().Be("boom");
    }

    [Fact]
    public void BuildErrorFrame_NullId_SerializesAsJsonNull()
    {
        var json = AgentFrameBuilder.BuildErrorFrame(null, AgentErrorCode.NotAuthenticated, "not authenticated");

        var obj = JObject.Parse(json);
        obj["id"]!.Type.Should().Be(JTokenType.Null);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Auth state machine (WS-AGENT-PROTOCOL.md section 4.2)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NewConnection_StartsUnauthenticated()
    {
        var connection = NewConnection(out _, out _);

        connection.State.Should().Be(AgentConnectionState.Unauthenticated);
        connection.DeviceUid.Should().BeNull();
    }

    [Theory]
    [InlineData("RequestToken")]
    [InlineData("RefreshToken")]
    [InlineData("RegisterDevice")]
    public void ValidateRequest_Unauthenticated_AllowsAuthMessageTypes(string messageType)
    {
        var connection = NewConnection(out _, out _);
        var payload = new JObject { ["MessageType"] = messageType, ["DeviceUid"] = "PC-1" };

        connection.ValidateRequest(payload).Should().Be(AgentRequestValidation.Allowed);
    }

    [Fact]
    public void ValidateRequest_Unauthenticated_RejectsAlertPayload()
    {
        var connection = NewConnection(out _, out _);
        var payload = new JObject { ["AlertType"] = "UrlAlert" };

        connection.ValidateRequest(payload).Should().Be(AgentRequestValidation.NotAuthenticated);
    }

    [Fact]
    public async Task HandleFrameAsync_UnauthenticatedAlertRequest_SendsNotAuthenticatedError_WithNullId()
    {
        var connection = NewConnection(out var sent, out _);
        var frame = RequestFrame("req-1", new JObject { ["AlertType"] = "UrlAlert" });

        await connection.HandleFrameAsync(frame, CancellationToken.None);

        var error = JObject.Parse(sent.Single());
        error["frame"]!.ToString().Should().Be(AgentFrameType.Error);
        error["code"]!.ToString().Should().Be(AgentErrorCode.NotAuthenticated);
        error["id"]!.Type.Should().Be(JTokenType.Null);
    }

    [Fact]
    public async Task HandleFrameAsync_SuccessfulTokenRequest_TransitionsToAuthenticated()
    {
        var backendResponse = """{"status":"TokenCreated","token":"tok-abc","expiration":"2026-08-20T10:15:30.000Z","deviceUid":"PC-JOHN-001"}""";
        var connection = NewConnection(out var sent, out var gatewayMock,
            forwardResult: AgentBackendResult.Ok(backendResponse));

        var payload = new JObject { ["MessageType"] = "RequestToken", ["DeviceUid"] = "PC-JOHN-001", ["Email"] = "john@example.com" };
        var frame = RequestFrame("aaa-111", payload);

        await connection.HandleFrameAsync(frame, CancellationToken.None);

        connection.State.Should().Be(AgentConnectionState.Authenticated);
        connection.DeviceUid.Should().Be("PC-JOHN-001");
        connection.Token.Should().Be("tok-abc");

        var response = JObject.Parse(sent.Single());
        response["frame"]!.ToString().Should().Be(AgentFrameType.Response);
        response["id"]!.ToString().Should().Be("aaa-111");
        response["payload"]!["status"]!.ToString().Should().Be("TokenCreated");
    }

    [Theory]
    [InlineData("TokenExpired")]
    [InlineData("InvalidToken")]
    public void ApplyBackendResponse_ExpiredOrInvalidToken_RevertsToUnauthenticated(string status)
    {
        var connection = NewConnection(out _, out _);
        Authenticate(connection, "PC-1", "tok-1");

        connection.ApplyBackendResponse($$"""{"status":"{{status}}"}""", null);

        connection.State.Should().Be(AgentConnectionState.Unauthenticated);
        connection.DeviceUid.Should().BeNull();
        connection.Token.Should().BeNull();
    }

    [Fact]
    public void ValidateRequest_Authenticated_MatchingToken_Allowed()
    {
        var connection = NewConnection(out _, out _);
        Authenticate(connection, "PC-1", "tok-1");

        var payload = new JObject { ["AlertType"] = "UrlAlert", ["Token"] = "tok-1" };

        connection.ValidateRequest(payload).Should().Be(AgentRequestValidation.Allowed);
    }

    [Fact]
    public void ValidateRequest_Authenticated_MismatchedDeviceUid_ReturnsDeviceMismatch()
    {
        var connection = NewConnection(out _, out _);
        Authenticate(connection, "PC-1", "tok-1");

        var payload = new JObject
        {
            ["AlertType"] = "UrlAlert",
            ["Token"] = "some-other-token",
            ["DeviceInfo"] = new JObject { ["DeviceUid"] = "PC-2" }
        };

        connection.ValidateRequest(payload).Should().Be(AgentRequestValidation.DeviceMismatch);
    }

    [Fact]
    public async Task HandleFrameAsync_Subscribe_Unauthenticated_SendsNotAuthenticatedError()
    {
        var connection = NewConnection(out var sent, out _);
        var frame = SubscribeFrame("PC-1");

        await connection.HandleFrameAsync(frame, CancellationToken.None);

        var error = JObject.Parse(sent.Single());
        error["code"]!.ToString().Should().Be(AgentErrorCode.NotAuthenticated);
    }

    [Fact]
    public async Task HandleFrameAsync_Subscribe_DeviceMismatch_SendsDeviceMismatchError()
    {
        var connection = NewConnection(out var sent, out var gatewayMock);
        Authenticate(connection, "PC-1", "tok-1");

        var frame = SubscribeFrame("PC-OTHER");
        await connection.HandleFrameAsync(frame, CancellationToken.None);

        var error = JObject.Parse(sent.Single());
        error["code"]!.ToString().Should().Be(AgentErrorCode.DeviceMismatch);
        gatewayMock.Verify(g => g.Subscribe(It.IsAny<string>(), It.IsAny<Func<string, string, Task>>()), Times.Never);
    }

    [Fact]
    public async Task HandleFrameAsync_Subscribe_MatchingDeviceUid_CreatesSubscription()
    {
        var subscriptionMock = new Mock<IAgentNotificationSubscription>();
        subscriptionMock.SetupGet(s => s.DeviceUid).Returns("PC-1");

        var gatewayMock = new Mock<IAgentBackendGateway>();
        gatewayMock
            .Setup(g => g.Subscribe("PC-1", It.IsAny<Func<string, string, Task>>()))
            .Returns(subscriptionMock.Object);

        var sent = new ConcurrentBag<string>();
        var connection = new AgentConnection(gatewayMock.Object, (json, _) => { sent.Add(json); return Task.CompletedTask; },
            NullLogger.Instance, "conn-1");
        Authenticate(connection, "PC-1", "tok-1");

        await connection.HandleFrameAsync(SubscribeFrame("PC-1"), CancellationToken.None);

        gatewayMock.Verify(g => g.Subscribe("PC-1", It.IsAny<Func<string, string, Task>>()), Times.Once);
        sent.Should().BeEmpty("a successful subscribe produces no error/response frame");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Request/response correlation by id (WS-AGENT-PROTOCOL.md section 5)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleFrameAsync_ConcurrentRequests_EachResponseEchoesItsOwnId()
    {
        var gatewayMock = new Mock<IAgentBackendGateway>();
        gatewayMock
            .Setup(g => g.ForwardRequestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string payloadJson, CancellationToken _) =>
            {
                var payload = JObject.Parse(payloadJson);
                var echoId = payload["EchoId"]!.ToString();
                return Task.FromResult(AgentBackendResult.Ok($$"""{"success":true,"echoId":"{{echoId}}"}"""));
            });

        var sent = new ConcurrentBag<string>();
        var connection = new AgentConnection(gatewayMock.Object, (json, _) => { sent.Add(json); return Task.CompletedTask; },
            NullLogger.Instance, "conn-1");
        Authenticate(connection, "PC-1", "tok-1");

        var ids = Enumerable.Range(0, 20).Select(i => $"req-{i}").ToArray();
        var tasks = ids.Select(id =>
        {
            var payload = new JObject { ["Token"] = "tok-1", ["EchoId"] = id };
            return connection.HandleFrameAsync(RequestFrame(id, payload), CancellationToken.None);
        });

        await Task.WhenAll(tasks);

        sent.Should().HaveCount(ids.Length);
        foreach (var raw in sent)
        {
            var frame = JObject.Parse(raw);
            var id = frame["id"]!.ToString();
            var echoId = frame["payload"]!["echoId"]!.ToString();
            id.Should().Be(echoId, "the response id must correlate to the request that produced it");
        }
    }

    [Fact]
    public async Task HandleFrameAsync_BackendTimeout_ReturnsTimeoutError_EchoingRequestId()
    {
        var connection = NewConnection(out var sent, out _, forwardResult: AgentBackendResult.Timeout());
        Authenticate(connection, "PC-1", "tok-1");

        var payload = new JObject { ["Token"] = "tok-1", ["AlertType"] = "UrlAlert" };
        await connection.HandleFrameAsync(RequestFrame("req-timeout", payload), CancellationToken.None);

        var error = JObject.Parse(sent.Single());
        error["code"]!.ToString().Should().Be(AgentErrorCode.Timeout);
        error["id"]!.ToString().Should().Be("req-timeout", "gateway.timeout is request-level — id must echo the request");
    }

    [Fact]
    public async Task HandleFrameAsync_BackendUnavailable_ReturnsErrorWithNullId()
    {
        var connection = NewConnection(out var sent, out _, forwardResult: AgentBackendResult.BackendUnavailable());
        Authenticate(connection, "PC-1", "tok-1");

        var payload = new JObject { ["Token"] = "tok-1", ["AlertType"] = "UrlAlert" };
        await connection.HandleFrameAsync(RequestFrame("req-1", payload), CancellationToken.None);

        var error = JObject.Parse(sent.Single());
        error["code"]!.ToString().Should().Be(AgentErrorCode.BackendUnavailable);
        error["id"]!.Type.Should().Be(JTokenType.Null, "gateway.backend_unavailable is connection-level per section 9");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static AgentFrame RequestFrame(string id, JObject payload) =>
        new() { Frame = AgentFrameType.Request, Id = id, Payload = payload };

    private static AgentFrame SubscribeFrame(string deviceUid) =>
        new() { Frame = AgentFrameType.Subscribe, DeviceUid = deviceUid };

    private static void Authenticate(AgentConnection connection, string deviceUid, string token) =>
        connection.ApplyBackendResponse(
            $$"""{"status":"TokenCreated","token":"{{token}}","deviceUid":"{{deviceUid}}"}""", null);

    private static AgentConnection NewConnection(
        out ConcurrentBag<string> sent, out Mock<IAgentBackendGateway> gatewayMock, AgentBackendResult? forwardResult = null)
    {
        gatewayMock = new Mock<IAgentBackendGateway>();
        gatewayMock
            .Setup(g => g.ForwardRequestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(forwardResult ?? AgentBackendResult.BackendUnavailable("no backend configured for this test"));

        var capturedSent = new ConcurrentBag<string>();
        var connection = new AgentConnection(
            gatewayMock.Object,
            (json, _) => { capturedSent.Add(json); return Task.CompletedTask; },
            NullLogger.Instance,
            "conn-1");

        sent = capturedSent;
        return connection;
    }
}
