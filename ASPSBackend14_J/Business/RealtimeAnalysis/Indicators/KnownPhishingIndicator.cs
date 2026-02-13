using Business.RealtimeAnalysis.Indicators;
using Business.RealtimeAnalysis.UserDomain;
using Common.Enums;

namespace Business.RealtimeAnalysis.Indicators;

/// <summary>
/// Indicator for URLs/domains found in known phishing database
/// </summary>
public class KnownPhishingIndicator : Indicator
{
    protected KnownPhishingIndicator() { }

    public KnownPhishingIndicator(
        string url,
        string domain,
        bool isKnownPhishing,
        bool isKnownPhishingDomain,
        PhishingCheckResultSource? source,
        int matchCount,
        BooleanScore score,
        AnalysisLevel layer,
        int sequence,
        float weight)
        : base(score, layer, sequence, weight)
    {
        this.Name = "Known Phishing Detection";
        //is.IndicatorType = IndicatorType.KnownPhishing;
        this.Source = IndicatorSource.Database;
        
        this.Url = url;
        this.Domain = domain;
        this.IsKnownPhishing = isKnownPhishing;
        this.IsKnownPhishingDomain = isKnownPhishingDomain;
        this.PhishingSource = source;
        this.MatchCount = matchCount;
        
        this.SetScore(score);
        this.SetValue(isKnownPhishing || isKnownPhishingDomain, 1.0f);
    }

    public override IndicatorType IndicatorType
    {
        get
        {
            return IndicatorType.KnownPhishing;
        }
    }

    /// <summary>
    /// The URL that was checked
    /// </summary>
    public string Url { get; private set; } = string.Empty;

    /// <summary>
    /// The domain that was checked
    /// </summary>
    public string Domain { get; private set; } = string.Empty;

    /// <summary>
    /// True if exact URL match found in database
    /// </summary>
    public bool IsKnownPhishing { get; private set; }

    /// <summary>
    /// True if domain found in database (even if URL doesn't match exactly)
    /// </summary>
    public bool IsKnownPhishingDomain { get; private set; }

    /// <summary>
    /// Source of the phishing data (e.g., "PhishTank", "OpenPhish")
    /// </summary>
    public PhishingCheckResultSource? PhishingSource { get; private set; }

    /// <summary>
    /// Number of phishing URLs found for this domain
    /// </summary>
    public int MatchCount { get; private set; }

    /// <summary>
    /// Threat level based on findings
    /// </summary>
    public string ThreatLevel
    {
        get
        {
            if (IsKnownPhishing) return "Critical";
            if (IsKnownPhishingDomain && MatchCount > 5) return "High";
            if (IsKnownPhishingDomain && MatchCount > 2) return "Medium";
            if (IsKnownPhishingDomain) return "Low";
            return "None";
        }
    }

    /// <summary>
    /// Get typed value
    /// </summary>
    public bool TypedValue => IsKnownPhishing || IsKnownPhishingDomain;
}
