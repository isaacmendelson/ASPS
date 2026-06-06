using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Common.Entities;

/// <summary>
/// Per-user weight set for the User Risk Score (URS) computation. One row
/// per user — the row is keyed by <see cref="UserKey"/>.
///
/// Originally introduced as an in-memory POCO under
/// <c>Business.RealtimeAnalysis.UserDomain</c> (ASPS-366 / ASPS-367); moved
/// to <c>Common.Entities</c> and made persisted as part of SCRUM-904 (URS
/// algorithm). See <c>docs/SCRUM-904-user-risk-score-design.md</c>:
///   * §1 — UserRiskProfile is the <em>weight set</em>, not the score itself
///     (the score is <see cref="Common.Models.UserRiskScore"/>);
///   * §5 — these weights feed the L2 (per-dimension subscore) aggregation;
///   * §7 — the auto-correction mechanism drifts these weights from the
///     global defaults toward what each user's history says actually predicts
///     harm <em>for them</em>.
///
/// The public API (the <c>Update*</c> + <c>Calculate*</c> methods) is
/// preserved from the original implementation so existing tests and callers
/// keep working unchanged.
/// </summary>
[Table("UserRiskProfiles")]
public class UserRiskProfile
{
    /// <summary>
    /// Primary key — the user this profile belongs to. Refers to
    /// <c>User.KeyField</c>. One row per user.
    /// </summary>
    [Key]
    [MaxLength(36)]
    [Column("UserKey")]
    public string UserKey { get; set; } = string.Empty;

    /// <summary>
    /// Historical tendency score (0-100) - measures user's past risky behavior patterns
    /// </summary>
    public double VulnerabilityScore { get; private set; }

    /// <summary>
    /// Current exposure score (0-100) - measures current exposure to threats
    /// </summary>
    public double ExposureScore { get; private set; }

    /// <summary>
    /// Weight for risky URL visits in risk calculation
    /// </summary>
    public double RiskyUrlWeight { get; private set; }

    /// <summary>
    /// Weight for suspicious call activities in risk calculation
    /// </summary>
    public double SuspiciousCallWeight { get; private set; }

    /// <summary>
    /// Weight for remote access sessions in risk calculation
    /// </summary>
    public double RemoteAccessWeight { get; private set; }

    /// <summary>
    /// Weight for active scam detection in risk calculation
    /// </summary>
    public double ScamInProgressWeight { get; private set; }

    /// <summary>
    /// Number of days to aggregate events for risk calculation
    /// </summary>
    public int AggregationPeriodDays { get; private set; }

    /// <summary>
    /// Time decay factor for historical events (0.0-1.0)
    /// Applied exponentially: TimeDecayFactor^days
    /// </summary>
    public double TimeDecayFactor { get; private set; }

    /// <summary>
    /// Creates a new UserRiskProfile with default weights and parameters
    /// </summary>
    public UserRiskProfile()
        : this(
            vulnerabilityScore: 0.0,
            exposureScore: 0.0,
            riskyUrlWeight: 1.0,
            suspiciousCallWeight: 1.5,
            remoteAccessWeight: 2.0,
            scamInProgressWeight: 3.0,
            aggregationPeriodDays: 30,
            timeDecayFactor: 0.95)
    {
    }

    /// <summary>
    /// Creates a new UserRiskProfile with custom parameters
    /// </summary>
    public UserRiskProfile(
        double vulnerabilityScore,
        double exposureScore,
        double riskyUrlWeight,
        double suspiciousCallWeight,
        double remoteAccessWeight,
        double scamInProgressWeight,
        int aggregationPeriodDays,
        double timeDecayFactor)
    {
        VulnerabilityScore = Clamp(vulnerabilityScore, 0, 100);
        ExposureScore = Clamp(exposureScore, 0, 100);
        RiskyUrlWeight = riskyUrlWeight;
        SuspiciousCallWeight = suspiciousCallWeight;
        RemoteAccessWeight = remoteAccessWeight;
        ScamInProgressWeight = scamInProgressWeight;
        AggregationPeriodDays = aggregationPeriodDays;
        TimeDecayFactor = Clamp(timeDecayFactor, 0, 1);
    }

    /// <summary>
    /// Update vulnerability score (historical tendency)
    /// </summary>
    public void UpdateVulnerabilityScore(double score)
    {
        VulnerabilityScore = Clamp(score, 0, 100);
    }

