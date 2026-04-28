using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApi.Services;
using Business.Commands;
using Business.Queries;
using System.ComponentModel.DataAnnotations;

namespace WebApi.Pages.Roadmaps
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

        public List<RoadmapListItem> Roadmaps { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool IncludeArchived { get; set; } = false;

        [BindProperty]
        public CreateInput Input { get; set; } = new();

        public class CreateInput
        {
            [Required(ErrorMessage = "שם הוא שדה חובה")]
            [StringLength(100, ErrorMessage = "השם לא יכול לעלות על 100 תווים")]
            public string Name { get; set; } = string.Empty;

            [StringLength(500, ErrorMessage = "התיאור לא יכול לעלות על 500 תווים")]
            public string? Description { get; set; }
        }

        private string? CurrentUser =>
            User.FindFirst("preferred_username")?.Value ?? User.Identity?.Name;

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadRoadmapsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostCreateAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadRoadmapsAsync();
                return Page();
            }
            try
            {
                var cmd = new CreateRoadmapCommand
                {
                    Name = Input.Name.Trim(),
                    Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim(),
                    CreatedBy = CurrentUser,
                };
                var result = await _cqrsClient.SendCommandAsync<CreateRoadmapCommandResult>(cmd);
                if (result.Success)
                {
                    _logger.LogInformation("Roadmap created (Id={Id}) by {User}", result.RoadmapId, CurrentUser);
                    return RedirectToPage("Edit", new { id = result.RoadmapId });
                }
                ErrorMessage = result.Message ?? "יצירה נכשלה";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating roadmap");
                ErrorMessage = $"שגיאה ביצירה: {ex.Message}";
            }
            await LoadRoadmapsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostArchiveAsync(int id)
        {
            try
            {
                var cmd = new ArchiveRoadmapCommand { Id = id, UpdatedBy = CurrentUser };
                var result = await _cqrsClient.SendCommandAsync<ArchiveRoadmapCommandResult>(cmd);
                if (result.Success)
                {
                    _logger.LogInformation("Roadmap archived (Id={Id}) by {User}", id, CurrentUser);
                    SuccessMessage = "השקף הועבר לארכיון";
                }
                else
                {
                    ErrorMessage = result.Message;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving roadmap {Id}", id);
                ErrorMessage = $"שגיאה: {ex.Message}";
            }
            await LoadRoadmapsAsync();
            return Page();
        }

        private async Task LoadRoadmapsAsync()
        {
            try
            {
                var query = new ListRoadmapsQuery { IncludeArchived = IncludeArchived };
                var result = await _cqrsClient.SendQueryAsync<ListRoadmapsQueryResult>(query);
                if (result.Success)
                {
                    Roadmaps = result.Roadmaps;
                }
                else
                {
                    ErrorMessage = result.Message;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing roadmaps");
                ErrorMessage = $"שגיאה בטעינת רשימה: {ex.Message}";
            }
        }
    }
}
