namespace Common.Enums;

/// <summary>
/// Per-source consent depth ladder. See docs/SCRUM-904-user-risk-score-design.md §3.5.
///
/// Consent is *not* binary. Each data source carries one level per user.
/// Numeric ordering is load-bearing: a higher value implies all lower-value
/// capabilities are also permitted. So a user at <see cref="ContentLocal"/>
/// also permits everything that <see cref="Metadata"/> and
/// <see cref="Presence"/> permit. This contract is relied on by
/// <c>ConsentService.IsPermitted(current, required)</c>.
///
/// Different data sources support different subsets of these levels — e.g. a
/// URL-risk check tops out at <see cref="Metadata"/> (the URL itself is the
/// data), and a darknet-leak feed only supports <see cref="None"/> /
/// <see cref="Presence"/>. The per-source ceiling is documented on
/// <see cref="DataSourceKind"/>.
/// </summary>
public enum DataConsentLevel
{
    /// <summary>
    /// Nothing is collected from this source.
    /// Privacy cost: zero. Signal value: zero.
    /// The aggregator for this source returns <em>no value, not zero</em>;
    /// the risk function renormalizes the remaining active weights and the
    /// confidence is honestly lowered. No signal is ever fabricated from
    /// data the user did not allow.
    /// </summary>
    None = 0,

    /// <summary>
    /// Count + timestamps only ("5 SMSes today; 1 flagged risky by carrier").
    /// Privacy cost: very low. Signal value: medium.
    /// </summary>
    Presence = 1,

    /// <summary>
    /// Sender / recipient / category — no content.
    /// Privacy cost: low. Signal value: high.
    /// </summary>
    Metadata = 2,

    /// <summary>
    /// Content read but processed only on device; only the verdict
    /// (not the content) is sent to the backend.
    /// Privacy cost: medium. Signal value: very high.
    /// </summary>
    ContentLocal = 3,

    /// <summary>
    /// Content uploaded to the backend for richer analysis.
    /// Privacy cost: high. Signal value: maximum.
    /// </summary>
    ContentShared = 4
}
