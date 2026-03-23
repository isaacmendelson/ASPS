using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApi.Services;
using Business.Queries;

namespace WebApi.Pages.Devices
{
    public class IndexModel : PageModel
    {
        private readonly ICQRSClient _cqrsClient;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ICQRSClient cqrsClient, ILogger<IndexModel> logger)
        {
            _cqrsClient = cqrsClient;
            _logger = logger;
        }

        public List<DeviceWithUser> Devices { get; set; } = new();
        public string? ErrorMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        public async Task OnGetAsync()
        {
            try
            {
                _logger.LogInformation("Loading devices via CQRS");

                var query = new GetAllDevicesQuery { Search = Search };
                var result = await _cqrsClient.SendQueryAsync<GetAllDevicesQueryResult>(query);

                if (result.Success)
                {
                    Devices = result.Devices;
                    _logger.LogInformation("Devices loaded: {Count}", Devices.Count);
                }
                else
                {
                    ErrorMessage = result.Message;
                    _logger.LogError("Failed to load devices: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading devices");
                ErrorMessage = $"Error loading devices: {ex.Message}";
            }
        }
    }
}
