using Business.Commands;
using Business.Handlers;
using Business.Queries;
using Business.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;

namespace Business.Messaging;

/// <summary>
/// Internal CQRS channel (port 5555, no CURVE — loopback only). Runs as an
/// <see cref="IHostedService"/> (ASPS-688, Messaging Refactoring Phase 5); previously
/// started/stopped manually from <c>Program.cs</c>.
/// </summary>
public class NetMQMessageProcessor : IHostedService, IDisposable
{
    private readonly ILogger<NetMQMessageProcessor> _logger;
    private readonly UserCommandHandlers? _userCommandHandlers;
    private readonly UserQueryHandlers? _userQueryHandlers;
    private readonly UserDeviceCommandHandlers? _deviceCommandHandlers;
    private readonly IServiceScopeFactory? _serviceScopeFactory;
    private readonly CurveKeyManager? _curveKeyManager;
    private readonly string _endpoint;
    private ResponseSocket? _responseSocket;
    private bool _isRunning;

    // JSON settings with type handling - SECURITY: Changed from .All to .None (ASPS-66)
    private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.None
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

    [ActivatorUtilitiesConstructor]
    public NetMQMessageProcessor(
        ILogger<NetMQMessageProcessor> logger,
        IServiceScopeFactory serviceScopeFactory,
        string endpoint = "tcp://*:5555",
        CurveKeyManager? curveKeyManager = null)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _endpoint = endpoint;
        _curveKeyManager = curveKeyManager;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _isRunning = true;
        _responseSocket = new ResponseSocket();
        _responseSocket.Options.Linger = TimeSpan.Zero;
        // No CURVE on internal localhost channel — encryption is on external ports 50001/50002
        _responseSocket.Bind(_endpoint);

        _logger.LogInformation($"NetMQ CQRS Message Processor started on {_endpoint} (internal channel, no CURVE)");

        Task.Run(() => ProcessMessages(cancellationToken), CancellationToken.None);
        return Task.CompletedTask;
    }

    private async Task ProcessMessages(CancellationToken cancellationToken)
    {
        while (_isRunning && _responseSocket != null && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Poll with a timeout so the loop can observe _isRunning/cancellation
                // without blocking StopAsync indefinitely.
                if (!_responseSocket.TryReceiveFrameString(TimeSpan.FromMilliseconds(500), out var message) || message is null)
                    continue;

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
            using var scope = _serviceScopeFactory?.CreateScope();
            var userCommandHandlers = scope?.ServiceProvider.GetRequiredService<UserCommandHandlers>() ?? _userCommandHandlers!;
            var userQueryHandlers = scope?.ServiceProvider.GetRequiredService<UserQueryHandlers>() ?? _userQueryHandlers!;
            var deviceCommandHandlers = scope?.ServiceProvider.GetRequiredService<UserDeviceCommandHandlers>() ?? _deviceCommandHandlers!;

            // Deserialize with type information
            var baseMessage = JsonConvert.DeserializeObject(message, _jsonSettings);
            
            object result;

            // Route based on actual type
            switch (baseMessage)
            {
                // User Commands
                case CreateUserCommand cmd:
                    result = await userCommandHandlers.HandleAsync(cmd);
                    break;
                case UpdateUserCommand cmd:
                    result = await userCommandHandlers.HandleAsync(cmd);
                    break;
                case DeleteUserCommand cmd:
                    result = await userCommandHandlers.HandleAsync(cmd);
                    break;

                // User Queries
                case GetAllUsersQuery query:
                    result = await userQueryHandlers.HandleAsync(query);
                    break;
                case GetUserByKeyQuery query:
                    result = await userQueryHandlers.HandleAsync(query);
                    break;
                case GetUserDetailsQuery query:
                    result = await userQueryHandlers.HandleAsync(query);
                    break;

                // Device Commands
                case CreateUserDeviceCommand cmd:
                    result = await deviceCommandHandlers.HandleAsync(cmd);
                    break;
                case UpdateUserDeviceCommand cmd:
                    result = await deviceCommandHandlers.HandleAsync(cmd);
                    break;
                case DeleteUserDeviceCommand cmd:
                    result = await deviceCommandHandlers.HandleAsync(cmd);
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

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _isRunning = false;
        _responseSocket?.Dispose();
        _responseSocket = null;
        _logger.LogInformation("NetMQ Message Processor stopped");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _isRunning = false;
        _responseSocket?.Dispose();
        _responseSocket = null;
    }
}
