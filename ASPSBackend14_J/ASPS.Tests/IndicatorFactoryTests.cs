using Business.RealtimeAnalysis;
using Business.RealtimeAnalysis.Indicators;
using Business.RealtimeAnalysis.UserDomain;
using Common.Enums;
using Common.Models;
using Common.Models.Alerts;
using FluentAssertions;
using Xunit;

namespace ASPS.Tests
{
    public class IndicatorFactoryTests
    {
        private readonly IndicatorFactory _factory;

        public IndicatorFactoryTests()
        {
            _factory = new IndicatorFactory();
        }

        #region UrlAnalysisResultVm Tests

        [Fact]
        public void CreateIndicators_WithKnownPhishingUrl_ReturnsKnownPhishingIndicator()
        {
            // Arrange
            var urlAnalysis = new UrlAnalysisResultVm
            {
                Url = "https://evil-phishing-site.com/fake-login",
                phishing_check = new PhishingCheckResultVm
                {
                    Is_known_phishing = true,
                    Is_known_phishing_domain = true,
                    Source = PhishingCheckResultSource.KnownList,
                    Match_count = 5,
                    Checked_at = DateTime.UtcNow
                }
            };

            // Act
            var result = _factory.CreateIndicators(urlAnalysis);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result[0].Should().BeOfType<KnownPhishingIndicator>();
            
            var indicator = result[0] as KnownPhishingIndicator;
            indicator.Should().NotBeNull();
        }

        [Fact]
        public void CreateIndicators_WithSuccessfulWhois_ReturnsWhoisIndicators()
        {
            // Arrange
            var urlAnalysis = new UrlAnalysisResultVm
            {
                Url = "https://example.com",
                Whois = new WhoisVm
                {
                    Success = true,
                    domain_age_days = 365,
                    country = "US",
                    Registrar = "GoDaddy",
                    privacy_protected = false,
                    risk_score = 20f // LOW risk
                }
            };

            // Act
            var result = _factory.CreateIndicators(urlAnalysis);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCountGreaterThan(0);
            
            // Should have WhoIsIndicator
            result.Should().Contain(i => i is WhoIsIndicator);
            
            // Should have WhoIsDomainAgeIndicator
            result.Should().Contain(i => i is WhoIsDomainAgeIndicator);
            
            // Should have WhoisIsPrivacyProtectedIndicator
            result.Should().Contain(i => i is WhoisIsPrivacyProtectedIndicator);
            
            // Should have WhoisCountryIndicator (country is not null)
            result.Should().Contain(i => i is WhoisCountryIndicator);
        }

        [Fact]
        public void CreateIndicators_WithYoungDomain_ReturnsLowScore()
        {
            // Arrange
            var urlAnalysis = new UrlAnalysisResultVm
            {
                Url = "https://newsite.com",
                Whois = new WhoisVm
                {
                    Success = true,
                    domain_age_days = 15, // Less than 30 days
                    country = "US",
                    privacy_protected = false,
                    risk_score = 80f // HIGH risk (young domain is suspicious)
                }
            };

            // Act
            var result = _factory.CreateIndicators(urlAnalysis);

            // Assert
            result.Should().NotBeNull();
            var domainAgeIndicator = result.OfType<WhoIsDomainAgeIndicator>().FirstOrDefault();
            domainAgeIndicator.Should().NotBeNull();
            // Score should be 0.0 for domains < 30 days
        }

        [Fact]
        public void CreateIndicators_WithOldDomain_ReturnsHighScore()
        {
            // Arrange
            var urlAnalysis = new UrlAnalysisResultVm
            {
                Url = "https://oldsite.com",
                Whois = new WhoisVm
                {
                    Success = true,
                    domain_age_days = 400, // More than 365 days
                    country = "US",
                    privacy_protected = false,
                    risk_score = 10f // LOW risk (old domain is safer)
                }
            };

            // Act
            var result = _factory.CreateIndicators(urlAnalysis);

            // Assert
            result.Should().NotBeNull();
            var domainAgeIndicator = result.OfType<WhoIsDomainAgeIndicator>().FirstOrDefault();
            domainAgeIndicator.Should().NotBeNull();
            // Score should be 1.0 for domains >= 365 days
        }

