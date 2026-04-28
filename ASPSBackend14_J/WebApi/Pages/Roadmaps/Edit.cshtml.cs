using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApi.Services;
using Business.Commands;
using Business.Queries;
using System.ComponentModel.DataAnnotations;

namespace WebApi.Pages.Roadmaps
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
        public int Id { get; set; }

        public GetRoadmapByIdQueryResult? Roadmap { get; set; }
        public string? ErrorMessage { get; set; }

        private string? CurrentUser =>
            User.FindFirst("preferred_username")?.Value ?? User.Identity?.Name;

        public async Task<IActionResult> OnGetAsync()
        {
            if (Id <= 0) return RedirectToPage("Index");

            try
            {
                var query = new GetRoadmapByIdQuery { Id = Id };
                var result = await _cqrsClient.SendQueryAsync<GetRoadmapByIdQueryResult>(query);
                if (!result.Success)
                {
                    ErrorMessage = result.Message ?? "Roadmap not found";
                    return Page();
                }
                Roadmap = result;
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading roadmap {Id}", Id);
                ErrorMessage = $"שגיאה בטעינה: {ex.Message}";
                return Page();
            }
        }

        // ---------- AJAX endpoints used by the SPA frontend (Phase 4) ----------

        public class SaveDto
        {
            [Required]
            public int Id { get; set; }
            [Required]
            public int ExpectedVersion { get; set; }
            [Required]
            public string Data { get; set; } = "{}";
        }

        public async Task<IActionResult> OnPostSaveAsync([FromBody] SaveDto body)
        {
            if (body is null || body.Id != Id)
            {
                return new JsonResult(new { success = false, message = "Invalid request" })
                    { StatusCode = 400 };
            }
            try
            {
                var cmd = new SaveRoadmapCommand
                {
                    Id = body.Id,
                    ExpectedVersion = body.ExpectedVersion,
                    Data = body.Data,
                    UpdatedBy = CurrentUser,
                };
                var result = await _cqrsClient.SendCommandAsync<SaveRoadmapCommandResult>(cmd);
                return new JsonResult(new
                {
                    success = result.Success,
                    message = result.Message,
                    newVersion = result.NewVersion,
                    lastUpdatedAt = result.LastUpdatedAt,
                    lastUpdatedBy = result.Success ? CurrentUser : null,
                    concurrencyConflict = result.ConcurrencyConflict,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving roadmap {Id}", Id);
                return new JsonResult(new { success = false, message = ex.Message })
                    { StatusCode = 500 };
            }
        }

        public class MetadataDto
        {
            [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
            [StringLength(500)] public string? Description { get; set; }
        }

        public async Task<IActionResult> OnPostMetadataAsync([FromBody] MetadataDto body)
        {
            if (body is null || string.IsNullOrWhiteSpace(body.Name))
            {
                return new JsonResult(new { success = false, message = "Name required" })
                    { StatusCode = 400 };
            }
            try
            {
                var cmd = new UpdateRoadmapMetadataCommand
                {
                    Id = Id,
                    Name = body.Name,
                    Description = body.Description,
                    UpdatedBy = CurrentUser,
                };
                var result = await _cqrsClient.SendCommandAsync<UpdateRoadmapMetadataCommandResult>(cmd);
                return new JsonResult(new { success = result.Success, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating metadata for roadmap {Id}", Id);
                return new JsonResult(new { success = false, message = ex.Message })
                    { StatusCode = 500 };
            }
        }
    }
}
