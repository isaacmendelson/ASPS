using Common.Entities;
using Common.Enums;
using Common.Models;
using Xunit;

namespace ASPS.Tests.Common;

public class UserTests
{
    [Fact]
    public void User_TypeName_ReturnsCorrectValue()
    {
        // Arrange & Act
        var user = new User();

        // Assert
        Assert.Equal("User", user.TypeName);
    }

    [Fact]
    public void User_InheritsFromEntity()
    {
        // Arrange & Act
        var user = new User();

        // Assert
        Assert.IsAssignableFrom<Entity>(user);
    }

    [Fact]
    public void User_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var user = new User();

        // Assert
        Assert.Equal(string.Empty, user.KeycloakUserId);
        Assert.Equal(string.Empty, user.FirstName);
        Assert.Equal(string.Empty, user.LastName);
        Assert.Equal(string.Empty, user.Address);
        Assert.Equal(string.Empty, user.City);
        Assert.Equal(string.Empty, user.State);
        Assert.Equal(string.Empty, user.Zip);
        Assert.Equal(string.Empty, user.Country);
        Assert.Equal(string.Empty, user.PhoneNumber);
        Assert.Equal(string.Empty, user.Email);
        Assert.Equal((UserRole)0, user.Role); // Default enum value
        Assert.Null(user.GuardianKey);
        Assert.Null(user.Locale);
        Assert.Null(user.Timezone);
    }

    [Fact]
    public void User_KeycloakUserId_CanBeSet()
    {
        // Arrange
        var user = new User();
        const string expectedId = "keycloak-uuid-123";

        // Act
        user.KeycloakUserId = expectedId;

        // Assert
        Assert.Equal(expectedId, user.KeycloakUserId);
    }

    [Fact]
    public void User_FirstName_CanBeSet()
    {
        // Arrange
        var user = new User();
        const string expectedFirstName = "John";

        // Act
        user.FirstName = expectedFirstName;

        // Assert
        Assert.Equal(expectedFirstName, user.FirstName);
    }

    [Fact]
    public void User_LastName_CanBeSet()
    {
        // Arrange
        var user = new User();
        const string expectedLastName = "Doe";

        // Act
        user.LastName = expectedLastName;

        // Assert
        Assert.Equal(expectedLastName, user.LastName);
    }

    [Fact]
    public void User_Address_CanBeSet()
    {
        // Arrange
        var user = new User();
        const string expectedAddress = "123 Main Street";

        // Act
        user.Address = expectedAddress;

        // Assert
        Assert.Equal(expectedAddress, user.Address);
    }

    [Fact]
    public void User_City_CanBeSet()
    {
        // Arrange
        var user = new User();
        const string expectedCity = "New York";

        // Act
        user.City = expectedCity;

        // Assert
        Assert.Equal(expectedCity, user.City);
    }

    [Fact]
    public void User_PhoneNumber_CanBeSet()
    {
        // Arrange
        var user = new User();
        const string expectedPhone = "+1234567890";

        // Act
        user.PhoneNumber = expectedPhone;

        // Assert
        Assert.Equal(expectedPhone, user.PhoneNumber);
    }

    [Fact]
    public void User_Email_CanBeSet()
    {
        // Arrange
        var user = new User();
        const string expectedEmail = "john.doe@example.com";

        // Act
        user.Email = expectedEmail;

        // Assert
        Assert.Equal(expectedEmail, user.Email);
    }

    [Theory]
    [InlineData(UserRole.Unknown)]
    [InlineData(UserRole.Self)]
    [InlineData(UserRole.Guardian)]
    [InlineData(UserRole.Other)]
    public void User_Role_AcceptsValidValues(UserRole role)
    {
        // Arrange & Act
        var user = new User { Role = role };

        // Assert
        Assert.Equal(role, user.Role);
    }

    [Fact]
    public void User_GuardianKey_CanBeSet()
    {
        // Arrange
        var user = new User();

        // Act
        user.GuardianKey = 123;

        // Assert
        Assert.Equal(123, user.GuardianKey);
    }

    [Fact]
    public void User_Locale_CanBeSet()
    {
        // Arrange
        var user = new User();
        const string expectedLocale = "en-US";

        // Act
        user.Locale = expectedLocale;

        // Assert
        Assert.Equal(expectedLocale, user.Locale);
    }

    [Fact]
    public void User_Timezone_CanBeSet()
    {
        // Arrange
        var user = new User();

        // Act
        user.Timezone = -5; // EST

        // Assert
        Assert.Equal(-5, user.Timezone);
    }

    [Fact]
    public void User_AllProperties_CanBeSet()
    {
        // Arrange
        var user = new User
        {
            KeycloakUserId = "kc-user-001",
            FirstName = "Jane",
            LastName = "Smith",
            Address = "456 Oak Avenue",
            City = "Los Angeles",
            State = "CA",
            Zip = "90001",
            Country = "USA",
            PhoneNumber = "+1987654321",
            Email = "jane.smith@example.com",
            Role = UserRole.Self,
            GuardianKey = 999,
            Locale = "en-US",
            Timezone = -8
        };

        // Assert
        Assert.Equal("kc-user-001", user.KeycloakUserId);
        Assert.Equal("Jane", user.FirstName);
        Assert.Equal("Smith", user.LastName);
        Assert.Equal("456 Oak Avenue", user.Address);
        Assert.Equal("Los Angeles", user.City);
        Assert.Equal("CA", user.State);
        Assert.Equal("90001", user.Zip);
        Assert.Equal("USA", user.Country);
        Assert.Equal("+1987654321", user.PhoneNumber);
        Assert.Equal("jane.smith@example.com", user.Email);
        Assert.Equal(UserRole.Self, user.Role);
        Assert.Equal(999, user.GuardianKey);
        Assert.Equal("en-US", user.Locale);
        Assert.Equal(-8, user.Timezone);
    }

    [Fact]
    public void User_Tag_GeneratesCorrectly_WithFirstAndLastName()
    {
        // Arrange
        var user = new User
        {
            KeyField = "User-001",
            FirstName = "Alice",
            LastName = "Johnson",
            KeycloakUserId = "kc-alice"
        };

        // Act
        var tag = user.Tag;

        // Assert
        Assert.NotNull(tag);
        Assert.Equal("Alice Johnson", tag.Name);
        Assert.Equal("User", tag.Type);
    }

    [Fact]
    public void User_Tag_GeneratesCorrectly_WithOnlyFirstName()
    {
        // Arrange
        var user = new User
        {
            KeyField = "User-002",
            FirstName = "Bob",
            LastName = "",
            KeycloakUserId = "kc-bob"
        };

        // Act
        var tag = user.Tag;

        // Assert
        Assert.NotNull(tag);
        Assert.Equal("Bob", tag.Name);
        Assert.Equal("User", tag.Type);
    }

    [Fact]
    public void User_Tag_FallsBackToKeycloakUserId()
    {
        // Arrange
        var user = new User
        {
            KeyField = "User-003",
            FirstName = "",
            LastName = "",
            KeycloakUserId = "kc-user-xyz"
        };

        // Act
        var tag = user.Tag;

        // Assert
        Assert.NotNull(tag);
        Assert.Equal("kc-user-xyz", tag.Name);
        Assert.Equal("User", tag.Type);
    }

    [Fact]
    public void User_Tag_IsCached()
    {
        // Arrange
        var user = new User
        {
            KeyField = "User-004",
            FirstName = "Charlie",
            LastName = "Brown"
        };

        // Act
        var tag1 = user.Tag;
        var tag2 = user.Tag;

        // Assert
        Assert.Same(tag1, tag2); // Should return the same cached instance
    }

    [Fact]
    public void User_NullableFields_CanBeNull()
    {
        // Arrange & Act
        var user = new User
        {
            GuardianKey = null,
            Locale = null,
            Timezone = null
        };

        // Assert
        Assert.Null(user.GuardianKey);
        Assert.Null(user.Locale);
        Assert.Null(user.Timezone);
    }
}
