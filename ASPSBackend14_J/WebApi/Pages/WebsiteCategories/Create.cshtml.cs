using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApi.Services;
using Business.Commands;
using Business.Queries;
using Common.Entities;
using System.ComponentModel.DataAnnotations;

namespace WebApi.Pages.WebsiteCategories
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
        public InputModel Input { get; set; } = new();

        public List<WebsiteCategory> AllParents { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Name is required")]
            [StringLength(200, ErrorMessage = "Name cannot exceed 200 characters")]
            public string Name { get; set; } = string.Empty;

            public string? ParentId { get; set; }

            [StringLength(100, ErrorMessage = "Source cannot exceed 100 characters")]
            public string? Source { get; set; }
        }

        public async Task OnGetAsync()
        {
            await LoadParentCategoriesAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadParentCategoriesAsync();
                return Page();
            }

            try
            {
                var command = new CreateWebsiteCategoryCommand
                {
                    Name = Input.Name.Trim(),
                    ParentId = string.IsNullOrWhiteSpace(Input.ParentId) ? null : Input.ParentId,
                    Source = string.IsNullOrWhiteSpace(Input.Source) ? null : Input.Source?.Trim()
                };

                var result = await _cqrsClient.SendCommandAsync<CreateWebsiteCategoryCommandResult>(command);

                if (result.Success)
                {
                    _logger.LogInformation("Website category created: {Name}", Input.Name);
                    return RedirectToPage("Index");
                }
                else
                {
                    ErrorMessage = result.Message;
                    _logger.LogWarning("Failed to create website category: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating website category");
                ErrorMessage = $"Error creating website category: {ex.Message}";
            }

            await LoadParentCategoriesAsync();
            return Page();
        }

        private async Task LoadParentCategoriesAsync()
        {
            try
            {
                var query = new GetParentCategoriesQuery();
                var result = await _cqrsClient.SendQueryAsync<GetParentCategoriesQueryResult>(query);

                if (result.Success)
                {
                    AllParents = result.Parents;
                }
                else
                {
                    _logger.LogWarning("Failed to load parent categories: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading parent categories");
            }
        }
    }
}
