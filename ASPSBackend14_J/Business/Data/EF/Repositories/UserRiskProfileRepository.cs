using Common.Entities;
using Interface.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Business.Data.EF.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IUserRiskProfileRepository"/>.
/// JIRA: SCRUM-904 — Step B.2.
/// </summary>
public class UserRiskProfileRepository : IUserRiskProfileRepository
{
    private readonly AppDbContext _context;

    public UserRiskProfileRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task<UserRiskProfile> GetByUserKeyAsync(string userKey)
    {
        if (string.IsNullOrWhiteSpace(userKey))
            throw new ArgumentException("UserKey cannot be empty", nameof(userKey));

        var row = await _context.UserRiskProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserKey == userKey);

        if (row is not null)
            return row;

        // No row yet — return a default-constructed profile so callers can
        // either compute against defaults or upsert later. We do NOT persist
        // here; that's the caller's choice. UserKey is pre-populated so a
        // subsequent UpsertAsync writes the correct row.
        return new UserRiskProfile { UserKey = userKey };
    }

    /// <inheritdoc />
    public async Task UpsertAsync(string userKey, UserRiskProfile profile)
    {
        if (string.IsNullOrWhiteSpace(userKey))
            throw new ArgumentException("UserKey cannot be empty", nameof(userKey));
        if (profile is null)
            throw new ArgumentNullException(nameof(profile));

        // userKey is authoritative — paper over a caller passing a profile
        // with an empty or different UserKey.
        profile.UserKey = userKey;

        var existing = await _context.UserRiskProfiles
            .FirstOrDefaultAsync(p => p.UserKey == userKey);

        if (existing is null)
        {
            _context.UserRiskProfiles.Add(profile);
        }
        else
        {
            // Copy the field-backed state from `profile` onto the tracked
            // entity. EF's CurrentValues.SetValues handles the field-mapped
            // properties correctly (it goes through the model metadata, not
            // the public setters).
            _context.Entry(existing).CurrentValues.SetValues(profile);
        }

        await _context.SaveChangesAsync();
    }
}
