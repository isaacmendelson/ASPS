using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Business.Queries;
using Common.Models;
using WebApi.Services;

namespace WebApi.Pages.Users
{
    /// <summary>
    /// SCRUM-904 Phase-1 wrap-up — minimal admin view of a user's most-recent
    /// User Risk Score. Renders the structured object: scalar + level band,
    /// confidence, axis subscores, per-dimension subscores, contributing
    /// signals, explanation, and the active data-source consent map.
    /// </summary>
    public class RiskDetailModel : PageModel
    {
        private readonly ICQRSClient _cqrsClient;
        private readonly ILogger<RiskDetailModel> _logger;

        public RiskDetailModel(ICQRSClient cqrsClient, ILogger<RiskDetailModel> logger)
        {
            _cqrsClient = cqrsClient;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public string UserKey { get; set; } = string.Empty;

        public UserRiskScore? Score { get; set; }
        public bool NoScoreYet { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            if (string.IsNullOrWhiteSpace(UserKey))
            {
                ErrorMessage = "Missing required query parameter: userKey";
                return;
            }

            try
            {
                var query = new GetLatestUserRiskScoreQuery { UserKey = UserKey };
                var result = await _cqrsClient.SendQueryAsync<GetLatestUserRiskScoreQueryResult>(query);

                if (result == null || !result.Success)
                {
                    ErrorMessage = result?.Message ?? "Query failed";
                    return;
                }

                if (result.Score == null)
                {
                    NoScoreYet = true;
                    return;
                }

                Score = result.Score;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading URS for user {UserKey}", UserKey);
                ErrorMessage = "Could not load URS — see backend logs.";
            }
        }

        // Color hint used by the view for the band badge.
        public string LevelCss => Score?.Level switch
        {
            "Critical" => "bg-danger text-white",
            "High"     => "bg-warning text-dark",
            "Elevated" => "bg-info text-dark",
            "Low"      => "bg-success text-white",
            _          => "bg-secondary text-white",
        };
    }
}