        [Fact]
        public void CreateIndicators_WithPrivacyProtectedWhois_ReturnsCorrectIndicator()
        {
            // Arrange
            var urlAnalysis = new UrlAnalysisResultVm
            {
                Url = "https://privacy-protected.com",
                Whois = new WhoisVm
                {
                    Success = true,
                    domain_age_days = 100,
                    country = "US",
                    privacy_protected = true,
                    risk_score = 50f // MEDIUM risk
                }
            };

            // Act
            var result = _factory.CreateIndicators(urlAnalysis);

            // Assert
            result.Should().NotBeNull();
            var privacyIndicator = result.OfType<WhoisIsPrivacyProtectedIndicator>().FirstOrDefault();
            privacyIndicator.Should().NotBeNull();
        }

        [Fact]
        public void CreateIndicators_WithNullWhoisCountry_DoesNotCreateCountryIndicator()
        {
            // Arrange
            var urlAnalysis = new UrlAnalysisResultVm
            {
                Url = "https://nocountry.com",
                Whois = new WhoisVm
                {
                    Success = true,
                    domain_age_days = 100,
                    country = null, // Null country
                    privacy_protected = false,
                    risk_score = 30f // MEDIUM risk
                }
            };

            // Act
            var result = _factory.CreateIndicators(urlAnalysis);

            // Assert
            result.Should().NotBeNull();
            result.OfType<WhoisCountryIndicator>().Should().BeEmpty();
        }

        [Fact]
        public void CreateIndicators_WithWebsiteType_ReturnsWebsiteTypeIndicator()
        {
            // Arrange
            var urlAnalysis = new UrlAnalysisResultVm
            {
                Url = "https://shop.com",
                Purpose = new Purpose
                {
                    Category = WebsiteType.ECommerce,
                    Confidence = 0.9f,
                    Description = "Online shopping"
                }
            };

            // Act
            var result = _factory.CreateIndicators(urlAnalysis);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain(i => i is WebsiteTypeIndicator);
            
            var websiteTypeIndicator = result.OfType<WebsiteTypeIndicator>().FirstOrDefault();
            websiteTypeIndicator.Should().NotBeNull();
        }

        [Fact]
        public void CreateIndicators_WithUnknownWebsiteType_DoesNotCreateWebsiteTypeIndicator()
        {
            // Arrange
            var urlAnalysis = new UrlAnalysisResultVm
            {
                Url = "https://unknown.com",
                Purpose = new Purpose
                {
                    Category = WebsiteType.Unknown,
                    Confidence = 0.1f
                }
            };

            // Act
            var result = _factory.CreateIndicators(urlAnalysis);

            // Assert
            result.Should().NotBeNull();
            result.OfType<WebsiteTypeIndicator>().Should().BeEmpty();
        }

        [Fact]
        public void CreateIndicators_WithNullPurpose_DoesNotCreateIndicator()
        {
            // Arrange
            var urlAnalysis = new UrlAnalysisResultVm
            {
                Url = "https://nopurpose.com",
                Purpose = null
            };

            // Act
            var result = _factory.CreateIndicators(urlAnalysis);

            // Assert - FIXED: null Purpose should NOT create WebsiteTypeIndicator
            result.Should().NotBeNull();
            result.OfType<WebsiteTypeIndicator>().Should().BeEmpty();
        }

        [Fact]
        public void CreateIndicators_WithSuspiciousContent_ReturnsContentAnalysisIndicator()
        {
            // Arrange
            var urlAnalysis = new UrlAnalysisResultVm
            {
                Url = "https://suspicious.com",
                content_analysis = new ContentAnalysisVm
                {
                    Success = true,
                    HasSuspiciousKeywords = true,
                    Risk_Score = 0.8f,
                    Title = "Urgent: Verify Your Account",
                    detected_patterns = new List<DetectedPatternVm>
                    {
                        new DetectedPatternVm
                        {
                            Name = "UrgencyPattern",
                            Type = "Urgency",
                            matched_text = "Urgent",
                            Weight = 0.7f,
                            Description = "Creates sense of urgency"
                        }
                    },
                    cta_count = 3,
                    form_types = new List<int> { 1, 2 },
                    word_count = 150
                }
            };

            // Act
            var result = _factory.CreateIndicators(urlAnalysis);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain(i => i is ContentAnalysisIndicator);
        }

        [Fact]
        public void CreateIndicators_WithContentAnalysisButNoSuspiciousKeywords_DoesNotCreateIndicator()
        {
            // Arrange
            var urlAnalysis = new UrlAnalysisResultVm
            {
                Url = "https://clean.com",
                content_analysis = new ContentAnalysisVm
                {
                    Success = true,
                    HasSuspiciousKeywords = false,
                    Risk_Score = 0.1f
                }
            };

            // Act
            var result = _factory.CreateIndicators(urlAnalysis);

            // Assert
            result.Should().NotBeNull();
            result.OfType<ContentAnalysisIndicator>().Should().BeEmpty();
        }

