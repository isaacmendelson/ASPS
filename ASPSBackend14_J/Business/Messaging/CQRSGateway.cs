using Business.Services;
using Common.Messaging;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Business.Messaging;

/// <summary>
/// CQRS Gateway — core: socket lifecycle, message routing, error responses.
/// Handler dispatch is split into CQRSGateway.Commands.cs and CQRSGateway.Queries.cs.
/// This runs in the ASPSBackend process, NOT in WebApi.
/// </summary>
public partial class CQRSGateway : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CQRSGateway> _logger;
    private readonly CurveKeyManager? _curveKeyManager;
    private readonly CqrsChannelSecurity? _channelSecurity;
    private readonly string _endpoint;
    private ResponseSocket? _socket;
    private bool _running;

    public CQRSGateway(
        IServiceProvider serviceProvider,
        ILogger<CQRSGateway> logger,
        string endpoint = "tcp://127.0.0.1:5556",
        CurveKeyManager? curveKeyManager = null,
        CqrsChannelSecurity? channelSecurity = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _endpoint = endpoint;
        _curveKeyManager = curveKeyManager;
        _channelSecurity = channelSecurity;
    }

    public void Start()
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

        Task.Run(() => ListenLoop());
    }

    private async Task ListenLoop()
    {
        while (_running && _socket != null)
        {
            try
            {
                // Receive message from WebApi
                var messageJson = _socket.ReceiveFrameString();
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

    internal string CreateErrorResponse(string message)
    {
        return JsonConvert.SerializeObject(new
        {
            Success = false,
            Message = message
        });
    }

    public void Stop()
    {
        _running = false;
        _socket?.Dispose();
        _socket = null;
        _logger.LogInformation("CQRS Gateway stopped");
    }

    public void Dispose()
    {
        Stop();
    }
}
