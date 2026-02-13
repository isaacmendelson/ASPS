using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApi.Services;
using Business.Queries;
using Common.Entities;
using Common.Models;

namespace WebApi.Pages.DeviceAlerts
{
    public class DetailsModel : PageModel
    {
        private readonly CQRSClient _cqrsClient;
        private readonly ILogger<DetailsModel> _logger;

        public DetailsModel(CQRSClient cqrsClient, ILogger<DetailsModel> logger)
        {
            _cqrsClient = cqrsClient;
            _logger = logger;
        }

        public DeviceAlertEntity? Alert { get; set; }
        public string? UserName { get; set; }
        public string? DeviceName { get; set; }
        public string? ErrorMessage { get; set; }
        public AnalysisResultContainer? AnalysisResult { get; set; }

        public async Task<IActionResult> OnGetAsync(string? key)
        {
            if (string.IsNullOrEmpty(key))
            {
                ErrorMessage = "Alert key is required";
                return Page();
            }

            try
            {
                _logger.LogInformation("Loading alert details for key: {Key}", key);

                // Get alert details
                var alertQuery = new GetAlertByKeyQuery
                {
                    AlertKey = new Key("DeviceAlert", key)
                };

                var alertResult = await _cqrsClient.SendQueryAsync<GetAlertByKeyQueryResult>(alertQuery);

                if (alertResult.Success && alertResult.Alert != null)
                {
                    Alert = alertResult.Alert;

                    // Get user name if alert has user
                    if (!string.IsNullOrEmpty(Alert.UserKeyField))
                    {
                        var userQuery = new GetUserByKeyQuery
                        {
                            UserKey = new Key("User", Alert.UserKeyField)
                        };

                        var userResult = await _cqrsClient.SendQueryAsync<GetUserByKeyQueryResult>(userQuery);

                        if (userResult.Success && userResult.User != null)
                        {
                            UserName = $"{userResult.User.FirstName} {userResult.User.LastName}";
                        }
                    }

                    // Get device name if alert has device
                    if (!string.IsNullOrEmpty(Alert.DeviceKeyField))
                    {
                        var deviceQuery = new GetDeviceByKeyQuery
                        {
                            DeviceKey = new Key("UserDevice", Alert.DeviceKeyField)
                        };

                        var deviceResult = await _cqrsClient.SendQueryAsync<GetDeviceByKeyQueryResult>(deviceQuery);

                        if (deviceResult.Success && deviceResult.Device != null)
                        {
                            DeviceName = $"{deviceResult.Device.Make} {deviceResult.Device.Model}";
                        }
                    }

                    // Get analysis result for this alert
                    try
                    {
                        var arQuery = new GetAnalysisResultByAlertKeyQuery
                        {
                            DeviceAlertKeyField = Alert.KeyField
                        };
                        var arResult = await _cqrsClient.SendQueryAsync<GetAnalysisResultByAlertKeyQueryResult>(arQuery);
                        if (arResult.Success && arResult.AnalysisResult != null)
                        {
                            AnalysisResult = arResult.AnalysisResult;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load analysis result for alert");
                    }

                    _logger.LogInformation("Loaded alert: {AlertType}", Alert.AlertType);
                }
                else
                {
                    ErrorMessage = alertResult.Message ?? "Alert not found";
                    _logger.LogWarning("Alert not found: {Key}", key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading alert details");
                ErrorMessage = $"Error loading alert: {ex.Message}";
            }

            return Page();
        }
    }
}
