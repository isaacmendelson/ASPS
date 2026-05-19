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

        /// <summary>Dynamic website-category names for the Category dropdowns
        /// (Add form + search filter). Loaded from the DB-backed
        /// WebsiteCategory table (replaces the obsolete WebsiteType enum).</summary>
        public List<string> CategoryNames { get; private set; } = new();

        /// <summary>userKey → "First Last" for the User-name column.</summary>
        public Dictionary<string, string> UserNames { get; private set; } = new();

        public string DisplayUserName(string? userKey)
        {
            if (string.IsNullOrWhiteSpace(userKey)) return "—";
            return UserNames.TryGetValue(userKey, out var n) && !string.IsNullOrWhiteSpace(n)
                ? n : userKey;
        }

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        public async Task<IActionResult> OnPostNotifyUserAsync(string domain, string? userKey)
        {
            return await NotifyAsync(domain, userKey, notifyAll: false);
        }

        public async Task<IActionResult> OnPostNotifyAllAsync(string domain)
        {
            return await NotifyAsync(domain, null, notifyAll: true);
        }

        public async Task<IActionResult> OnPostEditAsync(
            int id, string domain, string? category, string? userKey,
            string? scamInProgressKey, int trackMode, bool isActive, string? reason)
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(domain))
            {
                ErrorMessage = "Id and Domain are required for edit.";
                await LoadAsync();
                return Page();
            }
            try
            {
                var command = new UpdateTrackedDomainCommand
                {
                    Id = id,
                    Domain = domain.Trim(),
                    Category = string.IsNullOrWhiteSpace(category) ? "Manual" : category!.Trim(),
                    UserKeyField = userKey?.Trim(),
                    ScamInProgressKey = scamInProgressKey?.Trim(),
                    TrackMode = trackMode,
                    IsActive = isActive,
                    Reason = string.IsNullOrWhiteSpace(reason) ? "Admin edited tracked domain" : reason.Trim()
                };
                var result = await _cqrsClient.SendCommandAsync<UpdateTrackedDomainCommandResult>(command);
                if (result.Success)
                    return RedirectToPage("Index", new { addedOk = result.Message });
                ErrorMessage = result.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing tracked domain ID {Id}", id);
                ErrorMessage = $"Error: {ex.Message}";
            }
            await LoadAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            if (id <= 0)
            {
                ErrorMessage = "Id is required for delete.";
                await LoadAsync();
                return Page();
            }
            try
            {
                var result = await _cqrsClient.SendCommandAsync<DeleteTrackedDomainCommandResult>(
                    new DeleteTrackedDomainCommand { Id = id, Reason = "Admin deleted tracked domain" });
                if (result.Success)
                    return RedirectToPage("Index", new { addedOk = result.Message });
                ErrorMessage = result.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting tracked domain ID {Id}", id);
                ErrorMessage = $"Error: {ex.Message}";
            }
            await LoadAsync();
            return Page();
        }

        private async Task<IActionResult> NotifyAsync(string domain, string? userKey, bool notifyAll)
        {
            if (string.IsNullOrWhiteSpace(domain))
            {
                ErrorMessage = "Domain is required for notify.";
                await LoadAsync();
                return Page();
            }
            try
            {
                var command = new AddTrackedDomainCommand
                {
                    Domain = domain.Trim(),
                    UserKeyField = userKey?.Trim() ?? string.Empty,
                    NotifyOnly = true,
                    NotifyAllUsers = notifyAll,
                    Reason = notifyAll ? "Admin: notify all user devices"
                                       : "Admin: notify user devices"
                };
                var result = await _cqrsClient.SendCommandAsync<AddTrackedDomainCommandResult>(command);
                if (result.Success)
                    return RedirectToPage("Index", new { addedOk = result.Message });
                ErrorMessage = result.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying tracked domain {Domain}", domain);
                ErrorMessage = $"Error: {ex.Message}";
            }
            await LoadAsync();
            return Page();
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

                    await LoadUserNamesAsync();
                    await LoadCategoryNamesAsync();
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

        /// <summary>Load dynamic category names from the WebsiteCategory
        /// table for the dropdowns. Best-effort — failure leaves the list
        /// empty (dropdown still renders "All Categories").</summary>
        private async Task LoadCategoryNamesAsync()
        {
            try
            {
                var res = await _cqrsClient.SendQueryAsync<GetAllWebsiteCategoriesQueryResult>(
                    new GetAllWebsiteCategoriesQuery { Page = 1, PageSize = 1000 });
                if (res.Success)
                {
                    CategoryNames = res.Categories
                        .Select(c => c.Name)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Distinct()
                        .OrderBy(n => n)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load website categories for dropdown");
            }
        }

        /// <summary>Build the userKey → "First Last" map for the User-name
        /// column. Best-effort: a lookup failure must not break the list.</summary>
        private async Task LoadUserNamesAsync()
        {
            try
            {
                // Only the user keys actually referenced by the current page.
                var keys = TrackedDomains
                    .Select(d => d.UserKey)
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .Distinct()
                    .ToHashSet();
                if (keys.Count == 0) return;

                var usersResult = await _cqrsClient.SendQueryAsync<GetUsersWithDeviceCountsQueryResult>(
                    new GetUsersWithDeviceCountsQuery());
                if (usersResult.Success)
                {
                    foreach (var u in usersResult.Users)
                    {
                        var key = u.User?.KeyField;
                        if (string.IsNullOrWhiteSpace(key) || !keys.Contains(key)) continue;
                        UserNames[key!] = $"{u.User!.FirstName} {u.User.LastName}".Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not resolve user names for tracked domains");
            }
        }
    }
}
