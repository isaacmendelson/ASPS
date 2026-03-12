using Common.Entities;
using Common.Enums;
using Common.Models;
using Xunit;

namespace ASPS.Tests.Common;

public class UserAccountTests
{
    [Fact]
    public void UserAccount_TypeName_ReturnsCorrectValue()
    {
        // Arrange & Act
        var account = new UserAccount();

        // Assert
        Assert.Equal("UserAccount", account.TypeName);
    }

    [Fact]
    public void UserAccount_InheritsFromEntity()
    {
        // Arrange & Act
        var account = new UserAccount();

        // Assert
        Assert.IsAssignableFrom<Entity>(account);
    }

    [Fact]
    public void UserAccount_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var account = new UserAccount();

        // Assert
        Assert.Equal(string.Empty, account.UserKeyField);
        Assert.Equal((AccountType)0, account.AccountType); // Default enum value is 0
        Assert.Equal(string.Empty, account.LoginUrl);
        Assert.Equal(string.Empty, account.UserName);
        Assert.Equal(string.Empty, account.PasswordHash);
        Assert.False(account.Is2FactorAuth);
        Assert.Equal(string.Empty, account.LoginPhoneNumber);
    }

    [Fact]
    public void UserAccount_UserKeyField_CanBeSet()
    {
        // Arrange
        var account = new UserAccount();
        const string expectedUserKey = "user-123";

        // Act
        account.UserKeyField = expectedUserKey;

        // Assert
        Assert.Equal(expectedUserKey, account.UserKeyField);
    }

    [Fact]
    public void UserAccount_UserKey_SetsUserKeyField()
    {
        // Arrange
        var account = new UserAccount();
        var userKey = new Key("User", "user-456");

        // Act
        account.UserKey = userKey;

        // Assert
        Assert.Equal("user-456", account.UserKeyField);
        Assert.NotNull(account.UserKey);
        Assert.Equal("User", account.UserKey.Type);
        Assert.Equal("user-456", account.UserKey.Value);
    }

    [Fact]
    public void UserAccount_UserKey_GetsFromUserKeyField()
    {
        // Arrange
        var account = new UserAccount
        {
            UserKeyField = "user-789"
        };

        // Act
        var userKey = account.UserKey;

        // Assert
        Assert.NotNull(userKey);
        Assert.Equal("User", userKey.Type);
        Assert.Equal("user-789", userKey.Value);
    }

    [Theory]
    [InlineData(AccountType.Email)]
    [InlineData(AccountType.Social)]
    [InlineData(AccountType.Financial)]
    [InlineData(AccountType.Communication)]
    [InlineData(AccountType.Other)]
    public void UserAccount_AccountType_AcceptsValidValues(AccountType accountType)
    {
        // Arrange & Act
        var account = new UserAccount { AccountType = accountType };

        // Assert
        Assert.Equal(accountType, account.AccountType);
    }

    [Fact]
    public void UserAccount_LoginUrl_CanBeSet()
    {
        // Arrange
        var account = new UserAccount();
        const string expectedUrl = "https://mail.google.com";

        // Act
        account.LoginUrl = expectedUrl;

        // Assert
        Assert.Equal(expectedUrl, account.LoginUrl);
    }

    [Fact]
    public void UserAccount_UserName_CanBeSet()
    {
        // Arrange
        var account = new UserAccount();
        const string expectedUserName = "john.doe@example.com";

        // Act
        account.UserName = expectedUserName;

        // Assert
        Assert.Equal(expectedUserName, account.UserName);
    }

    [Fact]
    public void UserAccount_PasswordHash_CanBeSet()
    {
        // Arrange
        var account = new UserAccount();
        const string expectedHash = "$2a$10$abcdefghijklmnopqrstuv";

        // Act
        account.PasswordHash = expectedHash;

        // Assert
        Assert.Equal(expectedHash, account.PasswordHash);
    }

    [Fact]
    public void UserAccount_Is2FactorAuth_CanBeSetToTrue()
    {
        // Arrange
        var account = new UserAccount();

        // Act
        account.Is2FactorAuth = true;

        // Assert
        Assert.True(account.Is2FactorAuth);
    }

    [Fact]
    public void UserAccount_LoginPhoneNumber_CanBeSet()
    {
        // Arrange
        var account = new UserAccount();
        const string expectedPhone = "+1234567890";

        // Act
        account.LoginPhoneNumber = expectedPhone;

        // Assert
        Assert.Equal(expectedPhone, account.LoginPhoneNumber);
    }

    [Fact]
    public void UserAccount_AllProperties_CanBeSet()
    {
        // Arrange
        var account = new UserAccount
        {
            UserKeyField = "user-001",
            AccountType = AccountType.Email,
            LoginUrl = "https://gmail.com",
            UserName = "test@example.com",
            PasswordHash = "hashed_password_123",
            Is2FactorAuth = true,
            LoginPhoneNumber = "+9876543210"
        };

        // Assert
        Assert.Equal("user-001", account.UserKeyField);
        Assert.Equal(AccountType.Email, account.AccountType);
        Assert.Equal("https://gmail.com", account.LoginUrl);
        Assert.Equal("test@example.com", account.UserName);
        Assert.Equal("hashed_password_123", account.PasswordHash);
        Assert.True(account.Is2FactorAuth);
        Assert.Equal("+9876543210", account.LoginPhoneNumber);
    }

    [Fact]
    public void UserAccount_Tag_GeneratesCorrectly_WithUserName()
    {
        // Arrange
        var account = new UserAccount
        {
            KeyField = "UserAccount-001",
            UserName = "john.doe@example.com",
            AccountType = AccountType.Email
        };

        // Act
        var tag = account.Tag;

        // Assert
        Assert.NotNull(tag);
        Assert.Equal("john.doe@example.com", tag.Name);
        Assert.Equal("UserAccount", tag.Type);
    }

    [Fact]
    public void UserAccount_Tag_FallsBackToAccountType()
    {
        // Arrange
        var account = new UserAccount
        {
            KeyField = "UserAccount-002",
            UserName = "",
            AccountType = AccountType.Social
        };

        // Act
        var tag = account.Tag;

        // Assert
        Assert.NotNull(tag);
        Assert.Equal("Social", tag.Name);
        Assert.Equal("UserAccount", tag.Type);
    }

    [Fact]
    public void UserAccount_Tag_IsCached()
    {
        // Arrange
        var account = new UserAccount
        {
            KeyField = "UserAccount-003",
            UserName = "cached@test.com"
        };

        // Act
        var tag1 = account.Tag;
        var tag2 = account.Tag;

        // Assert
        Assert.Same(tag1, tag2); // Should return the same cached instance
    }
}
