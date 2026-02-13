using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApi.Services;
using Business.Queries;
using Common.Entities;
using Common.Models;

namespace WebApi.Pages.Devices
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

        public UserDevice? Device { get; set; }
        public string? OwnerName { get; set; }
        public List<DeviceAlertEntity> RecentAlerts { get; set; } = new();
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string? key, string? uid)
        {
            if (string.IsNullOrEmpty(key) && string.IsNullOrEmpty(uid))
            {
                ErrorMessage = "Device key or UID is required";
                return Page();
            }

            try
            {
                if (!string.IsNullOrEmpty(key))
                {
                    _logger.LogInformation("Loading device details for key: {Key}", key);

                    var deviceQuery = new GetDeviceByKeyQuery
                    {
                        DeviceKey = new Key("UserDevice", key)
                    };

                    var deviceResult = await _cqrsClient.SendQueryAsync<GetDeviceByKeyQueryResult>(deviceQuery);

                    if (deviceResult.Success && deviceResult.Device != null)
                    {
                        Device = deviceResult.Device;
                    }
                    else
                    {
                        ErrorMessage = deviceResult.Message ?? "Device not found";
                        _logger.LogWarning("Device not found: {Key}", key);
                    }
                }
                else
                {
                    _logger.LogInformation("Loading device details for UID: {Uid}", uid);

                    var deviceQuery = new GetDeviceByUidQuery
                    {
                        DeviceUid = uid!
                    };

                    var deviceResult = await _cqrsClient.SendQueryAsync<GetDeviceByUidQueryResult>(deviceQuery);

                    if (deviceResult.Success && deviceResult.Device != null)
                    {
                        Device = deviceResult.Device;
                    }
                    else
                    {
                        ErrorMessage = deviceResult.Message ?? "Device not found";
                        _logger.LogWarning("Device not found by UID: {Uid}", uid);
                    }
                }

                if (Device != null)
                {
                    // Get owner name if device is registered
                    if (!string.IsNullOrEmpty(Device.UserKeyField))
                    {
                        var userQuery = new GetUserByKeyQuery
                        {
                            UserKey = new Key("User", Device.UserKeyField)
                        };

                        var userResult = await _cqrsClient.SendQueryAsync<GetUserByKeyQueryResult>(userQuery);

                        if (userResult.Success && userResult.User != null)
                        {
                            OwnerName = $"{userResult.User.FirstName} {userResult.User.LastName}";
                        }
                    }

                    // Get recent alerts for this device (last 7 days)
                    var alertsQuery = new GetAlertsByDeviceQuery
                    {
                        DeviceUid = Device.DeviceUid,
                        Hours = 24 * 7  // 7 days
                    };

                    var alertsResult = await _cqrsClient.SendQueryAsync<GetAlertsByDeviceQueryResult>(alertsQuery);

                    if (alertsResult.Success)
                    {
                        RecentAlerts = alertsResult.Alerts.OrderByDescending(a => a.Timestamp).ToList();
                        _logger.LogInformation("Loaded device with {AlertCount} recent alerts", RecentAlerts.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading device details");
                ErrorMessage = $"Error loading device: {ex.Message}";
            }

            return Page();
        }
    }
}
