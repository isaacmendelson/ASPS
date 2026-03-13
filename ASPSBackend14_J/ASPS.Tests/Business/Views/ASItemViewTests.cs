using Xunit;
using FluentAssertions;
using Business.Views;
using Common.Models;

namespace ASPS.Tests.Business.Views;

public class ASItemViewTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithTag_CreatesInstance()
    {
        // Arrange
        var key = new Key("User", "123");
        var name = "Test User";
        var tag = new Tag(key, name, "");

        // Act
        var result = new ASItemView(tag);

        // Assert
        result.Should().NotBeNull();
        result.Tag.Should().Be(tag);
        result.Key.Should().Be(key);
        result.Name.Should().Be(name);
    }

    [Fact]
    public void Constructor_WithTag_SetsPropertiesCorrectly()
    {
        // Arrange
        var key = new Key("Device", "456");
        var name = "Device Name";
        var tag = new Tag(key, name, "description");

        // Act
        var result = new ASItemView(tag);

        // Assert
        result.Tag.Should().BeSameAs(tag);
        result.Key.Should().Be(key);
        result.Key.Value.Should().Be("456");
        result.Name.Should().Be(name);
    }

    [Fact]
    public void Constructor_WithKeyAndName_CreatesInstance()
    {
        // Arrange
        var key = new Key("Account", "789");
        var name = "Test Account";

        // Act
        var result = new ASItemView(key, name);

        // Assert
        result.Should().NotBeNull();
        result.Key.Should().Be(key);
        result.Name.Should().Be(name);
        result.Tag.Should().NotBeNull();
        result.Tag.Key.Should().Be(key);
        result.Tag.Name.Should().Be(name);
    }

    [Fact]
    public void Constructor_WithKeyAndNullName_CreatesInstanceWithEmptyString()
    {
        // Arrange
        var key = new Key("User", "100");
        string? name = null;

        // Act
        var result = new ASItemView(key, name);

        // Assert
        result.Should().NotBeNull();
        result.Key.Should().Be(key);
        result.Name.Should().Be(string.Empty);
        result.Tag.Name.Should().Be(string.Empty);
    }

    [Fact]
    public void Constructor_WithKeyAndEmptyName_CreatesInstance()
    {
        // Arrange
        var key = new Key("Device", "200");
        var name = "";

        // Act
        var result = new ASItemView(key, name);

        // Assert
        result.Should().NotBeNull();
        result.Key.Should().Be(key);
        result.Name.Should().Be(string.Empty);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void Key_CanBeSet()
    {
        // Arrange
        var originalKey = new Key("User", "1");
        var tag = new Tag(originalKey, "Name", "");
        var view = new ASItemView(tag);
        var newKey = new Key("User", "2");

        // Act
        view.Key = newKey;

        // Assert
        view.Key.Should().Be(newKey);
    }

    [Fact]
    public void Tag_IsReadOnly()
    {
        // Arrange
        var key = new Key("User", "1");
        var tag = new Tag(key, "Name", "");

        // Act
        var view = new ASItemView(tag);

        // Assert
        view.Tag.Should().BeSameAs(tag);
        // Tag property should be private set (compile-time check)
    }

    [Fact]
    public void Name_IsReadOnly()
    {
        // Arrange
        var key = new Key("User", "1");
        var name = "Test Name";

        // Act
        var view = new ASItemView(key, name);

        // Assert
        view.Name.Should().Be(name);
        // Name property should be private set (compile-time check)
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Constructor_WithTagHavingSpecialCharacters_CreatesInstance()
    {
        // Arrange
        var key = new Key("User", "special-123");
        var name = "Test @User #123!";
        var tag = new Tag(key, name, "");

        // Act
        var result = new ASItemView(tag);

        // Assert
        result.Name.Should().Be(name);
        result.Key.Should().Be(key);
    }

    [Fact]
    public void Constructor_WithKeyAndLongName_CreatesInstance()
    {
        // Arrange
        var key = new Key("User", "999");
        var longName = new string('A', 1000);

        // Act
        var result = new ASItemView(key, longName);

        // Assert
        result.Name.Should().Be(longName);
        result.Name.Length.Should().Be(1000);
    }

    [Fact]
    public void Constructor_WithKeyAndWhitespaceName_CreatesInstance()
    {
        // Arrange
        var key = new Key("User", "111");
        var name = "   ";

        // Act
        var result = new ASItemView(key, name);

        // Assert
        result.Name.Should().Be(name);
    }

    #endregion

    #region Multiple Instances Tests

    [Fact]
    public void MultipleInstances_HaveDifferentTags()
    {
        // Arrange
        var tag1 = new Tag(new Key("User", "1"), "User 1", "");
        var tag2 = new Tag(new Key("User", "2"), "User 2", "");

        // Act
        var view1 = new ASItemView(tag1);
        var view2 = new ASItemView(tag2);

        // Assert
        view1.Key.Should().NotBe(view2.Key);
        view1.Name.Should().NotBe(view2.Name);
        view1.Tag.Should().NotBe(view2.Tag);
    }

    [Fact]
    public void Constructor_CreatesNewTagInstance()
    {
        // Arrange
        var key = new Key("User", "1");
        var name = "Test User";

        // Act
        var view = new ASItemView(key, name);

        // Assert
        view.Tag.Should().NotBeNull();
        view.Tag.Key.Should().Be(key);
        view.Tag.Name.Should().Be(name);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Constructor_BothOverloads_ProduceSameResult()
    {
        // Arrange
        var key = new Key("User", "123");
        var name = "Same User";
        var tag = new Tag(key, name, "");

        // Act
        var view1 = new ASItemView(tag);
        var view2 = new ASItemView(key, name);

        // Assert
        view1.Key.Should().Be(view2.Key);
        view1.Name.Should().Be(view2.Name);
        view1.Tag.Key.Should().Be(view2.Tag.Key);
        view1.Tag.Name.Should().Be(view2.Tag.Name);
    }

    #endregion
}
