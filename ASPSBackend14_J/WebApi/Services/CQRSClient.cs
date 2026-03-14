using Business.Services;
using Common.Messaging;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;

namespace WebApi.Services;

/// <summary>
/// CQRS Client - Sends Commands/Queries to Business layer via NetMQ
/// This runs in WebApi and has ZERO database access
/// </summary>
public class CQRSClient : IDisposable
{
    private readonly string _endpoint;
    private readonly ILogger<CQRSClient> _logger;
    private readonly CurveKeyManager? _curveKeyManager;
    private readonly object _lock = new object();
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    public CQRSClient(string endpoint, ILogger<CQRSClient> logger, CurveKeyManager? curveKeyManager = null)
    {
        _endpoint = endpoint;
        _logger = logger;
        _curveKeyManager = curveKeyManager;
    }

    private RequestSocket CreateSocket()
    {
        var socket = new RequestSocket();
        // No CURVE on internal localhost channel — encryption is on external ports 50001/50002
        socket.Connect(_endpoint);
        return socket;
    }

    public async Task<TResult> SendQueryAsync<TResult>(Query query) where TResult : QueryResult
    {
        return await Task.Run(() =>
        {
            // Create a new socket for each request to avoid state issues
            using var socket = CreateSocket();
            
            try
            {
                // Serialize query
                var queryJson = JsonConvert.SerializeObject(query, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None
                });

                _logger.LogInformation("Sending query: {Query} to {Endpoint}", 
                    query.GetType().Name, _endpoint);

                lock (_lock)
                {
                    // Send query
                    var sent = socket.TrySendFrame(_timeout, queryJson);
                    
                    if (!sent)
                    {
                        _logger.LogError("Failed to send query {Query} - Send timeout after {Timeout}s", 
                            query.GetType().Name, _timeout.TotalSeconds);
                        throw new TimeoutException($"Failed to send query to {_endpoint} after {_timeout.TotalSeconds}s");
                    }

                    // Receive response
                    string? responseJson = null;
                    var received = socket.TryReceiveFrameString(_timeout, out responseJson);
                    
                    if (!received || responseJson == null)
                    {
                        _logger.LogError("Failed to receive response for {Query} - Receive timeout after {Timeout}s. Is ASPSBackend running?", 
                            query.GetType().Name, _timeout.TotalSeconds);
                        throw new TimeoutException($"No response from {_endpoint} after {_timeout.TotalSeconds}s. Ensure ASPSBackend is running and CQRS Gateway is started.");
                    }

                    _logger.LogInformation("Received response for {Query} ({Length} chars)", 
                        query.GetType().Name, responseJson.Length);

                    // Deserialize response with type information
                    var result = JsonConvert.DeserializeObject<TResult>(responseJson, new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.Auto
                    });

                    if (result == null)
                    {
                        throw new InvalidOperationException("Failed to deserialize query result");
                    }

                    if (!result.Success)
                    {
                        _logger.LogWarning("Query {Query} returned error: {Message}", 
                            query.GetType().Name, result.Message);
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending query {Query} to {Endpoint}", 
                    query.GetType().Name, _endpoint);
                
                // Return error result with helpful message
                var errorResult = Activator.CreateInstance<TResult>();
                errorResult.Success = false;
                
                if (ex is TimeoutException)
                {
                    errorResult.Message = $"Communication timeout after {_timeout.TotalSeconds}s. Is ASPSBackend running? Check if CQRS Gateway is listening on {_endpoint}";
                }
                else
                {
                    errorResult.Message = $"Communication error: {ex.Message}";
                }
                
                return errorResult;
            }
        });
    }

    public async Task<TResult> SendCommandAsync<TResult>(Command command) where TResult : CommandResult
    {
        return await Task.Run(() =>
        {
            // Create a new socket for each request to avoid state issues
            using var socket = CreateSocket();
            
            try
            {
                // Serialize command
                var commandJson = JsonConvert.SerializeObject(command, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None
                });

                _logger.LogInformation("Sending command: {Command} to {Endpoint}", 
                    command.GetType().Name, _endpoint);

                lock (_lock)
                {
                    // Send command
                    var sent = socket.TrySendFrame(_timeout, commandJson);
                    
                    if (!sent)
                    {
                        _logger.LogError("Failed to send command {Command} - Send timeout after {Timeout}s", 
                            command.GetType().Name, _timeout.TotalSeconds);
                        throw new TimeoutException($"Failed to send command to {_endpoint} after {_timeout.TotalSeconds}s");
                    }

                    // Receive response
                    string? responseJson = null;
                    var received = socket.TryReceiveFrameString(_timeout, out responseJson);
                    
                    if (!received || responseJson == null)
                    {
                        _logger.LogError("Failed to receive response for {Command} - Receive timeout after {Timeout}s. Is ASPSBackend running?", 
                            command.GetType().Name, _timeout.TotalSeconds);
                        throw new TimeoutException($"No response from {_endpoint} after {_timeout.TotalSeconds}s. Ensure ASPSBackend is running and CQRS Gateway is started.");
                    }

                    _logger.LogInformation("Received response for {Command} ({Length} chars)", 
                        command.GetType().Name, responseJson.Length);

                    // Deserialize response with type information
                    var result = JsonConvert.DeserializeObject<TResult>(responseJson, new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.Auto
                    });

                    if (result == null)
                    {
                        throw new InvalidOperationException("Failed to deserialize command result");
                    }

                    if (!result.Success)
                    {
                        _logger.LogWarning("Command {Command} returned error: {Message}", 
                            command.GetType().Name, result.Message);
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending command {Command} to {Endpoint}", 
                    command.GetType().Name, _endpoint);
                
                // Return error result with helpful message
                var errorResult = Activator.CreateInstance<TResult>();
                errorResult.Success = false;
                
                if (ex is TimeoutException)
                {
                    errorResult.Message = $"Communication timeout after {_timeout.TotalSeconds}s. Is ASPSBackend running? Check if CQRS Gateway is listening on {_endpoint}";
                }
                else
                {
                    errorResult.Message = $"Communication error: {ex.Message}";
                }
                
                return errorResult;
            }
        });
    }

    public void Dispose()
    {
        // No persistent socket to dispose
        _logger.LogInformation("CQRS Client disposed");
    }
}
