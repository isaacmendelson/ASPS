using Business.Messaging;
using Business.Messaging.Abstractions;
using Business.Messaging.Transport.NetMQ;
using Business.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json.Linq;

namespace ASPS.Tests.Business.Messaging;

/// <summary>
/// ASPS-686 (Messaging Refactoring Phase 4): NetMQCqrsTransport absorbs CQRSGateway's
/// socket lifecycle, CURVE setup, and HMAC envelope handling behind ICqrsTransport
/// (IHostedService + IDisposable). Mirrors CQRSGatewayTests, adapted to StartAsync/StopAsync.
/// </summary>
public sealed class NetMQCqrsTransportTests : IDisposable
{
    private const string Secret = "0123456789abcdef0123456789abcdef";
    private readonly ServiceProvider _services = new ServiceCollection().BuildServiceProvider();
    private readonly CqrsHandlerRegistry _registry = new();
    private readonly string _keysPath = Path.Combine(Path.GetTempPath(), $"asps-cqrs-transport-{Guid.NewGuid():N}", "keys.json");
    private NetMQCqrsTransport? _transport;

    [Fact]
    public void ImplementsExpectedInterfaces()
    {
        _transport = new NetMQCqrsTransport(_services, NullLogger<NetMQCqrsTransport>.Instance, _registry);

        Assert.IsAssignableFrom<ICqrsTransport>(_transport);
        Assert.IsAssignableFrom<IHostedService>(_transport);
        Assert.IsAssignableFrom<IDisposable>(_transport);
    }

    [Fact]
    public async Task StartAsync_RejectsMissingCurveEncryption()
    {
        var security = CreateSecurity();
        _transport = new NetMQCqrsTransport(_services, NullLogger<NetMQCqrsTransport>.Instance, _registry,
            "tcp://127.0.0.1:46556", null, security);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _transport.StartAsync(CancellationToken.None));
        Assert.Contains("CURVE", exception.Message);
    }

    [Fact]
    public async Task StartAsync_RejectsMissingApplicationAuthentication()
    {
        _transport = new NetMQCqrsTransport(_services, NullLogger<NetMQCqrsTransport>.Instance, _registry,
            "tcp://127.0.0.1:46557", CreateCurveManager(), null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _transport.StartAsync(CancellationToken.None));
        Assert.Contains("authenticated", exception.Message);
    }

    [Fact]
    public async Task StartAsync_WithCurveAndAuthentication_StartsAndStops()
    {
        _transport = new NetMQCqrsTransport(_services, NullLogger<NetMQCqrsTransport>.Instance, _registry,
            "tcp://127.0.0.1:46558", CreateCurveManager(), CreateSecurity());

        await _transport.StartAsync(CancellationToken.None);
        await _transport.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void DefaultEndpoint_IsRestrictedToLoopback()
    {
        var field = typeof(NetMQCqrsTransport).GetField("_endpoint",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        _transport = new NetMQCqrsTransport(_services, NullLogger<NetMQCqrsTransport>.Instance, _registry);

        Assert.Equal("tcp://127.0.0.1:5556", field!.GetValue(_transport));
    }

    [Fact]
    public async Task RunningTransport_RejectsPlaintextClient()
    {
        var endpoint = await StartTransportAsync(46560, curve => { });
        using var socket = new RequestSocket();
        socket.Connect(endpoint);

        Assert.True(socket.TrySendFrame(TimeSpan.FromSeconds(1),
            """{"MessageType":"Query","QueryType":"GetVersionQuery"}"""));
        Assert.False(socket.TryReceiveFrameString(TimeSpan.FromMilliseconds(500), out _));
    }

    [Fact]
    public async Task RunningTransport_RejectsUnsignedEnvelope()
    {
        CurveKeyManager? curve = null;
        var endpoint = await StartTransportAsync(46561, c => curve = c);
        using var socket = CreateCurveClient(endpoint, curve!);

        var response = SendAndReceive(socket,
            """{"MessageType":"Query","QueryType":"GetVersionQuery"}""");

        Assert.Contains("Invalid authenticated CQRS envelope", response);
    }

    [Fact]
    public async Task RunningTransport_RejectsUnauthorizedCommand()
    {
        CurveKeyManager? curve = null;
        var endpoint = await StartTransportAsync(46565, c => curve = c);
        using var socket = CreateCurveClient(endpoint, curve!);
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

    private async Task<string> StartTransportAsync(int port, Action<CurveKeyManager> captureCurve)
    {
        var curve = CreateCurveManager();
        captureCurve(curve);
        var endpoint = $"tcp://127.0.0.1:{port}";
        _transport = new NetMQCqrsTransport(_services, NullLogger<NetMQCqrsTransport>.Instance, _registry,
            endpoint, curve, CreateSecurity());
        await _transport.StartAsync(CancellationToken.None);
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
        _transport?.Dispose();
        _services.Dispose();
        var directory = Path.GetDirectoryName(_keysPath);
        if (directory is not null && Directory.Exists(directory))
            Directory.Delete(directory, true);
    }
}
