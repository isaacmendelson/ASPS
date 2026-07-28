using Business.Messaging;
using Business.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json.Linq;

namespace ASPS.Tests.Business.Messaging;

public sealed class CQRSGatewayTests : IDisposable
{
    private const string Secret = "0123456789abcdef0123456789abcdef";
    private readonly ServiceProvider _services = new ServiceCollection().BuildServiceProvider();
    private readonly string _keysPath = Path.Combine(Path.GetTempPath(), $"asps-cqrs-{Guid.NewGuid():N}", "keys.json");
    private CQRSGateway? _gateway;

    [Fact]
    public void Start_RejectsMissingCurveEncryption()
    {
        var security = CreateSecurity();
        _gateway = new CQRSGateway(_services, NullLogger<CQRSGateway>.Instance,
            "tcp://127.0.0.1:45556", null, security);

        var exception = Assert.Throws<InvalidOperationException>(() => _gateway.Start());
        Assert.Contains("CURVE", exception.Message);
    }

    [Fact]
    public void Start_RejectsMissingApplicationAuthentication()
    {
        _gateway = new CQRSGateway(_services, NullLogger<CQRSGateway>.Instance,
            "tcp://127.0.0.1:45557", CreateCurveManager(), null);

        var exception = Assert.Throws<InvalidOperationException>(() => _gateway.Start());
        Assert.Contains("authenticated", exception.Message);
    }

    [Fact]
    public void Start_WithCurveAndAuthentication_StartsAndStops()
    {
        _gateway = new CQRSGateway(_services, NullLogger<CQRSGateway>.Instance,
            "tcp://127.0.0.1:45558", CreateCurveManager(), CreateSecurity());

        _gateway.Start();
        _gateway.Stop();
    }

    [Fact]
    public void DefaultEndpoint_IsRestrictedToLoopback()
    {
        var field = typeof(CQRSGateway).GetField("_endpoint",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        _gateway = new CQRSGateway(_services, NullLogger<CQRSGateway>.Instance);

        Assert.Equal("tcp://127.0.0.1:5556", field!.GetValue(_gateway));
    }

    [Fact]
    public void Start_WithClientOnlyCurveMaterial_RejectsBeforeBind()
    {
        var serverKeys = CreateCurveManager();
        var publicKeyPath = Path.Combine(Path.GetDirectoryName(_keysPath)!,
            "curve-server-public-key.txt");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Security:CurveEnabled"] = "true",
                ["Security:CurveClientOnly"] = "true",
                ["Security:ServerPublicKeyFilePath"] = publicKeyPath
            }).Build();
        var clientOnly = new CurveKeyManager(configuration, NullLogger<CurveKeyManager>.Instance);
        _gateway = new CQRSGateway(_services, NullLogger<CQRSGateway>.Instance,
            "tcp://127.0.0.1:45559", clientOnly, CreateSecurity());

        var exception = Assert.Throws<InvalidOperationException>(() => _gateway.Start());

