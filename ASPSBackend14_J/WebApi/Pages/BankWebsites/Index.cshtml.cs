using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApi.Services;
using Business.Queries;
using Common.Entities;

namespace WebApi.Pages.BankWebsites
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

        public List<BankWebsite> BankWebsites { get; set; } = new();
        public string? ErrorMessage { get; set; }

        [BindProperty(SupportsGet = true, Name = "page")]
        public int CurrentPage { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 50;

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool? IsActive { get; set; }

        public int TotalCount { get; set; }
        public int TotalPages { get; set; }

        public async Task OnGetAsync()
        {
            try
            {
                if (CurrentPage < 1) CurrentPage = 1;

                _logger.LogInformation("Loading bank websites via CQRS (Page={Page}, Search={Search}, IsActive={IsActive})", CurrentPage, Search, IsActive);

                var query = new GetAllBankWebsitesQuery
                {
                    Page = CurrentPage,
                    PageSize = PageSize,
                    Search = Search,
                    IsActive = IsActive
                };
                var result = await _cqrsClient.SendQueryAsync<GetAllBankWebsitesQueryResult>(query);

                if (result.Success)
                {
                    BankWebsites = result.BankWebsites;
                    TotalCount = result.TotalCount;
                    TotalPages = (int)Math.Ceiling((double)result.TotalCount / result.PageSize);
                    if (TotalPages < 1) TotalPages = 1;
                    _logger.LogInformation("Bank websites loaded: {Count} of {Total}", BankWebsites.Count, TotalCount);
                }
                else
                {
                    ErrorMessage = result.Message;
                    _logger.LogError("Failed to load bank websites: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading bank websites");
                ErrorMessage = $"Error loading bank websites: {ex.Message}";
            }
        }
    }
}
