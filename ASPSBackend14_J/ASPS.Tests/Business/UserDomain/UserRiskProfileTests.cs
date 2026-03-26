using Xunit;
using FluentAssertions;
using Business.RealtimeAnalysis.UserDomain;

namespace ASPS.Tests.Business.UserDomain;

/// <summary>
/// Unit tests for UserRiskProfile (ASPS-366)
/// </summary>
public class UserRiskProfileTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_Default_InitializesWithDefaultValues()
    {
        // Act
        var sut = new UserRiskProfile();

        // Assert
        sut.VulnerabilityScore.Should().Be(0.0);
        sut.ExposureScore.Should().Be(0.0);
        sut.RiskyUrlWeight.Should().Be(1.0);
        sut.SuspiciousCallWeight.Should().Be(1.5);
        sut.RemoteAccessWeight.Should().Be(2.0);
        sut.ScamInProgressWeight.Should().Be(3.0);
        sut.AggregationPeriodDays.Should().Be(30);
        sut.TimeDecayFactor.Should().Be(0.95);
    }

    [Fact]
    public void Constructor_WithCustomValues_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new UserRiskProfile(
            vulnerabilityScore: 45.5,
            exposureScore: 67.3,
            riskyUrlWeight: 1.2,
            suspiciousCallWeight: 1.8,
            remoteAccessWeight: 2.5,
            scamInProgressWeight: 3.5,
            aggregationPeriodDays: 60,
            timeDecayFactor: 0.9
        );

        // Assert
        sut.VulnerabilityScore.Should().Be(45.5);
        sut.ExposureScore.Should().Be(67.3);
        sut.RiskyUrlWeight.Should().Be(1.2);
        sut.SuspiciousCallWeight.Should().Be(1.8);
        sut.RemoteAccessWeight.Should().Be(2.5);
        sut.ScamInProgressWeight.Should().Be(3.5);
        sut.AggregationPeriodDays.Should().Be(60);
        sut.TimeDecayFactor.Should().Be(0.9);
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(0, 0)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(110, 100)]
    public void Constructor_ClampsVulnerabilityScoreTo0_100(double input, double expected)
    {
        // Act
        var sut = new UserRiskProfile(
            vulnerabilityScore: input,
            exposureScore: 50,
            riskyUrlWeight: 1.0,
            suspiciousCallWeight: 1.5,
            remoteAccessWeight: 2.0,
            scamInProgressWeight: 3.0,
            aggregationPeriodDays: 30,
            timeDecayFactor: 0.95
        );

        // Assert
        sut.VulnerabilityScore.Should().Be(expected);
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(0, 0)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(110, 100)]
    public void Constructor_ClampsExposureScoreTo0_100(double input, double expected)
    {
        // Act
        var sut = new UserRiskProfile(
            vulnerabilityScore: 50,
            exposureScore: input,
            riskyUrlWeight: 1.0,
            suspiciousCallWeight: 1.5,
            remoteAccessWeight: 2.0,
            scamInProgressWeight: 3.0,
            aggregationPeriodDays: 30,
            timeDecayFactor: 0.95
        );

        // Assert
        sut.ExposureScore.Should().Be(expected);
    }

    [Theory]
    [InlineData(-0.1, 0)]
    [InlineData(0, 0)]
    [InlineData(0.5, 0.5)]
    [InlineData(0.95, 0.95)]
    [InlineData(1.0, 1.0)]
    [InlineData(1.1, 1.0)]
    public void Constructor_ClampsTimeDecayFactorTo0_1(double input, double expected)
    {
        // Act
        var sut = new UserRiskProfile(
            vulnerabilityScore: 50,
            exposureScore: 50,
            riskyUrlWeight: 1.0,
            suspiciousCallWeight: 1.5,
            remoteAccessWeight: 2.0,
            scamInProgressWeight: 3.0,
            aggregationPeriodDays: 30,
            timeDecayFactor: input
        );

        // Assert
        sut.TimeDecayFactor.Should().Be(expected);
    }

    #endregion

    #region UpdateVulnerabilityScore Tests

    [Fact]
    public void UpdateVulnerabilityScore_WithValidValue_UpdatesScore()
    {
        // Arrange
        var sut = new UserRiskProfile();

        // Act
        sut.UpdateVulnerabilityScore(75.5);

        // Assert
        sut.VulnerabilityScore.Should().Be(75.5);
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(0, 0)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(110, 100)]
    public void UpdateVulnerabilityScore_ClampsValueTo0_100(double input, double expected)
    {
        // Arrange
        var sut = new UserRiskProfile();

        // Act
        sut.UpdateVulnerabilityScore(input);

        // Assert
        sut.VulnerabilityScore.Should().Be(expected);
    }

    [Fact]
    public void UpdateVulnerabilityScore_CalledMultipleTimes_UpdatesToLatestValue()
    {
        // Arrange
        var sut = new UserRiskProfile();

        // Act
        sut.UpdateVulnerabilityScore(30);
        sut.UpdateVulnerabilityScore(60);
        sut.UpdateVulnerabilityScore(45);

        // Assert
        sut.VulnerabilityScore.Should().Be(45);
    }

    #endregion

    #region UpdateExposureScore Tests

    [Fact]
    public void UpdateExposureScore_WithValidValue_UpdatesScore()
    {
        // Arrange
        var sut = new UserRiskProfile();

        // Act
        sut.UpdateExposureScore(82.3);

        // Assert
        sut.ExposureScore.Should().Be(82.3);
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(0, 0)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(110, 100)]
    public void UpdateExposureScore_ClampsValueTo0_100(double input, double expected)
    {
        // Arrange
        var sut = new UserRiskProfile();

        // Act
        sut.UpdateExposureScore(input);

        // Assert
        sut.ExposureScore.Should().Be(expected);
    }

    [Fact]
    public void UpdateExposureScore_CalledMultipleTimes_UpdatesToLatestValue()
    {
        // Arrange
        var sut = new UserRiskProfile();

        // Act
        sut.UpdateExposureScore(25);
        sut.UpdateExposureScore(70);
        sut.UpdateExposureScore(55);

        // Assert
        sut.ExposureScore.Should().Be(55);
    }

    #endregion

    #region UpdateWeights Tests

    [Fact]
    public void UpdateWeights_WithRiskyUrlWeight_UpdatesOnlyRiskyUrlWeight()
    {
        // Arrange
        var sut = new UserRiskProfile();
        var originalSuspiciousCall = sut.SuspiciousCallWeight;
        var originalRemoteAccess = sut.RemoteAccessWeight;
        var originalScamInProgress = sut.ScamInProgressWeight;

        // Act
        sut.UpdateWeights(riskyUrlWeight: 2.5);

        // Assert
        sut.RiskyUrlWeight.Should().Be(2.5);
        sut.SuspiciousCallWeight.Should().Be(originalSuspiciousCall);
        sut.RemoteAccessWeight.Should().Be(originalRemoteAccess);
        sut.ScamInProgressWeight.Should().Be(originalScamInProgress);
    }

    [Fact]
    public void UpdateWeights_WithSuspiciousCallWeight_UpdatesOnlySuspiciousCallWeight()
    {
        // Arrange
        var sut = new UserRiskProfile();
        var originalRiskyUrl = sut.RiskyUrlWeight;

        // Act
        sut.UpdateWeights(suspiciousCallWeight: 2.8);

        // Assert
        sut.SuspiciousCallWeight.Should().Be(2.8);
        sut.RiskyUrlWeight.Should().Be(originalRiskyUrl);
    }

    [Fact]
    public void UpdateWeights_WithMultipleWeights_UpdatesAllSpecified()
    {
        // Arrange
        var sut = new UserRiskProfile();

        // Act
        sut.UpdateWeights(
            riskyUrlWeight: 1.3,
            remoteAccessWeight: 2.7,
            scamInProgressWeight: 3.8
        );

        // Assert
        sut.RiskyUrlWeight.Should().Be(1.3);
        sut.SuspiciousCallWeight.Should().Be(1.5); // Original default
        sut.RemoteAccessWeight.Should().Be(2.7);
        sut.ScamInProgressWeight.Should().Be(3.8);
    }

    [Fact]
    public void UpdateWeights_WithNullValues_DoesNotUpdate()
    {
        // Arrange
        var sut = new UserRiskProfile();
        var originalRiskyUrl = sut.RiskyUrlWeight;
        var originalSuspiciousCall = sut.SuspiciousCallWeight;

        // Act
        sut.UpdateWeights(riskyUrlWeight: null, suspiciousCallWeight: null);

        // Assert
        sut.RiskyUrlWeight.Should().Be(originalRiskyUrl);
        sut.SuspiciousCallWeight.Should().Be(originalSuspiciousCall);
    }

    #endregion

    #region UpdateTimeParameters Tests

    [Fact]
    public void UpdateTimeParameters_WithAggregationPeriod_UpdatesOnlyAggregationPeriod()
    {
        // Arrange
        var sut = new UserRiskProfile();
        var originalTimeDecay = sut.TimeDecayFactor;

        // Act
        sut.UpdateTimeParameters(aggregationPeriodDays: 45);

        // Assert
        sut.AggregationPeriodDays.Should().Be(45);
        sut.TimeDecayFactor.Should().Be(originalTimeDecay);
    }

    [Fact]
    public void UpdateTimeParameters_WithTimeDecayFactor_UpdatesOnlyTimeDecay()
    {
        // Arrange
        var sut = new UserRiskProfile();
        var originalAggregation = sut.AggregationPeriodDays;

        // Act
        sut.UpdateTimeParameters(timeDecayFactor: 0.88);

        // Assert
        sut.TimeDecayFactor.Should().Be(0.88);
        sut.AggregationPeriodDays.Should().Be(originalAggregation);
    }

    [Fact]
    public void UpdateTimeParameters_WithBothValues_UpdatesBoth()
    {
        // Arrange
        var sut = new UserRiskProfile();

        // Act
        sut.UpdateTimeParameters(aggregationPeriodDays: 90, timeDecayFactor: 0.92);

        // Assert
        sut.AggregationPeriodDays.Should().Be(90);
        sut.TimeDecayFactor.Should().Be(0.92);
    }

    [Theory]
    [InlineData(-0.1, 0)]
    [InlineData(0, 0)]
    [InlineData(0.5, 0.5)]
    [InlineData(1.0, 1.0)]
    [InlineData(1.5, 1.0)]
    public void UpdateTimeParameters_ClampsTimeDecayFactorTo0_1(double input, double expected)
    {
        // Arrange
        var sut = new UserRiskProfile();

        // Act
        sut.UpdateTimeParameters(timeDecayFactor: input);

        // Assert
        sut.TimeDecayFactor.Should().Be(expected);
    }

    [Fact]
    public void UpdateTimeParameters_WithNullValues_DoesNotUpdate()
    {
        // Arrange
        var sut = new UserRiskProfile();
        var originalAggregation = sut.AggregationPeriodDays;
        var originalTimeDecay = sut.TimeDecayFactor;

        // Act
        sut.UpdateTimeParameters(aggregationPeriodDays: null, timeDecayFactor: null);

        // Assert
        sut.AggregationPeriodDays.Should().Be(originalAggregation);
        sut.TimeDecayFactor.Should().Be(originalTimeDecay);
    }

    #endregion

    #region CalculateWeightedFactors Tests (ASPS-367)

    [Fact]
    public void CalculateWeightedFactors_WithNoEvents_ReturnsZero()
    {
        // Arrange
        var sut = new UserRiskProfile();

        // Act
        var result = sut.CalculateWeightedFactors();

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void CalculateWeightedFactors_WithRiskyUrls_AppliesCorrectWeight()
    {
        // Arrange
        var sut = new UserRiskProfile();

        // Act
        var result = sut.CalculateWeightedFactors(riskyUrlCount: 5);

        // Assert
        result.Should().Be(5 * 1.0); // 5 URLs × 1.0 weight = 5.0
    }

    [Fact]
    public void CalculateWeightedFactors_WithSuspiciousCalls_AppliesCorrectWeight()
    {
        // Arrange
        var sut = new UserRiskProfile();

        // Act
        var result = sut.CalculateWeightedFactors(suspiciousCallCount: 3);

        // Assert
        result.Should().Be(3 * 1.5); // 3 calls × 1.5 weight = 4.5
    }

    [Fact]
    public void CalculateWeightedFactors_WithRemoteAccess_AppliesCorrectWeight()
    {
        // Arrange
        var sut = new UserRiskProfile();

        // Act
        var result = sut.CalculateWeightedFactors(remoteAccessCount: 2);

        // Assert
        result.Should().Be(2 * 2.0); // 2 sessions × 2.0 weight = 4.0
    }

    [Fact]
    public void CalculateWeightedFactors_WithScamInProgress_AppliesCorrectWeight()
    {
        // Arrange
        var sut = new UserRiskProfile();

        // Act
        var result = sut.CalculateWeightedFactors(scamInProgressCount: 1);

        // Assert
        result.Should().Be(1 * 3.0); // 1 scam × 3.0 weight = 3.0
    }

    [Fact]
    public void CalculateWeightedFactors_WithMultipleEvents_SumsAllWeights()
    {
        // Arrange
        var sut = new UserRiskProfile();

        // Act
        var result = sut.CalculateWeightedFactors(
            riskyUrlCount: 5,
            suspiciousCallCount: 3,
            remoteAccessCount: 2,
            scamInProgressCount: 1
        );

        // Assert
        // (5×1.0) + (3×1.5) + (2×2.0) + (1×3.0) = 5 + 4.5 + 4 + 3 = 16.5
        result.Should().Be(16.5);
    }

    [Fact]
    public void CalculateWeightedFactors_WithCustomWeights_UsesCustomValues()
    {
        // Arrange
        var sut = new UserRiskProfile();
        sut.UpdateWeights(
            riskyUrlWeight: 2.0,
            suspiciousCallWeight: 2.5,
            remoteAccessWeight: 3.0,
            scamInProgressWeight: 4.0
        );

        // Act
        var result = sut.CalculateWeightedFactors(
            riskyUrlCount: 2,
            suspiciousCallCount: 1,
            remoteAccessCount: 1,
            scamInProgressCount: 1
        );

        // Assert
        // (2×2.0) + (1×2.5) + (1×3.0) + (1×4.0) = 4 + 2.5 + 3 + 4 = 13.5
        result.Should().Be(13.5);
    }

    #endregion

    #region CalculateActiveThreats Tests (ASPS-367)

    [Fact]
    public void CalculateActiveThreats_WithNoScams_ReturnsZero()
    {
        // Arrange
        var sut = new UserRiskProfile();

        // Act
        var result = sut.CalculateActiveThreats(scamInProgress: 0, confidence: 0.8);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void CalculateActiveThreats_WithZeroConfidence_ReturnsZero()
    {
        // Arrange
        var sut = new UserRiskProfile();

        // Act
        var result = sut.CalculateActiveThreats(scamInProgress: 1, confidence: 0);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void CalculateActiveThreats_WithValidInputs_CalculatesCorrectly()
    {
        // Arrange
        var sut = new UserRiskProfile();

        // Act
        var result = sut.CalculateActiveThreats(scamInProgress: 1, confidence: 0.8);

        // Assert
        // 1 scam × 3.0 weight × 0.8 confidence = 2.4
        result.Should().Be(2.4);
    }

    [Theory]
    [InlineData(-0.1, 0)]
    [InlineData(0, 0)]
    [InlineData(0.5, 1.5)]
    [InlineData(0.8, 2.4)]
    [InlineData(1.0, 3.0)]
    [InlineData(1.5, 3.0)]
    public void CalculateActiveThreats_ClampsConfidenceTo0_1(double confidence, double expected)
    {
        // Arrange
        var sut = new UserRiskProfile();

        // Act
        var result = sut.CalculateActiveThreats(scamInProgress: 1, confidence: confidence);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region CalculateRiskScore Tests (ASPS-367)

    [Fact]
    public void CalculateRiskScore_WithZeroInputs_ReturnsZero()
    {
        // Arrange
        var sut = new UserRiskProfile();

        // Act
        var result = sut.CalculateRiskScore(
            weightedFactors: 0,
            daysAgo: 0,
            activeThreats: 0
        );

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void CalculateRiskScore_CalculatesBaseRiskCorrectly()
    {
        // Arrange
        var sut = new UserRiskProfile();
        sut.UpdateVulnerabilityScore(50);
        sut.UpdateExposureScore(60);

        // Act
        var result = sut.CalculateRiskScore(
            weightedFactors: 0,
            daysAgo: 0,
            activeThreats: 0
        );

        // Assert
        // BaseRisk = 0.3 × 50 + 0.4 × 60 = 15 + 24 = 39
        // With daysAgo=0, timeDecay=0.95^0=1.0
        // Result = (39 + 0) × 1.0 + 0 = 39
        result.Should().Be(39);
    }

    [Fact]
    public void CalculateRiskScore_AppliesTimeDecayCorrectly()
    {
        // Arrange
        var sut = new UserRiskProfile();
        sut.UpdateVulnerabilityScore(50);
        sut.UpdateExposureScore(60);

        // Act
        var result = sut.CalculateRiskScore(
            weightedFactors: 0,
            daysAgo: 1,
            activeThreats: 0
        );

        // Assert
        // BaseRisk = 39 (from previous test)
        // TimeDecay = 0.95^1 = 0.95
        // Result = 39 × 0.95 = 37.05
        result.Should().BeApproximately(37.05, 0.01);
    }

    [Fact]
    public void CalculateRiskScore_WithWeightedFactors_AddsBeforeTimeDecay()
    {
        // Arrange
        var sut = new UserRiskProfile();
        sut.UpdateVulnerabilityScore(50);
        sut.UpdateExposureScore(60);

        // Act
        var result = sut.CalculateRiskScore(
            weightedFactors: 10,
            daysAgo: 1,
            activeThreats: 0
        );

        // Assert
        // BaseRisk = 39
        // (39 + 10) × 0.95 = 49 × 0.95 = 46.55
        result.Should().BeApproximately(46.55, 0.01);
    }

    [Fact]
    public void CalculateRiskScore_WithActiveThreats_AddsAfterTimeDecay()
    {
        // Arrange
        var sut = new UserRiskProfile();
        sut.UpdateVulnerabilityScore(50);
        sut.UpdateExposureScore(60);

        // Act
        var result = sut.CalculateRiskScore(
            weightedFactors: 0,
            daysAgo: 1,
            activeThreats: 5
        );

        // Assert
        // BaseRisk = 39
        // (39 × 0.95) + 5 = 37.05 + 5 = 42.05
        result.Should().BeApproximately(42.05, 0.01);
    }

    [Fact]
    public void CalculateRiskScore_CompleteFormula_CalculatesCorrectly()
    {
        // Arrange
        var sut = new UserRiskProfile();
        sut.UpdateVulnerabilityScore(50);
        sut.UpdateExposureScore(60);

        // Act
        var result = sut.CalculateRiskScore(
            weightedFactors: 10,
            daysAgo: 2,
            activeThreats: 5
        );

        // Assert
        // BaseRisk = 0.3×50 + 0.4×60 = 15 + 24 = 39
        // TimeDecay = 0.95^2 = 0.9025
        // Result = (39 + 10) × 0.9025 + 5 = 49 × 0.9025 + 5 = 44.2225 + 5 = 49.2225
        result.Should().BeApproximately(49.22, 0.01);
    }

    [Fact]
    public void CalculateRiskScore_ClampsResultTo100()
    {
        // Arrange
        var sut = new UserRiskProfile();
        sut.UpdateVulnerabilityScore(100);
        sut.UpdateExposureScore(100);

        // Act
        var result = sut.CalculateRiskScore(
            weightedFactors: 200,
            daysAgo: 0,
            activeThreats: 50
        );

        // Assert
        // BaseRisk = 0.3×100 + 0.4×100 = 70
        // (70 + 200) × 1.0 + 50 = 320 → clamped to 100
        result.Should().Be(100);
    }

    [Fact]
    public void CalculateRiskScore_WithOldEvents_AppliesStrongDecay()
    {
        // Arrange
        var sut = new UserRiskProfile();
        sut.UpdateVulnerabilityScore(50);
        sut.UpdateExposureScore(60);

        // Act
        var result = sut.CalculateRiskScore(
            weightedFactors: 10,
            daysAgo: 30,
            activeThreats: 0
        );

        // Assert
        // BaseRisk = 39
        // TimeDecay = 0.95^30 ≈ 0.2146
        // Result = (39 + 10) × 0.2146 ≈ 10.52
        result.Should().BeLessThan(11);
        result.Should().BeGreaterThan(10);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void UserRiskProfile_CompleteWorkflow_WorksCorrectly()
    {
        // Arrange - Create with custom values
        var sut = new UserRiskProfile(
            vulnerabilityScore: 30,
            exposureScore: 40,
            riskyUrlWeight: 1.0,
            suspiciousCallWeight: 1.5,
            remoteAccessWeight: 2.0,
            scamInProgressWeight: 3.0,
            aggregationPeriodDays: 30,
            timeDecayFactor: 0.95
        );

        // Act - Update scores
        sut.UpdateVulnerabilityScore(55);
        sut.UpdateExposureScore(70);

        // Act - Update weights
        sut.UpdateWeights(
            riskyUrlWeight: 1.2,
            scamInProgressWeight: 3.5
        );

        // Act - Update time parameters
        sut.UpdateTimeParameters(
            aggregationPeriodDays: 60,
            timeDecayFactor: 0.9
        );

        // Assert
        sut.VulnerabilityScore.Should().Be(55);
        sut.ExposureScore.Should().Be(70);
        sut.RiskyUrlWeight.Should().Be(1.2);
        sut.SuspiciousCallWeight.Should().Be(1.5); // Unchanged
        sut.RemoteAccessWeight.Should().Be(2.0); // Unchanged
        sut.ScamInProgressWeight.Should().Be(3.5);
        sut.AggregationPeriodDays.Should().Be(60);
        sut.TimeDecayFactor.Should().Be(0.9);
    }

    #endregion
}
