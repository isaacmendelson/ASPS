using Common.Entities;
using Common.Models;
using FluentAssertions;
using Xunit;

namespace ASPS.Tests.Common;

public class AnalysisResultContainerTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var keyField = "test-key";
        var userKeyField = "user-key";
        var discriminator = "TestAnalysis";
        var timestamp = DateTime.UtcNow;
        var jsonValue = "{\"result\":\"test\"}";

        // Act
        var result = new AnalysisResultContainer(
            keyField, userKeyField, discriminator, timestamp, jsonValue, false, null);

        // Assert
        result.Should().NotBeNull();
        result.UserKeyField.Should().Be(userKeyField);
        result.Discriminator.Should().Be(discriminator);
        result.JsonValue.Should().Be(jsonValue);
        result.HasError.Should().BeFalse();
        result.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void Constructor_WithErrorParameters_SetsErrorData()
    {
        // Arrange
        var errorMessage = "Test error";

        // Act
        var result = new AnalysisResultContainer(
            "key", "user-key", "Test", DateTime.UtcNow, null, true, errorMessage);

        // Assert
        result.HasError.Should().BeTrue();
        result.ErrorMessage.Should().Be(errorMessage);
        result.JsonValue.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithCacheFlag_SetsIsFromCache()
    {
        // Arrange & Act
        var result = new AnalysisResultContainer(
            "key", "user-key", "Test", DateTime.UtcNow, "{}", false, null, isFromCache: true);

        // Assert
        result.IsFromCache.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithDeviceAlert_SetsDeviceAlertKeyField()
    {
        // Arrange
        var deviceAlertKeyField = "alert-key";

        // Act
        var result = new AnalysisResultContainer(
            "key", "user-key", "Test", DateTime.UtcNow, "{}", false, null, 
            deviceAlertKeyField: deviceAlertKeyField);

        // Assert
        result.DeviceAlertKeyField.Should().Be(deviceAlertKeyField);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public void Constructor_IsFromCacheDefaults_ToFalseWhenNull(bool? isFromCache)
    {
        // Act
        var result = new AnalysisResultContainer(
            "key", "user-key", "Test", DateTime.UtcNow, "{}", false, null, isFromCache: isFromCache);

        // Assert
        if (isFromCache.HasValue)
            result.IsFromCache.Should().Be(isFromCache.Value);
        else
            result.IsFromCache.Should().BeFalse();
    }

    #endregion

    #region UserKey Property Tests

    [Fact]
    public void UserKey_Get_ReturnsKeyFromUserKeyField()
    {
        // Arrange
        var userKeyField = "test-user-key";
        var result = new AnalysisResultContainer(
            "key", userKeyField, "Test", DateTime.UtcNow, "{}", false, null);

        // Act
        var userKey = result.UserKey;

        // Assert
        userKey.Should().NotBeNull();
        userKey.Type.Should().Be("User");
        userKey.Value.Should().Be(userKeyField);
    }

    [Fact]
    public void UserKey_Set_UpdatesUserKeyField()
    {
        // Arrange
        var result = new AnalysisResultContainer(
            "key", "original", "Test", DateTime.UtcNow, "{}", false, null);
        var newKey = new Key("User", "new-user-key");

        // Act
        result.UserKey = newKey;

        // Assert
        result.UserKeyField.Should().Be("new-user-key");
        result.UserKey.Value.Should().Be("new-user-key");
    }

    #endregion

    #region DeviceAlertKey Property Tests

    [Fact]
    public void DeviceAlertKey_Get_WithNullField_ReturnsNull()
    {
        // Arrange
        var result = new AnalysisResultContainer(
            "key", "user-key", "Test", DateTime.UtcNow, "{}", false, null);

        // Act
        var alertKey = result.DeviceAlertKey;

        // Assert
        alertKey.Should().BeNull();
    }

    [Fact]
    public void DeviceAlertKey_Get_WithValidField_ReturnsKey()
    {
        // Arrange
        var alertKeyField = "alert-key";
        var result = new AnalysisResultContainer(
            "key", "user-key", "Test", DateTime.UtcNow, "{}", false, null, 
            deviceAlertKeyField: alertKeyField);

        // Act
        var alertKey = result.DeviceAlertKey;

        // Assert
        alertKey.Should().NotBeNull();
        alertKey!.Type.Should().Be("DeviceAlertEntity");
        alertKey.Value.Should().Be(alertKeyField);
    }

    [Fact]
    public void DeviceAlertKey_Set_UpdatesDeviceAlertKeyField()
    {
        // Arrange
        var result = new AnalysisResultContainer(
            "key", "user-key", "Test", DateTime.UtcNow, "{}", false, null);
        var newKey = new Key("DeviceAlertEntity", "new-alert-key");

        // Act
        result.DeviceAlertKey = newKey;

        // Assert
        result.DeviceAlertKeyField.Should().Be("new-alert-key");
        result.DeviceAlertKey!.Value.Should().Be("new-alert-key");
    }

    [Fact]
    public void DeviceAlertKey_SetToNull_ClearsDeviceAlertKeyField()
    {
        // Arrange
        var result = new AnalysisResultContainer(
            "key", "user-key", "Test", DateTime.UtcNow, "{}", false, null, 
            deviceAlertKeyField: "alert-key");

        // Act
        result.DeviceAlertKey = null;

        // Assert
        result.DeviceAlertKeyField.Should().BeNull();
        result.DeviceAlertKey.Should().BeNull();
    }

    #endregion

    #region Tag Property Tests

    [Fact]
    public void Tag_CreatesTagWithDiscriminatorAndTimestamp()
    {
        // Arrange
        var discriminator = "TestAnalysis";
        var timestamp = new DateTime(2026, 3, 12, 10, 30, 0, DateTimeKind.Utc);
        var result = new AnalysisResultContainer(
            "key", "user-key", discriminator, timestamp, "{}", false, null);

        // Act
        var tag = result.Tag;

        // Assert
        tag.Should().NotBeNull();
        tag.Name.Should().Contain(discriminator);
        tag.Name.Should().Contain("2026-03-12");
        tag.Name.Should().Contain("10:30");
    }

    [Fact]
    public void Tag_TypeName_IsAnalysisResultContainer()
    {
        // Arrange
        var result = new AnalysisResultContainer(
            "key", "user-key", "Test", DateTime.UtcNow, "{}", false, null);

        // Act & Assert
        result.TypeName.Should().Be("AnalysisResultContainer");
    }

    #endregion

    #region Edge Cases

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{\"data\":null}")]
    public void Constructor_WithVariousJsonValues_AcceptsInput(string? jsonValue)
    {
        // Act
        var result = new AnalysisResultContainer(
            "key", "user-key", "Test", DateTime.UtcNow, jsonValue, false, null);

        // Assert
        result.JsonValue.Should().Be(jsonValue);
    }

    [Fact]
    public void Constructor_WithPastTimestamp_PreservesTimestamp()
    {
        // Arrange
        var pastTimestamp = DateTime.UtcNow.AddHours(-24);

        // Act
        var result = new AnalysisResultContainer(
            "key", "user-key", "Test", pastTimestamp, "{}", false, null);

        // Assert
        result.Timestamp.Should().Be(pastTimestamp);
    }

    [Fact]
    public void Constructor_WithFutureTimestamp_PreservesTimestamp()
    {
        // Arrange
        var futureTimestamp = DateTime.UtcNow.AddHours(24);

        // Act
        var result = new AnalysisResultContainer(
            "key", "user-key", "Test", futureTimestamp, "{}", false, null);

        // Assert
        result.Timestamp.Should().Be(futureTimestamp);
    }

    #endregion
}

public class UrlAnalysisResultContainerTests
{
    #region Basic Properties

    [Fact]
    public void Domain_CanBeSetAndRetrieved()
    {
        // Arrange
        var result = new UrlAnalysisResultContainer
        {
            Domain = "example.com"
        };

        // Act & Assert
        result.Domain.Should().Be("example.com");
    }

    [Fact]
    public void Url_CanBeSetAndRetrieved()
    {
        // Arrange
        var url = "https://example.com/path";
        var result = new UrlAnalysisResultContainer
        {
            Url = url
        };

        // Act & Assert
        result.Url.Should().Be(url);
    }

    [Fact]
    public void TypeName_IsUrlAnalysisResult()
    {
        // Arrange
        var result = new UrlAnalysisResultContainer();

        // Act & Assert
        result.TypeName.Should().Be("UrlAnalysisResult");
    }

    #endregion

    #region Default Values

    [Fact]
    public void DefaultConstructor_SetsEmptyStrings()
    {
        // Act
        var result = new UrlAnalysisResultContainer();

        // Assert
        result.Domain.Should().BeEmpty();
        result.Url.Should().BeEmpty();
    }

    #endregion

    #region Edge Cases

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("example.com")]
    [InlineData("sub.example.com")]
    [InlineData("deep.sub.example.com")]
    public void Domain_AcceptsVariousFormats(string domain)
    {
        // Arrange
        var result = new UrlAnalysisResultContainer
        {
            Domain = domain
        };

        // Assert
        result.Domain.Should().Be(domain);
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://example.com")]
    [InlineData("https://example.com:8080/path?query=value")]
    [InlineData("")]
    public void Url_AcceptsVariousFormats(string url)
    {
        // Arrange
        var result = new UrlAnalysisResultContainer
        {
            Url = url
        };

        // Assert
        result.Url.Should().Be(url);
    }

    #endregion
}