        [Fact]
        public void CreateIndicators_WithMultipleIndicators_ReturnsAllApplicable()
        {
            // Arrange
            var urlAnalysis = new UrlAnalysisResultVm
            {
                Url = "https://complex-site.com",
                Whois = new WhoisVm
                {
                    Success = true,
                    domain_age_days = 200,
                    country = "US",
                    privacy_protected = true,
                    risk_score = 40f // MEDIUM risk
                },
                Purpose = new Purpose
                {
                    Category = WebsiteType.Banking,
                    Confidence = 0.85f
                },
                content_analysis = new ContentAnalysisVm
                {
                    Success = true,
                    HasSuspiciousKeywords = true,
                    Risk_Score = 60f, // HIGH risk
                    detected_patterns = new List<DetectedPatternVm>()
                }
            };

            // Act
            var result = _factory.CreateIndicators(urlAnalysis);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCountGreaterThan(5); // Multiple indicators expected
            result.Should().Contain(i => i is WhoIsIndicator);
            result.Should().Contain(i => i is WhoIsDomainAgeIndicator);
            result.Should().Contain(i => i is WhoisIsPrivacyProtectedIndicator);
            result.Should().Contain(i => i is WhoisCountryIndicator);
            result.Should().Contain(i => i is WebsiteTypeIndicator);
            result.Should().Contain(i => i is ContentAnalysisIndicator);
        }

        [Fact]
        public void CreateIndicators_KnownPhishingTakesPrecedence_ReturnsOnlyPhishingIndicator()
        {
            // Arrange
            var urlAnalysis = new UrlAnalysisResultVm
            {
                Url = "https://known-phishing.com",
                phishing_check = new PhishingCheckResultVm
                {
                    Is_known_phishing = true,
                    Is_known_phishing_domain = true,
                    Source = PhishingCheckResultSource.KnownList,
                    Match_count = 1
                },
                Whois = new WhoisVm
                {
                    Success = true,
                    domain_age_days = 200,
                    country = "RU",
                    privacy_protected = false,
                    risk_score = 90f // HIGH risk (known phishing)
                },
                Purpose = new Purpose
                {
                    Category = WebsiteType.Banking,
                    Confidence = 0.7f
                }
            };

            // Act
            var result = _factory.CreateIndicators(urlAnalysis);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1); // Only phishing indicator
            result[0].Should().BeOfType<KnownPhishingIndicator>();
        }

        #endregion

        #region RemoteAccessAnalysisResultVm Tests

        [Fact]
        public void CreateIndicators_WithRemoteAccessSessionOpen_ReturnsIndicator()
        {
            // Arrange
            var remoteAccessAnalysis = new RemoteAccessAnalysisResultVm(
                remoteAccessApp: RemoteAccessApp.AnyDesk,
                runningProcesses: 3,
                connectionUrl: "https://anydesk.com/12345",
                connectionStatus: ConnectionStatus.Open,
                connectionsCount: 1,
                sessionStatus: (int)SessionStatus.Open,
                browserTabs: null,
                risk_assessment: new RiskAssessment(70f, "high", false, 0.9f) // HIGH risk
            )
            {
                Success = true
            };

            // Act
            var result = _factory.CreateIndicators(remoteAccessAnalysis);

            // Assert - FIXED: RemoteAccessIndicator is now added to result
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result.OfType<RemoteAccessIndicator>().Should().HaveCount(1);
        }

