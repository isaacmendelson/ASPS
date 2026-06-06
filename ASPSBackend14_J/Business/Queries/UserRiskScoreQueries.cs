using Common.Messaging;
using Common.Models;

namespace Business.Queries;

/// <summary>
/// SCRUM-904 Phase 1 wrap-up — fetch the most recent persisted
/// <see cref="UserRiskScore"/> snapshot for a given user. Returns null on
/// the result when no snapshot has been computed yet.
/// </summary>
public class GetLatestUserRiskScoreQuery : Query
{
    public GetLatestUserRiskScoreQuery() { QueryType = nameof(GetLatestUserRiskScoreQuery); }

    /// <summary>The user key (User.KeyField) whose URS to fetch.</summary>
    public string UserKey { get; set; } = string.Empty;
}

public class GetLatestUserRiskScoreQueryResult : QueryResult
{
    /// <summary>
    /// The most recent persisted URS for the user, or null if none has been
    /// computed yet (e.g. a user with no recent alert activity).
    /// </summary>
    public UserRiskScore? Score { get; set; }
}
