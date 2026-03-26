using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApi.Services;
using Business.Queries;
using Common.Entities;

namespace WebApi.Pages.BlacklistedPhoneNumbers
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

        public List<BlacklistedPhoneNumber> PhoneNumbers { get; set; } = new();
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

                _logger.LogInformation("Loading blacklisted phone numbers via CQRS (Page={Page}, Search={Search})", CurrentPage, Search);

                var query = new GetAllBlacklistedPhoneNumbersQuery
                {
                    Page = CurrentPage,
                    PageSize = PageSize,
                    Search = Search
                };
                var result = await _cqrsClient.SendQueryAsync<GetAllBlacklistedPhoneNumbersQueryResult>(query);

                if (result.Success)
                {
                    PhoneNumbers = result.PhoneNumbers;
                    TotalCount = result.TotalCount;
                    TotalPages = (int)Math.Ceiling((double)result.TotalCount / result.PageSize);
                    if (TotalPages < 1) TotalPages = 1;
                    _logger.LogInformation("Blacklisted phone numbers loaded: {Count} of {Total}", PhoneNumbers.Count, TotalCount);
                }
                else
                {
                    ErrorMessage = result.Message;
                    _logger.LogError("Failed to load blacklisted phone numbers: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading blacklisted phone numbers");
                ErrorMessage = $"Error loading blacklisted phone numbers: {ex.Message}";
            }
        }
    }
}
