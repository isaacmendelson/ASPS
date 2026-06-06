using Common.Entities;

namespace Interface.Repositories;

/// <summary>
/// Repository for <see cref="UserRiskProfile"/> — the persisted per-user
/// weight set used by the URS calculator. One row per user.
///
/// JIRA: SCRUM-904 — Step B.2. See
/// <c>docs/SCRUM-904-user-risk-score-design.md</c> for context.
/// </summary>
public interface IUserRiskProfileRepository
{
    /// <summary>
    /// Returns the user's persisted <see cref="UserRiskProfile"/>, or a fresh
    /// default-constructed instance when no row exists.
    ///
    /// IMPORTANT: a missing row is NOT auto-persisted here — the caller
    /// decides when (and whether) to save. This keeps reads side-effect-free
    /// and avoids materialising rows for users who never get a URS computed.
    /// The returned default has <c>UserKey = userKey</c> already populated so
    /// a subsequent <see cref="UpsertAsync"/> writes the right row.
    /// </summary>
    Task<UserRiskProfile> GetByUserKeyAsync(string userKey);

    /// <summary>
    /// Insert or update the user's <see cref="UserRiskProfile"/>. The
    /// <paramref name="userKey"/> argument is authoritative — if
    /// <paramref name="profile"/> has a different (or empty)
    /// <c>UserKey</c>, this method assigns the canonical value before saving.
    /// </summary>
    Task UpsertAsync(string userKey, UserRiskProfile profile);
}
