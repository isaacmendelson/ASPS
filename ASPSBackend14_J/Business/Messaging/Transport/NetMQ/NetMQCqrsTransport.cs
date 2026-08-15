using Business.Messaging.Abstractions;
using Business.Services;
using Business.Messaging;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Business.Messaging.Transport.NetMQ;

/// <summary>
/// NetMQ-backed CQRS transport — socket lifecycle, CURVE setup, HMAC envelope handling,
/// and command/query routing, extracted from <see cref="CQRSGateway"/> behind
/// <see cref="ICqrsTransport"/> (ASPS-686, Messaging Refactoring Phase 4).
/// Handler dispatch is delegated to <see cref="CqrsHandlerRegistry"/>.
/// Runs in the ASPSBackend process, NOT in WebApi, as an <see cref="Microsoft.Extensions.Hosting.IHostedService"/>.
/// </summary>
public sealed class NetMQCqrsTransport : ICqrsTransport
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NetMQCqrsTransport> _logger;
    private readonly CqrsHandlerRegistry _registry;
    private readonly CurveKeyManager? _curveKeyManager;
    private readonly CqrsChannelSecurity? _channelSecurity;
    private readonly string _endpoint;
    private ResponseSocket? _socket;
    private volatile bool _running;
    private Task? _listenTask;

    public NetMQCqrsTransport(
        IServiceProvider serviceProvider,
        ILogger<NetMQCqrsTransport> logger,
        CqrsHandlerRegistry registry,
        string endpoint = "tcp://127.0.0.1:5556",
        CurveKeyManager? curveKeyManager = null,
        CqrsChannelSecurity? channelSecurity = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _endpoint = endpoint;
        _curveKeyManager = curveKeyManager;
        _channelSecurity = channelSecurity;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _running = true;
        _socket = new ResponseSocket();
        _socket.Options.Linger = TimeSpan.Zero;
        if (_curveKeyManager?.HasServerKeyPair != true)
            throw new InvalidOperationException(
                "CQRS gateway requires valid CURVE server public and private key material.");
        if (_channelSecurity is null)
            throw new InvalidOperationException("CQRS gateway requires authenticated channel security.");
        _curveKeyManager.ApplyServerCurve(_socket);
        _socket.Bind(_endpoint);

        _logger.LogInformation("CQRS Gateway started on {Endpoint} with CURVE and authenticated envelopes", _endpoint);
        _logger.LogInformation("Listening for Commands and Queries from WebApi...");

        _listenTask = Task.Run(() => ListenLoop(cancellationToken), CancellationToken.None);
        return Task.CompletedTask;
    }

    private async Task ListenLoop(CancellationToken cancellationToken)
    {
        while (_running && _socket != null && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Poll with a timeout so the loop can observe _running/cancellation
                // without blocking StopAsync indefinitely.
                if (!_socket.TryReceiveFrameString(TimeSpan.FromMilliseconds(500), out var messageJson) || messageJson is null)
                    continue;

                _logger.LogInformation("Received CQRS message ({Length} chars)", messageJson.Length);

                // Process message
                var response = await ProcessMessageAsync(messageJson);

                // Send response back to WebApi
                _socket.SendFrame(response);
                _logger.LogInformation("Sent CQRS response ({Length} chars)", response.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CQRS Gateway loop");

                if (_socket != null)
                {
                    var errorResponse = JsonConvert.SerializeObject(new
                    {
                        Success = false,
                        Message = $"Gateway error: {ex.Message}"
                    });
                    _socket.SendFrame(errorResponse);
                }
            }
        }
    }

    private async Task<string> ProcessMessageAsync(string messageJson)
    {
        try
        {
            if (_channelSecurity is null)
            {
                _logger.LogWarning("Rejected unauthenticated CQRS request");
                return CreateErrorResponse("CQRS channel security is unavailable.");
            }
            if (!_channelSecurity.TryUnprotect(messageJson, out var authenticatedPayload, out var clientId, out var authenticationError))
            {
                _logger.LogWarning("Rejected unauthenticated CQRS request");
                return CreateErrorResponse(authenticationError);
            }

            messageJson = authenticatedPayload;
            // Parse JSON to determine message type
            var jObject = JObject.Parse(messageJson);

            var messageType = jObject["MessageType"]?.ToString();

            if (string.IsNullOrEmpty(messageType))
            {
                return CreateErrorResponse("MessageType field is missing or empty");
            }

            _logger.LogInformation("Processing {MessageType} message", messageType);

            // Route to appropriate handler based on message type
            return messageType switch
            {
                "Query" => await ProcessQueryAsync(messageJson, jObject),
                "Command" => await ProcessCommandAsync(messageJson, jObject, clientId),
                _ => CreateErrorResponse($"Unknown message type: {messageType}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            return CreateErrorResponse($"Processing error: {ex.Message}");
        }
    }

    /// <summary>
    /// Query dispatch — delegated to CqrsHandlerRegistry (mirrors CQRSGateway.Queries.cs).
    /// </summary>
    private async Task<string> ProcessQueryAsync(string messageJson, JObject jObject)
    {
        using var scope = _serviceProvider.CreateScope();

        var queryType = jObject["QueryType"]?.ToString();

        if (string.IsNullOrEmpty(queryType))
        {
            return CreateErrorResponse("QueryType field is missing or empty");
        }

        _logger.LogInformation("Handling query: {QueryType}", queryType);

        return await _registry.DispatchAsync(queryType, messageJson, scope);
    }

    /// <summary>
    /// Command dispatch — authorization stays here (gateway-level concern); actual
    /// handler dispatch is delegated to CqrsHandlerRegistry (mirrors CQRSGateway.Commands.cs).
    /// </summary>
    private async Task<string> ProcessCommandAsync(string messageJson, JObject jObject, string clientId)
    {
        using var scope = _serviceProvider.CreateScope();

        var commandType = jObject["CommandType"]?.ToString();

        if (string.IsNullOrEmpty(commandType))
        {
            return CreateErrorResponse("CommandType field is missing or empty");
        }
        if (_channelSecurity?.IsCommandAuthorized(commandType) != true)
        {
            _logger.LogWarning("Client {ClientId} is not authorized for command {CommandType}", clientId, commandType);
            return CreateErrorResponse($"Command is not authorized: {commandType}");
        }

        _logger.LogInformation("Handling command: {CommandType}", commandType);

        return await _registry.DispatchAsync(commandType, messageJson, scope);
    }

    internal string CreateErrorResponse(string message)
    {
        return JsonConvert.SerializeObject(new
        {
            Success = false,
            Message = message
        });
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _running = false;
        _socket?.Dispose();
        _socket = null;
        _logger.LogInformation("CQRS Gateway stopped");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _running = false;
        _socket?.Dispose();
        _socket = null;
    }
}
