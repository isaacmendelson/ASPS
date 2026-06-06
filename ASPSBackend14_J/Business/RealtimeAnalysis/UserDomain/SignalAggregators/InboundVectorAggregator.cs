using Common.Enums;

namespace Business.RealtimeAnalysis.UserDomain.SignalAggregators;

/// <summary>
/// A-dimension — inbound attack vector (SCRUM-904 §3.A).
///
/// <b>Step-B stub.</b> The high-value sources for this dimension
/// (inbound malicious messages, spoofed-number calls, darknet leak feed) are
/// not yet ingested — they arrive in Phase 3 of the design rollout (§9), each
/// gated by privacy / product / legal work. Until then this aggregator
/// returns the empty result so the URS pipeline keeps working with whatever
/// other dimensions are available.
/// </summary>
public class InboundVectorAggregator : ISignalAggregator
{
    public DataSourceKind[] RequiredSources { get; } =
    {
        DataSourceKind.InboundSmsReading,
        DataSourceKind.InboundEmailReading,
        DataSourceKind.CallLogAndSpoofedNumberDetection,
        DataSourceKind.DarknetLeakMonitoring,
    };

    public virtual Task<AggregatorResult> AggregateAsync(
        string userKey,
        IReadOnlyDictionary<DataSourceKind, DataConsentLevel> consent)
    {
        // No data, so we cannot contribute. The confidence math in the
        // calculator notes the absence and lowers overall confidence
        // proportionally, rather than fabricating signal.
        return Task.FromResult(AggregatorResult.Empty);
    }
}
