using Xunit;
using FluentAssertions;
using Common.Models;

namespace ASPS.Tests.Common
{
    public class RiskAssessmentTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithAllParameters_CreatesInstance()
        {
            // Arrange
            var riskScore = 75f;
            var riskLevel = "high";
            var isScam = true;
            var confidence = 0.92f;

            // Act
            var assessment = new RiskAssessment(riskScore, riskLevel, isScam, confidence);

            // Assert
            assessment.Should().NotBeNull();
            assessment.risk_score.Should().Be(75f);
            assessment.risk_level.Should().Be("high");
            assessment.is_scam.Should().BeTrue();
            assessment.confidence.Should().Be(0.92f);
        }

        [Fact]
        public void Constructor_WithZeroValues_CreatesInstance()
        {
            // Arrange
            var riskScore = 0f; // 0 = error/no result
            var riskLevel = "error";
            var isScam = false;
            var confidence = 0.0f;

            // Act
            var assessment = new RiskAssessment(riskScore, riskLevel, isScam, confidence);

            // Assert
            assessment.Should().NotBeNull();
            assessment.risk_score.Should().Be(0f);
            assessment.risk_level.Should().Be("error");
            assessment.is_scam.Should().BeFalse();
            assessment.confidence.Should().Be(0.0f);
        }

        [Fact]
        public void Constructor_WithMaxValues_CreatesInstance()
        {
            // Arrange
            var riskScore = 100f; // Maximum risk on new scale
            var riskLevel = "critical";
            var isScam = true;
            var confidence = 1.0f;

            // Act
            var assessment = new RiskAssessment(riskScore, riskLevel, isScam, confidence);

            // Assert
            assessment.risk_score.Should().Be(100f);
            assessment.risk_level.Should().Be("critical");
            assessment.is_scam.Should().BeTrue();
            assessment.confidence.Should().Be(1.0f);
        }

        [Fact]
        public void Constructor_WithEmptyRiskLevel_CreatesInstance()
        {
            // Arrange
            var riskScore = 50f; // Medium risk
            var riskLevel = string.Empty;
            var isScam = false;
            var confidence = 0.5f;

            // Act
            var assessment = new RiskAssessment(riskScore, riskLevel, isScam, confidence);

            // Assert
            assessment.risk_level.Should().Be(string.Empty);
        }

        #endregion

        #region Property Tests

        [Theory]
        [InlineData(0f)]    // Error/no result
        [InlineData(15f)]   // LOW risk
        [InlineData(45f)]   // MEDIUM risk
        [InlineData(75f)]   // HIGH risk
        [InlineData(100f)]  // Maximum risk
        public void RiskScore_WithDifferentValues_CanBeSetAndRetrieved(float score)
        {
            // Arrange
            var assessment = new RiskAssessment(score, "test", false, 0.5f);

            // Act
            var result = assessment.risk_score;

            // Assert
            result.Should().Be(score);
        }

