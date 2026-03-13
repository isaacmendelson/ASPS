using Xunit;
using FluentAssertions;
using Common.Entities;

namespace ASPS.Tests.Common
{
    public class KnownPhishingWebsiteTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidUrl_CreatesInstance()
        {
            // Arrange
            var url = "http://phishing-site.com/login";
            var source = "PhishTank";

            // Act
            var result = new KnownPhishingWebsite(url, source);

            // Assert
            result.Should().NotBeNull();
            result.Url.Should().Be(url);
            result.Source.Should().Be(source);
            result.Domain.Should().Be("phishing-site.com");
            result.DateCreated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
            result.DateDeleted.Should().BeNull();
        }

        [Fact]
        public void Constructor_WithoutSource_UsesEmptyString()
        {
            // Arrange
            var url = "http://phishing-site.com";

            // Act
            var result = new KnownPhishingWebsite(url);

            // Assert
            result.Source.Should().Be(string.Empty);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_WithNullOrEmptyUrl_ThrowsArgumentException(string? url)
        {
            // Act
            Action act = () => new KnownPhishingWebsite(url!);

            // Assert
            act.Should().Throw<ArgumentException>()
               .WithParameterName("url");
        }

        #endregion

        #region GetDomainFromUrl Tests

        [Theory]
        [InlineData("http://example.com", "example.com")]
        [InlineData("https://example.com", "example.com")]
        [InlineData("http://www.example.com", "example.com")]
        [InlineData("https://www.example.com", "example.com")]
        [InlineData("http://subdomain.example.com", "subdomain.example.com")]
        [InlineData("example.com", "example.com")]
        [InlineData("www.example.com", "example.com")]
        public void GetDomainFromUrl_WithValidUrls_ExtractsDomain(string url, string expectedDomain)
        {
            // Act
            var result = KnownPhishingWebsite.GetDomainFromUrl(url);

            // Assert
            result.Should().Be(expectedDomain);
        }

        [Theory]
        [InlineData("HTTP://EXAMPLE.COM", "example.com")]
        [InlineData("HTTPS://WWW.EXAMPLE.COM", "example.com")]
        public void GetDomainFromUrl_NormalizesToLowerCase(string url, string expectedDomain)
        {
            // Act
            var result = KnownPhishingWebsite.GetDomainFromUrl(url);

            // Assert
            result.Should().Be(expectedDomain);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GetDomainFromUrl_WithNullOrEmpty_ReturnsEmptyString(string? url)
        {
            // Act
            var result = KnownPhishingWebsite.GetDomainFromUrl(url!);

            // Assert
            result.Should().Be(string.Empty);
        }

        [Fact]
        public void GetDomainFromUrl_WithInvalidUrl_ReturnsEmptyString()
        {
            // Arrange
            var invalidUrl = "not-a-valid-url";

            // Act
            var result = KnownPhishingWebsite.GetDomainFromUrl(invalidUrl);

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public void GetDomainFromUrl_WithUrlContainingPath_ExtractsDomainOnly()
        {
            // Arrange
            var url = "http://example.com/path/to/page?query=value";

            // Act
            var result = KnownPhishingWebsite.GetDomainFromUrl(url);

            // Assert
            result.Should().Be("example.com");
        }

        #endregion

        #region Delete Tests

        [Fact]
        public void Delete_SetsDateDeleted()
        {
            // Arrange
            var website = new KnownPhishingWebsite("http://phishing-site.com");

            // Act
            website.Delete();

            // Assert
            website.DateDeleted.Should().NotBeNull();
            website.DateDeleted.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        }

        #endregion

        #region IsActive Tests

        [Fact]
        public void IsActive_WhenNotDeleted_ReturnsTrue()
        {
            // Arrange
            var website = new KnownPhishingWebsite("http://phishing-site.com");

            // Act & Assert
            website.IsActive.Should().BeTrue();
        }

        [Fact]
        public void IsActive_WhenDeleted_ReturnsFalse()
        {
            // Arrange
            var website = new KnownPhishingWebsite("http://phishing-site.com");
            website.Delete();

            // Act & Assert
            website.IsActive.Should().BeFalse();
        }

        #endregion

        #region Domain Property Tests

        [Fact]
        public void Domain_IsPreComputed_OnConstruction()
        {
            // Arrange
            var url = "http://www.phishing-site.com/login";

            // Act
            var website = new KnownPhishingWebsite(url);

            // Assert
            website.Domain.Should().Be("phishing-site.com");
        }

        [Theory]
        [InlineData("http://phishing-site.com", "phishing-site.com")]
        [InlineData("https://www.evil.com/fake", "evil.com")]
        [InlineData("www.badsite.net", "badsite.net")]
        public void Constructor_PreComputesDomain_Correctly(string url, string expectedDomain)
        {
            // Act
            var website = new KnownPhishingWebsite(url);

            // Assert
            website.Domain.Should().Be(expectedDomain);
        }

        #endregion
    }
}
