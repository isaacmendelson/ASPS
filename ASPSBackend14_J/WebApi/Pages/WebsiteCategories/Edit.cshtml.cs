using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApi.Services;
using Business.Commands;
using Business.Queries;
using Common.Entities;
using System.ComponentModel.DataAnnotations;

namespace WebApi.Pages.WebsiteCategories
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
        public string Name { get; set; } = string.Empty;

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public WebsiteCategory? Category { get; set; }
        public List<WebsiteCategory> AllParents { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public class InputModel
        {
            public string? ParentId { get; set; }

            [StringLength(100, ErrorMessage = "Source cannot exceed 100 characters")]
            public string? Source { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                return RedirectToPage("Index");
            }

            await LoadCategoryAsync();
            await LoadParentCategoriesAsync();

            if (Category == null)
            {
                ErrorMessage = "Category not found.";
                return Page();
            }

            // Pre-fill form with existing data
            Input.ParentId = Category.ParentId;
            Input.Source = Category.Source;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadCategoryAsync();
                await LoadParentCategoriesAsync();
                return Page();
            }

            try
            {
                var command = new UpdateWebsiteCategoryCommand
                {
                    Name = Name,
                    ParentId = string.IsNullOrWhiteSpace(Input.ParentId) ? null : Input.ParentId,
                    Source = string.IsNullOrWhiteSpace(Input.Source) ? null : Input.Source?.Trim()
                };

                var result = await _cqrsClient.SendCommandAsync<UpdateWebsiteCategoryCommandResult>(command);

                if (result.Success)
                {
                    _logger.LogInformation("Website category updated: {Name}", Name);
                    return RedirectToPage("Index");
                }
                else
                {
                    ErrorMessage = result.Message;
                    _logger.LogWarning("Failed to update website category: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating website category");
                ErrorMessage = $"Error updating website category: {ex.Message}";
            }

            await LoadCategoryAsync();
            await LoadParentCategoriesAsync();
            return Page();
        }

        private async Task LoadCategoryAsync()
        {
            try
            {
                var query = new GetWebsiteCategoryByNameQuery { Name = Name };
                var result = await _cqrsClient.SendQueryAsync<GetWebsiteCategoryByNameQueryResult>(query);

                if (result.Success)
                {
                    Category = result.Category;
                }
                else
                {
                    _logger.LogWarning("Failed to load category: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading category");
            }
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
