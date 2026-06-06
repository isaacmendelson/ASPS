using Business.Data.EF;
using Common.Entities;
using Common.Enums;
using Common.Models;
using Interface.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Business.RealtimeAnalysis.UserDomain.SignalAggregators;

/// <summary>
/// B-dimension — observed behaviour (SCRUM-904 §3.B).
///
/// MVP scope (only ✅ data per design §9):
///   B.1 risky URL visits  — count UrlAlertEntity per user in the aggregation window
///   B.5 RemoteAccess sessions — count RemoteAccessAlertEntity per user in the window
///
/// B.4 (sensitive-site activity) and full B.1 risk_score-weighting via
/// AnalysisResultContainer parsing are deferred to a later iteration; documented
/// inline below.
/// </summary>
public class BehaviorAggregator : ISignalAggregator
{
    private readonly AppDbContext _db;
    private readonly IUserRiskProfileRepository _profiles;

    public BehaviorAggregator(AppDbContext db, IUserRiskProfileRepository profiles)
    {
        _db = db;
        _profiles = profiles;
    }

    public DataSourceKind[] RequiredSources { get; } =
    {
        DataSourceKind.UrlBrowsingAnalysis,
        DataSourceKind.RemoteAccessMonitoring,
    };

    public virtual async Task<AggregatorResult> AggregateAsync(
        string userKey,
        IReadOnlyDictionary<DataSourceKind, DataConsentLevel> consent)
    {
        // Short-circuit if the user has consented none of our sources.
        if (AllNone(consent))
            return AggregatorResult.Empty;

        var profile = await _profiles.GetByUserKeyAsync(userKey);
        var windowDays = profile.AggregationPeriodDays > 0 ? profile.AggregationPeriodDays : 30;
        var since = DateTime.UtcNow.AddDays(-windowDays);

        var signals = new List<ContributingSignal>();
        double accumulated = 0.0;

        // B.1 — Risky URL visits. MVP: count UrlAlerts. TODO: weight by per-URL
        // risk_score parsed from the AnalysisResultContainer.JsonValue.
        if (IsPermitted(consent, DataSourceKind.UrlBrowsingAnalysis))
        {
            var urlCount = await _db.DeviceAlerts.OfType<UrlAlertEntity>()
                .Where(a => a.UserKeyField == userKey && a.Timestamp >= since)
                .CountAsync();
            if (urlCount > 0)
            {
                var contribution = urlCount * profile.RiskyUrlWeight;
                accumulated += contribution;
                signals.Add(new ContributingSignal
                {
                    SignalType = "B.1",
                    Weight = profile.RiskyUrlWeight,
                    Value = urlCount,
                    Timestamp = DateTime.UtcNow,
                    DecayedContribution = contribution,
                });
            }
        }

        // B.5 — RemoteAccess sessions in the window.
        if (IsPermitted(consent, DataSourceKind.RemoteAccessMonitoring))
        {
            var raCount = await _db.DeviceAlerts.OfType<RemoteAccessAlertEntity>()
                .Where(a => a.UserKeyField == userKey && a.Timestamp >= since)
                .CountAsync();
            if (raCount > 0)
            {
                var contribution = raCount * profile.RemoteAccessWeight;
                accumulated += contribution;
                signals.Add(new ContributingSignal
                {
                    SignalType = "B.5",
                    Weight = profile.RemoteAccessWeight,
                    Value = raCount,
                    Timestamp = DateTime.UtcNow,
                    DecayedContribution = contribution,
                });
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

    private static bool IsPermitted(IReadOnlyDictionary<DataSourceKind, DataConsentLevel> consent, DataSourceKind src) =>
        Level(consent, src) >= DataConsentLevel.Presence;

    private static DataConsentLevel Level(IReadOnlyDictionary<DataSourceKind, DataConsentLevel> consent, DataSourceKind src) =>
        consent.TryGetValue(src, out var l) ? l : DataConsentLevel.None;

    private static double Clamp(double value, double min, double max) =>
        value < min ? min : value > max ? max : value;
}
