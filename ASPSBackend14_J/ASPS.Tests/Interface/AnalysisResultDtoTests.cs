using System;
using FluentAssertions;
using Interface.Analysis;
using Xunit;

namespace ASPS.Tests.Interface;

public class AnalysisResultDtoTests
{
    #region Constructor Tests

    [Fact]
    public void DefaultConstructor_CreatesInstance_WithUtcTimestamp()
    {
        // Act
        var result = new AnalysisResultDto();

        // Assert
        result.Should().NotBeNull();
        result.UserKeyField.Should().BeEmpty();
        result.Discriminator.Should().BeEmpty();
        result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var userKeyField = "user-123";
        var discriminator = "TestAnalysis";
        var timestamp = new DateTime(2026, 3, 12, 10, 30, 0, DateTimeKind.Utc);
        var jsonValue = "{\"result\":\"test\"}";

        // Act
        var result = new AnalysisResultDto(
            userKeyField, discriminator, timestamp, jsonValue);

        // Assert
        result.Should().NotBeNull();
        result.UserKeyField.Should().Be(userKeyField);
        result.Discriminator.Should().Be(discriminator);
        result.Timestamp.Should().Be(timestamp);
        result.JsonValue.Should().Be(jsonValue);
        result.HasError.Should().BeFalse();
        result.ErrorMessage.Should().BeNull();
        result.IsFromCache.Should().BeFalse();
        result.DeviceAlertKeyField.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithErrorParameters_SetsErrorData()
    {
        // Arrange
        var errorMessage = "Test error occurred";
        var timestamp = DateTime.UtcNow;

        // Act
        var result = new AnalysisResultDto(
            "user-123", "Test", timestamp, null, true, errorMessage);

        // Assert
        result.HasError.Should().BeTrue();
        result.ErrorMessage.Should().Be(errorMessage);
        result.JsonValue.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullUserKeyField_ThrowsArgumentNullException()
    {
        // Arrange
        string? userKeyField = null;

        // Act
        Action act = () => new AnalysisResultDto(
            userKeyField!, "Test", DateTime.UtcNow);

        // Assert
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("userKeyField");
    }

    [Fact]
    public void Constructor_WithNullDiscriminator_ThrowsArgumentNullException()
    {
        // Arrange
        string? discriminator = null;

        // Act
        Action act = () => new AnalysisResultDto(
            "user-123", discriminator!, DateTime.UtcNow);

        // Assert
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("discriminator");
    }

    #endregion

    #region Property Tests

    [Fact]
    public void UserKeyField_CanBeSetAndRetrieved()
    {
        // Arrange
        var dto = new AnalysisResultDto();
        var userKeyField = "user-456";

        // Act
        dto.UserKeyField = userKeyField;

        // Assert
        dto.UserKeyField.Should().Be(userKeyField);
    }

    [Fact]
    public void Discriminator_CanBeSetAndRetrieved()
    {
        // Arrange
        var dto = new AnalysisResultDto();
        var discriminator = "CustomAnalysis";

        // Act
        dto.Discriminator = discriminator;

        // Assert
        dto.Discriminator.Should().Be(discriminator);
    }

    [Fact]
    public void JsonValue_CanBeSetAndRetrieved()
    {
        // Arrange
        var dto = new AnalysisResultDto();
        var jsonValue = "{\"data\":\"value\"}";

        // Act
        dto.JsonValue = jsonValue;

        // Assert
        dto.JsonValue.Should().Be(jsonValue);
    }

    [Fact]
    public void HasError_CanBeSetAndRetrieved()
    {
        // Arrange
        var dto = new AnalysisResultDto();

        // Act
        dto.HasError = true;

        // Assert
        dto.HasError.Should().BeTrue();
    }

    [Fact]
    public void ErrorMessage_CanBeSetAndRetrieved()
    {
        // Arrange
        var dto = new AnalysisResultDto();
        var errorMessage = "Something went wrong";

        // Act
        dto.ErrorMessage = errorMessage;

        // Assert
        dto.ErrorMessage.Should().Be(errorMessage);
    }

    [Fact]
    public void Timestamp_CanBeSetAndRetrieved()
    {
        // Arrange
        var dto = new AnalysisResultDto();
        var timestamp = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        dto.Timestamp = timestamp;

        // Assert
        dto.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void IsFromCache_CanBeSetAndRetrieved()
    {
        // Arrange
        var dto = new AnalysisResultDto();

        // Act
        dto.IsFromCache = true;

        // Assert
        dto.IsFromCache.Should().BeTrue();
    }

    [Fact]
    public void DeviceAlertKeyField_CanBeSetAndRetrieved()
    {
        // Arrange
        var dto = new AnalysisResultDto();
        var alertKeyField = "alert-789";

        // Act
        dto.DeviceAlertKeyField = alertKeyField;

        // Assert
        dto.DeviceAlertKeyField.Should().Be(alertKeyField);
    }

    #endregion

    #region Edge Cases

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"complex\":{\"nested\":\"data\"}}")]
    public void JsonValue_WithVariousInputs_AcceptsValue(string? jsonValue)
    {
        // Act
        var dto = new AnalysisResultDto(
            "user-123", "Test", DateTime.UtcNow, jsonValue);

        // Assert
        dto.JsonValue.Should().Be(jsonValue);
    }

    [Fact]
    public void Constructor_WithPastTimestamp_PreservesTimestamp()
    {
        // Arrange
        var pastTimestamp = DateTime.UtcNow.AddDays(-30);

        // Act
        var dto = new AnalysisResultDto(
            "user-123", "Test", pastTimestamp);

        // Assert
        dto.Timestamp.Should().Be(pastTimestamp);
    }

    [Fact]
    public void Constructor_WithFutureTimestamp_PreservesTimestamp()
    {
        // Arrange
        var futureTimestamp = DateTime.UtcNow.AddDays(30);

        // Act
        var dto = new AnalysisResultDto(
            "user-123", "Test", futureTimestamp);

        // Assert
        dto.Timestamp.Should().Be(futureTimestamp);
    }

    [Theory]
    [InlineData(true, "Error occurred")]
    [InlineData(true, "")]
    [InlineData(true, null)]
    [InlineData(false, null)]
    public void Constructor_WithErrorCombinations_SetsCorrectly(bool hasError, string? errorMessage)
    {
        // Act
        var dto = new AnalysisResultDto(
            "user-123", "Test", DateTime.UtcNow, null, hasError, errorMessage);

        // Assert
        dto.HasError.Should().Be(hasError);
        dto.ErrorMessage.Should().Be(errorMessage);
    }

    [Fact]
    public void Constructor_WithAllOptionalParameters_SetsDefaults()
    {
        // Act
        var dto = new AnalysisResultDto(
            "user-123", "Test", DateTime.UtcNow);

        // Assert
        dto.JsonValue.Should().BeNull();
        dto.HasError.Should().BeFalse();
        dto.ErrorMessage.Should().BeNull();
        dto.IsFromCache.Should().BeFalse();
        dto.DeviceAlertKeyField.Should().BeNull();
    }

    #endregion

    #region Serialization Tests

    [Fact]
    public void DTO_CanBeInstantiatedForSerialization()
    {
        // Arrange & Act
        var dto = new AnalysisResultDto
        {
            UserKeyField = "user-999",
            Discriminator = "SerializationTest",
            Timestamp = DateTime.UtcNow,
            JsonValue = "{\"test\":true}",
            HasError = false,
            IsFromCache = true
        };

        // Assert
        dto.Should().NotBeNull();
        dto.UserKeyField.Should().Be("user-999");
        dto.Discriminator.Should().Be("SerializationTest");
        dto.JsonValue.Should().Be("{\"test\":true}");
        dto.IsFromCache.Should().BeTrue();
    }

    #endregion
}

public class UrlAnalysisResultDtoTests
{
    #region Constructor Tests

