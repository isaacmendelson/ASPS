using NetMQ;
using NetMQ.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Business.RealtimeAnalysis.UserDomain;
using Business.Services;

namespace Business.Messaging;

/// <summary>
/// Publishes analysis result notifications to subscribed clients via PUB socket
/// </summary>
public class NotificationPublisher : IDisposable
{
    private readonly ILogger<NotificationPublisher> _logger;
    private readonly PublisherSocket _publisherSocket;
    private readonly object _sendLock = new();
    private readonly string _endpoint;
    private bool _isRunning;

    public NotificationPublisher(IConfiguration configuration, ILogger<NotificationPublisher> logger, CurveKeyManager? curveKeyManager = null)
    {
        _logger = logger;

        // Get notification port from configuration
        var port = configuration.GetValue<int>("NetMQ:NotificationPublisherPort", 50002);
        _endpoint = $"tcp://*:{port}";

        // Create PUB socket with optional CURVE encryption
        _publisherSocket = new PublisherSocket();
        curveKeyManager?.ApplyServerCurve(_publisherSocket);
        _publisherSocket.Bind(_endpoint);

        _isRunning = true;
        var encStatus = curveKeyManager?.IsEnabled == true ? "CURVE encrypted" : "unencrypted";
        _logger.LogInformation($"NotificationPublisher started on {_endpoint} ({encStatus})");
    }

    /// <summary>
    /// Publish analysis result notification to subscribers
    /// Topic format: "device:{deviceUid}" or "user:{userKey}"
    /// </summary>
    public void PublishAnalysisResult(string? deviceUid, string? userKeyField, AnalysisResultNotification? analysisResultNotification)
    {
        if (!_isRunning || analysisResultNotification == null)
        {
            _logger.LogWarning("NotificationPublisher is not running or notification is null, skipping");
            return;
        }

        if (string.IsNullOrEmpty(deviceUid) && string.IsNullOrEmpty(userKeyField))
        {
            _logger.LogWarning("Both deviceUid and userKeyField are null/empty, skipping notification");
            return;
        }

        try
        {
            // Create notification message
            var notification = new
            {
                Type = "AnalysisResult",
                Timestamp = DateTime.UtcNow,
                DeviceUid = deviceUid ?? string.Empty,
                Data = analysisResultNotification
            };

            // Use Newtonsoft.Json with TypeNameHandling to properly serialize polymorphic types
            var jsonSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None
            };

            var json = JsonConvert.SerializeObject(notification, jsonSettings);

            lock (_sendLock)
            {
                if (!string.IsNullOrEmpty(deviceUid))
                {
                    var deviceTopic = $"device:{deviceUid}";
                    _publisherSocket.SendMoreFrame(deviceTopic).SendFrame(json);
                    _logger.LogDebug("Published notification to topic '{DeviceTopic}'", deviceTopic);
                }

                if (!string.IsNullOrEmpty(userKeyField))
                {
                    var userTopic = $"user:{userKeyField}";
                    _publisherSocket.SendMoreFrame(userTopic).SendFrame(json);
                    _logger.LogDebug("Published notification to topic '{UserTopic}'", userTopic);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing notification");
        }
    }

    public void Stop()
    {
        _isRunning = false;
        _logger.LogInformation("NotificationPublisher stopped");
    }

    public void Dispose()
    {
        Stop();
        _publisherSocket?.Dispose();
    }
}
