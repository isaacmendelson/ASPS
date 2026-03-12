using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApi.Services;
using Business.Queries;
using Common.Entities;

namespace WebApi.Pages.TrackedDomains
{
    public class IndexModel : PageModel
    {
        private readonly CQRSClient _cqrsClient;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(CQRSClient cqrsClient, ILogger<IndexModel> logger)
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

        public async Task OnGetAsync()
        {
            try
            {
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
