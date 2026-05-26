using Business.Data.EF;
using Common.Entities;
using Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace Business.Services;

/// <summary>
/// Read/write consent + write the audit log.
/// See docs/SCRUM-904-user-risk-score-design.md §3.5.
///
/// Defaults are returned (not persisted) for any (user × source) pair with
/// no row in <see cref="AppDbContext.UserConsentPreferences"/>. This keeps
/// the table small and avoids touching rows the user has never seen.
///
/// Every <see cref="SetLevelAsync"/> writes both the
/// <see cref="UserConsentPreferences"/> row and a
/// <see cref="UserConsentAuditLog"/> entry in a single SaveChanges call —
/// the two writes are atomic at the DB transaction level for InnoDB / MySQL.
///
/// NOT in Step A: the guardian-approval workflow for vulnerable users
/// (deferred to SCRUM-913). See the TODO in <see cref="SetLevelAsync"/>.
/// </summary>
public class ConsentService
{
    private readonly AppDbContext _db;

    public ConsentService(AppDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    /// Recommended (balanced) baseline from design §3.5 — never max, never
    /// min. Returned when a user has no explicit row for a given source.
    /// Hardcoded here rather than seeded into the DB so a fresh install +
    /// a no-op consent screen still gives the user the documented defaults.
    /// </summary>
    private static readonly IReadOnlyDictionary<DataSourceKind, DataConsentLevel> Defaults =
        new Dictionary<DataSourceKind, DataConsentLevel>
        {
            // Core protection — Metadata baseline:
            { DataSourceKind.UrlBrowsingAnalysis,            DataConsentLevel.Metadata },
            { DataSourceKind.RemoteAccessMonitoring,         DataConsentLevel.Metadata },
            { DataSourceKind.ImmediateDangerCorrelation,     DataConsentLevel.Metadata },
            { DataSourceKind.SensitiveSiteClassification,    DataConsentLevel.Metadata },
            { DataSourceKind.DomainDwellAndFormSubmit,       DataConsentLevel.Metadata },

            // Derived (no new capture) — Presence baseline:
            { DataSourceKind.BrowsingPatternBaseline,        DataConsentLevel.Presence },

            // Sensitive / 3rd-party — None until explicit opt-in:
            { DataSourceKind.InboundSmsReading,              DataConsentLevel.None },
            { DataSourceKind.InboundEmailReading,            DataConsentLevel.None },
            { DataSourceKind.CallLogAndSpoofedNumberDetection, DataConsentLevel.None },
            { DataSourceKind.DarknetLeakMonitoring,          DataConsentLevel.None },
        };

    /// <summary>
    /// Returns the user's current consent level for <paramref name="source"/>,
    /// or the default for that source if no explicit row exists. Does NOT
    /// write a row on read.
    /// </summary>
    public async Task<DataConsentLevel> GetLevelAsync(string userKey, DataSourceKind source)
    {
        if (string.IsNullOrWhiteSpace(userKey))
            throw new ArgumentException("UserKey cannot be empty", nameof(userKey));

        var row = await _db.UserConsentPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserKey == userKey && p.Source == source);

        return row?.Level ?? DefaultFor(source);
    }

    /// <summary>
    /// Full per-user consent picture across every known source. Sources
    /// without an explicit row are filled with the default level.
    /// </summary>
    public async Task<IReadOnlyDictionary<DataSourceKind, DataConsentLevel>> GetAllAsync(string userKey)
    {
        if (string.IsNullOrWhiteSpace(userKey))
            throw new ArgumentException("UserKey cannot be empty", nameof(userKey));

        var rows = await _db.UserConsentPreferences
            .AsNoTracking()
            .Where(p => p.UserKey == userKey)
            .ToListAsync();

        var result = new Dictionary<DataSourceKind, DataConsentLevel>();
        foreach (DataSourceKind source in Enum.GetValues(typeof(DataSourceKind)))
        {
            var row = rows.FirstOrDefault(r => r.Source == source);
            result[source] = row?.Level ?? DefaultFor(source);
        }
        return result;
    }

    /// <summary>
    /// Upserts the user's consent row for <paramref name="source"/> to
    /// <paramref name="newLevel"/> AND writes a matching audit-log entry
    /// capturing the old → new transition. Both writes happen in a single
    /// <c>SaveChangesAsync</c>.
    ///
    /// <paramref name="changedBy"/> is the UserKey of the actor (the user
    /// themselves, or a guardian). <paramref name="reason"/> is free text
    /// (e.g. "user toggled in settings", "guardian approval granted").
    ///
    /// No-op (no DB write, no audit row) when the requested level equals
    /// the current effective level.
    /// </summary>
    public async Task SetLevelAsync(
        string userKey,
        DataSourceKind source,
        DataConsentLevel newLevel,
        string? changedBy,
        string? reason)
    {
        if (string.IsNullOrWhiteSpace(userKey))
            throw new ArgumentException("UserKey cannot be empty", nameof(userKey));

        // TODO(SCRUM-913): guardian-approval gating before disabling critical
        // sources (ImmediateDangerCorrelation / RemoteAccessMonitoring /
        // UrlBrowsingAnalysis) for vulnerable accounts. When a user is
        // configured as vulnerable AND has a linked guardian AND newLevel
        // would reduce the effective level of a critical source, the change
        // must be queued for guardian approval rather than applied
        // immediately. Out of scope for Step A.

        var existing = await _db.UserConsentPreferences
            .FirstOrDefaultAsync(p => p.UserKey == userKey && p.Source == source);

        var oldLevel = existing?.Level ?? DefaultFor(source);

        // No-op when nothing actually changes.
        if (existing != null && oldLevel == newLevel)
            return;
        if (existing == null && oldLevel == newLevel)
            return;

        if (existing == null)
        {
            _db.UserConsentPreferences.Add(new UserConsentPreferences(userKey, source, newLevel, changedBy));
        }
        else
        {
            existing.UpdateLevel(newLevel, changedBy);
        }

        _db.UserConsentAuditLogs.Add(new UserConsentAuditLog(
            userKey: userKey,
            source: source,
            oldLevel: oldLevel,
            newLevel: newLevel,
            changedBy: changedBy,
            reason: reason));

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Pure helper: a permission check that exploits the numeric ordering of
    /// <see cref="DataConsentLevel"/> — a higher value implies all lower-value
    /// capabilities are also permitted (see enum doc).
    /// </summary>
    public bool IsPermitted(DataConsentLevel current, DataConsentLevel required)
        => current >= required;

    /// <summary>
    /// The hardcoded default level for a given source from §3.5 of the
    /// design. Any source not in the dictionary falls back to
    /// <see cref="DataConsentLevel.None"/> (the conservative choice).
    /// </summary>
    public static DataConsentLevel DefaultFor(DataSourceKind source)
        => Defaults.TryGetValue(source, out var level) ? level : DataConsentLevel.None;
}
