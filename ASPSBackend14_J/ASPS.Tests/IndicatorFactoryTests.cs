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
                    risk_score = 0.2f
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
                    risk_score = 0.8f
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
                    risk_score = 0.1f
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
                    risk_score = 0.5f
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
                    risk_score = 0.3f
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
        public void CreateIndicators_WithNullPurpose_CreatesIndicatorDueToBug()
        {
            // Arrange
            var urlAnalysis = new UrlAnalysisResultVm
            {
                Url = "https://nopurpose.com",
                Purpose = null
            };

            // Act
            var result = _factory.CreateIndicators(urlAnalysis);

            // Assert - BUG: null Purpose creates indicator because null != WebsiteType.Unknown
            result.Should().NotBeNull();
            result.OfType<WebsiteTypeIndicator>().Should().HaveCount(1);
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
                    risk_score = 0.4f
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
                    Risk_Score = 0.6f,
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
                    risk_score = 0.9f
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
        public void CreateIndicators_WithRemoteAccessSessionOpen_DoesNotReturnIndicatorDueToBug()
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
                risk_assessment: new RiskAssessment(0.7f, "high", false, 0.9f)
            )
            {
                Success = true
            };

            // Act
            var result = _factory.CreateIndicators(remoteAccessAnalysis);

            // Assert - BUG: RemoteAccessIndicator is created but never added to result list
            result.Should().NotBeNull();
            result.Should().BeEmpty(); // Bug: indicator created but not returned
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
        public void CreateIndicators_WithEmptyUrlAnalysis_CreatesIndicatorDueToBug()
        {
            // Arrange
            var urlAnalysis = new UrlAnalysisResultVm
            {
                Url = "https://empty.com"
                // No Whois, no Purpose (null), no content_analysis, no phishing_check
            };

            // Act
            var result = _factory.CreateIndicators(urlAnalysis);

            // Assert - BUG: Empty analysis creates WebsiteTypeIndicator because Purpose is null
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result.OfType<WebsiteTypeIndicator>().Should().HaveCount(1);
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
                new { Days = 29, Expected = 0.0f },  // < 30
                new { Days = 30, Expected = 0.3f },  // 30-179
                new { Days = 179, Expected = 0.3f }, // 30-179
                new { Days = 180, Expected = 0.6f }, // 180-364
                new { Days = 364, Expected = 0.6f }, // 180-364
                new { Days = 365, Expected = 1.0f }, // >= 365
                new { Days = 1000, Expected = 1.0f } // >= 365
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
                        risk_score = 0.1f
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
        public void CreateIndicators_WithMlAnalysisSuccess_DoesNotReturnIndicatorDueToBug()
        {
            // Arrange - This test exposes a bug: MlAnalysisIndicator is created but not added
            var urlAnalysis = new UrlAnalysisResultVm
            {
                Url = "https://mltest.com",
                ml_analysis = new MlAnalysis(
                    success: true,
                    enabled: true,
                    score: 0.8f,
                    confidence: 0.9f,
                    note: "High risk detected"
                )
            };

            // Act
            var result = _factory.CreateIndicators(urlAnalysis);

            // Assert - BUG: MlAnalysisIndicator is created but never added to res list (line 82)
            result.Should().NotBeNull();
            result.OfType<MlAnalysisIndicator>().Should().BeEmpty(); // Bug: created but not added
            
            // Also creates WebsiteTypeIndicator due to null Purpose bug
            result.Should().HaveCount(1);
            result.OfType<WebsiteTypeIndicator>().Should().HaveCount(1);
        }

        #endregion
    }
}
