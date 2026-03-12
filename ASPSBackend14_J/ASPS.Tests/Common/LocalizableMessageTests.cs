using Xunit;
using FluentAssertions;
using Common.Models;

namespace ASPS.Tests.Common
{
    public class LocalizableMessageTests
    {
        // Test implementation class for abstract LocalizableMessage
        private class TestLocalizableMessage : LocalizableMessage
        {
            public TestLocalizableMessage(string key)
            {
                Key = key;
            }

            public override string ToString()
            {
                return $"TestMessage:{Key}";
            }
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithKey_SetsKeyProperty()
        {
            // Arrange
            var expectedKey = "test.message.key";

            // Act
            var message = new TestLocalizableMessage(expectedKey);

            // Assert
            message.Should().NotBeNull();
            message.Key.Should().Be(expectedKey);
        }

        [Fact]
        public void Constructor_WithEmptyKey_SetsEmptyKey()
        {
            // Arrange
            var expectedKey = string.Empty;

            // Act
            var message = new TestLocalizableMessage(expectedKey);

            // Assert
            message.Key.Should().Be(string.Empty);
        }

        #endregion

        #region Key Property Tests

        [Theory]
        [InlineData("alert.phishing.detected")]
        [InlineData("error.connection.failed")]
        [InlineData("info.update.available")]
        public void Key_WithDifferentValues_ReturnsCorrectValue(string key)
        {
            // Arrange & Act
            var message = new TestLocalizableMessage(key);

            // Assert
            message.Key.Should().Be(key);
        }

        [Fact]
        public void Key_DefaultValue_IsEmptyString()
        {
            // Arrange & Act
            var message = new TestLocalizableMessage(string.Empty);

            // Assert
            message.Key.Should().NotBeNull();
            message.Key.Should().BeEmpty();
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_WhenOverridden_ReturnsCustomFormat()
        {
            // Arrange
            var key = "test.key";
            var message = new TestLocalizableMessage(key);

            // Act
            var result = message.ToString();

            // Assert
            result.Should().Be("TestMessage:test.key");
        }

        [Fact]
        public void ToString_WithEmptyKey_ReturnsFormattedString()
        {
            // Arrange
            var message = new TestLocalizableMessage(string.Empty);

            // Act
            var result = message.ToString();

            // Assert
            result.Should().Be("TestMessage:");
        }

        #endregion

        #region Abstract Class Tests

        [Fact]
        public void LocalizableMessage_IsAbstract()
        {
            // Act & Assert
            typeof(LocalizableMessage).IsAbstract.Should().BeTrue();
        }

        [Fact]
        public void LocalizableMessage_HasAbstractToStringMethod()
        {
            // Arrange
            var toStringMethod = typeof(LocalizableMessage).GetMethod("ToString");

            // Act & Assert
            toStringMethod.Should().NotBeNull();
            toStringMethod!.IsAbstract.Should().BeTrue();
        }

        [Fact]
        public void LocalizableMessage_KeyPropertyHasProtectedSetter()
        {
            // Arrange
            var keyProperty = typeof(LocalizableMessage).GetProperty("Key");

            // Act & Assert
            keyProperty.Should().NotBeNull();
            keyProperty!.CanRead.Should().BeTrue();
            keyProperty.CanWrite.Should().BeTrue();
            keyProperty.SetMethod!.IsFamily.Should().BeTrue(); // protected
        }

        #endregion

        #region Inheritance Tests

        [Fact]
        public void TestLocalizableMessage_InheritsFromLocalizableMessage()
        {
            // Arrange
            var message = new TestLocalizableMessage("test");

            // Act & Assert
            message.Should().BeAssignableTo<LocalizableMessage>();
        }

        [Fact]
        public void DerivedClass_CanAccessProtectedSetter()
        {
            // Arrange
            var initialKey = "initial.key";
            var message = new TestLocalizableMessage(initialKey);

            // Act
            var newKey = "updated.key";
            message = new TestLocalizableMessage(newKey);

            // Assert
            message.Key.Should().Be(newKey);
        }

        #endregion
    }
}
