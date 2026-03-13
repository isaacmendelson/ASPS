using Business.Commands;
using Business.Handlers;
using Business.Queries;
using Business.Services;
using Microsoft.Extensions.Logging;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;

namespace Business.Messaging;

public class NetMQMessageProcessor : IDisposable
{
    private readonly ILogger<NetMQMessageProcessor> _logger;
    private readonly UserCommandHandlers _userCommandHandlers;
    private readonly UserQueryHandlers _userQueryHandlers;
    private readonly UserDeviceCommandHandlers _deviceCommandHandlers;
    private readonly CurveKeyManager? _curveKeyManager;
    private readonly string _endpoint;
    private ResponseSocket? _responseSocket;
    private bool _isRunning;

    // JSON settings with type handling
    private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.All
    };

    public NetMQMessageProcessor(
        ILogger<NetMQMessageProcessor> logger,
        UserCommandHandlers userCommandHandlers,
        UserQueryHandlers userQueryHandlers,
        UserDeviceCommandHandlers deviceCommandHandlers,
        string endpoint = "tcp://*:5555",
        CurveKeyManager? curveKeyManager = null)
    {
        _logger = logger;
        _userCommandHandlers = userCommandHandlers;
        _userQueryHandlers = userQueryHandlers;
        _deviceCommandHandlers = deviceCommandHandlers;
        _endpoint = endpoint;
        _curveKeyManager = curveKeyManager;
    }

    public void Start()
    {
        _isRunning = true;
        _responseSocket = new ResponseSocket();
        _responseSocket.Options.Linger = TimeSpan.Zero;
        // No CURVE on internal localhost channel — encryption is on external ports 50001/50002
        _responseSocket.Bind(_endpoint);

        _logger.LogInformation($"NetMQ CQRS Message Processor started on {_endpoint} (internal channel, no CURVE)");

        Task.Run(() => ProcessMessages());
    }

    private async Task ProcessMessages()
    {
        while (_isRunning && _responseSocket != null)
        {
            try
            {
                var message = _responseSocket.ReceiveFrameString();
                _logger.LogInformation($"Received message: {message}");

                var response = await ProcessMessageAsync(message);
                
                _responseSocket.SendFrame(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
                _responseSocket?.SendFrame(JsonConvert.SerializeObject(new { Success = false, Message = ex.Message }));
            }
        }
    }

    private async Task<string> ProcessMessageAsync(string message)
    {
        try
        {
            // Deserialize with type information
            var baseMessage = JsonConvert.DeserializeObject(message, _jsonSettings);
            
            object result;

            // Route based on actual type
            switch (baseMessage)
            {
                // User Commands
                case CreateUserCommand cmd:
                    result = await _userCommandHandlers.HandleAsync(cmd);
                    break;
                case UpdateUserCommand cmd:
                    result = await _userCommandHandlers.HandleAsync(cmd);
                    break;
                case DeleteUserCommand cmd:
                    result = await _userCommandHandlers.HandleAsync(cmd);
                    break;

                // User Queries
                case GetAllUsersQuery query:
                    result = await _userQueryHandlers.HandleAsync(query);
                    break;
                case GetUserByKeyQuery query:
                    result = await _userQueryHandlers.HandleAsync(query);
                    break;
                case GetUserDetailsQuery query:
                    result = await _userQueryHandlers.HandleAsync(query);
                    break;

                // Device Commands
                case CreateUserDeviceCommand cmd:
                    result = await _deviceCommandHandlers.HandleAsync(cmd);
                    break;
                case UpdateUserDeviceCommand cmd:
                    result = await _deviceCommandHandlers.HandleAsync(cmd);
                    break;
                case DeleteUserDeviceCommand cmd:
                    result = await _deviceCommandHandlers.HandleAsync(cmd);
                    break;

                default:
                    result = new { Success = false, Message = $"Unknown message type: {baseMessage?.GetType().Name}" };
                    break;
            }

            return JsonConvert.SerializeObject(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            return JsonConvert.SerializeObject(new { Success = false, Message = ex.Message });
        }
    }

    public void Stop()
    {
        _isRunning = false;
        _logger.LogInformation("NetMQ Message Processor stopped");
    }

    public void Dispose()
    {
        Stop();
        _responseSocket?.Dispose();
    }
}
