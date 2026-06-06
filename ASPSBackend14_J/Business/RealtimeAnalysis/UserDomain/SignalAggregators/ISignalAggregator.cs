using Common.Enums;
using Common.Models;

namespace Business.RealtimeAnalysis.UserDomain.SignalAggregators;

/// <summary>
/// Per-dimension aggregator over a user's recent events (SCRUM-904 §5 L2).
/// Each aggregator owns one of the four URS dimensions and short-circuits if
/// the user has consented none of the data sources it depends on.
/// </summary>
public interface ISignalAggregator
{
    /// <summary>
    /// The data sources this aggregator reads. If <em>all</em> of these are at
    /// <see cref="DataConsentLevel.None"/> in the user's consent map, the
    /// aggregator returns <c>(0, [])</c> without querying anything.
    /// </summary>
    DataSourceKind[] RequiredSources { get; }

    /// <summary>
    /// 0–100 subscore + the individual signals that produced it (for
    /// explainability + auto-correction attribution).
    /// </summary>
    Task<AggregatorResult> AggregateAsync(
        string userKey,
        IReadOnlyDictionary<DataSourceKind, DataConsentLevel> consent);
}

/// <summary>Result of a single aggregator pass: subscore + the contributing signals.</summary>
public record AggregatorResult(double Subscore, IReadOnlyList<ContributingSignal> Signals)
{
    /// <summary>Empty result — used by stubs and by the consent-disabled short-circuit.</summary>
    public static readonly AggregatorResult Empty = new(0.0, Array.Empty<ContributingSignal>());
}