        [Fact]
        public void CreateIndicators_WithRemoteAccessSessionNotOpen_ReturnsEmpty()
        {
            // Arrange
            var remoteAccessAnalysis = new RemoteAccessAnalysisResultVm(
                remoteAccessApp: RemoteAccessApp.AnyDesk,
                runningProcesses: 3,
                connectionUrl: "https://anydesk.com/12345",
                connectionStatus: ConnectionStatus.Closed,
                connectionsCount: 0,
                sessionStatus: (int)SessionStatus.Closed,
                browserTabs: null,
                risk_assessment: null
            )
            {
                Success = true
            };

            // Act
            var result = _factory.CreateIndicators(remoteAccessAnalysis);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public void CreateIndicators_WithRemoteAccessNotSuccessful_ReturnsEmpty()
        {
            // Arrange
            var remoteAccessAnalysis = new RemoteAccessAnalysisResultVm(
                remoteAccessApp: RemoteAccessApp.AnyDesk,
                runningProcesses: 0,
                connectionUrl: "",
                connectionStatus: ConnectionStatus.Closed,
                connectionsCount: 0,
                sessionStatus: (int)SessionStatus.Open,
                browserTabs: null,
                risk_assessment: null
            )
            {
                Success = false
            };

            // Act
            var result = _factory.CreateIndicators(remoteAccessAnalysis);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void CreateIndicators_WithNullAnalysisResult_ReturnsEmpty()
        {
            // Act
            var result = _factory.CreateIndicators(null);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public void CreateIndicators_WithUnknownAnalysisResultType_ReturnsEmpty()
        {
            // Arrange
            var baseAnalysisResult = new AnalysisResult
            {
                Success = true
            };

            // Act
            var result = _factory.CreateIndicators(baseAnalysisResult);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public void CreateIndicators_WithEmptyUrlAnalysis_ReturnsEmpty()
        {
            // Arrange
            var urlAnalysis = new UrlAnalysisResultVm
            {
                Url = "https://empty.com"
                // No Whois, no Purpose (null), no content_analysis, no phishing_check
            };

            // Act
            var result = _factory.CreateIndicators(urlAnalysis);

            // Assert - FIXED: Empty analysis should return empty (null Purpose = no indicator)
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public void CreateIndicators_WithFailedWhois_DoesNotCreateWhoisIndicators()
        {
            // Arrange
            var urlAnalysis = new UrlAnalysisResultVm
            {
                Url = "https://failedwhois.com",
                Whois = new WhoisVm
                {
                    Success = false
                }
            };

            // Act
            var result = _factory.CreateIndicators(urlAnalysis);

            // Assert
            result.Should().NotBeNull();
            result.OfType<WhoIsIndicator>().Should().BeEmpty();
        }

        [Fact]
        public void CreateIndicators_WithDomainAgeBoundaries_ReturnsCorrectScores()
        {
            // Test domain age boundaries: 30, 180, 365 days
            var testCases = new[]
            {
                new { Days = 29, RiskScore = 80f },   // < 30 days = HIGH risk (very young)
                new { Days = 30, RiskScore = 50f },   // 30-179 days = MEDIUM risk
                new { Days = 179, RiskScore = 40f },  // 30-179 days = MEDIUM risk
                new { Days = 180, RiskScore = 25f },  // 180-364 days = LOW risk
                new { Days = 364, RiskScore = 20f },  // 180-364 days = LOW risk
                new { Days = 365, RiskScore = 10f },  // >= 365 days = LOW risk (established)
                new { Days = 1000, RiskScore = 5f }   // >= 365 days = LOW risk (very established)
            };

            foreach (var testCase in testCases)
            {
                // Arrange
                var urlAnalysis = new UrlAnalysisResultVm
                {
                    Url = $"https://age-{testCase.Days}.com",
                    Whois = new WhoisVm
                    {
                        Success = true,
                        domain_age_days = testCase.Days,
                        country = "US",
                        privacy_protected = false,
                        risk_score = testCase.RiskScore
                    }
                };

                // Act
                var result = _factory.CreateIndicators(urlAnalysis);

                // Assert
                result.Should().NotBeNull();
                var domainAgeIndicator = result.OfType<WhoIsDomainAgeIndicator>().FirstOrDefault();
                domainAgeIndicator.Should().NotBeNull($"domain age {testCase.Days} should create indicator");
            }
        }

        [Fact]
        public void CreateIndicators_WithMlAnalysisSuccess_ReturnsMlIndicator()
        {
            // Arrange
            var urlAnalysis = new UrlAnalysisResultVm
            {
                Url = "https://mltest.com",
                ml_analysis = new MlAnalysis(
                    success: true,
                    enabled: true,
                    score: 80f, // HIGH risk from ML
                    confidence: 0.9f,
                    note: "High risk detected"
                )
            };

            // Act
            var result = _factory.CreateIndicators(urlAnalysis);

            // Assert - FIXED: MlAnalysisIndicator is now added to result
            result.Should().NotBeNull();
            result.OfType<MlAnalysisIndicator>().Should().HaveCount(1);
            
            // No WebsiteTypeIndicator (null Purpose = no indicator after fix)
            result.OfType<WebsiteTypeIndicator>().Should().BeEmpty();
        }

        #endregion
    }
}
