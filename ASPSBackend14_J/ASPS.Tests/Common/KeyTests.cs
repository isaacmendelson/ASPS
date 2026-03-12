using Xunit;
using FluentAssertions;
using Common.Models;
using System.Xml;
using System.Xml.Linq;
using System.IO;

namespace ASPS.Tests.Common
{
    public class KeyTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_Default_CreatesEmptyKey()
        {
            // Act
            var key = new Key();

            // Assert
            key.Should().NotBeNull();
            key.Type.Should().Be(string.Empty);
            key.Value.Should().Be(string.Empty);
            key.InstanceName.Should().BeNull();
        }

        [Fact]
        public void Constructor_WithTypeAndValue_CreatesKey()
        {
            // Arrange
            var type = "DeviceKey";
            var value = "device123";

            // Act
            var key = new Key(type, value);

            // Assert
            key.Should().NotBeNull();
            key.Type.Should().Be("DeviceKey");
            key.Value.Should().Be("device123");
            key.InstanceName.Should().BeNull();
        }

        [Fact]
        public void Constructor_WithAllParameters_CreatesKey()
        {
            // Arrange
            var type = "UserKey";
            var value = "user456";
            var instanceName = "instance1";

            // Act
            var key = new Key(type, value, instanceName);

            // Assert
            key.Should().NotBeNull();
            key.Type.Should().Be("UserKey");
            key.Value.Should().Be("user456");
            key.InstanceName.Should().Be("instance1");
        }

        #endregion

        #region Equals Tests

