using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApi.Services;
using Business.Queries;
using Common.Entities;

namespace WebApi.Pages.DeviceAlerts
{
    public class AlertWithInfo
    {
        public DeviceAlertEntity Alert { get; set; } = null!;
        public string? UserName { get; set; }
        public string? DeviceName { get; set; }
    }

    public class IndexModel : PageModel
    {
        private readonly ICQRSClient _cqrsClient;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ICQRSClient cqrsClient, ILogger<IndexModel> logger)
        {
            _cqrsClient = cqrsClient;
            _logger = logger;
        }

        public List<AlertWithInfo> AlertsWithInfo { get; set; } = new();
        public int TimeFilter { get; set; } = 24;
        public string? ErrorMessage { get; set; }

        [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        public async Task OnGetAsync(int? hours)
        {
            TimeFilter = hours ?? 24;

            try
            {
                _logger.LogInformation("Loading device alerts via CQRS (last {Hours} hours)", TimeFilter);

                var query = new GetRecentAlertsQuery { Hours = TimeFilter, Search = Search };
                var result = await _cqrsClient.SendQueryAsync<GetRecentAlertsQueryResult>(query);

                if (result.Success)
                {
                    // Load user and device info for each alert
                    foreach (var alert in result.Alerts)
                    {
                        var alertInfo = new AlertWithInfo { Alert = alert };

                        // Get user name
                        if (!string.IsNullOrEmpty(alert.UserKeyField))
                        {
                            try
                            {
                                var userQuery = new GetUserByKeyQuery
                                {
                                    UserKey = new Common.Models.Key("User", alert.UserKeyField)
                                };
                                var userResult = await _cqrsClient.SendQueryAsync<GetUserByKeyQueryResult>(userQuery);
                                if (userResult.Success && userResult.User != null)
                                {
                                    alertInfo.UserName = $"{userResult.User.FirstName} {userResult.User.LastName}";
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to load user for alert");
                            }
                        }

                        AlertsWithInfo.Add(alertInfo);
                    }

                    _logger.LogInformation("Alerts loaded: {Count}", AlertsWithInfo.Count);
                }
                else
                {
                    ErrorMessage = result.Message;
                    _logger.LogError("Failed to load alerts: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading device alerts");
                ErrorMessage = $"Error loading alerts: {ex.Message}";
            }
        }
    }
}
