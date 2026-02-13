using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApi.Services;
using Business.Queries;

namespace WebApi.Pages
{
    public class IndexModel : PageModel
    {
        private readonly CQRSClient _cqrsClient;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            CQRSClient cqrsClient,
            ILogger<IndexModel> logger)
        {
            _cqrsClient = cqrsClient;
            _logger = logger;
        }

        public int UsersCount { get; set; }
        public int DevicesCount { get; set; }
        public int AlertsCount { get; set; }
        public int PhishingWebsitesCount { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            try
            {
                _logger.LogInformation("Loading dashboard via CQRS (NetMQ)");
                
                // Send query to Business layer via NetMQ
                var query = new GetDashboardStatsQuery();
                var result = await _cqrsClient.SendQueryAsync<GetDashboardStatsQueryResult>(query);

                if (result.Success)
                {
                    UsersCount = result.UsersCount;
                    DevicesCount = result.DevicesCount;
                    AlertsCount = result.AlertsCount24h;
                    PhishingWebsitesCount = result.PhishingWebsitesCount;
                    
                    _logger.LogInformation("Dashboard loaded: {Users} users, {Devices} devices, {Alerts} alerts",
                        UsersCount, DevicesCount, AlertsCount);
                }
                else
                {
                    ErrorMessage = result.Message;
                    _logger.LogError("Dashboard query failed: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard");
                ErrorMessage = $"Error loading dashboard: {ex.Message}";
                UsersCount = 0;
                DevicesCount = 0;
                AlertsCount = 0;
                PhishingWebsitesCount = 0;
            }
        }
    }
}
