using Common.Enums;

namespace Common.Models;

/// <summary>
/// User Risk Score (URS) — structured assessment per user.
/// See docs/SCRUM-904-user-risk-score-design.md §1.
///
/// This is a *computed* object. It is not persisted directly; persisted
/// snapshots use <see cref="Common.Entities.UserRiskScoreHistory"/>.
///
/// The headline scalar is <see cref="Score"/> (0–100). The decomposition
/// (<see cref="AxisScores"/>, <see cref="DimensionScores"/>,
/// <see cref="ContributingSignals"/>, <see cref="Explanation"/>,
/// <see cref="RecommendedActions"/>, <see cref="DataSourcesActive"/>) is
/// load-bearing — action selection, explainability and auto-correction all
/// depend on it. See §1 of the design doc for why.
/// </summary>
public class UserRiskScore
{
    /// <summary>Headline 0–100 scalar (rounded from the logistic risk function).</summary>
    public int Score { get; set; }

    /// <summary>Band derived from <see cref="Score"/>: "Low" | "Elevated" | "High" | "Critical".</summary>
    public string Level { get; set; } = "Low";

    /// <summary>
    /// 0.0–1.0. Consent-aware confidence: reflects breadth of permitted data
    /// sources + freshness, not just statistical confidence. A user who has
    /// opted many sources to NONE will have honestly-reduced confidence.
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>UTC timestamp when this score was computed.</summary>
    public DateTime ComputedAt { get; set; }

    /// <summary>UTC timestamp after which this score must be recomputed.</summary>
    public DateTime ValidUntil { get; set; }

    /// <summary>Four orthogonal axes: Vulnerability / Exposure / Live / Correlation.</summary>
    public AxisScoreSet AxisScores { get; set; } = new();

    /// <summary>Four per-dimension subscores (0–100).</summary>
    public DimensionScoreSet DimensionScores { get; set; } = new();

    /// <summary>
    /// The individual events that drove the headline number. Used for
    /// explainability and for auto-correction attribution.
    /// </summary>
    public List<ContributingSignal> ContributingSignals { get; set; } = new();

    /// <summary>Human-readable top-3 reasons for this score.</summary>
    public string Explanation { get; set; } = string.Empty;

    /// <summary>
    /// Protective action keys implied by this score. The policy layer
    /// translates these keys into concrete actions (warnings, blocks,
    /// guardian notifications, etc.).
    /// </summary>
    public List<string> RecommendedActions { get; set; } = new();

    /// <summary>
    /// Data sources that contributed to this score, with their consent level
    /// and last-observed timestamp. Drives confidence and the renormalization
    /// of weights across permitted sources (§3.5).
    /// </summary>
    public List<DataSourcePresence> DataSourcesActive { get; set; } = new();

    /// <summary>
    /// Compute the band string for a given score using the design §1
    /// thresholds: ≤30 Low, 31–60 Elevated, 61–85 High, 86+ Critical.
    /// </summary>
    public static string ComputeLevel(int score)
    {
        if (score <= 30) return "Low";
        if (score <= 60) return "Elevated";
        if (score <= 85) return "High";
        return "Critical";
    }
}

/// <summary>
/// Per-axis 0–100 subscores. See design §2 / §5 L3 for the axis composition
/// formulas. Vulnerability and Exposure are the two orthogonal slow/fast
/// axes; Live and Correlation are mediating streams.
/// </summary>
public class AxisScoreSet
{
    /// <summary>Vulnerability axis 0–100 (slow-moving "how prone is this user").</summary>
    public double VulnerabilityScore { get; set; }

    /// <summary>Exposure axis 0–100 (fast-moving "what is hitting them now").</summary>
    public double ExposureScore { get; set; }

    /// <summary>Live axis 0–100 (something dangerous happening right now).</summary>
    public double LiveScore { get; set; }

    /// <summary>Correlation axis 0–100 (suspicious overlap of signals).</summary>
    public double CorrelationScore { get; set; }
}

/// <summary>
/// Per-dimension 0–100 subscores, one per dimension from design §1.
/// These are the aggregator outputs before the axis-composition step.
/// </summary>
public class DimensionScoreSet
{
    /// <summary>3.A — Inbound attack vector dimension.</summary>
    public double InboundAttackVector { get; set; }

    /// <summary>3.B — Observed behavior dimension.</summary>
    public double ObservedBehavior { get; set; }

    /// <summary>3.C — Live threat indicator dimension.</summary>
    public double LiveThreatIndicator { get; set; }

    /// <summary>3.D — Cross-modal correlation dimension.</summary>
    public double CrossModalCorrelation { get; set; }
}

/// <summary>
/// A single signal that contributed to a UserRiskScore — the unit of
/// explainability and auto-correction attribution. See design §5 L2 + §7.
/// </summary>
public class ContributingSignal
{
    /// <summary>Identifier of the signal type (e.g. "B.1", "C.1", "D.2").</summary>
    public string SignalType { get; set; } = string.Empty;

    /// <summary>Weight applied to this signal at computation time.</summary>
    public double Weight { get; set; }

    /// <summary>Raw magnitude of the event (1 for binary, graded for risk_score-like signals).</summary>
    public double Value { get; set; }

    /// <summary>UTC timestamp of the underlying event.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>The post-decay, post-confidence contribution this signal added to its dimension subscore.</summary>
    public double DecayedContribution { get; set; }
}

/// <summary>
/// Presence record for one data source — what consent level it is at for
/// this user, and when we last saw a signal from it. Drives the consent-aware
/// confidence and weight renormalization in §3.5.
/// </summary>
public class DataSourcePresence
{
    /// <summary>The data source.</summary>
    public DataSourceKind Source { get; set; }

    /// <summary>Current consent level for this (user × source) pair.</summary>
    public DataConsentLevel ConsentLevel { get; set; }

    /// <summary>Most recent UTC timestamp at which a signal from this source was observed for the user, or null if never.</summary>
    public DateTime? LastObservedAt { get; set; }
}
