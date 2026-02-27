using Business.Queries;
using Microsoft.AspNetCore.SignalR;
using WebApi.Services;

namespace WebApi.Hubs
{
    /// <summary>
    /// SignalR Hub for sending real-time notifications to connected clients.
    /// Validates device tokens on connection when deviceUid/token query params are provided.
    /// Admin page connections (no token) are allowed through.
    /// </summary>
    public class NotificationsHub : Hub
    {
        private readonly ILogger<NotificationsHub> _logger;
        private readonly CQRSClient _cqrsClient;

        private const string DeviceUidKey = "ValidatedDeviceUid";
        private const string UserKeyFieldKey = "ValidatedUserKeyField";

        public NotificationsHub(ILogger<NotificationsHub> logger, CQRSClient cqrsClient)
        {
            _logger = logger;
            _cqrsClient = cqrsClient;
        }

        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            var deviceUid = httpContext?.Request.Query["deviceUid"].ToString();
            var token = httpContext?.Request.Query["token"].ToString();

            // If device credentials are provided, validate them
            if (!string.IsNullOrEmpty(deviceUid) && !string.IsNullOrEmpty(token))
            {
                try
                {
                    var query = new ValidateDeviceTokenQuery
                    {
                        DeviceUid = deviceUid,
                        TokenValue = token
                    };

                    var result = await _cqrsClient.SendQueryAsync<ValidateDeviceTokenQueryResult>(query);

                    if (!result.Success || !result.IsValid)
                    {
                        _logger.LogWarning(
                            "SignalR connection rejected for device {DeviceUid}: invalid token",
                            deviceUid);
                        Context.Abort();
                        return;
                    }

                    // Store validated identity for subscription checks
                    Context.Items[DeviceUidKey] = deviceUid;
                    Context.Items[UserKeyFieldKey] = result.UserKeyField;

                    _logger.LogInformation(
                        "Device {DeviceUid} authenticated for SignalR connection {ConnectionId}",
                        deviceUid, Context.ConnectionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error validating device token for SignalR connection");
                    Context.Abort();
                    return;
                }
            }
            else
            {
                _logger.LogInformation("Client connected without device token (admin): {ConnectionId}",
                    Context.ConnectionId);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Client can call this to subscribe to specific notification types.
        /// If the caller authenticated with a device token, they can only subscribe to their own group.
        /// </summary>
        public async Task SubscribeToNotifications(string clientId)
        {
            // If this connection was authenticated with a device token, enforce ownership
            if (Context.Items.TryGetValue(DeviceUidKey, out var validatedDeviceUid))
            {
                var deviceUid = validatedDeviceUid?.ToString();
                if (!string.Equals(clientId, deviceUid, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "Device {DeviceUid} attempted to subscribe to group for {ClientId} - denied",
                        deviceUid, clientId);
                    return;
                }
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"client_{clientId}");
            _logger.LogInformation("Client {ClientId} subscribed with connection {ConnectionId}",
                clientId, Context.ConnectionId);
        }

        /// <summary>
        /// Client can call this to unsubscribe
        /// </summary>
        public async Task UnsubscribeFromNotifications(string clientId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"client_{clientId}");
            _logger.LogInformation("Client {ClientId} unsubscribed", clientId);
        }
    }
}
