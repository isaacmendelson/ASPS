// =============================================================================
// TEMPLATE: Unit Test File
// Copy this file and rename to: {ClassName}Tests.cs
// =============================================================================

using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
// Add other usings as needed

namespace ASPS.Tests.{Folder}  // Common, Business, WebApi, Interface
{
    /// <summary>
    /// Unit tests for {ClassName}
    /// </summary>
    public class {ClassName}Tests
    {
        #region Fields

        // Mocks
        private readonly Mock<ILogger<{ClassName}>> _loggerMock;
        // Add other mocks here

        // System Under Test
        private readonly {ClassName} _sut;

        #endregion

        #region Constructor

        public {ClassName}Tests()
        {
            // Initialize mocks
            _loggerMock = new Mock<ILogger<{ClassName}>>();
            
            // Create System Under Test
            _sut = new {ClassName}(_loggerMock.Object);
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParams_CreatesInstance()
        {
            // Arrange & Act
            var instance = new {ClassName}(_loggerMock.Object);

            // Assert
            instance.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange & Act
            Action act = () => new {ClassName}(null!);

            // Assert
            act.Should().Throw<ArgumentNullException>();
        }

        #endregion

        #region {MethodName} Tests

        [Fact]
        public void {MethodName}_WhenValidInput_ReturnsExpectedResult()
        {
            // Arrange
            var input = "test";

            // Act
            var result = _sut.{MethodName}(input);

            // Assert
            result.Should().NotBeNull();
            // Add more assertions
        }

        [Fact]
        public void {MethodName}_WhenNullInput_ThrowsException()
        {
            // Arrange
            string input = null!;

            // Act
            Action act = () => _sut.{MethodName}(input);

            // Assert
            act.Should().Throw<ArgumentNullException>();
        }

        [Theory]
        [InlineData("input1", "expected1")]
        [InlineData("input2", "expected2")]
        [InlineData("", null)]
        public void {MethodName}_WithVariousInputs_ReturnsExpected(string input, string expected)
        {
            // Act
            var result = _sut.{MethodName}(input);

            // Assert
            result.Should().Be(expected);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void {MethodName}_WithEmptyString_HandlesGracefully()
        {
            // Arrange
            var input = string.Empty;

            // Act
            var result = _sut.{MethodName}(input);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void {MethodName}_WithWhitespace_HandlesGracefully()
        {
            // Arrange
            var input = "   ";

            // Act
            var result = _sut.{MethodName}(input);

            // Assert
            // Add assertion based on expected behavior
        }

        #endregion
    }
}

// =============================================================================
// INSTRUCTIONS:
// 1. Replace {ClassName} with actual class name
// 2. Replace {Folder} with: Common, Business, WebApi, or Interface
// 3. Replace {MethodName} with actual method names
// 4. Add/remove test methods as needed
// 5. Run: dotnet test --filter "FullyQualifiedName~{ClassName}Tests"
// =============================================================================
