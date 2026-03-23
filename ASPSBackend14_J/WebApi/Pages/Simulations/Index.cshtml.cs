using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApi.Services;
using Business.Queries;
using Business.Commands;
using Common.Models;

namespace WebApi.Pages.Simulations
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

        public List<Common.Entities.Simulation> Simulations { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        public async Task OnGetAsync()
        {
            try
            {
                _logger.LogInformation("Loading simulations via CQRS");

                var query = new GetSimulationsQuery { SearchText = Search };
                var result = await _cqrsClient.SendQueryAsync<GetSimulationsQueryResult>(query);

                if (result.Success)
                {
                    Simulations = result.Simulations;
                    _logger.LogInformation("Simulations loaded: {Count}", Simulations.Count);
                }
                else
                {
                    ErrorMessage = result.Message;
                    _logger.LogError("Failed to load simulations: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading simulations");
                ErrorMessage = $"Error loading simulations: {ex.Message}";
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(string simulationKey)
        {
            try
            {
                _logger.LogInformation("Deleting simulation: {Key}", simulationKey);

                var command = new DeleteSimulationCommand
                {
                    SimulationKey = new Key("Simulation", simulationKey)
                };

                var result = await _cqrsClient.SendCommandAsync<DeleteSimulationCommandResult>(command);

                if (result.Success)
                {
                    SuccessMessage = "Simulation deleted successfully.";
                    _logger.LogInformation("Simulation deleted: {Key}", simulationKey);
                }
                else
                {
                    ErrorMessage = result.Message;
                    _logger.LogError("Failed to delete simulation: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting simulation");
                ErrorMessage = $"Error deleting simulation: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRunAsync(string simulationKey)
        {
            try
            {
                _logger.LogInformation("Running simulation: {Key}", simulationKey);

                // TODO: Get current user key from session/authentication
                var requestorKey = new Key("User", "admin-user-key"); 

                var command = new RunSimulationCommand
                {
                    SimulationKey = new Key("Simulation", simulationKey),
                    RequestorKey = requestorKey
                };

                var result = await _cqrsClient.SendCommandAsync<RunSimulationCommandResult>(command);

                if (result.Success)
                {
                    SuccessMessage = $"Simulation started successfully. {result.ExecutedSteps}/{result.TotalSteps} steps executed.";
                    _logger.LogInformation("Simulation run completed: {Key}", simulationKey);
                }
                else
                {
                    ErrorMessage = result.Message;
                    _logger.LogError("Failed to run simulation: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running simulation");
                ErrorMessage = $"Error running simulation: {ex.Message}";
            }

            return RedirectToPage();
        }
    }
}
