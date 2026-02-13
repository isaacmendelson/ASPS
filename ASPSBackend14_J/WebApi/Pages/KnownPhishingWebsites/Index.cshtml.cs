using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApi.Services;
using Business.Queries;
using Common.Entities;

namespace WebApi.Pages.KnownPhishingWebsites
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

        public List<KnownPhishingWebsite> PhishingWebsites { get; set; } = new();
        public string? ErrorMessage { get; set; }

        [BindProperty(SupportsGet = true, Name = "page")]
        public int CurrentPage { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 50;

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        public int TotalCount { get; set; }
        public int TotalPages { get; set; }

        public async Task OnGetAsync()
        {
            try
            {
                if (CurrentPage < 1) CurrentPage = 1;

                _logger.LogInformation("Loading known phishing websites via CQRS (Page={Page}, Search={Search})", CurrentPage, Search);

                var query = new GetAllPhishingWebsitesQuery
                {
                    Page = CurrentPage,
                    PageSize = PageSize,
                    Search = Search
                };
                var result = await _cqrsClient.SendQueryAsync<GetAllPhishingWebsitesQueryResult>(query);

                if (result.Success)
                {
                    PhishingWebsites = result.PhishingWebsites;
                    TotalCount = result.TotalCount;
                    TotalPages = (int)Math.Ceiling((double)result.TotalCount / result.PageSize);
                    if (TotalPages < 1) TotalPages = 1;
                    _logger.LogInformation("Phishing websites loaded: {Count} of {Total}", PhishingWebsites.Count, TotalCount);
                }
                else
                {
                    ErrorMessage = result.Message;
                    _logger.LogError("Failed to load phishing websites: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading phishing websites");
                ErrorMessage = $"Error loading phishing websites: {ex.Message}";
            }
        }
    }
}
