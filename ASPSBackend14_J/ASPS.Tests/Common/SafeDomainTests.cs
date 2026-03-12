using Common.Entities;
using Xunit;

namespace ASPS.Tests.Common;

public class SafeDomainTests
{
    [Fact]
    public void SafeDomain_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var safeDomain = new SafeDomain();

        // Assert
        Assert.Equal(0, safeDomain.Id);
        Assert.Equal(string.Empty, safeDomain.Domain);
        Assert.Null(safeDomain.DateCreated);
        Assert.False(safeDomain.IsDeleted);
    }

    [Fact]
    public void SafeDomain_SetDomain_StoresValueCorrectly()
    {
        // Arrange
        var safeDomain = new SafeDomain();
        const string expectedDomain = "example.com";

        // Act
        safeDomain.Domain = expectedDomain;

        // Assert
        Assert.Equal(expectedDomain, safeDomain.Domain);
    }

    [Fact]
    public void SafeDomain_SetDateCreated_StoresValueCorrectly()
    {
        // Arrange
        var safeDomain = new SafeDomain();
        var expectedDate = DateTime.UtcNow;

        // Act
        safeDomain.DateCreated = expectedDate;

        // Assert
        Assert.Equal(expectedDate, safeDomain.DateCreated);
    }

    [Fact]
    public void SafeDomain_SetIsDeleted_StoresValueCorrectly()
    {
        // Arrange
        var safeDomain = new SafeDomain();

        // Act
        safeDomain.IsDeleted = true;

        // Assert
        Assert.True(safeDomain.IsDeleted);
    }

    [Fact]
    public void SafeDomain_AllProperties_CanBeSet()
    {
        // Arrange
        var safeDomain = new SafeDomain
        {
            Id = 123,
            Domain = "secure-site.org",
            DateCreated = new DateTime(2026, 3, 12),
            IsDeleted = false
        };

        // Assert
        Assert.Equal(123, safeDomain.Id);
        Assert.Equal("secure-site.org", safeDomain.Domain);
        Assert.Equal(new DateTime(2026, 3, 12), safeDomain.DateCreated);
        Assert.False(safeDomain.IsDeleted);
    }

    [Theory]
    [InlineData("")]
    [InlineData("google.com")]
    [InlineData("sub.domain.example.co.uk")]
    public void SafeDomain_Domain_AcceptsDifferentFormats(string domain)
    {
        // Arrange & Act
        var safeDomain = new SafeDomain { Domain = domain };

        // Assert
        Assert.Equal(domain, safeDomain.Domain);
    }

    [Fact]
    public void SafeDomain_DomainMaxLength_IsEnforced()
    {
        // Arrange
        var longDomain = new string('a', 255);
        var safeDomain = new SafeDomain();

        // Act
        safeDomain.Domain = longDomain;

        // Assert
        Assert.Equal(255, safeDomain.Domain.Length);
        Assert.Equal(longDomain, safeDomain.Domain);
    }
}
