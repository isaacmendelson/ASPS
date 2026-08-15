using Business.DomainEvents;
using Business.Messaging.Abstractions;
using Business.RealtimeAnalysis.UserDomain;
using Business.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Common.Generated.Messaging.V1;

namespace Business.Messaging;

/// <summary>
/// Publishes analysis result notifications to subscribed clients via PUB socket.
/// NetMQ-backed implementation of <see cref="INotificationEgress"/> — the transport
/// boundary for outbound notifications (ASPS-682).
/// </summary>
public class NetMQNotificationEgress : INotificationEgress, IDisposable
{
    private readonly ILogger<NetMQNotificationEgress> _logger;
    private readonly PublisherSocket _publisherSocket;
    private readonly object _sendLock = new();
    private readonly string _endpoint;
    private bool _isRunning;

    public NetMQNotificationEgress(IConfiguration configuration, ILogger<NetMQNotificationEgress> logger, string? endpoint = null, CurveKeyManager? curveKeyManager = null)
    {
        _logger = logger;

        // Use provided endpoint or get from configuration
        if (!string.IsNullOrEmpty(endpoint))
        {
            _endpoint = endpoint;
        }
        else
        {
            var port = configuration.GetValue<int>("NetMQ:NotificationPublisherPort", 50002);
            _endpoint = $"tcp://*:{port}";
        }

        // Create PUB socket with optional CURVE encryption
        _publisherSocket = new PublisherSocket();
        curveKeyManager?.ApplyServerCurve(_publisherSocket);
        _publisherSocket.Bind(_endpoint);

        _isRunning = true;
        var encStatus = curveKeyManager?.IsEnabled == true ? "CURVE encrypted" : "unencrypted";
        _logger.LogInformation($"NotificationPublisher started on {_endpoint} ({encStatus})");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INotificationEgress — async wrappers. The NetMQ send is synchronous, so
    // these simply invoke the existing sync methods and return a completed Task.
    // ─────────────────────────────────────────────────────────────────────────

    public Task PublishAnalysisResultAsync(
        string? deviceUid,
        string? userKeyField,
        AnalysisResultNotification? analysisResultNotification,
        MessageIdentityV1? messagingIdentity = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        PublishAnalysisResult(deviceUid, userKeyField, analysisResultNotification, messagingIdentity);
        return Task.CompletedTask;
    }

    public Task PublishImmediateDangerEventAsync(
        string? deviceUid,
        string? userKeyField,
        ImmediateDangerEvent? immediateDangerEvent,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        PublishImmediateDangerEvent(deviceUid, userKeyField, immediateDangerEvent);
        return Task.CompletedTask;
    }

    public Task PublishImmediateDangerEndedAsync(
        string? deviceUid,
        string? userKeyField,
        ImmediateDangerEnded? immediateDangerEnded,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        PublishImmediateDangerEnded(deviceUid, userKeyField, immediateDangerEnded);
        return Task.CompletedTask;
    }

    public Task PublishSetTrackedDomainsAsync(
        IEnumerable<string> deviceUids,
        string? userKeyField,
        SetTrackedDomains? evt,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        PublishSetTrackedDomains(deviceUids, userKeyField, evt);
        return Task.CompletedTask;
    }

    public Task PublishSetBrowserTabsPolicyAsync(
        string? deviceUid,
        string? userKeyField,
        string mode,
        DateTime? validUntil,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        PublishSetBrowserTabsPolicy(deviceUid, userKeyField, mode, validUntil);
        return Task.CompletedTask;
    }

    public Task PublishSnapshotAsync(string deviceUid, string json, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        PublishSnapshot(deviceUid, json);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Publish analysis result notification to subscribers
    /// Topic format: "device:{deviceUid}" or "user:{userKey}"
    /// </summary>
    /// 

    public virtual void PublishImmediateDangerEnded(string? deviceUid, string? userKeyField, ImmediateDangerEnded? immediateDangerEnded)
    {
        if (!_isRunning || immediateDangerEnded == null)
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

            var immediateDangerEndedNotification = new ImmediateDangerEndedNotification(immediateDangerEnded.ImmediateDangerKey, immediateDangerEnded.UserKey, immediateDangerEnded.DeviceUid, immediateDangerEnded.EndTime, []);

            var notification = new
            {
                Type = "ImmediateDangerEndedNotification",
                Timestamp = DateTime.UtcNow,
                DeviceUid = deviceUid ?? string.Empty,
                Data = immediateDangerEndedNotification
            };

            

            // Use Newtonsoft.Json with TypeNameHandling to properly serialize polymorphic types
            var jsonSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
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
                    _logger.LogDebug($"Published notification to topic '{deviceTopic}'", deviceTopic);
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

    /// <summary>
    /// Publish a SetTrackedDomains event to every device of the user.
    /// ASPS-371: cross-device tracked-domain sync. The desktop agent
    /// subscribes only to "device:{deviceUid}", so we fan out to each
    /// device topic; we also emit "user:{userKeyField}" for any user-level
    /// subscriber (admin UI). The agent translates this into a
    /// 'tracked_domains:set' WebSocket message for the Chrome extension.
    /// </summary>
    public virtual void PublishSetTrackedDomains(
        IEnumerable<string> deviceUids, string? userKeyField, SetTrackedDomains? evt)
    {
        if (!_isRunning || evt == null)
        {
            _logger.LogWarning("NotificationPublisher is not running or notification is null, skipping");
            return;
        }

        var deviceList = (deviceUids ?? Enumerable.Empty<string>())
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct()
            .ToList();

        if (deviceList.Count == 0 && string.IsNullOrEmpty(userKeyField))
        {
            _logger.LogWarning("SetTrackedDomains: no target devices and no userKeyField, skipping");
            return;
        }

        try
        {
            var notification = new
            {
                Type = "SetTrackedDomainsNotification",
                Timestamp = DateTime.UtcNow,
                Data = new
                {
                    evt.UserKeyField,
                    evt.IsCrossPlatformLock,
                    evt.Reason,
                    evt.DistributionTimestamp,
                    TrackedDomains = evt.TrackedDomains
                }
            };

            var jsonSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None
            };

            var json = JsonConvert.SerializeObject(notification, jsonSettings);

            lock (_sendLock)
            {
                foreach (var deviceUid in deviceList)
                {
                    var deviceTopic = $"device:{deviceUid}";
                    _publisherSocket.SendMoreFrame(deviceTopic).SendFrame(json);
                    _logger.LogDebug("Published SetTrackedDomains to topic '{DeviceTopic}'", deviceTopic);
                }

                if (!string.IsNullOrEmpty(userKeyField))
                {
                    var userTopic = $"user:{userKeyField}";
                    _publisherSocket.SendMoreFrame(userTopic).SendFrame(json);
                    _logger.LogDebug("Published SetTrackedDomains to topic '{UserTopic}'", userTopic);
                }
            }

            _logger.LogInformation(
                "[NotificationPublisher] SetTrackedDomains published: {DomainCount} domain(s) to {DeviceCount} device(s), user={UserKey}",
                evt.TrackedDomains?.Count ?? 0, deviceList.Count, userKeyField);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing SetTrackedDomains notification");
        }
    }

    public virtual void PublishImmediateDangerEvent(string? deviceUid, string? userKeyField, ImmediateDangerEvent? immediateDangerEvent)
    {
        if (!_isRunning || immediateDangerEvent == null)
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
                Type = "ImmediateDangerNotification",
                Timestamp = DateTime.UtcNow,
                DeviceUid = deviceUid ?? string.Empty,
                Data = immediateDangerEvent
            };

            // Use Newtonsoft.Json with TypeNameHandling to properly serialize polymorphic types
            var jsonSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
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
                    _logger.LogDebug($"Published notification to topic '{deviceTopic}'", deviceTopic);
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

    public virtual void PublishAnalysisResult(
        string? deviceUid,
        string? userKeyField,
        AnalysisResultNotification? analysisResultNotification) =>
        PublishAnalysisResult(deviceUid, userKeyField, analysisResultNotification, null);

    public virtual void PublishAnalysisResult(
        string? deviceUid,
        string? userKeyField,
        AnalysisResultNotification? analysisResultNotification,
        MessageIdentityV1? messagingIdentity)
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
            object notification = messagingIdentity is null
                ? new
                {
                    Type = "AnalysisResult",
                    Timestamp = DateTime.UtcNow,
                    DeviceUid = deviceUid ?? string.Empty,
                    Data = analysisResultNotification
                }
                : MessageEnvelopeFactoryV1.CreateSuccess(messagingIdentity, analysisResultNotification);

            // Use Newtonsoft.Json with TypeNameHandling to properly serialize polymorphic types
            var jsonSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None
            };

            var json = notification is MessageEnvelopeV1
                ? System.Text.Json.JsonSerializer.Serialize(notification)
                : JsonConvert.SerializeObject(notification, jsonSettings);

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

    /// <summary>
    /// Publish a BrowserTabs policy override to a device (or all devices of a user).
    /// The desktop agent applies the override until <paramref name="validUntil"/>
    /// elapses, then reverts to its built-in default ('incoming_only').
    /// </summary>
    /// <param name="deviceUid">Target device UID, or null/empty to publish only on the user topic.</param>
    /// <param name="userKeyField">Target user key (KeyField.Value), or null/empty to publish only on the device topic.</param>
    /// <param name="mode">"incoming_only" | "always" | "never". Other values are rejected by the agent.</param>
    /// <param name="validUntil">UTC expiry. Null = no expiry (override stays until next override or agent restart).</param>
    public virtual void PublishSetBrowserTabsPolicy(string? deviceUid, string? userKeyField, string mode, DateTime? validUntil)
    {
        if (!_isRunning)
        {
            _logger.LogWarning("NotificationPublisher is not running, skipping SetBrowserTabsPolicy");
            return;
        }

        if (string.IsNullOrEmpty(deviceUid) && string.IsNullOrEmpty(userKeyField))
        {
            _logger.LogWarning("Both deviceUid and userKeyField are null/empty, skipping SetBrowserTabsPolicy");
            return;
        }

        if (string.IsNullOrEmpty(mode))
        {
            _logger.LogWarning("SetBrowserTabsPolicy called with empty mode, skipping");
            return;
        }

        try
        {
            var payload = new SetBrowserTabsPolicyNotification(
                deviceUid ?? string.Empty,
                userKeyField ?? string.Empty,
                mode,
                validUntil);

            var notification = new
            {
                Type = "SetBrowserTabsPolicyNotification",
                Timestamp = DateTime.UtcNow,
                DeviceUid = deviceUid ?? string.Empty,
                Data = payload
            };

            var jsonSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                Formatting = Formatting.None
            };

            var json = JsonConvert.SerializeObject(notification, jsonSettings);

            lock (_sendLock)
            {
                if (!string.IsNullOrEmpty(deviceUid))
                {
                    var deviceTopic = $"device:{deviceUid}";
                    _publisherSocket.SendMoreFrame(deviceTopic).SendFrame(json);
                    _logger.LogInformation(
                        "Published SetBrowserTabsPolicy to '{Topic}' mode={Mode} validUntil={ValidUntil}",
                        deviceTopic, mode, validUntil?.ToString("o") ?? "permanent");
                }

                if (!string.IsNullOrEmpty(userKeyField))
                {
                    var userTopic = $"user:{userKeyField}";
                    _publisherSocket.SendMoreFrame(userTopic).SendFrame(json);
                    _logger.LogInformation(
                        "Published SetBrowserTabsPolicy to '{Topic}' mode={Mode} validUntil={ValidUntil}",
                        userTopic, mode, validUntil?.ToString("o") ?? "permanent");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing SetBrowserTabsPolicy notification");
        }
    }

    /// <summary>
    /// Publish raw pre-serialized JSON on the device topic.
    /// Used by <see cref="ReconnectSnapshotService"/> to replay outbox entries
    /// and send reconnect snapshots without re-serializing.
    /// ASPS-620.
    /// </summary>
    public virtual void PublishSnapshot(string deviceUid, string json)
    {
        if (!_isRunning || string.IsNullOrEmpty(deviceUid) || string.IsNullOrEmpty(json))
            return;

        try
        {
            var deviceTopic = $"device:{deviceUid}";
            lock (_sendLock)
            {
                _publisherSocket.SendMoreFrame(deviceTopic).SendFrame(json);
            }
            _logger.LogDebug("[NotificationPublisher] Snapshot frame sent to '{Topic}'", deviceTopic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing snapshot to device {DeviceUid}", deviceUid);
        }
    }

    public virtual void Stop()
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
