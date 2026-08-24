
using Business.DomainEvents;
using Business.RealtimeAnalysis;
using Business.RealtimeAnalysis.UserDomain;
using Business.Services;
using Business.Views;
using Common.Entities;
using Common.Enums;
using Common.Exceptions;
using Common.Interfaces;
using Common.Models;
using Common.Models.Alerts;
using Common.Generated.Messaging.V1;
using Interface.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Business.Messaging;

/// <summary>
/// Business logic for device alerts and token/device lifecycle messages — token validation,
/// alert routing, message dispatch, device registration. Transport-agnostic: contains no
/// NetMQ dependency. Extracted from <c>RealTimeAlertListener</c> (ASPS-684) — the transport
/// layer now lives in <see cref="NetMQAlertIngress"/>, which delegates here.
/// </summary>
public class AlertProcessor
{
    private readonly ILogger<AlertProcessor> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ASView _asView;
    private readonly UserDomainManagerService _userDomainService;
    private readonly TokenStore _tokenStore;
    private readonly CurveKeyManager? _curveKeyManager;
    private readonly RateLimiter _rateLimiter;
    private readonly List<IDomainEventHandler> _eventHandlers = new();
    private readonly AlertPersistenceActor _alertPersistenceActor;
    private readonly DomainEventPublisher _domainEventPublisher;
    private readonly MessageDeduplicator _messageDeduplicator;
    private readonly MessagingCompatibilityOptions _messagingCompatibility;
    private ReconnectSnapshotService? _reconnectSnapshotService;
    public bool AcceptLegacyV0 => _messagingCompatibility.AcceptLegacyV0;
    internal Action<DeviceAlertReceived>? DomainDispatchObserver { get; set; }

    /// <summary>
    /// Inject the reconnect snapshot service after construction.
    /// ASPS-620: keeps constructor signature backward-compatible.
    /// </summary>
    public void SetReconnectSnapshotService(ReconnectSnapshotService service)
        => _reconnectSnapshotService = service;

    public AlertProcessor(
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        ASView asView,
        UserDomainManagerService userDomainService,
        TokenStore tokenStore,
        CurveKeyManager? curveKeyManager = null,
        IConfiguration? configuration = null)
    {
        _logger = loggerFactory.CreateLogger<AlertProcessor>();
        _serviceProvider = serviceProvider;
        _asView = asView;
        _userDomainService = userDomainService;
        _tokenStore = tokenStore;
        _curveKeyManager = curveKeyManager;
        _rateLimiter = new RateLimiter();
        _messageDeduplicator = new MessageDeduplicator(
            TimeSpan.FromMinutes(15), capacity: 100_000);
        _messagingCompatibility = new MessagingCompatibilityOptions
        {
            AcceptLegacyV0 = configuration?.GetValue<bool>(
                "Messaging:AcceptLegacyV0", false) ?? false
        };
        _alertPersistenceActor = new AlertPersistenceActor(loggerFactory, _asView, _serviceProvider);
        this.RegisterEventHandler(_alertPersistenceActor);
        this.RegisterEventHandler(_asView);

        _domainEventPublisher = new DomainEventPublisher(_eventHandlers);
    }

    public void RegisterEventHandler(IDomainEventHandler handler)
    {
        _eventHandlers.Add(handler);
        _logger.LogInformation($"Registered event handler: {handler.GetType().Name}");
    }

    // ─────────────────────────────────────────────────────────────────────
    // Message Router — checks MessageType and dispatches accordingly
    // ─────────────────────────────────────────────────────────────────────

