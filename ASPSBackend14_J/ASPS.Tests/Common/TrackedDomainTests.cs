using Xunit;
using FluentAssertions;
using Common.Entities;

namespace ASPS.Tests.Common
{
    public class TrackedDomainTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParams_CreatesInstance()
        {
            // Arrange
            var domain = "google-analytics.com";
            var category = "Analytics";

            // Act
            var result = new TrackedDomain(domain, category);

            // Assert
            result.Should().NotBeNull();
            result.Domain.Should().Be("google-analytics.com");
            result.Category.Should().Be("Analytics");
            result.IsActive.Should().BeTrue();
            result.DateCreated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
            result.DateModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
            result.DateDeleted.Should().BeNull();
        }

        [Fact]
        public void Constructor_NormalizesDomain_ToLowerCase()
        {
            // Arrange
            var domain = "Google-Analytics.COM";
            var category = "Analytics";

            // Act
            var result = new TrackedDomain(domain, category);

            // Assert
            result.Domain.Should().Be("google-analytics.com");
        }

        [Fact]
        public void Constructor_TrimsDomain_RemovesWhitespace()
        {
            // Arrange
            var domain = "  google-analytics.com  ";
            var category = "Analytics";

            // Act
            var result = new TrackedDomain(domain, category);

            // Assert
            result.Domain.Should().Be("google-analytics.com");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_WithNullOrEmptyDomain_ThrowsArgumentException(string? domain)
        {
            // Arrange
            var category = "Analytics";

            // Act
            Action act = () => new TrackedDomain(domain!, category);

            // Assert
            act.Should().Throw<ArgumentException>()
               .WithParameterName("domain");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_WithNullOrEmptyCategory_ThrowsArgumentException(string? category)
        {
            // Arrange
            var domain = "google-analytics.com";

            // Act
            Action act = () => new TrackedDomain(domain, category!);

            // Assert
            act.Should().Throw<ArgumentException>()
               .WithParameterName("category");
        }

        #endregion

        #region Update Tests

        [Fact]
        public void Update_WithNewCategory_UpdatesCategory()
        {
            // Arrange
            var domain = new TrackedDomain("google-analytics.com", "Analytics");
            var oldModified = domain.DateModified;
            Thread.Sleep(10);

            // Act
            domain.Update(category: "Advertising");

            // Assert
            domain.Category.Should().Be("Advertising");
            domain.DateModified.Should().BeAfter(oldModified);
        }

        [Fact]
        public void Update_WithNewActiveStatus_UpdatesIsActive()
        {
            // Arrange
            var domain = new TrackedDomain("google-analytics.com", "Analytics");

            // Act
            domain.Update(isActive: false);

            // Assert
            domain.IsActive.Should().BeFalse();
        }

        [Fact]
        public void Update_WithBothParams_UpdatesBoth()
        {
            // Arrange
            var domain = new TrackedDomain("google-analytics.com", "Analytics");

            // Act
            domain.Update(category: "Social", isActive: false);

            // Assert
            domain.Category.Should().Be("Social");
            domain.IsActive.Should().BeFalse();
        }

        [Fact]
        public void Update_WithNullParams_DoesNotChange()
        {
            // Arrange
            var domain = new TrackedDomain("google-analytics.com", "Analytics");
            var originalCategory = domain.Category;
            var originalActive = domain.IsActive;

            // Act
            domain.Update();

            // Assert
            domain.Category.Should().Be(originalCategory);
            domain.IsActive.Should().Be(originalActive);
        }

        #endregion

        #region Delete Tests

        [Fact]
        public void Delete_SetsDateDeleted()
        {
            // Arrange
            var domain = new TrackedDomain("google-analytics.com", "Analytics");

            // Act
            domain.Delete();

            // Assert
            domain.DateDeleted.Should().NotBeNull();
            domain.DateDeleted.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        }

        [Fact]
        public void Delete_UpdatesDateModified()
        {
            // Arrange
            var domain = new TrackedDomain("google-analytics.com", "Analytics");
            var oldModified = domain.DateModified;
            Thread.Sleep(10);

            // Act
            domain.Delete();

            // Assert
            domain.DateModified.Should().BeAfter(oldModified);
        }

        #endregion

        #region IsEnabled Tests

        [Fact]
        public void IsEnabled_WhenActiveAndNotDeleted_ReturnsTrue()
        {
            // Arrange
            var domain = new TrackedDomain("google-analytics.com", "Analytics");

            // Act & Assert
            domain.IsEnabled.Should().BeTrue();
        }

        [Fact]
        public void IsEnabled_WhenDeleted_ReturnsFalse()
        {
            // Arrange
            var domain = new TrackedDomain("google-analytics.com", "Analytics");
            domain.Delete();

            // Act & Assert
            domain.IsEnabled.Should().BeFalse();
        }

        [Fact]
        public void IsEnabled_WhenInactive_ReturnsFalse()
        {
            // Arrange
            var domain = new TrackedDomain("google-analytics.com", "Analytics");
            domain.Update(isActive: false);

            // Act & Assert
            domain.IsEnabled.Should().BeFalse();
        }

        [Fact]
        public void IsEnabled_WhenDeletedAndInactive_ReturnsFalse()
        {
            // Arrange
            var domain = new TrackedDomain("google-analytics.com", "Analytics");
            domain.Update(isActive: false);
            domain.Delete();

            // Act & Assert
            domain.IsEnabled.Should().BeFalse();
        }

        #endregion
    }
}
