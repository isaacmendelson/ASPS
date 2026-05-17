using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApi.Services;
using Business.Queries;
using Business.Commands;
using Common.Entities;

namespace WebApi.Pages.TrackedDomains
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

        public List<TrackedDomain> TrackedDomains { get; set; } = new();
        public string? ErrorMessage { get; set; }

        [BindProperty(SupportsGet = true, Name = "page")]
        public int CurrentPage { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 50;

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Category { get; set; }

        public int TotalCount { get; set; }
        public int TotalPages { get; set; }

        // ─── Add-tracked-domain form (ASPS-371) ───────────────────────────
        [BindProperty]
        public string NewDomain { get; set; } = string.Empty;

        [BindProperty]
        public string NewCategory { get; set; } = "Manual";

        [BindProperty]
        public string? NewUserKey { get; set; }

        [BindProperty]
        public int NewTrackMode { get; set; } = 1; // 0=None,1=Surf,2=Click

        [BindProperty]
        public string? NewReason { get; set; }

        public string? StatusMessage { get; set; }

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        public async Task<IActionResult> OnPostAddAsync()
        {
            if (string.IsNullOrWhiteSpace(NewDomain))
            {
                ErrorMessage = "Domain is required.";
                await LoadAsync();
                return Page();
            }

            try
            {
                var command = new AddTrackedDomainCommand
                {
                    Domain = NewDomain.Trim(),
                    Category = string.IsNullOrWhiteSpace(NewCategory) ? "Manual" : NewCategory.Trim(),
                    UserKeyField = NewUserKey?.Trim() ?? string.Empty,
                    TrackMode = NewTrackMode,
                    Reason = NewReason?.Trim()
                };

                var result = await _cqrsClient.SendCommandAsync<AddTrackedDomainCommandResult>(command);

                if (result.Success)
                {
                    _logger.LogInformation("Tracked domain added via admin: {Domain} (ID {Id})",
                        command.Domain, result.TrackedDomainId);
                    return RedirectToPage("Index", new { addedOk = result.Message });
                }

                ErrorMessage = result.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding tracked domain {Domain}", NewDomain);
                ErrorMessage = $"Error adding tracked domain: {ex.Message}";
            }

            await LoadAsync();
            return Page();
        }

        [BindProperty(SupportsGet = true)]
        public string? AddedOk { get; set; }

        private async Task LoadAsync()
        {
            try
            {
                if (!string.IsNullOrEmpty(AddedOk))
                    StatusMessage = AddedOk;

                if (CurrentPage < 1) CurrentPage = 1;

                _logger.LogInformation("Loading tracked domains via CQRS (Page={Page}, Search={Search}, Category={Category})",
                    CurrentPage, Search, Category);

                var query = new GetAllTrackedDomainsQuery
                {
                    Page = CurrentPage,
                    PageSize = PageSize,
                    Search = Search,
                    Category = Category
                };
                var result = await _cqrsClient.SendQueryAsync<GetAllTrackedDomainsQueryResult>(query);

                if (result.Success)
                {
                    TrackedDomains = result.TrackedDomains;
                    TotalCount = result.TotalCount;
                    TotalPages = (int)Math.Ceiling((double)result.TotalCount / result.PageSize);
                    if (TotalPages < 1) TotalPages = 1;
                    _logger.LogInformation("Tracked domains loaded: {Count} of {Total}", TrackedDomains.Count, TotalCount);
                }
                else
                {
                    ErrorMessage = result.Message;
                    _logger.LogError("Failed to load tracked domains: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading tracked domains");
                ErrorMessage = $"Error loading tracked domains: {ex.Message}";
            }
        }
    }
}
