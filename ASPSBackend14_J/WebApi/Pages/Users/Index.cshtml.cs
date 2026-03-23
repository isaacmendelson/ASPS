using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApi.Services;
using Business.Queries;
using Business.Commands;
using Common.Enums;

namespace WebApi.Pages.Users
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

        public List<UserWithDeviceCount> Users { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        // Create User form fields
        [BindProperty]
        public string FirstName { get; set; } = string.Empty;
        [BindProperty]
        public string LastName { get; set; } = string.Empty;
        [BindProperty]
        public string Email { get; set; } = string.Empty;
        [BindProperty]
        public string PhoneNumber { get; set; } = string.Empty;
        [BindProperty]
        public string Address { get; set; } = string.Empty;
        [BindProperty]
        public string City { get; set; } = string.Empty;
        [BindProperty]
        public string? State { get; set; }
        [BindProperty]
        public string Zip { get; set; } = string.Empty;
        [BindProperty]
        public string Country { get; set; } = string.Empty;
        [BindProperty]
        public UserRole Role { get; set; }
        [BindProperty]
        public string? Locale { get; set; }
        [BindProperty]
        public int? Timezone { get; set; }

        public async Task OnGetAsync()
        {
            try
            {
                _logger.LogInformation("Loading users via CQRS");

                var query = new GetUsersWithDeviceCountsQuery { Search = Search };
                var result = await _cqrsClient.SendQueryAsync<GetUsersWithDeviceCountsQueryResult>(query);

                if (result.Success)
                {
                    Users = result.Users;
                    _logger.LogInformation("Users loaded: {Count}", Users.Count);
                }
                else
                {
                    ErrorMessage = result.Message;
                    _logger.LogError("Failed to load users: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading users");
                ErrorMessage = $"Error loading users: {ex.Message}";
            }
        }

        public async Task<IActionResult> OnPostCreateAsync()
        {
            try
            {
                _logger.LogInformation("Creating user via CQRS: {FirstName} {LastName}", FirstName, LastName);

                var command = new CreateUserAdminCommand
                {
                    FirstName = FirstName,
                    LastName = LastName,
                    Email = Email,
                    PhoneNumber = PhoneNumber,
                    Address = Address,
                    City = City,
                    State = State,
                    Zip = Zip,
                    Country = Country,
                    Role = Role,
                    Locale = Locale,
                    Timezone = Timezone
                };

                var result = await _cqrsClient.SendCommandAsync<CreateUserAdminCommandResult>(command);

                if (result.Success)
                {
                    _logger.LogInformation("User created successfully");
                    return RedirectToPage("/Users/Index", new { success = "User created successfully" });
                }
                else
                {
                    ErrorMessage = result.Message;
                    _logger.LogError("Failed to create user: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                ErrorMessage = $"Error creating user: {ex.Message}";
            }

            await OnGetAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(string key)
        {
            try
            {
                _logger.LogInformation("Deleting user via CQRS: {Key}", key);

                var command = new DeleteUserCommand
                {
                    UserKey = new Common.Models.Key("User", key)
                };

                var result = await _cqrsClient.SendCommandAsync<DeleteUserCommandResult>(command);

                if (result.Success)
                {
                    SuccessMessage = "User deleted successfully";
                    _logger.LogInformation("User deleted: {Key}", key);
                }
                else
                {
                    ErrorMessage = result.Message;
                    _logger.LogError("Failed to delete user: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user");
                ErrorMessage = $"Error deleting user: {ex.Message}";
            }

            // Reload users
            await OnGetAsync();
            return Page();
        }
    }
}
