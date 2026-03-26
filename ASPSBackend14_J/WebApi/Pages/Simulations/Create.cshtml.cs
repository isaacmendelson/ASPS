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
    public class CreateModel : PageModel
    {
        private readonly ICQRSClient _cqrsClient;
        private readonly ILogger<CreateModel> _logger;

        public CreateModel(ICQRSClient cqrsClient, ILogger<CreateModel> logger)
        {
            _cqrsClient = cqrsClient;
            _logger = logger;
        }

        [BindProperty]
        public string Name { get; set; } = string.Empty;

        [BindProperty]
        public string Description { get; set; } = string.Empty;

        [BindProperty]
        public string StepsJson { get; set; } = "[]";

        public string? ErrorMessage { get; set; }
        public List<SimulationUserDto> AvailableUsers { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadAvailableUsers();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                _logger.LogInformation("Creating simulation: {Name}", Name);

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

                // Get current user from authentication
                var keycloakId = User.FindFirst("sub")?.Value ?? User.FindFirst("preferred_username")?.Value;
                if (string.IsNullOrEmpty(keycloakId))
                {
                    ErrorMessage = "User not authenticated";
                    await LoadAvailableUsers();
                    return Page();
                }

                var userQuery = new GetUserByKeycloakIdQuery { KeycloakUserId = keycloakId };
                var userResult = await _cqrsClient.SendQueryAsync<GetUserByKeycloakIdQueryResult>(userQuery);
                
                if (userResult?.User == null)
                {
                    _logger.LogWarning("Creator user not found: {KeycloakId}", keycloakId);
                    ErrorMessage = "Your user account was not found in the system";
                    await LoadAvailableUsers();
                    return Page();
                }

                var creatorKey = userResult.User.Key;

                var command = new CreateSimulationCommand
                {
                    Name = Name,
                    Description = Description ?? string.Empty,
                    CreatorKey = creatorKey,
                    Steps = steps
                };

                var result = await _cqrsClient.SendCommandAsync<CreateSimulationCommandResult>(command);

                if (result.Success)
                {
                    _logger.LogInformation("Simulation created successfully: {Key}", result.SimulationKey?.Value);
                    return RedirectToPage("Index");
                }
                else
                {
                    ErrorMessage = result.Message;
                    _logger.LogError("Failed to create simulation: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating simulation");
                ErrorMessage = $"Error creating simulation: {ex.Message}";
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
