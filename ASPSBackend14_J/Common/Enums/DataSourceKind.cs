namespace Common.Enums;

/// <summary>
/// Catalog of consent-configurable data sources that can feed
/// <see cref="Common.Models.UserRiskScore"/>. Drawn from the default-levels
/// table in docs/SCRUM-904-user-risk-score-design.md §3.5.
///
/// Stored as a string in the database (see EF config) so adding a new
/// source is non-breaking and a value can be retired without column
/// renumbering. Each member documents the highest consent level that is
/// meaningful for that source — levels beyond the ceiling are not useful
/// (e.g. a URL is metadata by nature; the URL itself <em>is</em> the
/// signal).
/// </summary>
public enum DataSourceKind
{
    /// <summary>
    /// URL browsing analysis — the URL + the analyzer's risk score.
    /// Ceiling: <see cref="DataConsentLevel.Metadata"/> (the URL itself is the data).
    /// Default at install: <see cref="DataConsentLevel.Metadata"/>.
    /// </summary>
    UrlBrowsingAnalysis = 0,

    /// <summary>
    /// Remote-access monitoring — which remote-access app + when, no screen contents.
    /// Ceiling: <see cref="DataConsentLevel.Metadata"/>.
    /// Default at install: <see cref="DataConsentLevel.Metadata"/>.
    /// </summary>
    RemoteAccessMonitoring = 1,

    /// <summary>
    /// ImmediateDanger correlation — combines remote-access + sensitive site.
    /// Ceiling: <see cref="DataConsentLevel.Metadata"/>.
    /// Default at install: <see cref="DataConsentLevel.Metadata"/>. Critical
    /// source — guardian-approval-required-to-disable for vulnerable users
    /// (SCRUM-913).
    /// </summary>
    ImmediateDangerCorrelation = 2,

    /// <summary>
    /// Sensitive-site classification — classifies the site, not the user's actions on it.
    /// Ceiling: <see cref="DataConsentLevel.Metadata"/>.
    /// Default at install: <see cref="DataConsentLevel.Metadata"/>.
    /// </summary>
    SensitiveSiteClassification = 3,

    /// <summary>
    /// Inbound SMS reading — for phishing-text detection.
    /// Ceiling: <see cref="DataConsentLevel.ContentShared"/> (content is the strongest signal).
    /// Default at install: <see cref="DataConsentLevel.None"/> (sensitive; user must opt in).
    /// </summary>
    InboundSmsReading = 4,

    /// <summary>
    /// Inbound email reading — for phishing-email detection.
    /// Ceiling: <see cref="DataConsentLevel.ContentShared"/>.
    /// Default at install: <see cref="DataConsentLevel.None"/> (sensitive; user must opt in).
    /// </summary>
    InboundEmailReading = 5,

    /// <summary>
    /// Call log + spoofed-number detection — incoming calls and known-bad number flags.
    /// Ceiling: <see cref="DataConsentLevel.Metadata"/> (per-call number + timestamp; no audio).
    /// Default at install: <see cref="DataConsentLevel.None"/> (sensitive; user must opt in).
    /// </summary>
    CallLogAndSpoofedNumberDetection = 6,

    /// <summary>
    /// Darknet / breach-leak monitoring (HIBP-style) — requires sharing the user's
    /// contact info with a third-party feed.
    /// Ceiling: <see cref="DataConsentLevel.Presence"/> (only the presence of a leak record is signalled).
    /// Default at install: <see cref="DataConsentLevel.None"/> (user enters email manually to opt in).
    /// </summary>
    DarknetLeakMonitoring = 7,

    /// <summary>
    /// Per-domain dwell time + form-submit detection on risky domains.
    /// Ceiling: <see cref="DataConsentLevel.Metadata"/> (no form-content captured).
    /// Default at install: <see cref="DataConsentLevel.Metadata"/>.
    /// </summary>
    DomainDwellAndFormSubmit = 8,

    /// <summary>
    /// Browsing-pattern baseline — rolling, on-device aggregate of already-collected URL data.
    /// Ceiling: <see cref="DataConsentLevel.Presence"/> (derived; no new capture).
    /// Default at install: <see cref="DataConsentLevel.Presence"/>.
    /// </summary>
    BrowsingPatternBaseline = 9
}
