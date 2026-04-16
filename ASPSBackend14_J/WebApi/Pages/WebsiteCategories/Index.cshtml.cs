using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApi.Services;
using Business.Queries;
using Common.Entities;

namespace WebApi.Pages.WebsiteCategories
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

        public List<WebsiteCategory> Categories { get; set; } = new();
        public List<WebsiteCategory> AllParents { get; set; } = new();
        public string? ErrorMessage { get; set; }

        [BindProperty(SupportsGet = true, Name = "page")]
        public int CurrentPage { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 50;

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ParentId { get; set; }

        public int TotalCount { get; set; }
        public int TotalPages { get; set; }

        public async Task OnGetAsync()
        {
            try
            {
                if (CurrentPage < 1) CurrentPage = 1;

                _logger.LogInformation("Loading website categories via CQRS (Page={Page}, Search={Search}, ParentId={ParentId})", 
                    CurrentPage, Search, ParentId);

                // Load all categories with filters
                var query = new GetAllWebsiteCategoriesQuery
                {
                    Page = CurrentPage,
                    PageSize = PageSize,
                    Search = Search,
                    ParentId = ParentId
                };
                var result = await _cqrsClient.SendQueryAsync<GetAllWebsiteCategoriesQueryResult>(query);

                if (result.Success)
                {
                    Categories = result.Categories;
                    TotalCount = result.TotalCount;
                    TotalPages = (int)Math.Ceiling((double)result.TotalCount / result.PageSize);
                    if (TotalPages < 1) TotalPages = 1;
                    _logger.LogInformation("Website categories loaded: {Count} of {Total}", Categories.Count, TotalCount);
                }
                else
                {
                    ErrorMessage = result.Message;
                    _logger.LogError("Failed to load website categories: {Message}", result.Message);
                }

                // Load parent categories for dropdown
                var parentQuery = new GetParentCategoriesQuery();
                var parentResult = await _cqrsClient.SendQueryAsync<GetParentCategoriesQueryResult>(parentQuery);

                if (parentResult.Success)
                {
                    AllParents = parentResult.Parents;
                }
                else
                {
                    _logger.LogWarning("Failed to load parent categories: {Message}", parentResult.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading website categories");
                ErrorMessage = $"Error loading website categories: {ex.Message}";
            }
        }
    }
}
