using Business.Data.EF;
using Common.Entities;
using Common.Enums;
using Common.Models;
using Interface.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Business.RealtimeAnalysis.UserDomain.SignalAggregators;

/// <summary>
/// C-dimension — live threat indicator (SCRUM-904 §3.C).
///
/// MVP scope:
///   C.1 — active ImmediateDanger for the user (EndTime is null)
///   C.4 — ScamInProgress correlation key on an active danger
///
/// C.2 (open RemoteAccess without sensitive-site overlap) is implicitly
/// covered for now by the behaviour aggregator's RA count; a dedicated
/// open-session check on <see cref="RemoteAccessAlertEntity.SessionStatus"/>
/// is deferred to the next iteration.
/// </summary>
public class LiveThreatAggregator : ISignalAggregator
{
    private readonly AppDbContext _db;
    private readonly IUserRiskProfileRepository _profiles;

    public LiveThreatAggregator(AppDbContext db, IUserRiskProfileRepository profiles)
    {
        _db = db;
        _profiles = profiles;
    }

    public DataSourceKind[] RequiredSources { get; } =
    {
        DataSourceKind.ImmediateDangerCorrelation,
        DataSourceKind.RemoteAccessMonitoring,
    };

    public virtual async Task<AggregatorResult> AggregateAsync(
        string userKey,
        IReadOnlyDictionary<DataSourceKind, DataConsentLevel> consent)
    {
        if (AllNone(consent))
            return AggregatorResult.Empty;

        var profile = await _profiles.GetByUserKeyAsync(userKey);
        var signals = new List<ContributingSignal>();
        double accumulated = 0.0;

        // C.1 — Active ImmediateDanger (EndTime is null). Each active danger
        // is catastrophic-class; weight heavily.
        if (Level(consent, DataSourceKind.ImmediateDangerCorrelation) >= DataConsentLevel.Presence)
        {
            var activeDangers = await _db.ImmediateDangers
                .Where(d => d.UserKeyField == userKey && d.EndTime == null)
                .ToListAsync();

            if (activeDangers.Count > 0)
            {
                // Each active danger contributes heavily — design §7 gives the
                // ScamInProgress signal a starting weight of 30. We multiply
                // by the profile's ScamInProgressWeight (default 3.0) and a
                // catastrophe factor of 10 so a single active danger lands
                // squarely in the High band.
                var contribution = activeDangers.Count * profile.ScamInProgressWeight * 10;
                accumulated += contribution;
                signals.Add(new ContributingSignal
                {
                    SignalType = "C.1",
                    Weight = profile.ScamInProgressWeight,
                    Value = activeDangers.Count,
                    Timestamp = activeDangers.Max(d => d.Timestamp),
                    DecayedContribution = contribution,
                });

                // C.4 — A ScamInProgress key set on the active danger lifts it
                // further; the backend has correlated this to a known scam.
                var withScamKey = activeDangers.Count(d => !string.IsNullOrEmpty(d.ScamInProgressKeyField));
                if (withScamKey > 0)
                {
                    var c4Contribution = withScamKey * profile.ScamInProgressWeight * 5;
                    accumulated += c4Contribution;
                    signals.Add(new ContributingSignal
                    {
                        SignalType = "C.4",
                        Weight = profile.ScamInProgressWeight,
                        Value = withScamKey,
                        Timestamp = DateTime.UtcNow,
                        DecayedContribution = c4Contribution,
                    });
                }
            }
        }

        return new AggregatorResult(Clamp(accumulated, 0, 100), signals);
    }

    private bool AllNone(IReadOnlyDictionary<DataSourceKind, DataConsentLevel> consent)
    {
        foreach (var src in RequiredSources)
            if (Level(consent, src) != DataConsentLevel.None)
                return false;
        return true;
    }

    private static DataConsentLevel Level(IReadOnlyDictionary<DataSourceKind, DataConsentLevel> consent, DataSourceKind src) =>
        consent.TryGetValue(src, out var l) ? l : DataConsentLevel.None;

    private static double Clamp(double value, double min, double max) =>
        value < min ? min : value > max ? max : value;
}
