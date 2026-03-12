using Common.Entities;
using FluentAssertions;
using Xunit;

namespace ASPS.Tests.Common;

public class DeviceTokenEntityTests
{
    #region Property Assignment Tests

    [Fact]
    public void DeviceUid_CanBeSetAndRetrieved()
    {
        // Arrange
        var entity = new DeviceTokenEntity();
        var deviceUid = "device-12345";

        // Act
        entity.DeviceUid = deviceUid;

        // Assert
        entity.DeviceUid.Should().Be(deviceUid);
    }

    [Fact]
    public void TokenValue_CanBeSetAndRetrieved()
    {
        // Arrange
        var entity = new DeviceTokenEntity();
        var tokenValue = "token-abcdef123456";

        // Act
        entity.TokenValue = tokenValue;

        // Assert
        entity.TokenValue.Should().Be(tokenValue);
    }

    [Fact]
    public void UserKeyField_CanBeSetAndRetrieved()
    {
        // Arrange
        var entity = new DeviceTokenEntity();
        var userKeyField = "user-key-789";

        // Act
        entity.UserKeyField = userKeyField;

        // Assert
        entity.UserKeyField.Should().Be(userKeyField);
    }

    [Fact]
    public void DateCreated_CanBeSetAndRetrieved()
    {
        // Arrange
        var entity = new DeviceTokenEntity();
        var dateCreated = new DateTime(2026, 3, 12, 10, 0, 0, DateTimeKind.Utc);

        // Act
        entity.DateCreated = dateCreated;

        // Assert
        entity.DateCreated.Should().Be(dateCreated);
    }

    [Fact]
    public void Expiration_CanBeSetAndRetrieved()
    {
        // Arrange
        var entity = new DeviceTokenEntity();
        var expiration = new DateTime(2026, 4, 12, 10, 0, 0, DateTimeKind.Utc);

        // Act
        entity.Expiration = expiration;

        // Assert
        entity.Expiration.Should().Be(expiration);
    }

    #endregion

    #region Default Values Tests

    [Fact]
    public void DefaultConstructor_SetsEmptyStrings()
    {
        // Act
        var entity = new DeviceTokenEntity();

        // Assert
        entity.DeviceUid.Should().BeEmpty();
        entity.TokenValue.Should().BeEmpty();
        entity.UserKeyField.Should().BeEmpty();
    }

    [Fact]
    public void DefaultConstructor_SetsDefaultDateTimes()
    {
        // Act
        var entity = new DeviceTokenEntity();

        // Assert
        entity.DateCreated.Should().Be(default(DateTime));
        entity.Expiration.Should().Be(default(DateTime));
    }

    #endregion

    #region Object Initializer Tests

    [Fact]
    public void ObjectInitializer_CanSetAllProperties()
    {
        // Arrange
        var deviceUid = "device-123";
        var tokenValue = "token-abc";
        var userKeyField = "user-456";
        var dateCreated = DateTime.UtcNow;
        var expiration = DateTime.UtcNow.AddDays(30);

        // Act
        var entity = new DeviceTokenEntity
        {
            DeviceUid = deviceUid,
            TokenValue = tokenValue,
            UserKeyField = userKeyField,
            DateCreated = dateCreated,
            Expiration = expiration
        };

        // Assert
        entity.DeviceUid.Should().Be(deviceUid);
        entity.TokenValue.Should().Be(tokenValue);
        entity.UserKeyField.Should().Be(userKeyField);
        entity.DateCreated.Should().Be(dateCreated);
        entity.Expiration.Should().Be(expiration);
    }

    #endregion

    #region Token Expiration Logic Tests

