using Business.Services;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;

namespace WebApi.Services;

public class NetMQClientService : INetMQClientService, IDisposable
{
    private readonly string _endpoint;
    private readonly ILogger<NetMQClientService> _logger;
    private readonly CurveKeyManager? _curveKeyManager;
    private RequestSocket? _requestSocket;
    private readonly object _lock = new();

    // JSON settings with type handling - SECURITY: Changed from .All to .None (ASPS-66)
    private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.None
    };

    public NetMQClientService(string endpoint, ILogger<NetMQClientService> logger, CurveKeyManager? curveKeyManager = null)
    {
        _endpoint = endpoint;
        _logger = logger;
        _curveKeyManager = curveKeyManager;
        InitializeSocket();
    }

    private void InitializeSocket()
    {
        _requestSocket = new RequestSocket();
        // No CURVE on internal localhost channel — encryption is on external ports 50001/50002
        _requestSocket.Connect(_endpoint);
        _logger.LogInformation($"NetMQ client connected to {_endpoint}");
    }

    public async Task<TResult> SendCommandAsync<TCommand, TResult>(TCommand command)
        where TCommand : Common.Messaging.Command
        where TResult : Common.Messaging.CommandResult
    {
        return await SendMessageAsync<TCommand, TResult>(command);
    }

    public async Task<TResult> SendQueryAsync<TQuery, TResult>(TQuery query)
        where TQuery : Common.Messaging.Query
        where TResult : Common.Messaging.QueryResult
    {
        return await SendMessageAsync<TQuery, TResult>(query);
    }

    private async Task<TResult> SendMessageAsync<TMessage, TResult>(TMessage message)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                try
                {
                    // Serialize with type information
                    var json = JsonConvert.SerializeObject(message, _jsonSettings);
                    _logger.LogDebug($"Sending: {json}");

                    _requestSocket!.SendFrame(json);

                    var response = _requestSocket.ReceiveFrameString();
                    _logger.LogDebug($"Received: {response}");

                    var result = JsonConvert.DeserializeObject<TResult>(response);
                    return result ?? throw new Exception("Failed to deserialize response");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending message via NetMQ");
                    throw;
                }
            }
        });
    }

    public void Dispose()
    {
        _requestSocket?.Dispose();
    }
}
