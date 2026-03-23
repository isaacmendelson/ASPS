using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApi.Services;
using Business.Queries;
using Business.Commands;
using Common.Entities;
using Common.Enums;
using Common.Models;

namespace WebApi.Pages.Users
{
    public class DetailsModel : PageModel
    {
        private readonly ICQRSClient _cqrsClient;
        private readonly ILogger<DetailsModel> _logger;

        public DetailsModel(ICQRSClient cqrsClient, ILogger<DetailsModel> logger)
        {
            _cqrsClient = cqrsClient;
            _logger = logger;
        }

        public User? User { get; set; }
        public List<UserDevice> Devices { get; set; } = new();
        public List<DeviceAlertEntity> RecentAlerts { get; set; } = new();
        public string? ErrorMessage { get; set; }

        // Device form fields
        [BindProperty]
        public string DeviceUid { get; set; } = string.Empty;
        [BindProperty]
        public DeviceType DeviceType { get; set; }
        [BindProperty]
        public OperatingSystemType DeviceOS { get; set; }
        [BindProperty]
        public string DeviceMake { get; set; } = string.Empty;
        [BindProperty]
        public string DeviceModel { get; set; } = string.Empty;
        [BindProperty]
        public string? DevicePhoneNumber { get; set; }

        public async Task<IActionResult> OnGetAsync(string? key)
        {
            if (string.IsNullOrEmpty(key))
            {
                ErrorMessage = "User key is required";
                return Page();
            }

            await LoadUserDetailsAsync(key);
            return Page();
        }

        public async Task<IActionResult> OnPostCreateDeviceAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                ErrorMessage = "User key is required";
                return Page();
            }

            try
            {
                _logger.LogInformation("Creating device for user {Key}: {DeviceUid}", key, DeviceUid);

                var command = new CreateUserDeviceCommand
                {
                    UserKey = new Key("User", key),
                    DeviceUid = DeviceUid,
                    DeviceType = DeviceType,
                    OperatingSystem = DeviceOS,
                    Make = DeviceMake,
                    Model = DeviceModel,
                    PhoneNumber = DevicePhoneNumber
                };

                var result = await _cqrsClient.SendCommandAsync<CreateUserDeviceCommandResult>(command);

                if (result.Success)
                {
                    _logger.LogInformation("Device created successfully for user {Key}", key);
                    return RedirectToPage("/Users/Details", new { key, success = "Device added successfully" });
                }
                else
                {
                    ErrorMessage = result.Message;
                    _logger.LogError("Failed to create device: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating device");
                ErrorMessage = $"Error creating device: {ex.Message}";
            }

            await LoadUserDetailsAsync(key);
            return Page();
        }

        private async Task LoadUserDetailsAsync(string key)
        {
            try
            {
                _logger.LogInformation("Loading user details for key: {Key}", key);

                // Get user details
                var userQuery = new GetUserByKeyQuery
                {
                    UserKey = new Key("User", key)
                };

                var userResult = await _cqrsClient.SendQueryAsync<GetUserByKeyQueryResult>(userQuery);

                if (userResult.Success && userResult.User != null)
                {
                    User = userResult.User;

                    // Get user's devices
                    var devicesQuery = new GetDevicesByUserQuery
                    {
                        UserKey = new Key("User", key)
                    };

                    var devicesResult = await _cqrsClient.SendQueryAsync<GetDevicesByUserQueryResult>(devicesQuery);

                    if (devicesResult.Success)
                    {
                        Devices = devicesResult.Devices;
                        _logger.LogInformation("Loaded user with {DeviceCount} devices", Devices.Count);
                    }

                    // Get recent alerts for this user (last 7 days)
                    var alertsQuery = new GetRecentAlertsQuery
                    {
                        Hours = 24 * 7  // 7 days
                    };

                    var alertsResult = await _cqrsClient.SendQueryAsync<GetRecentAlertsQueryResult>(alertsQuery);

                    if (alertsResult.Success)
                    {
                        // Filter alerts to only those from this user's devices
                        var deviceUids = Devices.Select(d => d.DeviceUid).ToHashSet();
                        RecentAlerts = alertsResult.Alerts
                            .Where(a => deviceUids.Contains(a.DeviceUid))
                            .OrderByDescending(a => a.Timestamp)
                            .ToList();

                        _logger.LogInformation("Loaded {AlertCount} recent alerts for user", RecentAlerts.Count);
                    }
                }
                else
                {
                    ErrorMessage = userResult.Message ?? "User not found";
                    _logger.LogWarning("User not found: {Key}", key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user details");
                ErrorMessage = $"Error loading user: {ex.Message}";
            }
        }
    }
}