    internal async Task<object> RouteMessageAsync(string message)
    {
        try
        {
            var jObject = JObject.Parse(message);
            if (jObject["schemaVersion"] != null)
                return await ProcessEnvelopeAsync(message, jObject);
            var messageType = jObject["MessageType"]?.ToString();

            // Rate limit token endpoints
            if (messageType is "RequestToken" or "RegisterDevice" or "RefreshToken")
            {
                var deviceUid = jObject["DeviceUid"]?.ToString() ?? "unknown";
                var maxRequests = messageType == "RegisterDevice" ? 3 : 5;
                var rateLimitKey = $"{messageType}:{deviceUid}";

                if (!_rateLimiter.IsAllowed(rateLimitKey, maxRequests, TimeSpan.FromMinutes(1)))
                {
                    _logger.LogWarning("Rate limit exceeded for {MessageType} from device {DeviceUid}", messageType, deviceUid);
                    return new { success = false, message = "Rate limit exceeded. Try again later." };
                }
            }

            return messageType switch
            {
                "RequestToken" => await HandleRequestTokenWithSnapshotAsync(jObject),
                "RegisterDevice" => await HandleRegisterDeviceWithSnapshotAsync(jObject),
                "RefreshToken" => HandleRefreshToken(jObject),
                "NotificationAck" => await HandleNotificationAckAsync(jObject),
                _ => await ProcessLegacyAlertAsync(message, jObject)
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse message JSON");
            return new { success = false, message = "Invalid JSON" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error routing message");
            return new { success = false, message = "Error processing message" };
        }
    }

    private async Task<object> ProcessLegacyAlertAsync(string message, JObject alert)
    {
        try
        {
            MessagingCompatibility.AdaptLegacyIngress(
                alert, _messagingCompatibility);
        }
        catch (MessagingCompatibilityException ex)
        {
            return new { success = false, code = ex.Code, message = ex.Message };
        }
        return await ProcessAlertAsync(alert.ToString(Formatting.None), alert);
    }

    /// <summary>
    /// Maps a v1 envelope's request messageType to the concrete alert entity it carries and the
    /// messageType of its "accepted" response. ASPS-732 extends the envelope ingress from
    /// url_scan.request only to every alert family the desktop agent sends; the discriminator
    /// (<see cref="Common.Models.DeviceAlert.AlertType"/>) is stamped here from this map — not
    /// trusted from the wire — so <see cref="ProcessAlertAsync"/> always dispatches through the
    /// correct branch regardless of what the client put in payload.alert.AlertType.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (string Accepted, Type EntityType, string Discriminator)> EnvelopeRequestTypes =
        new Dictionary<string, (string, Type, string)>(StringComparer.Ordinal)
        {
            ["url_scan.request"] = ("url_scan.accepted", typeof(UrlAlert), nameof(UrlAlert)),
            ["track_url.request"] = ("track_url.accepted", typeof(TrackUrlAlert), nameof(TrackUrlAlert)),
            ["tab_closed.request"] = ("tab_closed.accepted", typeof(TabClosedAlert), nameof(TabClosedAlert)),
            ["tab_changed.request"] = ("tab_changed.accepted", typeof(TabChangedAlert), nameof(TabChangedAlert)),
            ["remote_access.request"] = ("remote_access.accepted", typeof(RemoteAccessAlert), nameof(RemoteAccessAlert)),
        };

    /// <summary>Request types whose payload.alert carries Url/TabId that must echo the envelope's immutable context.
    /// RemoteAccessAlert has no Url/TabId of its own (ConnectionUrl is not a tab context) — only DeviceUid is checked.</summary>
    private static readonly HashSet<string> UrlBasedEnvelopeRequestTypes = new(StringComparer.Ordinal)
    {
        "url_scan.request", "track_url.request", "tab_closed.request", "tab_changed.request"
    };

    internal async Task<object> ProcessEnvelopeAsync(string message, JObject wire)
    {
        var validation = MessageEnvelopeValidator.DeserializeAndValidate(
            message, out var envelope, requireDeviceId: true);
        if (!validation.IsValid)
            return CreateEnvelopeError(wire, validation.ErrorCode!, validation.ErrorMessage!);

        if (!EnvelopeRequestTypes.TryGetValue(envelope!.MessageType, out var mapping))
            return CreateEnvelopeError(wire, "protocol.unsupported_message_type",
                "Backend ingress accepts url_scan.request, track_url.request, tab_closed.request, tab_changed.request, remote_access.request only.");

        var alertToken = wire["payload"]?["alert"];
        if (alertToken is not JObject alertObject)
            return CreateEnvelopeError(wire, "protocol.invalid_payload", "payload.alert is required.");

        var alertDeviceId = alertObject["DeviceInfo"]?["DeviceUid"]?.ToString();
        var contextMatches = string.Equals(alertDeviceId, envelope.Context.DeviceId, StringComparison.Ordinal);
        if (contextMatches && UrlBasedEnvelopeRequestTypes.Contains(envelope.MessageType))
        {
            var alertUrl = alertObject["Url"]?.ToString();
            var alertTabId = alertObject["TabId"]?.ToString();
            contextMatches =
                string.Equals(alertUrl, envelope.Context.Url, StringComparison.Ordinal) &&
                string.Equals(alertTabId, envelope.Context.TabId, StringComparison.Ordinal);
        }
        if (!contextMatches)
            return CreateEnvelopeError(wire, "validation.immutable_context_mismatch", "payload.alert does not match immutable context.");

        var typedAlert = (Common.Models.DeviceAlert?)JsonConvert.DeserializeObject(
            alertObject.ToString(Formatting.None), mapping.EntityType);
        if (typedAlert is null)
            return CreateEnvelopeError(wire, "protocol.invalid_payload", "payload.alert is invalid.");

        // Claim only after the complete envelope, payload and immutable context
        // have passed validation. A malformed first attempt cannot poison a
        // corrected retry that reuses the same wire messageId.
        if (!_messageDeduplicator.TryBegin(envelope.MessageId))
        {
            return new MessageEnvelopeV1
            {
                MessageId = Guid.NewGuid(),
                CorrelationId = envelope.CorrelationId,
                RequestId = envelope.RequestId,
                MessageType = mapping.Accepted,
                SentAt = DateTimeOffset.UtcNow,
                Source = "backend",
                Context = envelope.Context,
                Outcome = null,
                Payload = System.Text.Json.JsonSerializer.SerializeToElement(
                    new { accepted = true, duplicate = true })
            };
        }

        typedAlert.AlertType = mapping.Discriminator;
        typedAlert.MessagingIdentity = new MessageIdentityV1(
            envelope.MessageId, envelope.CorrelationId, envelope.RequestId,
            envelope.Context.DeviceId!, envelope.Context.TabId, envelope.Context.Url);
        alertObject = JObject.FromObject(typedAlert);

        var legacyResult = await ProcessAlertAsync(alertObject.ToString(Formatting.None), alertObject);
        var resultJson = System.Text.Json.JsonSerializer.SerializeToElement(legacyResult);
        return new MessageEnvelopeV1
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = envelope.CorrelationId,
            RequestId = envelope.RequestId,
            MessageType = mapping.Accepted,
            SentAt = DateTimeOffset.UtcNow,
            Source = "backend",
            Context = envelope.Context,
            Outcome = null,
            Payload = resultJson
        };
    }

    private static MessageEnvelopeV1 CreateEnvelopeError(JObject wire, string code, string message)
    {
        static Guid ReadGuid(JToken? token) => Guid.TryParse(token?.ToString(), out var value) ? value : Guid.NewGuid();
        var contextToken = wire["context"];
        var context = new MessageContextV1
        {
            DeviceId = contextToken?["deviceId"]?.Type == JTokenType.Null ? null : contextToken?["deviceId"]?.ToString(),
            TabId = contextToken?["tabId"]?.Type == JTokenType.Null ? null : contextToken?["tabId"]?.ToString(),
            Url = contextToken?["url"]?.ToString() ?? "https://invalid.local/"
        };
        // Derive the error family from the wire's own messageType (e.g. "track_url.request" ->
        // "track_url.error") so a rejected non-url_scan envelope doesn't get mislabeled as a
        // url_scan error. Falls back to url_scan.error when the wire messageType is missing,
        // malformed, or not one of the known "<family>.request" shapes (e.g. schema-invalid input).
        var wireMessageType = wire["messageType"]?.ToString();
        var errorType = wireMessageType is not null && EnvelopeRequestTypes.ContainsKey(wireMessageType)
            ? wireMessageType[..^".request".Length] + ".error"
            : "url_scan.error";
        return new MessageEnvelopeV1
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = ReadGuid(wire["correlationId"]),
            RequestId = ReadGuid(wire["requestId"]),
            MessageType = errorType,
            SentAt = DateTimeOffset.UtcNow,
            Source = "backend",
            Context = context,
            Outcome = new MessageOutcomeV1
            {
                Status = "error",
                Error = new MessageErrorV1 { Code = code, Message = message, Retryable = false }
            },
            Payload = System.Text.Json.JsonSerializer.SerializeToElement(new { })
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    // RequestToken — device asks for its token (first connect or reconnect)
    // ─────────────────────────────────────────────────────────────────────

    private object HandleRequestToken(JObject jObject)
    {
        var deviceUid = jObject["DeviceUid"]?.ToString();
        var email     = jObject["Email"]?.ToString();

        if (string.IsNullOrEmpty(deviceUid))
            return new { status = "Error", message = "DeviceUid is required" };

        _logger.LogInformation("RequestToken from device {DeviceUid}", deviceUid);

        // Check if device exists in ASView
        var userDevice = _asView.FindUserDeviceByDeviceUid(deviceUid);
        if (userDevice == null)
        {
            _logger.LogInformation("Device {DeviceUid} not recognized", deviceUid);
            return new { status = "DeviceNotRecognized", deviceUid };
        }

        // Security: if email was provided, verify it belongs to the device owner.
        // This prevents an attacker who only knows the DeviceUid from obtaining a token.
        // If email is absent (legacy client or first-run), we allow the request but log a warning —
        // clients should always send email; enforce strictly once all clients are updated.
        if (!string.IsNullOrEmpty(email))
        {
            var deviceOwner = userDevice.UserKey != null
                ? _asView.FindUserByKey(userDevice.UserKey)
                : null;
            if (deviceOwner == null || !string.Equals(deviceOwner.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "RequestToken: email mismatch for device {DeviceUid} — rejecting",
                    deviceUid);
                // Return same response as DeviceNotRecognized to avoid leaking
                // whether the DeviceUid exists at all.
                return new { status = "DeviceNotRecognized", deviceUid };
            }
        }
        else
        {
            _logger.LogWarning(
                "RequestToken from device {DeviceUid} without email — allowed for now, update client to send email",
                deviceUid);
        }

        // Device + email verified — check if there's already a valid token
        // Get user email for response
        var owner = userDevice.UserKey != null ? _asView.FindUserByKey(userDevice.UserKey) : null;
        var userEmail = owner?.Email ?? email ?? "";

        var existingToken = _tokenStore.GetToken(deviceUid);
        if (existingToken != null)
        {
            var validation = _tokenStore.ValidateToken(deviceUid, existingToken.TokenValue);
            if (validation == TokenValidationResult.Valid)
            {
                _logger.LogInformation("Returning existing valid token for device {DeviceUid}", deviceUid);
                return new
                {
                    status = "TokenCreated",
                    token = existingToken.TokenValue,
                    expiration = existingToken.Expiration.ToString("o"),
                    deviceUid,
                    email = userEmail,
                    serverPublicKey = _curveKeyManager?.ServerPublicKeyZ85 ?? string.Empty
                };
            }
        }

        // Create a new token
        var newToken = _tokenStore.CreateToken(deviceUid, userDevice.UserKeyField ?? string.Empty);
        return new
        {
            status = "TokenCreated",
            token = newToken.TokenValue,
            expiration = newToken.Expiration.ToString("o"),
            deviceUid,
            email = userEmail,
            serverPublicKey = _curveKeyManager?.ServerPublicKeyZ85 ?? string.Empty
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    // RegisterDevice — new device registers with user email
    // ─────────────────────────────────────────────────────────────────────

    private async Task<object> HandleRegisterDevice(JObject jObject)
    {
        var peerMajors = jObject["SupportedSchemaMajors"] is JArray majors
            ? majors.Values<int>()
            : new[] { 0 };
        MessagingNegotiationResult negotiation;
        try
        {
            negotiation = MessagingCompatibility.Negotiate(
                peerMajors, _messagingCompatibility);
        }
        catch (MessagingCompatibilityException ex)
        {
            return new { status = "Error", code = ex.Code, message = ex.Message };
        }

        var deviceUid = jObject["DeviceUid"]?.ToString();
        var email = jObject["Email"]?.ToString();
        var deviceTypeInt = jObject["DeviceType"]?.Value<int>() ?? (int)DeviceType.PersonalComputer;
        var osTypeInt = jObject["OperatingSystem"]?.Value<int>() ?? (int)OperatingSystemType.Windows;
        var mac = jObject["MAC"]?.ToString() ?? string.Empty;

        if (string.IsNullOrEmpty(deviceUid) || string.IsNullOrEmpty(email))
        {
            return new { status = "Error", message = "DeviceUid and Email are required" };
        }

        _logger.LogInformation("RegisterDevice: DeviceUid={DeviceUid}, Email={Email}", deviceUid, email);

        // Look up user by email (must be active and not disabled)
        var user = _asView.FindUserByEmailActive(email);
        if (user == null)
        {
            _logger.LogWarning("RegisterDevice: No active user found for email {Email}", email);
            return new { status = "InvalidUser", message = "User not found or account is disabled" };
        }

        // Check if device already exists (might be a re-registration)
        var existingDevice = _asView.FindUserDeviceByDeviceUid(deviceUid);
        if (existingDevice != null)
        {
            // Security: verify the requesting user owns this device.
            // Prevents re-registration attacks where an attacker supplies a known
            // DeviceUid with a different email to hijack the device token.
            if (existingDevice.UserKeyField != user.KeyField)
            {
                _logger.LogWarning(
                    "RegisterDevice: Device {DeviceUid} belongs to a different user. Rejecting re-registration attempt.",
                    deviceUid);
                return new { status = "Unauthorized", message = "Device is registered to a different user account." };
            }

            _logger.LogInformation("Device {DeviceUid} already exists for same user, creating new token", deviceUid);
            var token = _tokenStore.CreateToken(deviceUid, existingDevice.UserKeyField ?? string.Empty);
            return new
            {
                status = "Registered",
                token = token.TokenValue,
                expiration = token.Expiration.ToString("o"),
                deviceUid,
                schemaMajor = negotiation.SchemaMajor,
                supportedSchemaMajors = MessagingCompatibility.SupportedSchemaMajors,
                serverPublicKey = _curveKeyManager?.ServerPublicKeyZ85 ?? string.Empty
            };
        }

        // Create new PersonalComputer device entity
        var newDevice = new PersonalComputer
        {
            KeyField = Guid.NewGuid().ToString(),
            UserKeyField = user.KeyField,
            DeviceType = (DeviceType)deviceTypeInt,
            DeviceUid = deviceUid,
            OperatingSystem = (OperatingSystemType)osTypeInt,
            MAC = mac,
            MonitoringStatus = DeviceMonitoringStatus.Enabled,
            DateCreated = DateTime.UtcNow
        };

        try
        {
            // Save to database
            using var scope = _serviceProvider.CreateScope();
            var deviceRepo = scope.ServiceProvider.GetRequiredService<IUserDeviceRepository>();
            await deviceRepo.AddAsync(newDevice);

            // Add to ASView in-memory cache
            _asView.AddUserDevice(newDevice);

            _logger.LogInformation("Device {DeviceUid} registered for user {UserKey}", deviceUid, user.KeyField);

            // Create token
            var token = _tokenStore.CreateToken(deviceUid, user.KeyField);
            return new
            {
                status = "Registered",
                token = token.TokenValue,
                expiration = token.Expiration.ToString("o"),
                deviceUid,
                schemaMajor = negotiation.SchemaMajor,
                supportedSchemaMajors = MessagingCompatibility.SupportedSchemaMajors,
                serverPublicKey = _curveKeyManager?.ServerPublicKeyZ85 ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register device {DeviceUid}", deviceUid);
            return new { status = "Error", message = "Failed to register device" };
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // RefreshToken — device requests a new token using its expired token
    // ─────────────────────────────────────────────────────────────────────

    private object HandleRefreshToken(JObject jObject)
    {
        var deviceUid = jObject["DeviceUid"]?.ToString();
        var oldToken = jObject["Token"]?.ToString();

        if (string.IsNullOrEmpty(deviceUid) || string.IsNullOrEmpty(oldToken))
        {
            return new { status = "Error", message = "DeviceUid and Token are required" };
        }

        _logger.LogInformation("RefreshToken request from device {DeviceUid}", deviceUid);

        var newToken = _tokenStore.RefreshToken(deviceUid, oldToken);
        if (newToken == null)
        {
            _logger.LogWarning("RefreshToken failed for device {DeviceUid}", deviceUid);
            return new { status = "RefreshDenied", message = "Token refresh denied. Please re-register." };
        }

        return new
        {
            status = "TokenRefreshed",
            token = newToken.TokenValue,
            expiration = newToken.Expiration.ToString("o"),
            deviceUid,
            serverPublicKey = _curveKeyManager?.ServerPublicKeyZ85 ?? string.Empty
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    // ASPS-620: Snapshot-on-reconnect wrappers for RequestToken / RegisterDevice
    // ─────────────────────────────────────────────────────────────────────

    private Task<object> HandleRequestTokenWithSnapshotAsync(JObject jObject)
    {
        var result = HandleRequestToken(jObject);
        // Trigger snapshot if auth succeeded
        if (result is { } r && r.GetType().GetProperty("status")?.GetValue(r)?.ToString() == "TokenCreated")
        {
            var deviceUid = jObject["DeviceUid"]?.ToString();
            if (!string.IsNullOrEmpty(deviceUid) && _reconnectSnapshotService != null)
                _ = Task.Run(() => _reconnectSnapshotService.SendSnapshotAsync(deviceUid));
        }
        return Task.FromResult(result);
    }

    private async Task<object> HandleRegisterDeviceWithSnapshotAsync(JObject jObject)
    {
        var result = await HandleRegisterDevice(jObject);
        if (result is { } r && r.GetType().GetProperty("status")?.GetValue(r)?.ToString() == "Registered")
        {
            var deviceUid = jObject["DeviceUid"]?.ToString();
            if (!string.IsNullOrEmpty(deviceUid) && _reconnectSnapshotService != null)
                _ = Task.Run(() => _reconnectSnapshotService.SendSnapshotAsync(deviceUid));
        }
        return result;
    }

    // ─────────────────────────────────────────────────────────────────────
    // ASPS-620: NotificationAck — device ACKs a received notification
    // ─────────────────────────────────────────────────────────────────────

    private async Task<object> HandleNotificationAckAsync(JObject jObject)
    {
        var deviceUid = jObject["DeviceUid"]?.ToString();
        var messageIdStr = jObject["MessageId"]?.ToString();

        if (string.IsNullOrEmpty(deviceUid))
            return new { status = "Error", message = "DeviceUid is required" };

        if (!Guid.TryParse(messageIdStr, out var messageId))
            return new { status = "Error", message = "MessageId must be a valid GUID" };

        // Validate the device token before accepting the ACK
        var token = jObject["Token"]?.ToString();
        var tokenValidation = _tokenStore.ValidateToken(deviceUid, token);
        if (tokenValidation == TokenValidationResult.InvalidToken)
            return new { status = "InvalidToken", message = "Token is invalid." };
        if (tokenValidation == TokenValidationResult.TokenExpired)
            return new { status = "TokenExpired", message = "Token has expired." };

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var outboxRepo = scope.ServiceProvider.GetRequiredService<Interface.Repositories.INotificationOutboxRepository>();
            await outboxRepo.AcknowledgeAsync(deviceUid, messageId);

            _logger.LogInformation(
                "[ASPS-620] NotificationAck processed — device={Device} messageId={MessageId}",
                deviceUid, messageId);

            return new { status = "Acknowledged", messageId = messageId.ToString() };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ASPS-620] Error processing NotificationAck from device={Device}", deviceUid);
            return new { status = "Error", message = "Failed to process ACK" };
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // ProcessAlertAsync — alert processing with token validation
    // ─────────────────────────────────────────────────────────────────────

    // Phase 1: fast — parse, validate token, ACK immediately, fire background task
    internal Task<object> ProcessAlertAsync(string message, JObject jObject)
    {
        try
        {
            string alertType = jObject["AlertType"]?.ToString() ?? "";

            Common.Models.DeviceAlert? alert = alertType switch
            {
                "RemoteAccessAlert" => JsonConvert.DeserializeObject<RemoteAccessAlert>(message),
                "UrlAlert"          => JsonConvert.DeserializeObject<UrlAlert>(message),
                "TrackUrlAlert"     => JsonConvert.DeserializeObject<TrackUrlAlert>(message),
                "TabClosedAlert"    => JsonConvert.DeserializeObject<TabClosedAlert>(message),
                "TabChangedAlert"   => JsonConvert.DeserializeObject<TabChangedAlert>(message),
                _ => InferAlertType(message, alertType)
            };

            if (alert == null)
            {
                _logger.LogWarning("Failed to deserialize alert (type={AlertType})", alertType);
                return Task.FromResult<object>(new { success = false, message = "Failed to deserialize alert" });
            }

            // Reject UrlAlerts for local/loopback addresses — defense-in-depth
            if (alert is UrlAlert urlAlertCheck && IsLocalUrl(urlAlertCheck.Url))
            {
                _logger.LogDebug("Skipping local URL alert from device {DeviceUid}: {Url}",
                    alert.DeviceInfo.DeviceUid, urlAlertCheck.Url);
                return Task.FromResult<object>(new { success = false, message = "Local URLs are not analyzed." });
            }

            var deviceUid = alert.DeviceInfo.DeviceUid;
            var tokenValidation = _tokenStore.ValidateToken(deviceUid, alert.Token);

            if (tokenValidation == TokenValidationResult.InvalidToken)
            {
                _logger.LogWarning("Invalid token from device {DeviceUid}", deviceUid);
                return Task.FromResult<object>(new { status = "InvalidToken", message = "Token is invalid. Please authenticate." });
            }
            if (tokenValidation == TokenValidationResult.TokenExpired)
            {
                _logger.LogInformation("Expired token from device {DeviceUid}", deviceUid);
                return Task.FromResult<object>(new { status = "TokenExpired", message = "Token has expired. Please refresh." });
            }

            var userDevice = _asView.FindUserDeviceByDeviceUid(deviceUid);
            if (userDevice == null)
            {
                _logger.LogWarning("Device not found: {DeviceUid}", deviceUid);
                return Task.FromResult<object>(new { status = "DeviceNotRecognized", message = "Device not found." });
            }
            alert.DeviceInfo.Key = userDevice.Key;

            var user = _asView.FindUserByKey(userDevice.UserKey);
            if (user == null)
            {
                _logger.LogWarning("User not found for device {DeviceUid}", deviceUid);
                return Task.FromResult<object>(new { success = false, message = "User not found." });
            }

            var domainEvent = new DeviceAlertReceived(
                alert,
                alert.Priority,
                alert.DeviceInfo.DeviceUid,
                DateTime.UtcNow,
                alert.Timestamp,
                Guid.NewGuid().ToString()
            );

            // Phase 2: fire analysis in background — ACK is returned immediately
            _ = Task.Run(() => DispatchAlertInBackground(domainEvent, userDevice.UserKey, deviceUid));

            _logger.LogInformation("Alert accepted from device {DeviceUid}, type={AlertType} — dispatching analysis",
                deviceUid, alertType);

            return Task.FromResult<object>(new
            {
                success  = true,
                message  = "Alert accepted — analysis in progress",
                alertType,
                deviceUid,
                timestamp = DateTime.UtcNow.ToString("o"),
                priority  = alert.Priority.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ProcessAlertAsync");
            return Task.FromResult<object>(new { success = false, message = "Error processing alert" });
        }
    }

    // Phase 2: runs on thread pool — analysis can take up to 30s (Python/ML)
    private async Task DispatchAlertInBackground(DeviceAlertReceived domainEvent, Key userKey, string deviceUid)
    {
        try
        {
            DomainDispatchObserver?.Invoke(domainEvent);

            this._domainEventPublisher.Register(domainEvent);
            this._domainEventPublisher.RaiseAll();

            var userManager = this._userDomainService.GetOrCreateManagerForUser(userKey);
            if (userManager != null)
            {
                userManager.Handle(domainEvent);
                _logger.LogInformation($"Analysis dispatched for device {deviceUid}, user {userKey}");
            }
            else
            {
                _logger.LogWarning("No UDAnalysisManager found for device {DeviceUid}", deviceUid);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in background analysis dispatch for device {DeviceUid}", deviceUid);
        }
    }

    private static bool IsLocalUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        try
        {
            var host = new Uri(url).Host.ToLowerInvariant();
            return host == "localhost"  ||
                   host == "127.0.0.1" ||
                   host == "::1"       ||
                   host == "0.0.0.0"   ||
                   host.StartsWith("127.");
        }
        catch
        {
            return false;
        }
    }

    private Common.Models.DeviceAlert? InferAlertType(string message, string alertType)
    {
        _logger.LogWarning("Unknown AlertType '{AlertType}' — inferring from message content", alertType);
        if (message.Contains("\"Url\""))
            return JsonConvert.DeserializeObject<UrlAlert>(message);
        if (message.Contains("\"RemoteAccessApp\"") || message.Contains("\"ConnectionUrl\""))
            return JsonConvert.DeserializeObject<RemoteAccessAlert>(message);
        _logger.LogError("Could not determine alert type from message content");
        return null;
    }
}