    [Fact]
    public void IsExpired_WhenExpirationInPast_ShouldBeTrue()
    {
        // Arrange
        var entity = new DeviceTokenEntity
        {
            Expiration = DateTime.UtcNow.AddDays(-1)
        };

        // Act
        var isExpired = entity.Expiration < DateTime.UtcNow;

        // Assert
        isExpired.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WhenExpirationInFuture_ShouldBeFalse()
    {
        // Arrange
        var entity = new DeviceTokenEntity
        {
            Expiration = DateTime.UtcNow.AddDays(30)
        };

        // Act
        var isExpired = entity.Expiration < DateTime.UtcNow;

        // Assert
        isExpired.Should().BeFalse();
    }

    [Fact]
    public void TokenLifespan_CanBeCalculated()
    {
        // Arrange
        var dateCreated = DateTime.UtcNow;
        var expiration = dateCreated.AddDays(30);
        var entity = new DeviceTokenEntity
        {
            DateCreated = dateCreated,
            Expiration = expiration
        };

        // Act
        var lifespan = entity.Expiration - entity.DateCreated;

        // Assert
        lifespan.TotalDays.Should().BeApproximately(30, 0.01);
    }

    #endregion

    #region Edge Cases

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    [InlineData("very-long-device-uid-with-many-characters-123456789")]
    public void DeviceUid_AcceptsVariousInputs(string deviceUid)
    {
        // Arrange
        var entity = new DeviceTokenEntity();

        // Act
        entity.DeviceUid = deviceUid;

        // Assert
        entity.DeviceUid.Should().Be(deviceUid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ")] // JWT-like
    public void TokenValue_AcceptsVariousInputs(string tokenValue)
    {
        // Arrange
        var entity = new DeviceTokenEntity();

        // Act
        entity.TokenValue = tokenValue;

        // Assert
        entity.TokenValue.Should().Be(tokenValue);
    }

    [Fact]
    public void SameDeviceUid_CanHaveMultipleTokens()
    {
        // Arrange
        var deviceUid = "device-123";
        
        var token1 = new DeviceTokenEntity
        {
            DeviceUid = deviceUid,
            TokenValue = "token-1"
        };
        
        var token2 = new DeviceTokenEntity
        {
            DeviceUid = deviceUid,
            TokenValue = "token-2"
        };

        // Assert
        token1.DeviceUid.Should().Be(token2.DeviceUid);
        token1.TokenValue.Should().NotBe(token2.TokenValue);
    }

    [Fact]
    public void PastDates_CanBeAssigned()
    {
        // Arrange
        var pastDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var entity = new DeviceTokenEntity
        {
            DateCreated = pastDate,
            Expiration = pastDate.AddDays(30)
        };

        // Assert
        entity.DateCreated.Should().Be(pastDate);
        entity.Expiration.Should().BeAfter(pastDate);
    }

    [Fact]
    public void FutureDates_CanBeAssigned()
    {
        // Arrange
        var futureDate = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var entity = new DeviceTokenEntity
        {
            DateCreated = DateTime.UtcNow,
            Expiration = futureDate
        };

        // Assert
        entity.Expiration.Should().Be(futureDate);
    }

    #endregion

    #region Business Logic Tests

    [Fact]
    public void TypicalScenario_30DayToken()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var entity = new DeviceTokenEntity
        {
            DeviceUid = "mobile-device-001",
            TokenValue = Guid.NewGuid().ToString(),
            UserKeyField = "user-123",
            DateCreated = now,
            Expiration = now.AddDays(30)
        };

        // Assert
        entity.DeviceUid.Should().NotBeEmpty();
        entity.TokenValue.Should().NotBeEmpty();
        entity.UserKeyField.Should().NotBeEmpty();
        entity.Expiration.Should().BeAfter(entity.DateCreated);
        (entity.Expiration - entity.DateCreated).TotalDays.Should().BeApproximately(30, 0.01);
    }

    [Fact]
    public void RemainingTime_CanBeCalculated()
    {
        // Arrange
        var entity = new DeviceTokenEntity
        {
            Expiration = DateTime.UtcNow.AddHours(24)
        };

        // Act
        var remainingTime = entity.Expiration - DateTime.UtcNow;

        // Assert
        remainingTime.TotalHours.Should().BeApproximately(24, 1);
    }

    #endregion
}
