using Business.Queries;
using Business.Services;
using Microsoft.Extensions.Logging;

namespace Business.Handlers;

/// <summary>
/// SCRUM-904 — query handlers for User Risk Score lookups. The compute path
/// is event-driven via <see cref="UserRiskScoreService"/>; this handler
/// exposes the persisted snapshot to the WebApi / admin UI side via CQRS.
/// </summary>
public class UserRiskScoreQueryHandlers
{
    private readonly UserRiskScoreService _service;
    private readonly ILogger<UserRiskScoreQueryHandlers> _logger;

    public UserRiskScoreQueryHandlers(
        UserRiskScoreService service,
        ILogger<UserRiskScoreQueryHandlers> logger)
    {
        _service = service;
        _logger = logger;
    }

    public virtual async Task<GetLatestUserRiskScoreQueryResult> HandleAsync(GetLatestUserRiskScoreQuery query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query.UserKey))
            {
                return new GetLatestUserRiskScoreQueryResult
                {
                    Success = false,
                    Message = "UserKey is required",
                };
            }

            var urs = await _service.GetLatestAsync(query.UserKey);
            return new GetLatestUserRiskScoreQueryResult
            {
                Success = true,
                Score = urs,  // may be null — caller renders "not yet computed"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching latest URS for user {UserKey}", query.UserKey);
            return new GetLatestUserRiskScoreQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}",
            };
        }
    }
}
