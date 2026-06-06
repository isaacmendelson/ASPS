using Business.RealtimeAnalysis.UserDomain;
using Business.RealtimeAnalysis.UserDomain.SignalAggregators;
using Common.Enums;
using Common.Models;
using FluentAssertions;
using Moq;
using Xunit;

namespace ASPS.Tests.Business.UserDomain;

/// <summary>
/// Unit tests for <see cref="UserRiskScoreCalculator"/> (SCRUM-904 Step B).
/// The four aggregators are mocked so the calculator can be tested in
/// isolation from the DB and from the real signal logic; only the
/// L3 axis composition + L4 logistic risk function + the consent-aware
/// confidence math are exercised here.
/// </summary>
public class UserRiskScoreCalculatorTests
{
    private const string TestUserKey = "test-user-1";

    // The default-permitted consent map used by tests that don't care about
    // the consent dimension. Every source the four aggregators declare as
    // required is set to Metadata so the calculator's confidence = 1.0.
    private static IReadOnlyDictionary<DataSourceKind, DataConsentLevel> FullyPermittedConsent()
    {
        return Enum.GetValues<DataSourceKind>()
            .ToDictionary(k => k, _ => DataConsentLevel.Metadata);
    }

    private static UserRiskScoreCalculator BuildCalculator(
        AggregatorResult? behavior = null,
        AggregatorResult? live = null,
        AggregatorResult? inbound = null,
        AggregatorResult? correlation = null)
    {
        var b = new Mock<BehaviorAggregator>(null!, null!) { CallBase = false };
        b.Setup(x => x.AggregateAsync(It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<DataSourceKind, DataConsentLevel>>()))
            .ReturnsAsync(behavior ?? AggregatorResult.Empty);

        var l = new Mock<LiveThreatAggregator>(null!, null!) { CallBase = false };
        l.Setup(x => x.AggregateAsync(It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<DataSourceKind, DataConsentLevel>>()))
            .ReturnsAsync(live ?? AggregatorResult.Empty);

        var i = new Mock<InboundVectorAggregator>() { CallBase = false };
        i.Setup(x => x.AggregateAsync(It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<DataSourceKind, DataConsentLevel>>()))
            .ReturnsAsync(inbound ?? AggregatorResult.Empty);

        var c = new Mock<CrossModalCorrelationAggregator>() { CallBase = false };
        c.Setup(x => x.AggregateAsync(It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<DataSourceKind, DataConsentLevel>>()))
            .ReturnsAsync(correlation ?? AggregatorResult.Empty);

        return new UserRiskScoreCalculator(b.Object, l.Object, i.Object, c.Object);
    }

    [Fact]
    public async Task NoSignals_AndNoConsent_GivesLowScoreWithZeroConfidence()
    {
        var calc = BuildCalculator();

        var result = await calc.CalculateAsync(TestUserKey,
            new Dictionary<DataSourceKind, DataConsentLevel>());

        // Logistic baseline at linear=0, bias=50 → very small score in Low band.
        result.Score.Should().BeInRange(0, 30);
        result.Level.Should().Be("Low");
        result.Confidence.Should().Be(0.0);
        result.ContributingSignals.Should().BeEmpty();
        result.AxisScores.LiveScore.Should().Be(0);
        result.DataSourcesActive.Should()
            .OnlyContain(d => d.ConsentLevel == DataConsentLevel.None);
    }

    [Fact]
    public async Task LiveThreatAt80_PushesUrsIntoHighOrCriticalBand_AndLiveAxisReflectsIt()
    {
        var liveSignal = new ContributingSignal
        {
            SignalType = "C.1",
            Weight = 3.0,
            Value = 1,
            Timestamp = DateTime.UtcNow,
            DecayedContribution = 80,
        };

        var calc = BuildCalculator(
            live: new AggregatorResult(80, new List<ContributingSignal> { liveSignal }));

        var result = await calc.CalculateAsync(TestUserKey, FullyPermittedConsent());

        // Live axis weight is 1.5; 1.5 × 80 − 50 = +70; σ(+4.2) is ~0.985 → ~99.
        result.Score.Should().BeGreaterThanOrEqualTo(60);
        result.Level.Should().BeOneOf("High", "Critical");
        result.AxisScores.LiveScore.Should().Be(80);
        result.DimensionScores.LiveThreatIndicator.Should().Be(80);
        result.ContributingSignals.Should().ContainSingle(s => s.SignalType == "C.1");
    }

    [Fact]
    public async Task BehaviorAlone_AtMidRange_StaysInLowOrElevatedBand()
    {
        // Behavior=50 alone → linear = 0.7×50 = 35; 35 − 50 = −15;
        // σ(−0.9) ≈ 0.289 → ~29. Stays Low band — modest browsing-only
        // signal shouldn't trigger Critical.
        var sig = new ContributingSignal { SignalType = "B.1", Value = 5, Weight = 1.0, Timestamp = DateTime.UtcNow, DecayedContribution = 50 };
        var calc = BuildCalculator(
            behavior: new AggregatorResult(50, new List<ContributingSignal> { sig }));

        var result = await calc.CalculateAsync(TestUserKey, FullyPermittedConsent());

        result.Score.Should().BeLessThanOrEqualTo(60);
        result.Level.Should().BeOneOf("Low", "Elevated");
        result.AxisScores.VulnerabilityScore.Should().Be(50);
        result.DimensionScores.ObservedBehavior.Should().Be(50);
    }

    [Fact]
    public async Task PartialConsent_ReducesConfidenceProportionally()
    {
        // Only UrlBrowsingAnalysis is permitted; the other required sources
        // across the four aggregators are at None. The calculator's
        // confidence is (permitted required sources / total required sources).
        var consent = new Dictionary<DataSourceKind, DataConsentLevel>
        {
            [DataSourceKind.UrlBrowsingAnalysis] = DataConsentLevel.Metadata,
        };

        var calc = BuildCalculator();
        var result = await calc.CalculateAsync(TestUserKey, consent);

        // The four aggregators collectively declare more than one required
        // source, so permitting only one should give a strictly-fractional
        // confidence below 1.0 and above 0.
        result.Confidence.Should().BeGreaterThan(0.0).And.BeLessThan(1.0);
        result.DataSourcesActive.Should()
            .Contain(d => d.Source == DataSourceKind.UrlBrowsingAnalysis
                          && d.ConsentLevel == DataConsentLevel.Metadata);
        result.DataSourcesActive.Should()
            .Contain(d => d.Source == DataSourceKind.InboundSmsReading
                          && d.ConsentLevel == DataConsentLevel.None);
    }
}
