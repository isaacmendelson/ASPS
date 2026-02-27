
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
using Interface.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Business.Messaging;

public enum SocketMode
{
    Pull,  // One-way communication (fire-and-forget)
    Rep    // Two-way communication (request-response)
}

public class RealTimeAlertListener : IDisposable
{
    private readonly ILogger<RealTimeAlertListener> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ASView _asView;
    private readonly UserDomainManagerService _userDomainService;
    private readonly TokenStore _tokenStore;
    private readonly CurveKeyManager? _curveKeyManager;
    private readonly RateLimiter _rateLimiter;
    private readonly List<IDomainEventHandler> _eventHandlers = new();
    private ResponseSocket? _repSocket;
    private PullSocket? _pullSocket;
    private bool _isRunning;
    private readonly int _port;
    private readonly SocketMode _mode;
    private readonly AlertPersistenceActor _alertPersistenceActor;

    public RealTimeAlertListener(
        ILoggerFactory _loggerFactory,
        IServiceProvider serviceProvider,
        ASView asView,
        UserDomainManagerService userDomainService,
        TokenStore tokenStore,
        CurveKeyManager? curveKeyManager = null,
        int port = 50001,
        SocketMode mode = SocketMode.Rep)
    {
        this._logger = _loggerFactory.CreateLogger<RealTimeAlertListener>();
        _serviceProvider = serviceProvider;
        _asView = asView;
        _userDomainService = userDomainService;
        _tokenStore = tokenStore;
        _curveKeyManager = curveKeyManager;
        _rateLimiter = new RateLimiter();
        _port = port;
        _mode = mode;
        var scope = _serviceProvider.CreateScope();
        var deviceAlertRepository = scope.ServiceProvider.GetRequiredService<IDeviceAlertRepository>();
        _alertPersistenceActor = new AlertPersistenceActor(deviceAlertRepository, _loggerFactory, _asView, _serviceProvider);
        this.RegisterEventHandler(_alertPersistenceActor);
        this.RegisterEventHandler(_asView);
    }

    public void RegisterEventHandler(IDomainEventHandler handler)
    {
        _eventHandlers.Add(handler);
    }

    public void Start()
    {
        _isRunning = true;

        if (_mode == SocketMode.Rep)
        {
            _repSocket = new ResponseSocket();
            _repSocket.Options.Linger = TimeSpan.Zero;
            _curveKeyManager?.ApplyServerCurve(_repSocket);
            _repSocket.Bind($"tcp://*:{_port}");
            var encStatus = _curveKeyManager?.IsEnabled == true ? "CURVE encrypted" : "unencrypted";
            _logger.LogInformation($"Real-time alert listener started on tcp://*:{_port} (REP mode - {encStatus})");
        }
        else
        {
            _pullSocket = new PullSocket();
            _pullSocket.Options.Linger = TimeSpan.Zero;
            _curveKeyManager?.ApplyServerCurve(_pullSocket);
            _pullSocket.Bind($"tcp://*:{_port}");
            var encStatus = _curveKeyManager?.IsEnabled == true ? "CURVE encrypted" : "unencrypted";
            _logger.LogInformation($"Real-time alert listener started on tcp://*:{_port} (PULL mode - {encStatus})");
        }

        Task.Run(() => ListenForAlerts());
    }