    [Fact]
    public void DefaultConstructor_CreatesInstance()
    {
        // Act
        var result = new UrlAnalysisResultDto();

        // Assert
        result.Should().NotBeNull();
        result.Domain.Should().BeEmpty();
        result.Url.Should().BeEmpty();
        result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var userKeyField = "user-123";
        var discriminator = "UrlAnalysis";
        var timestamp = DateTime.UtcNow;
        var domain = "example.com";
        var url = "https://example.com/test";
        var jsonValue = "{\"url_data\":\"test\"}";

        // Act
        var result = new UrlAnalysisResultDto(
            userKeyField, discriminator, timestamp, domain, url, jsonValue);

        // Assert
        result.Should().NotBeNull();
        result.UserKeyField.Should().Be(userKeyField);
        result.Discriminator.Should().Be(discriminator);
        result.Timestamp.Should().Be(timestamp);
        result.Domain.Should().Be(domain);
        result.Url.Should().Be(url);
        result.JsonValue.Should().Be(jsonValue);
    }

    [Fact]
    public void Constructor_WithNullDomain_ThrowsArgumentNullException()
    {
        // Arrange
        string? domain = null;

        // Act
        Action act = () => new UrlAnalysisResultDto(
            "user-123", "Test", DateTime.UtcNow, domain!, "https://test.com");

        // Assert
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("domain");
    }