        [Theory]
        [InlineData("low")]
        [InlineData("medium")]
        [InlineData("high")]
        [InlineData("critical")]
        public void RiskLevel_WithDifferentValues_CanBeSetAndRetrieved(string level)
        {
            // Arrange
            var assessment = new RiskAssessment(0.5f, level, false, 0.5f);

            // Act
            var result = assessment.risk_level;

            // Assert
            result.Should().Be(level);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void IsScam_WithDifferentValues_CanBeSetAndRetrieved(bool isScam)
        {
            // Arrange
            var assessment = new RiskAssessment(0.5f, "medium", isScam, 0.5f);

            // Act
            var result = assessment.is_scam;

            // Assert
            result.Should().Be(isScam);
        }

        [Theory]
        [InlineData(0.0f)]
        [InlineData(0.33f)]
        [InlineData(0.67f)]
        [InlineData(1.0f)]
        public void Confidence_WithDifferentValues_CanBeSetAndRetrieved(float confidence)
        {
            // Arrange
            var assessment = new RiskAssessment(0.5f, "medium", false, confidence);

            // Act
            var result = assessment.confidence;

            // Assert
            result.Should().Be(confidence);
        }

        #endregion

        #region Setter Tests

        [Fact]
        public void RiskScore_CanBeModifiedAfterConstruction()
        {
            // Arrange
            var assessment = new RiskAssessment(50f, "medium", false, 0.5f);

            // Act
            assessment.risk_score = 90f;

            // Assert
            assessment.risk_score.Should().Be(90f);
        }

        [Fact]
        public void RiskLevel_CanBeModifiedAfterConstruction()
        {
            // Arrange
            var assessment = new RiskAssessment(50f, "medium", false, 0.5f);

            // Act
            assessment.risk_level = "high";

            // Assert
            assessment.risk_level.Should().Be("high");
        }

        [Fact]
        public void IsScam_CanBeModifiedAfterConstruction()
        {
            // Arrange
            var assessment = new RiskAssessment(50f, "medium", false, 0.5f);

            // Act
            assessment.is_scam = true;

            // Assert
            assessment.is_scam.Should().BeTrue();
        }

        [Fact]
        public void Confidence_CanBeModifiedAfterConstruction()
        {
            // Arrange
            var assessment = new RiskAssessment(50f, "medium", false, 0.5f);

            // Act
            assessment.confidence = 0.85f;

            // Assert
            assessment.confidence.Should().Be(0.85f);
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public void Constructor_WithNegativeRiskScore_CreatesInstance()
        {
            // Arrange & Act
            var assessment = new RiskAssessment(-10f, "invalid", false, 0.5f);

            // Assert
            assessment.risk_score.Should().Be(-10f);
        }

        [Fact]
        public void Constructor_WithRiskScoreAboveHundred_CreatesInstance()
        {
            // Arrange & Act
            var assessment = new RiskAssessment(150f, "invalid", false, 0.5f);

            // Assert
            assessment.risk_score.Should().Be(150f);
        }

        [Fact]
        public void Constructor_WithNegativeConfidence_CreatesInstance()
        {
            // Arrange & Act
            var assessment = new RiskAssessment(0.5f, "medium", false, -0.5f);

            // Assert
            assessment.confidence.Should().Be(-0.5f);
        }

        [Fact]
        public void Constructor_WithConfidenceAboveOne_CreatesInstance()
        {
            // Arrange & Act
            var assessment = new RiskAssessment(0.5f, "medium", false, 1.5f);

            // Assert
            assessment.confidence.Should().Be(1.5f);
        }

        #endregion

        #region Real-World Scenarios

        [Fact]
        public void RiskAssessment_ForPhishingScam_HasCorrectProperties()
        {
            // Arrange
            var riskScore = 95f; // HIGH risk (dangerous)
            var riskLevel = "critical";
            var isScam = true;
            var confidence = 0.98f;

            // Act
            var assessment = new RiskAssessment(riskScore, riskLevel, isScam, confidence);

            // Assert
            assessment.risk_score.Should().BeGreaterThan(90f);
            assessment.risk_level.Should().Be("critical");
            assessment.is_scam.Should().BeTrue();
            assessment.confidence.Should().BeGreaterThan(0.95f);
        }

        [Fact]
        public void RiskAssessment_ForLegitimateWebsite_HasCorrectProperties()
        {
            // Arrange
            var riskScore = 10f; // LOW risk (safe)
            var riskLevel = "low";
            var isScam = false;
            var confidence = 0.85f;

            // Act
            var assessment = new RiskAssessment(riskScore, riskLevel, isScam, confidence);

            // Assert
            assessment.risk_score.Should().BeLessThan(30f); // LOW range
            assessment.risk_level.Should().Be("low");
            assessment.is_scam.Should().BeFalse();
            assessment.confidence.Should().BeGreaterThan(0.8f);
        }

        [Fact]
        public void RiskAssessment_ForSuspiciousWebsite_HasCorrectProperties()
        {
            // Arrange
            var riskScore = 45f; // MEDIUM risk
            var riskLevel = "medium";
            var isScam = false;
            var confidence = 0.7f;

            // Act
            var assessment = new RiskAssessment(riskScore, riskLevel, isScam, confidence);

            // Assert
            assessment.risk_score.Should().BeGreaterThan(30f).And.BeLessThan(61f); // MEDIUM range
            assessment.risk_level.Should().Be("medium");
            assessment.is_scam.Should().BeFalse();
            assessment.confidence.Should().BeGreaterThan(0.6f);
        }

        #endregion
    }
}
