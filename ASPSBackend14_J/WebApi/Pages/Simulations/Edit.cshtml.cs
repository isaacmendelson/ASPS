using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApi.Services;
using Business.Commands;
using Business.Queries;
using Common.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebApi.Pages.Simulations
{
    public class EditModel : PageModel
    {
        private readonly ICQRSClient _cqrsClient;
        private readonly ILogger<EditModel> _logger;

        public EditModel(ICQRSClient cqrsClient, ILogger<EditModel> logger)
        {
            _cqrsClient = cqrsClient;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public string Key { get; set; } = string.Empty;

        [BindProperty]
        public string Name { get; set; } = string.Empty;

        [BindProperty]
        public string Description { get; set; } = string.Empty;

        [BindProperty]
        public string StepsJson { get; set; } = "[]";

        public string? ErrorMessage { get; set; }
        public List<SimulationUserDto> AvailableUsers { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(Key))
            {
                return RedirectToPage("Index");
            }

            try
            {
                _logger.LogInformation("Loading simulation for edit: {Key}", Key);

                var query = new GetSimulationDetailsQuery
                {
                    SimulationKey = new Key("Simulation", Key)
                };

                var result = await _cqrsClient.SendQueryAsync<GetSimulationDetailsQueryResult>(query);

                if (result.Success && result.Simulation != null)
                {
                    Name = result.Simulation.Name;
                    Description = result.Simulation.Description;
                    StepsJson = result.Simulation.SimulationStepsJson ?? "[]";

                    _logger.LogInformation("Simulation loaded: {Name}", Name);
                }
                else
                {
                    ErrorMessage = result.Message ?? "Simulation not found.";
                    _logger.LogError("Failed to load simulation: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading simulation");
                ErrorMessage = $"Error loading simulation: {ex.Message}";
            }

            await LoadAvailableUsers();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                _logger.LogInformation("Updating simulation: {Key}", Key);

                if (string.IsNullOrWhiteSpace(Name))
                {
                    ErrorMessage = "Simulation name is required.";
                    await LoadAvailableUsers();
                    return Page();
                }

                // Parse steps from JSON
                SimulationStep[] steps = Array.Empty<SimulationStep>();
                if (!string.IsNullOrEmpty(StepsJson))
                {
                    try
                    {
                        var options = new JsonSerializerOptions
                        {
                            Converters = { new JsonStringEnumConverter() },
                            PropertyNameCaseInsensitive = true
                        };
                        steps = JsonSerializer.Deserialize<SimulationStep[]>(StepsJson, options) ?? Array.Empty<SimulationStep>();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to parse steps JSON");
                        ErrorMessage = "Invalid steps JSON format.";
                        await LoadAvailableUsers();
                        return Page();
                    }
                }

                var command = new UpdateSimulationCommand
                {
                    SimulationKey = new Key("Simulation", Key),
                    Name = Name,
                    Description = Description ?? string.Empty,
                    Steps = steps
                };

                var result = await _cqrsClient.SendCommandAsync<UpdateSimulationCommandResult>(command);

                if (result.Success)
                {
                    _logger.LogInformation("Simulation updated successfully: {Key}", Key);
                    return RedirectToPage("Index");
                }
                else
                {
                    ErrorMessage = result.Message;
                    _logger.LogError("Failed to update simulation: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating simulation");
                ErrorMessage = $"Error updating simulation: {ex.Message}";
            }

            await LoadAvailableUsers();
            return Page();
        }

        private async Task LoadAvailableUsers()
        {
            try
            {
                var query = new GetSimulationUsersQuery();
                var result = await _cqrsClient.SendQueryAsync<GetSimulationUsersQueryResult>(query);

                if (result.Success)
                {
                    AvailableUsers = result.Users;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading available users");
            }
        }
    }
}
