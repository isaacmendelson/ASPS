using Xunit;
using FluentAssertions;
using Common.Exceptions;
using Common.Enums;
using Common.Models;

namespace ASPS.Tests.Common;

public class ErrorMessageTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithKeyAndMessage_CreatesInstance()
    {
        // Arrange
        var key = "TestError";
        var message = "Test error message";

        // Act
        var result = new ErrorMessage(key, message);

        // Assert
        result.Should().NotBeNull();
        result.Key.Should().Be(key);
        result.Message.Should().Be(message);
        result.StatusCode.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithKeyMessageAndStatusCode_CreatesInstance()
    {
        // Arrange
        var key = "ValidationError";
        var message = "Validation failed";
        var statusCode = ResultStatusCode.ValidationError;

        // Act
        var result = new ErrorMessage(key, message, statusCode);

        // Assert
        result.Should().NotBeNull();
        result.Key.Should().Be(key);
        result.Message.Should().Be(message);
        result.StatusCode.Should().Be(statusCode);
    }

    [Fact]
    public void Constructor_WithFileAndMethod_CreatesInstance()
    {
        // Arrange
        var key = "ServerError";
        var message = "System error occurred";
        var fromFile = "TestFile.cs";
        var fromMethod = "TestMethod";
        var statusCode = ResultStatusCode.ServerError;

        // Act
        var result = new ErrorMessage(key, message, fromFile, fromMethod, statusCode);

        // Assert
        result.Should().NotBeNull();
        result.Key.Should().Be(key);
        result.Message.Should().Be(message);
        result.StatusCode.Should().Be(statusCode);
    }

    #endregion

    #region AddParam Tests

    [Fact]
    public void AddParam_WithBoolValue_AddsToData()
    {
        // Arrange
        var error = new ErrorMessage("TestKey", "Test message");
        var paramKey = "IsValid";
        var paramValue = true;

        // Act
        var result = error.AddParam(paramKey, paramValue);

        // Assert
        result.Should().BeSameAs(error); // Fluent API
        error[paramKey].Should().Be(paramValue);
        error.GetDataKeys().Should().Contain(paramKey);
    }

    [Fact]
    public void AddParam_WithIntValue_AddsToData()
    {
        // Arrange
        var error = new ErrorMessage("TestKey", "Test message");
        var paramKey = "Count";
        var paramValue = 42;

        // Act
        var result = error.AddParam(paramKey, paramValue);

        // Assert
        result.Should().BeSameAs(error);
        error[paramKey].Should().Be(paramValue);
    }

    [Fact]
    public void AddParam_WithStringValue_AddsToData()
    {
        // Arrange
        var error = new ErrorMessage("TestKey", "Test message");
        var paramKey = "Username";
        var paramValue = "testuser";

        // Act
        var result = error.AddParam(paramKey, paramValue);

        // Assert
        result.Should().BeSameAs(error);
        error[paramKey].Should().Be(paramValue);
    }

    [Fact]
    public void AddParam_WithKeyValue_AddsToData()
    {
        // Arrange
        var error = new ErrorMessage("TestKey", "Test message");
        var paramKey = "EntityKey";
        var paramValue = new Key("User", "123");

        // Act
        var result = error.AddParam(paramKey, paramValue);

        // Assert
        result.Should().BeSameAs(error);
        error[paramKey].Should().Be(paramValue.ToString());
    }

    [Fact]
    public void AddParam_WithTagValue_AddsToData()
    {
        // Arrange
        var error = new ErrorMessage("TestKey", "Test message");
        var paramKey = "EntityTag";
        var tag = new Tag(new Key("User", "1"), "Test User", "");

        // Act
        var result = error.AddParam(paramKey, tag);

        // Assert
        result.Should().BeSameAs(error);
        error[paramKey].Should().Be(tag);
    }

    [Fact]
    public void AddParam_WithStringEnumerable_AddsToData()
    {
        // Arrange
        var error = new ErrorMessage("TestKey", "Test message");
        var paramKey = "Items";
        var paramValue = new[] { "item1", "item2", "item3" };

        // Act
        var result = error.AddParam(paramKey, paramValue);

        // Assert
        result.Should().BeSameAs(error);
        error[paramKey].Should().BeEquivalentTo(paramValue);
    }

    [Fact]
    public void AddParam_WithKeyEnumerable_AddsToData()
    {
        // Arrange
        var error = new ErrorMessage("TestKey", "Test message");
        var paramKey = "Keys";
        var keys = new[] { new Key("User", "1"), new Key("User", "2") };

        // Act
        var result = error.AddParam(paramKey, keys);

        // Assert
        result.Should().BeSameAs(error);
        var dataKeys = error[paramKey] as Key[];
        dataKeys.Should().HaveCount(2);
    }

    [Fact]
    public void AddParam_WithTagEnumerable_AddsToData()
    {
        // Arrange
        var error = new ErrorMessage("TestKey", "Test message");
        var paramKey = "Tags";
        var tags = new[]
        {
            new Tag(new Key("User", "1"), "User 1", ""),
            new Tag(new Key("User", "2"), "User 2", "")
        };

        // Act
        var result = error.AddParam(paramKey, tags);

        // Assert
        result.Should().BeSameAs(error);
        var dataTags = error[paramKey] as Tag[];
        dataTags.Should().HaveCount(2);
    }

    #endregion

    #region AddParamIfNotNull Tests

    [Fact]
    public void AddParamIfNotNull_WithNullString_DoesNotAdd()
    {
        // Arrange
        var error = new ErrorMessage("TestKey", "Test message");
        string? nullValue = null;

        // Act
        var result = error.AddParamIfNotNull("NullParam", nullValue);

        // Assert
        result.Should().BeSameAs(error);
        error.GetDataKeys().Should().NotContain("NullParam");
    }

    [Fact]
    public void AddParamIfNotNull_WithValidString_AddsToData()
    {
        // Arrange
        var error = new ErrorMessage("TestKey", "Test message");
        var paramValue = "validValue";

        // Act
        var result = error.AddParamIfNotNull("ValidParam", paramValue);

        // Assert
        result.Should().BeSameAs(error);
        error["ValidParam"].Should().Be(paramValue);
    }

    [Fact]
    public void AddParamIfNotNull_WithNullKey_DoesNotAdd()
    {
        // Arrange
        var error = new ErrorMessage("TestKey", "Test message");
        Key? nullKey = null;

        // Act
        var result = error.AddParamIfNotNull("NullKey", nullKey);

        // Assert
        result.Should().BeSameAs(error);
        error.GetDataKeys().Should().NotContain("NullKey");
    }

    [Fact]
    public void AddParamIfNotNull_WithValidKey_AddsToData()
    {
        // Arrange
        var error = new ErrorMessage("TestKey", "Test message");
        var key = new Key("User", "123");

        // Act
        var result = error.AddParamIfNotNull("ValidKey", key);

        // Assert
        result.Should().BeSameAs(error);
        error["ValidKey"].Should().Be(key);
    }

    [Fact]
    public void AddParamIfNotNull_WithNullTag_DoesNotAdd()
    {
        // Arrange
        var error = new ErrorMessage("TestKey", "Test message");
        Tag? nullTag = null;

        // Act
        var result = error.AddParamIfNotNull("NullTag", nullTag);

        // Assert
        result.Should().BeSameAs(error);
        error.GetDataKeys().Should().NotContain("NullTag");
    }

    [Fact]
    public void AddParamIfNotNull_WithValidTag_AddsToData()
    {
        // Arrange
        var error = new ErrorMessage("TestKey", "Test message");
        var tag = new Tag(new Key("User", "1"), "Test User", "");

        // Act
        var result = error.AddParamIfNotNull("ValidTag", tag);

        // Assert
        result.Should().BeSameAs(error);
        error["ValidTag"].Should().Be(tag);
    }

    #endregion

    #region GetDataKeys Tests

    [Fact]
    public void GetDataKeys_WithNoParams_ReturnsEmptyList()
    {
        // Arrange
        var error = new ErrorMessage("TestKey", "Test message");

        // Act
        var result = error.GetDataKeys();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetDataKeys_WithMultipleParams_ReturnsAllKeys()
    {
        // Arrange
        var error = new ErrorMessage("TestKey", "Test message")
            .AddParam("Key1", "Value1")
            .AddParam("Key2", 42)
            .AddParam("Key3", true);

        // Act
        var result = error.GetDataKeys();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain("Key1");
        result.Should().Contain("Key2");
        result.Should().Contain("Key3");
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsKey()
    {
        // Arrange
        var key = "TestErrorKey";
        var error = new ErrorMessage(key, "Test message");

        // Act
        var result = error.ToString();

        // Assert
        result.Should().Be(key);
    }

    #endregion

    #region Create Factory Method Tests

    [Fact]
    public void Create_WithKeyAndMessage_CreatesInstance()
    {
        // Arrange
        var key = "FactoryError";
        var message = "Factory created error";

        // Act
        var result = ErrorMessage.Create(key, message);

        // Assert
        result.Should().NotBeNull();
        result.Key.Should().Be(key);
        result.Message.Should().Be(message);
        result.StatusCode.Should().BeNull();
    }

    [Fact]
    public void Create_WithStatusCode_CreatesInstance()
    {
        // Arrange
        var key = "FactoryError";
        var message = "Factory created error";
        var statusCode = ResultStatusCode.ValidationError;

        // Act
        var result = ErrorMessage.Create(key, message, statusCode);

        // Assert
        result.Should().NotBeNull();
        result.Key.Should().Be(key);
        result.Message.Should().Be(message);
        result.StatusCode.Should().Be(statusCode);
    }

    #endregion

    #region ParameterValueInvalid Factory Tests

    [Fact]
    public void ParameterValueInvalid_Create_CreatesValidationError()
    {
        // Arrange
        var message = "Invalid value provided";
        var parameterName = "Username";

        // Act
        var result = ParameterValueInvalid.Create(message, parameterName);

        // Assert
        result.Should().NotBeNull();
        result.Key.Should().Be("ParameterValueInvalid");
        result.Message.Should().Be(message);
        result.StatusCode.Should().Be(ResultStatusCode.ValidationError);
        result["ParameterName"].Should().Be(parameterName);
    }

    #endregion

    #region Fluent API Chaining Tests

    [Fact]
    public void FluentAPI_MultipleAddParams_ChainCorrectly()
    {
        // Arrange & Act
        var error = new ErrorMessage("ChainTest", "Testing fluent API")
            .AddParam("Param1", "Value1")
            .AddParam("Param2", 42)
            .AddParam("Param3", true)
            .AddParamIfNotNull("Param4", "Value4");

        // Assert
        error.GetDataKeys().Should().HaveCount(4);
        error["Param1"].Should().Be("Value1");
        error["Param2"].Should().Be(42);
        error["Param3"].Should().Be(true);
        error["Param4"].Should().Be("Value4");
    }

    #endregion

    #region OriginalRaisedErrorKey Tests

    [Fact]
    public void OriginalRaisedErrorKey_CanBeSetAndGet()
    {
        // Arrange
        var error = new ErrorMessage("TestKey", "Test message");
        var originalKey = "OriginalError";

        // Act
        error.OriginalRaisedErrorKey = originalKey;

        // Assert
        error.OriginalRaisedErrorKey.Should().Be(originalKey);
    }

    [Fact]
    public void OriginalRaisedErrorKey_DefaultsToNull()
    {
        // Arrange & Act
        var error = new ErrorMessage("TestKey", "Test message");

        // Assert
        error.OriginalRaisedErrorKey.Should().BeNull();
    }

    #endregion
}