    private async Task ListenForAlerts()
    {
        while (_isRunning)
        {
            try
            {
                byte[] messageBytes;

                // Receive message based on socket mode
                if (_mode == SocketMode.Rep)
                {
                    messageBytes = _repSocket!.ReceiveFrameBytes();
                }
                else
                {
                    messageBytes = _pullSocket!.ReceiveFrameBytes();
                }

                string message;
                try
                {
                    message = System.Text.Encoding.UTF8.GetString(messageBytes);
                    _logger.LogInformation($"Received message: {message}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to decode message as UTF-8: {ex.Message}");
                    _logger.LogError($"Received bytes (hex): {BitConverter.ToString(messageBytes)}");

                    if (_mode == SocketMode.Rep)
                    {
                        SendResponse(new
                        {
                            success = false,
                            message = "Failed to decode message as UTF-8",
                            error = ex.Message
                        });
                    }
                    continue;
                }

                // Route message and get result
                var result = await RouteMessageAsync(message);

                if (_mode == SocketMode.Rep)
                {
                    SendResponse(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");

                if (_mode == SocketMode.Rep)
                {
                    SendResponse(new
                    {
                        success = false,
                        message = "Error processing message",
                        error = ex.Message
                    });
                }
            }
        }
    }

    private void SendResponse(object response)
    {
        try
        {
            var json = JsonConvert.SerializeObject(response);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            _repSocket!.SendFrame(bytes);
            _logger.LogDebug($"Response sent: {json}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending response");
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Message Router — checks MessageType and dispatches accordingly
    // ─────────────────────────────────────────────────────────────────────

    private async Task<object> RouteMessageAsync(string message)
    {
        try
        {
            var jObject = JObject.Parse(message);
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
                "RequestToken" => HandleRequestToken(jObject),
                "RegisterDevice" => await HandleRegisterDevice(jObject),
                "RefreshToken" => HandleRefreshToken(jObject),
                _ => await ProcessAlertAsync(message, jObject) // No MessageType = alert
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse message JSON");
            return new { success = false, message = "Invalid JSON", error = ex.Message };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error routing message");
            return new { success = false, message = "Error processing message", error = ex.Message };
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // RequestToken — device asks for its token (first connect or reconnect)
    // ─────────────────────────────────────────────────────────────────────

    private object HandleRequestToken(JObject jObject)
    {
        var deviceUid = jObject["DeviceUid"]?.ToString();

        if (string.IsNullOrEmpty(deviceUid))
        {
            return new { status = "Error", message = "DeviceUid is required" };
        }

        _logger.LogInformation("RequestToken from device {DeviceUid}", deviceUid);

        // Check if device exists in ASView
        var userDevice = _asView.FindUserDeviceByDeviceUid(deviceUid);
        if (userDevice == null)
        {
            _logger.LogInformation("Device {DeviceUid} not recognized", deviceUid);
            return new { status = "DeviceNotRecognized", deviceUid };
        }

        // Device exists — check if there's already a valid token
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
            serverPublicKey = _curveKeyManager?.ServerPublicKeyZ85 ?? string.Empty
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    // RegisterDevice — new device registers with user email
    // ─────────────────────────────────────────────────────────────────────

    private async Task<object> HandleRegisterDevice(JObject jObject)
    {
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
            _logger.LogInformation("Device {DeviceUid} already exists, creating new token", deviceUid);
            var token = _tokenStore.CreateToken(deviceUid, existingDevice.UserKeyField ?? string.Empty);
            return new
            {
                status = "Registered",
                token = token.TokenValue,
                expiration = token.Expiration.ToString("o"),
                deviceUid,
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
                serverPublicKey = _curveKeyManager?.ServerPublicKeyZ85 ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register device {DeviceUid}", deviceUid);
            return new { status = "Error", message = "Failed to register device", error = ex.Message };
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
    // ProcessAlertAsync — alert processing with token validation
    // ─────────────────────────────────────────────────────────────────────

    private async Task<object> ProcessAlertAsync(string message, JObject jObject)
    {
        try
        {
            string alertType = jObject["AlertType"]?.ToString() ?? "";

            Common.Models.DeviceAlert? alert = null;

            switch (alertType)
            {
                case "RemoteAccessAlert":
                    alert = JsonConvert.DeserializeObject<RemoteAccessAlert>(message);
                    break;
                case "UrlAlert":
                    alert = JsonConvert.DeserializeObject<UrlAlert>(message);
                    break;
                default:
                    _logger.LogWarning($"Unknown AlertType: {alertType}. Attempting to infer type from JSON structure.");

                    if (message.Contains("\"Url\""))
                    {
                        alert = JsonConvert.DeserializeObject<UrlAlert>(message);
                    }
                    else if (message.Contains("\"RemoteAccessApp\"") || message.Contains("\"ConnectionUrl\""))
                    {
                        alert = JsonConvert.DeserializeObject<RemoteAccessAlert>(message);
                    }
                    else
                    {
                        _logger.LogError($"Could not determine alert type from message: {message}");
                        return new
                        {
                            success = false,
                            message = "Unknown alert type",
                            alertType = alertType
                        };
                    }
                    break;
            }

            if (alert == null)
            {
                _logger.LogWarning("Failed to deserialize alert");
                return new
                {
                    success = false,
                    message = "Failed to deserialize alert",
                    error = "Deserialization failed"
                };
            }

            // ── Token Validation ──
            var deviceUid = alert.DeviceInfo.DeviceUid;
            var tokenValidation = _tokenStore.ValidateToken(deviceUid, alert.Token);

            switch (tokenValidation)
            {
                case TokenValidationResult.InvalidToken:
                    _logger.LogWarning("Invalid token from device {DeviceUid}", deviceUid);
                    return new { status = "InvalidToken", message = "Token is invalid. Please authenticate." };

                case TokenValidationResult.TokenExpired:
                    _logger.LogInformation("Expired token from device {DeviceUid}", deviceUid);
                    return new { status = "TokenExpired", message = "Token has expired. Please refresh." };
            }

            // ── Token is valid — proceed with alert processing ──

            var userDevice = _asView.FindUserDeviceByDeviceUid(deviceUid);

            if (userDevice == null)
            {
                var errorMsg = $"Device not found: {deviceUid}";
                _logger.LogWarning(errorMsg);
                throw new DomainException(
                    new Common.Exceptions.ErrorMessage("DeviceNotFound", errorMsg, Common.Enums.ResultStatusCode.NotFound));
            }
            alert.DeviceInfo.Key = userDevice.Key;

            var user = _asView.FindUserByKey(userDevice.UserKey);
            if (user == null)
            {
                var errorMsg = $"User not found for device: {deviceUid}";
                _logger.LogWarning(errorMsg);
                throw new DomainException(
                    new Common.Exceptions.ErrorMessage("UserNotFound", errorMsg, Common.Enums.ResultStatusCode.NotFound));
            }

            _logger.LogInformation($"Alert from device {deviceUid} associated with user {user.Key}");

            var keyField = Guid.NewGuid().ToString();

            var domainEvent = new DeviceAlertReceived(
                alert,
                alert.Priority,
                alert.DeviceInfo.DeviceUid,
                DateTime.UtcNow,
                alert.Timestamp,
                keyField
            );

            var userManager = await _userDomainService.GetManagerForDeviceAsync(alert.DeviceInfo.DeviceUid);
            if (userManager != null)
            {
                userManager.Handle(domainEvent);
                _logger.LogInformation($"Alert routed to UDAnalysisManager for user: {userManager.UDUser.Key}");
            }
            else
            {
                _logger.LogWarning($"No UDAnalysisManager found for device: {alert.DeviceInfo.DeviceUid}");
            }

            foreach (var handler in _eventHandlers)
            {
                if (handler.GetHandleableEvents().Contains(typeof(DeviceAlertReceived)))
                {
                    _ = handler.Handle(domainEvent);
                }
            }

            _logger.LogInformation($"Device alert processed: {alert.DeviceInfo.DeviceUid}, Type: {alertType}");

            return new
            {
                success = true,
                message = "Alert processed successfully",
                alertType = alertType,
                deviceUid = alert.DeviceInfo.DeviceUid,
                timestamp = DateTime.UtcNow.ToString("o"),
                priority = alert.Priority.ToString()
            };
        }
        catch (DomainException domainEx)
        {
            _logger.LogWarning(domainEx, $"Domain error in ProcessAlertAsync: {domainEx.Code}");
            return new
            {
                success = false,
                message = domainEx.Code,
                error = domainEx.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in ProcessAlertAsync. Message: {message}");
            return new
            {
                success = false,
                message = "Error processing alert",
                error = ex.Message
            };
        }
    }

    public void Stop()
    {
        _isRunning = false;
        _logger.LogInformation("Real-time alert listener stopped");
    }

    public void Dispose()
    {
        Stop();
        _pullSocket?.Dispose();
        _repSocket?.Dispose();
    }
}
