using Business.Messaging.Abstractions;
using Business.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Common.Generated.Messaging.V1;
using System;

namespace Business.Messaging;

public enum SocketMode
{
    Pull,   // One-way, fire-and-forget (PullSocket)
    Router  // Concurrent request-response (RouterSocket — scales to thousands of devices)
            // Compatible with REQ clients; no client-side changes needed.
}

/// <summary>
/// Transport layer for real-time device alerts — socket management, frame parsing, CURVE setup.
/// Delegates all business logic (token validation, alert routing, device registration) to
/// <see cref="AlertProcessor"/>. Extracted from <c>RealTimeAlertListener</c> (ASPS-684).
/// </summary>
public class NetMQAlertIngress : IAlertIngress
{
    private readonly ILogger<NetMQAlertIngress> _logger;
    private readonly AlertProcessor _alertProcessor;
    private readonly CurveKeyManager? _curveKeyManager;
    private RouterSocket? _routerSocket;  // replaces ResponseSocket — handles concurrent clients
    private PullSocket? _pullSocket;
    private readonly object _sendLock = new();  // RouterSocket is not thread-safe for sends
    private bool _isRunning;
    private readonly int _port;
    private readonly SocketMode _mode;

    public NetMQAlertIngress(
        ILoggerFactory loggerFactory,
        AlertProcessor alertProcessor,
        CurveKeyManager? curveKeyManager = null,
        int port = 50001,
        SocketMode mode = SocketMode.Router,
        IConfiguration? configuration = null)
    {
        _logger = loggerFactory.CreateLogger<NetMQAlertIngress>();
        _alertProcessor = alertProcessor;
        _curveKeyManager = curveKeyManager;
        _port = port;
        _mode = mode;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _isRunning = true;
        var encStatus = _curveKeyManager?.IsEnabled == true ? "CURVE encrypted" : "unencrypted";

        if (_mode == SocketMode.Router)
        {
            _routerSocket = new RouterSocket();
            _routerSocket.Options.Linger = TimeSpan.Zero;
            _curveKeyManager?.ApplyServerCurve(_routerSocket);
            _routerSocket.Bind($"tcp://*:{_port}");
            _logger.LogInformation(
                "Real-time alert listener started on tcp://*:{Port} (ROUTER mode — concurrent, {Enc})",
                _port, encStatus);
        }
        else
        {
            _pullSocket = new PullSocket();
            _pullSocket.Options.Linger = TimeSpan.Zero;
            _curveKeyManager?.ApplyServerCurve(_pullSocket);
            _pullSocket.Bind($"tcp://*:{_port}");
            _logger.LogInformation(
                "Real-time alert listener started on tcp://*:{Port} (PULL mode — fire-and-forget, {Enc})",
                _port, encStatus);
        }

        Task.Run(() => ListenForAlerts());
        return Task.CompletedTask;
    }

    private void ListenForAlerts()
    {
        while (_isRunning)
        {
            try
            {
                if (_mode == SocketMode.Router)
                {
                    var incoming = _routerSocket!.ReceiveMultipartMessage();

                    _logger.LogDebug("ROUTER received message with {Count} frames", incoming.FrameCount);
                    for (int i = 0; i < incoming.FrameCount; i++)
                    {
                        var frameContent = incoming[i].BufferSize > 100
                            ? $"[{incoming[i].BufferSize} bytes]"
                            : System.Text.Encoding.UTF8.GetString(incoming[i].Buffer);
                        _logger.LogDebug("  Frame {Index}: {Content}", i, frameContent);
                    }

                    if (incoming.FrameCount < 3)
                    {
                        _logger.LogWarning("Malformed ROUTER message: expected 3 frames, got {Count}", incoming.FrameCount);
                        continue;
                    }

                    var identity     = incoming[0].Buffer.ToArray();
                    var messageBytes = incoming[2].Buffer;

                    _ = Task.Run(async () => await ProcessRouterMessageAsync(identity, messageBytes));
                }
                else
                {
                    var messageBytes = _pullSocket!.ReceiveFrameBytes();
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var message = System.Text.Encoding.UTF8.GetString(messageBytes);
                            var jObject = JObject.Parse(message);
                            await _alertProcessor.ProcessAlertAsync(message, jObject);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing PULL message");
                        }
                    });
                }
            }
            catch (Exception ex) when (_isRunning)
            {
                _logger.LogError(ex, "Error in receive loop");
            }
        }
    }

    private async Task ProcessRouterMessageAsync(byte[] identity, byte[] messageBytes)
    {
        object result;
        try
        {
            string message;
            try
            {
                message = System.Text.Encoding.UTF8.GetString(messageBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decode message as UTF-8");
                result = new { success = false, message = "Failed to decode message" };
                SendRouterResponse(identity, result);
                return;
            }

            result = await _alertProcessor.RouteMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            result = new { success = false, message = "Error processing message" };
        }

        SendRouterResponse(identity, result);
    }

    private void SendRouterResponse(byte[] identity, object response)
    {
        try
        {
            var json = response is MessageEnvelopeV1
                ? System.Text.Json.JsonSerializer.Serialize(response)
                : JsonConvert.SerializeObject(response);
            var reply = new NetMQMessage();
            reply.Append(identity);
            reply.AppendEmptyFrame();
            reply.Append(json);

            lock (_sendLock)
            {
                _routerSocket!.SendMultipartMessage(reply);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending router response");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _isRunning = false;
        _logger.LogInformation("Real-time alert listener stopped");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _isRunning = false;
        _pullSocket?.Dispose();
        _routerSocket?.Dispose();
    }
}