        Assert.Contains("public and private", exception.Message);
        Assert.False(clientOnly.HasServerKeyPair);
        Assert.True(serverKeys.HasServerKeyPair);
    }

    [Fact]
    public void RunningGateway_RejectsPlaintextClient()
    {
        var endpoint = StartGateway(45560, out _);
        using var socket = new RequestSocket();
        socket.Connect(endpoint);

        Assert.True(socket.TrySendFrame(TimeSpan.FromSeconds(1),
            """{"MessageType":"Query","QueryType":"GetVersionQuery"}"""));
        Assert.False(socket.TryReceiveFrameString(TimeSpan.FromMilliseconds(500), out _));
    }

    [Fact]
    public void RunningGateway_RejectsUnsignedEnvelope()
    {
        var endpoint = StartGateway(45561, out var curve);
        using var socket = CreateCurveClient(endpoint, curve);

        var response = SendAndReceive(socket,
            """{"MessageType":"Query","QueryType":"GetVersionQuery"}""");

        Assert.Contains("Invalid authenticated CQRS envelope", response);
    }

    [Fact]
    public void RunningGateway_RejectsTamperedPayload()
    {
        var endpoint = StartGateway(45562, out var curve);
        using var socket = CreateCurveClient(endpoint, curve);
        var envelope = JObject.Parse(CreateSecurity().Protect(
            """{"MessageType":"Query","QueryType":"GetVersionQuery"}"""));
        envelope["Payload"] = """{"MessageType":"Command","CommandType":"DeleteUserCommand"}""";

        var response = SendAndReceive(socket, envelope.ToString());

        Assert.Contains("Invalid authenticated CQRS envelope", response);
    }

    [Fact]
    public void RunningGateway_RejectsReplayedNonce()
    {
        var endpoint = StartGateway(45563, out var curve);
        using var socket = CreateCurveClient(endpoint, curve);
        var envelope = CreateSecurity().Protect(
            """{"MessageType":"Command","CommandType":"UpdateUserCommand"}""");

        _ = SendAndReceive(socket, envelope);
        var replayResponse = SendAndReceive(socket, envelope);

        Assert.Contains("already been used", replayResponse);
    }

    [Fact]
    public void RunningGateway_RejectsUnauthorizedClient()
    {
        var endpoint = StartGateway(45564, out var curve);
        using var socket = CreateCurveClient(endpoint, curve);
        var envelope = CreateSecurity("attacker").Protect(
            """{"MessageType":"Query","QueryType":"GetVersionQuery"}""");

        var response = SendAndReceive(socket, envelope);

        Assert.Contains("not authorized", response);
    }

    [Fact]
    public void RunningGateway_RejectsUnauthorizedCommand()
    {
        var endpoint = StartGateway(45565, out var curve);
        using var socket = CreateCurveClient(endpoint, curve);
        var envelope = CreateSecurity().Protect(
            """{"MessageType":"Command","CommandType":"DeleteUserCommand"}""");

        var response = SendAndReceive(socket, envelope);

        Assert.Contains("Command is not authorized", response);
    }

    private CqrsChannelSecurity CreateSecurity(string clientId = "asps-webapi") =>
        new(new CqrsChannelSecurityOptions
        {
            ClientId = clientId,
            SharedSecret = Secret,
            AllowedCommands = new HashSet<string>(StringComparer.Ordinal) { "UpdateUserCommand" }
        });

    private string StartGateway(int port, out CurveKeyManager curve)
    {
        curve = CreateCurveManager();
        var endpoint = $"tcp://127.0.0.1:{port}";
        _gateway = new CQRSGateway(_services, NullLogger<CQRSGateway>.Instance,
            endpoint, curve, CreateSecurity());
        _gateway.Start();
        Thread.Sleep(100);
        return endpoint;
    }

    private static RequestSocket CreateCurveClient(string endpoint, CurveKeyManager curve)
    {
        var socket = new RequestSocket();
        socket.Options.Linger = TimeSpan.Zero;
        curve.ApplyClientCurve(socket);
        socket.Connect(endpoint);
        return socket;
    }

    private static string SendAndReceive(RequestSocket socket, string message)
    {
        Assert.True(socket.TrySendFrame(TimeSpan.FromSeconds(2), message));
        Assert.True(socket.TryReceiveFrameString(TimeSpan.FromSeconds(2), out var response));
        return response!;
    }

    private CurveKeyManager CreateCurveManager()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:CurveEnabled"] = "true",
            ["Security:KeysFilePath"] = _keysPath
        }).Build();
        return new CurveKeyManager(configuration, NullLogger<CurveKeyManager>.Instance);
    }

    public void Dispose()
    {
        _gateway?.Dispose();
        _services.Dispose();
        var directory = Path.GetDirectoryName(_keysPath);
        if (directory is not null && Directory.Exists(directory))
            Directory.Delete(directory, true);
    }
}