    [Fact]
    public void Constructor_WithNullUrl_ThrowsArgumentNullException()
    {
        // Arrange
        string? url = null;

        // Act
        Action act = () => new UrlAnalysisResultDto(
            "user-123", "Test", DateTime.UtcNow, "example.com", url!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("url");
    }

    #endregion

    #region Property Tests

    [Fact]
    public void Domain_CanBeSetAndRetrieved()
    {
        // Arrange
        var dto = new UrlAnalysisResultDto();
        var domain = "test.com";

        // Act
        dto.Domain = domain;

        // Assert
        dto.Domain.Should().Be(domain);
    }

    [Fact]
    public void Url_CanBeSetAndRetrieved()
    {
        // Arrange
        var dto = new UrlAnalysisResultDto();
        var url = "https://test.com/path";

        // Act
        dto.Url = url;

        // Assert
        dto.Url.Should().Be(url);
    }

    #endregion

    #region Edge Cases

    [Theory]
    [InlineData("example.com")]
    [InlineData("sub.example.com")]
    [InlineData("deep.sub.example.com")]
    [InlineData("xn--80akhbyknj4f.xn--p1ai")] // Internationalized domain
    public void Domain_WithVariousFormats_AcceptsValue(string domain)
    {
        // Act
        var dto = new UrlAnalysisResultDto(
            "user-123", "Test", DateTime.UtcNow, domain, "https://test.com");

        // Assert
        dto.Domain.Should().Be(domain);
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://example.com")]
    [InlineData("https://example.com:8080")]
    [InlineData("https://example.com/path?query=value")]
    [InlineData("https://user:pass@example.com/path#fragment")]
    public void Url_WithVariousFormats_AcceptsValue(string url)
    {
        // Act
        var dto = new UrlAnalysisResultDto(
            "user-123", "Test", DateTime.UtcNow, "example.com", url);

        // Assert
        dto.Url.Should().Be(url);
    }

    [Fact]
    public void Constructor_WithErrorData_SetsErrorProperties()
    {
        // Arrange
        var errorMessage = "URL analysis failed";

        // Act
        var dto = new UrlAnalysisResultDto(
            "user-123", "UrlAnalysis", DateTime.UtcNow, 
            "example.com", "https://example.com", null, true, errorMessage);

        // Assert
        dto.HasError.Should().BeTrue();
        dto.ErrorMessage.Should().Be(errorMessage);
        dto.Domain.Should().Be("example.com");
        dto.Url.Should().Be("https://example.com");
    }

    #endregion

    #region Inheritance Tests

    [Fact]
    public void UrlAnalysisResultDto_InheritsFromAnalysisResultDto()
    {
        // Arrange
        var dto = new UrlAnalysisResultDto();

        // Assert
        dto.Should().BeAssignableTo<AnalysisResultDto>();
    }

    [Fact]
    public void UrlAnalysisResultDto_CanAccessBaseProperties()
    {
        // Arrange
        var dto = new UrlAnalysisResultDto(
            "user-123", "UrlAnalysis", DateTime.UtcNow, 
            "example.com", "https://example.com");

        // Act
        dto.IsFromCache = true;
        dto.DeviceAlertKeyField = "alert-123";

        // Assert
        dto.IsFromCache.Should().BeTrue();
        dto.DeviceAlertKeyField.Should().Be("alert-123");
        dto.UserKeyField.Should().Be("user-123");
        dto.Discriminator.Should().Be("UrlAnalysis");
    }

    #endregion
}
