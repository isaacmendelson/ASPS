using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace WebApi.Hubs
{
    /// <summary>
    /// SignalR Hub for sending real-time notifications to connected clients
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
            _logger.LogInformation($"Client connected: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation($"Client disconnected: {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Client can call this to subscribe to specific notification types
        /// </summary>
        public async Task SubscribeToNotifications(string clientId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"client_{clientId}");
            _logger.LogInformation($"Client {clientId} subscribed with connection {Context.ConnectionId}");
        }

        /// <summary>
        /// Client can call this to unsubscribe
        /// </summary>
        public async Task UnsubscribeFromNotifications(string clientId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"client_{clientId}");
            _logger.LogInformation($"Client {clientId} unsubscribed");
        }
    }
}