    /// <summary>
    /// Update exposure score (current exposure)
    /// </summary>
    public void UpdateExposureScore(double score)
    {
        ExposureScore = Clamp(score, 0, 100);
    }

    /// <summary>
    /// Update weight parameters
    /// </summary>
    public void UpdateWeights(
        double? riskyUrlWeight = null,
        double? suspiciousCallWeight = null,
        double? remoteAccessWeight = null,
        double? scamInProgressWeight = null)
    {
        if (riskyUrlWeight.HasValue)
            RiskyUrlWeight = riskyUrlWeight.Value;
        if (suspiciousCallWeight.HasValue)
            SuspiciousCallWeight = suspiciousCallWeight.Value;
        if (remoteAccessWeight.HasValue)
            RemoteAccessWeight = remoteAccessWeight.Value;
        if (scamInProgressWeight.HasValue)
            ScamInProgressWeight = scamInProgressWeight.Value;
    }

    /// <summary>
    /// Update time-based parameters
    /// </summary>
    public void UpdateTimeParameters(int? aggregationPeriodDays = null, double? timeDecayFactor = null)
    {
        if (aggregationPeriodDays.HasValue)
            AggregationPeriodDays = aggregationPeriodDays.Value;
        if (timeDecayFactor.HasValue)
            TimeDecayFactor = Clamp(timeDecayFactor.Value, 0, 1);
    }

    /// <summary>
    /// Calculate final risk score using the formula:
    /// FinalRiskScore = min(100, (BaseRisk + Σ WeightedFactors) × TimeDecayFactor^days + ActiveThreats)
    /// Where BaseRisk = 0.3 × VulnerabilityScore + 0.4 × ExposureScore
    /// ASPS-367: Risk Score Calculation
    /// </summary>
    /// <param name="weightedFactors">Sum of weighted risk factors (risky URLs, suspicious calls, etc.)</param>
    /// <param name="daysAgo">Number of days since the event occurred (for time decay)</param>
    /// <param name="activeThreats">Active threat score (ScamInProgress × Confidence)</param>
    /// <returns>Final risk score (0-100)</returns>
    public double CalculateRiskScore(double weightedFactors, int daysAgo, double activeThreats)
    {
        // BaseRisk = 0.3 × VulnerabilityScore + 0.4 × ExposureScore
        double baseRisk = (0.3 * VulnerabilityScore) + (0.4 * ExposureScore);

        // Apply time decay: TimeDecayFactor^days
        double timeDecay = Math.Pow(TimeDecayFactor, daysAgo);

        // Calculate risk before active threats
        double riskWithDecay = (baseRisk + weightedFactors) * timeDecay;

        // Add active threats (not affected by time decay)
        double finalScore = riskWithDecay + activeThreats;

        // Clamp to 0-100
        return Clamp(finalScore, 0, 100);
    }

    /// <summary>
    /// Calculate weighted risk factors from event counts
    /// </summary>
    /// <param name="riskyUrlCount">Number of risky URL visits</param>
    /// <param name="suspiciousCallCount">Number of suspicious calls</param>
    /// <param name="remoteAccessCount">Number of remote access sessions</param>
    /// <param name="scamInProgressCount">Number of active scam detections</param>
    /// <returns>Sum of weighted factors</returns>
    public double CalculateWeightedFactors(
        int riskyUrlCount = 0,
        int suspiciousCallCount = 0,
        int remoteAccessCount = 0,
        int scamInProgressCount = 0)
    {
        return (riskyUrlCount * RiskyUrlWeight) +
               (suspiciousCallCount * SuspiciousCallWeight) +
               (remoteAccessCount * RemoteAccessWeight) +
               (scamInProgressCount * ScamInProgressWeight);
    }

    /// <summary>
    /// Calculate active threat score
    /// </summary>
    /// <param name="scamInProgress">Number of active scams</param>
    /// <param name="confidence">Confidence level (0.0-1.0)</param>
    /// <returns>Active threat score</returns>
    public double CalculateActiveThreats(int scamInProgress, double confidence)
    {
        return scamInProgress * ScamInProgressWeight * Clamp(confidence, 0, 1);
    }

    /// <summary>
    /// Helper method to clamp values within range
    /// </summary>
    private static double Clamp(double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
