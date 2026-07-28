using Microsoft.AspNetCore.SignalR;
using WebApi.Security;

namespace WebApi.Hubs
{
    /// <summary>
    /// SignalR Hub for sending real-time notifications to connected clients.
    /// Connections are authorized by NotificationsHubPolicy before the hub runs.
    /// Notification-group membership is derived only from trusted claims.
    /// </summary>
    public class NotificationsHub : Hub
    {
        private readonly ILogger<NotificationsHub> _logger;
        public NotificationsHub(ILogger<NotificationsHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var deviceUid = Context.User?.FindFirst(HubClaimTypes.DeviceUid)?.Value;
            _logger.LogInformation(
                "Authorized SignalR client connected: {ConnectionId}, device {DeviceUid}",
                Context.ConnectionId,
                deviceUid ?? "admin");

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
            var requestedGroup = $"client_{clientId}";
            var allowedGroups = Context.User?
                .FindAll(HubClaimTypes.NotificationGroup)
                .Select(claim => claim.Value);

            if (allowedGroups == null ||
                !allowedGroups.Contains(requestedGroup, StringComparer.Ordinal))
            {
                _logger.LogWarning(
                    "SignalR client {ConnectionId} attempted to subscribe to unauthorized group {Group}",
                    Context.ConnectionId,
                    requestedGroup);
                throw new HubException("Not authorized for the requested notification group");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, requestedGroup);
            _logger.LogInformation("Client {ClientId} subscribed with connection {ConnectionId}",
                clientId, Context.ConnectionId);
        }

        /// <summary>
        /// Client can call this to unsubscribe
        /// </summary>
        public async Task UnsubscribeFromNotifications(string clientId)
        {
            var requestedGroup = $"client_{clientId}";
            var isAllowed = Context.User?
                .FindAll(HubClaimTypes.NotificationGroup)
                .Any(claim => string.Equals(
                    claim.Value,
                    requestedGroup,
                    StringComparison.Ordinal)) == true;

            if (!isAllowed)
            {
                throw new HubException(
                    "Not authorized for the requested notification group");
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, requestedGroup);
            _logger.LogInformation("Client {ClientId} unsubscribed", clientId);
        }
    }
}
