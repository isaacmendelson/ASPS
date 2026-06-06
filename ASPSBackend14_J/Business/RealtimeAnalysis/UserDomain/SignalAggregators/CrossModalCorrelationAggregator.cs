using Common.Enums;

namespace Business.RealtimeAnalysis.UserDomain.SignalAggregators;

/// <summary>
/// D-dimension — cross-modal correlation (SCRUM-904 §3.D).
///
/// <b>Step-B stub.</b> The high-value correlations (spoofed call → click within
/// window; lure-conversion chain; multi-modal anomaly) all require at least
/// two of the A-dimension sources to be ingested, which is Phase 3 work.
/// Until those land this aggregator returns the empty result.
/// </summary>
public class CrossModalCorrelationAggregator : ISignalAggregator
{
    public DataSourceKind[] RequiredSources { get; } =
    {
        DataSourceKind.InboundSmsReading,
        DataSourceKind.CallLogAndSpoofedNumberDetection,
        DataSourceKind.UrlBrowsingAnalysis,
    };

    public virtual Task<AggregatorResult> AggregateAsync(
        string userKey,
        IReadOnlyDictionary<DataSourceKind, DataConsentLevel> consent)
    {
        // Needs ≥ 2 active sources from A+B+C to produce a meaningful value.
        return Task.FromResult(AggregatorResult.Empty);
    }
}