        [Fact]
        public void Equals_WithSameValues_ReturnsTrue()
        {
            // Arrange
            var key1 = new Key("Type1", "Value1", "Instance1");
            var key2 = new Key("Type1", "Value1", "Instance1");

            // Act
            var result = key1.Equals(key2);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void Equals_WithDifferentType_ReturnsFalse()
        {
            // Arrange
            var key1 = new Key("Type1", "Value1", "Instance1");
            var key2 = new Key("Type2", "Value1", "Instance1");

            // Act
            var result = key1.Equals(key2);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void Equals_WithDifferentValue_ReturnsFalse()
        {
            // Arrange
            var key1 = new Key("Type1", "Value1", "Instance1");
            var key2 = new Key("Type1", "Value2", "Instance1");

            // Act
            var result = key1.Equals(key2);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void Equals_WithDifferentInstanceName_ReturnsFalse()
        {
            // Arrange
            var key1 = new Key("Type1", "Value1", "Instance1");
            var key2 = new Key("Type1", "Value1", "Instance2");

            // Act
            var result = key1.Equals(key2);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void Equals_WithNullInstance_ReturnsFalse()
        {
            // Arrange
            var key1 = new Key("Type1", "Value1", "Instance1");
            var key2 = new Key("Type1", "Value1", null);

            // Act
            var result = key1.Equals(key2);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void Equals_WithNull_ReturnsFalse()
        {
            // Arrange
            var key = new Key("Type1", "Value1");

            // Act
            var result = key.Equals(null);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void Equals_WithSameReference_ReturnsTrue()
        {
            // Arrange
            var key = new Key("Type1", "Value1");

            // Act
            var result = key.Equals(key);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void Equals_WithObjectType_WorksCorrectly()
        {
            // Arrange
            var key1 = new Key("Type1", "Value1", "Instance1");
            object key2 = new Key("Type1", "Value1", "Instance1");

            // Act
            var result = key1.Equals(key2);

            // Assert
            result.Should().BeTrue();
        }

        #endregion

        #region GetHashCode Tests

        [Fact]
        public void GetHashCode_WithSameValues_ReturnsSameHash()
        {
            // Arrange
            var key1 = new Key("Type1", "Value1", "Instance1");
            var key2 = new Key("Type1", "Value1", "Instance1");

            // Act
            var hash1 = key1.GetHashCode();
            var hash2 = key2.GetHashCode();

            // Assert
            hash1.Should().Be(hash2);
        }

        [Fact]
        public void GetHashCode_WithDifferentValues_ReturnsDifferentHash()
        {
            // Arrange
            var key1 = new Key("Type1", "Value1", "Instance1");
            var key2 = new Key("Type2", "Value2", "Instance2");

            // Act
            var hash1 = key1.GetHashCode();
            var hash2 = key2.GetHashCode();

            // Assert
            hash1.Should().NotBe(hash2);
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_WithoutInstanceName_ReturnsTypeColonValue()
        {
            // Arrange
            var key = new Key("DeviceKey", "device123");

            // Act
            var result = key.ToString();

            // Assert
            result.Should().Be("DeviceKey:device123");
        }

        [Fact]
        public void ToString_WithInstanceName_ReturnsFullFormat()
        {
            // Arrange
            var key = new Key("UserKey", "user456", "instance1");

            // Act
            var result = key.ToString();

            // Assert
            result.Should().Be("UserKey:user456:instance1");
        }

        [Fact]
        public void ToString_WithEmptyValues_ReturnsColonOnly()
        {
            // Arrange
            var key = new Key();

            // Act
            var result = key.ToString();

            // Assert
            result.Should().Be(":");
        }

        #endregion

        #region Operator Tests

        [Fact]
        public void EqualityOperator_WithSameValues_ReturnsTrue()
        {
            // Arrange
            var key1 = new Key("Type1", "Value1", "Instance1");
            var key2 = new Key("Type1", "Value1", "Instance1");

            // Act
            var result = key1 == key2;

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void EqualityOperator_WithDifferentValues_ReturnsFalse()
        {
            // Arrange
            var key1 = new Key("Type1", "Value1");
            var key2 = new Key("Type2", "Value2");

            // Act
            var result = key1 == key2;

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void EqualityOperator_WithBothNull_ReturnsTrue()
        {
            // Arrange
            Key? key1 = null;
            Key? key2 = null;

            // Act
            var result = key1 == key2;

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void EqualityOperator_WithOneNull_ReturnsFalse()
        {
            // Arrange
            var key1 = new Key("Type1", "Value1");
            Key? key2 = null;

            // Act
            var result = key1 == key2;

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void InequalityOperator_WithDifferentValues_ReturnsTrue()
        {
            // Arrange
            var key1 = new Key("Type1", "Value1");
            var key2 = new Key("Type2", "Value2");

            // Act
            var result = key1 != key2;

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void InequalityOperator_WithSameValues_ReturnsFalse()
        {
            // Arrange
            var key1 = new Key("Type1", "Value1", "Instance1");
            var key2 = new Key("Type1", "Value1", "Instance1");

            // Act
            var result = key1 != key2;

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region XML Serialization Tests

        [Fact]
        public void GetSchema_ReturnsNull()
        {
            // Arrange
            var key = new Key("Type1", "Value1");

            // Act
            var schema = key.GetSchema();

            // Assert
            schema.Should().BeNull();
        }

        [Fact]
        public void WriteXml_WithAllProperties_WritesCorrectly()
        {
            // Arrange
            var key = new Key("DeviceKey", "device123", "instance1");
            var stringWriter = new StringWriter();
            var xmlWriter = XmlWriter.Create(stringWriter);

            // Act
            xmlWriter.WriteStartElement("Key");
            key.WriteXml(xmlWriter);
            xmlWriter.WriteEndElement();
            xmlWriter.Flush();

            var result = stringWriter.ToString();

            // Assert
            result.Should().Contain("Type=\"DeviceKey\"");
            result.Should().Contain("Value=\"device123\"");
            result.Should().Contain("InstanceName=\"instance1\"");
        }

        [Fact]
        public void WriteXml_WithoutInstanceName_DoesNotWriteInstanceName()
        {
            // Arrange
            var key = new Key("UserKey", "user456");
            var stringWriter = new StringWriter();
            var xmlWriter = XmlWriter.Create(stringWriter);

            // Act
            xmlWriter.WriteStartElement("Key");
            key.WriteXml(xmlWriter);
            xmlWriter.WriteEndElement();
            xmlWriter.Flush();

            var result = stringWriter.ToString();

            // Assert
            result.Should().Contain("Type=\"UserKey\"");
            result.Should().Contain("Value=\"user456\"");
            result.Should().NotContain("InstanceName");
        }

        [Fact]
        public void ReadXml_WithAllAttributes_ReadsCorrectly()
        {
            // Arrange
            var xml = "<Key Type=\"DeviceKey\" Value=\"device123\" InstanceName=\"instance1\" />";
            var xmlReader = XmlReader.Create(new StringReader(xml));
            var key = new Key();

            // Act
            key.ReadXml(xmlReader);

            // Assert
            key.Type.Should().Be("DeviceKey");
            key.Value.Should().Be("device123");
            key.InstanceName.Should().Be("instance1");
        }

        [Fact]
        public void ReadXml_WithoutInstanceName_ReadsCorrectly()
        {
            // Arrange
            var xml = "<Key Type=\"UserKey\" Value=\"user456\" />";
            var xmlReader = XmlReader.Create(new StringReader(xml));
            var key = new Key();

            // Act
            key.ReadXml(xmlReader);

            // Assert
            key.Type.Should().Be("UserKey");
            key.Value.Should().Be("user456");
            key.InstanceName.Should().BeNull();
        }

        [Fact]
        public void ReadXml_WithMissingAttributes_UsesDefaults()
        {
            // Arrange
            var xml = "<Key />";
            var xmlReader = XmlReader.Create(new StringReader(xml));
            var key = new Key();

            // Act
            key.ReadXml(xmlReader);

            // Assert
            key.Type.Should().Be(string.Empty);
            key.Value.Should().Be(string.Empty);
            key.InstanceName.Should().BeNull();
        }

        #endregion
    }
}
