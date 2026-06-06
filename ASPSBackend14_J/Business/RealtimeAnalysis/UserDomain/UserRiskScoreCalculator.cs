using Business.RealtimeAnalysis.UserDomain.SignalAggregators;
using Common.Enums;
using Common.Models;

namespace Business.RealtimeAnalysis.UserDomain;

/// <summary>
/// L1 → L4 pipeline for User Risk Score (SCRUM-904 §5).
///
///   per-event RiskAssessment        — already exists upstream (L1)
///   per-dimension subscores         — signal aggregators (L2)
///   axis composition                — Vulnerability / Exposure / Live / Correlation (L3)
///   logistic risk function          — the headline 0–100 scalar (L4)
///
/// Consent-aware: the consent map is threaded into each aggregator, and the
/// final <see cref="UserRiskScore.Confidence"/> reflects the breadth of
/// permitted sources — a user who has opted many sources to NONE gets an
/// honestly-reduced confidence rather than a fabricated number (§3.5).
/// </summary>
public class UserRiskScoreCalculator
{
    // Logistic risk-function weights (design §7 starting values).
    private const double WeightVulnerability = 0.7;
    private const double WeightExposure      = 1.0;
    private const double WeightLive          = 1.5;
    private const double WeightCorrelation   = 1.3;
    private const double Bias                = 50.0;   // θ
    private const double Steepness           = 0.06;   // k

    private readonly BehaviorAggregator _behavior;
    private readonly LiveThreatAggregator _live;
    private readonly InboundVectorAggregator _inbound;
    private readonly CrossModalCorrelationAggregator _correlation;

    public UserRiskScoreCalculator(
        BehaviorAggregator behavior,
        LiveThreatAggregator live,
        InboundVectorAggregator inbound,
        CrossModalCorrelationAggregator correlation)
    {
        _behavior = behavior;
        _live = live;
        _inbound = inbound;
        _correlation = correlation;
    }

    /// <summary>
    /// Run the full pipeline. The consent map should come from
    /// <c>ConsentService.GetAllAsync(userKey)</c>; the calculator never reads
    /// consent itself — keeps the calculator pure-functional w.r.t. consent
    /// and easy to test.
    /// </summary>
    public async Task<UserRiskScore> CalculateAsync(
        string userKey,
        IReadOnlyDictionary<DataSourceKind, DataConsentLevel> consent)
    {
        // L2 — run aggregators in parallel.
        var behaviorTask    = _behavior.AggregateAsync(userKey, consent);
        var liveTask        = _live.AggregateAsync(userKey, consent);
        var inboundTask     = _inbound.AggregateAsync(userKey, consent);
        var correlationTask = _correlation.AggregateAsync(userKey, consent);
        await Task.WhenAll(behaviorTask, liveTask, inboundTask, correlationTask);

        var b = await behaviorTask;
        var l = await liveTask;
        var i = await inboundTask;
        var c = await correlationTask;

        // L3 — axis composition. In this MVP each axis tracks its primary
        // dimension; the cross-cutting w_D contribution to Vuln/Exp is left
        // for when the CorrelationAggregator returns non-empty results.
        var vulnerability = Clamp(b.Subscore, 0, 100);
        var exposure      = Clamp(i.Subscore, 0, 100);
        var liveAxis      = Clamp(l.Subscore, 0, 100);
        var correlation   = Clamp(c.Subscore, 0, 100);

        // L4 — logistic. URS = round(100 · σ(linear − θ))
        var linear =
              WeightVulnerability * vulnerability
            + WeightExposure      * exposure
            + WeightLive          * liveAxis
            + WeightCorrelation   * correlation;
        var raw = 100.0 * Sigmoid(linear - Bias);
        var score = (int)Math.Round(raw);

        // Consent-aware confidence: fraction of required sources that the
        // user has permitted at all (≥ Presence), across the four aggregators.
        var allRequired = _behavior.RequiredSources
            .Concat(_live.RequiredSources)
            .Concat(_inbound.RequiredSources)
            .Concat(_correlation.RequiredSources)
            .Distinct()
            .ToArray();
        var permitted = allRequired.Count(src =>
            consent.TryGetValue(src, out var lvl) && lvl >= DataConsentLevel.Presence);
        var confidence = allRequired.Length == 0 ? 0.0 : (double)permitted / allRequired.Length;

        // Active-source presence list (for the structured object's audit trail).
        var dataSources = allRequired
            .Select(src => new DataSourcePresence
            {
                Source = src,
                ConsentLevel = consent.TryGetValue(src, out var lvl) ? lvl : DataConsentLevel.None,
                LastObservedAt = null, // populated when a real freshness signal arrives
            })
            .ToList();

        // Union of contributing signals.
        var contributing = new List<ContributingSignal>();
        contributing.AddRange(b.Signals);
        contributing.AddRange(l.Signals);
        contributing.AddRange(i.Signals);
        contributing.AddRange(c.Signals);

        var explanation = BuildExplanation(contributing);

        var now = DateTime.UtcNow;
        return new UserRiskScore
        {
            Score = score,
            Level = UserRiskScore.ComputeLevel(score),
            Confidence = confidence,
            ComputedAt = now,
            ValidUntil = now.AddHours(1),
            AxisScores = new AxisScoreSet
            {
                VulnerabilityScore = vulnerability,
                ExposureScore = exposure,
                LiveScore = liveAxis,
                CorrelationScore = correlation,
            },
            DimensionScores = new DimensionScoreSet
            {
                ObservedBehavior      = b.Subscore,
                LiveThreatIndicator   = l.Subscore,
                InboundAttackVector   = i.Subscore,
                CrossModalCorrelation = c.Subscore,
            },
            ContributingSignals = contributing,
            DataSourcesActive = dataSources,
            Explanation = explanation,
            RecommendedActions = new List<string>(),  // policy layer populates these later
        };
    }

    private static string BuildExplanation(IReadOnlyList<ContributingSignal> signals)
    {
        if (signals.Count == 0)
            return "No risk signals collected within the active consent scope.";

        var top = signals
            .OrderByDescending(s => s.DecayedContribution)
            .Take(3)
            .Select(s => $"{s.SignalType} (×{s.Value:0.##} → {s.DecayedContribution:0.#})");
        return "Top contributors: " + string.Join(", ", top);
    }

    private static double Sigmoid(double x) => 1.0 / (1.0 + Math.Exp(-Steepness * x));

    private static double Clamp(double v, double min, double max) =>
        v < min ? min : v > max ? max : v;
}
