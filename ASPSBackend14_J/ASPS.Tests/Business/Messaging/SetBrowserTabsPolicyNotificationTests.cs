using Xunit;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Business.Messaging;
using System;

namespace ASPS.Tests.Business.Messaging;

/// <summary>
/// Unit tests for SetBrowserTabsPolicyNotification (FR-028).
/// Covers model construction, valid policy modes, ValidUntil UTC semantics,
/// and JSON serialization round-trip.
/// </summary>
public class SetBrowserTabsPolicyNotificationTests
{
    #region Construction

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var validUntil = new DateTime(2026, 12, 31, 23, 0, 0, DateTimeKind.Utc);
        var before = DateTime.UtcNow;

        // Act
        var sut = new SetBrowserTabsPolicyNotification("DEV-001", "user-key-1", "incoming_only", validUntil);

        // Assert
        var after = DateTime.UtcNow;
        sut.DeviceUid.Should().Be("DEV-001");
        sut.UserKey.Should().Be("user-key-1");
        sut.Mode.Should().Be("incoming_only");
        sut.ValidUntil.Should().Be(validUntil);
        sut.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Constructor_WithNullValidUntil_SetsNullExpiry()
    {
        // Act
        var sut = new SetBrowserTabsPolicyNotification("DEV-002", "user-key-2", "always", null);

        // Assert — null means no expiry (override stays until next override or agent restart)
        sut.ValidUntil.Should().BeNull();
    }

    [Fact]
    public void Timestamp_IsSetToUtcNowDuringConstruction()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var sut = new SetBrowserTabsPolicyNotification("DEV-003", "user-key-3", "never", null);

        // Assert
        var after = DateTime.UtcNow;
        sut.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    #endregion

    #region Valid policy modes

    [Theory]
    [InlineData("incoming_only")]
    [InlineData("always")]
    [InlineData("never")]
    public void Constructor_AcceptsAllValidPolicyModes(string mode)
    {
        // Act
        var sut = new SetBrowserTabsPolicyNotification("DEV-004", "user-key-4", mode, null);

        // Assert
        sut.Mode.Should().Be(mode);
    }

    #endregion

    #region ValidUntil UTC semantics

    [Fact]
    public void ValidUntil_WhenSetToUtcDateTime_PreservesKindAndValue()
    {
        // Arrange
        var utcTime = new DateTime(2027, 6, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var sut = new SetBrowserTabsPolicyNotification("DEV-005", "user-key-5", "always", utcTime);

        // Assert
        sut.ValidUntil.Should().Be(utcTime);
        sut.ValidUntil!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void ValidUntil_Null_SignifiesNoExpiry()
    {
        // Act
        var sut = new SetBrowserTabsPolicyNotification("DEV-006", "user-key-6", "never", null);

        // Assert
        sut.ValidUntil.HasValue.Should().BeFalse();
    }

    #endregion

    #region JSON serialization

    [Fact]
    public void Serialization_IncludesAllDataMemberFields()
    {
        // Arrange
        var validUntil = new DateTime(2026, 12, 31, 12, 0, 0, DateTimeKind.Utc);
        var sut = new SetBrowserTabsPolicyNotification("DEV-SERIAL-001", "user-serial", "always", validUntil);

        // Act
        var json = JsonConvert.SerializeObject(sut);
        var jObject = JObject.Parse(json);

        // Assert — all [DataMember] fields must appear in output
        jObject["DeviceUid"]?.ToString().Should().Be("DEV-SERIAL-001");
        jObject["UserKey"]?.ToString().Should().Be("user-serial");
        jObject["Mode"]?.ToString().Should().Be("always");
        jObject["ValidUntil"].Should().NotBeNull();
        jObject["Timestamp"].Should().NotBeNull();
    }

    [Fact]
    public void Serialization_WhenValidUntilNull_SerializesNullField()
    {
        // Arrange
        var sut = new SetBrowserTabsPolicyNotification("DEV-SERIAL-002", "user-k", "never", null);

        // Act
        var json = JsonConvert.SerializeObject(sut);
        var jObject = JObject.Parse(json);

        // Assert
        jObject.ContainsKey("ValidUntil").Should().BeTrue();
        jObject["ValidUntil"]!.Type.Should().Be(JTokenType.Null);
    }

    [Fact]
    public void SerializationRoundTrip_PreservesAllFields()
    {
        // Arrange
        var validUntil = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
        var original = new SetBrowserTabsPolicyNotification("DEV-RT-001", "user-rt", "incoming_only", validUntil);

        // Act — protected parameterless ctor is used during deserialization
        var json = JsonConvert.SerializeObject(original);
        var settings = new JsonSerializerSettings
        {
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
        };
        var restored = JsonConvert.DeserializeObject<SetBrowserTabsPolicyNotification>(json, settings);

        // Assert
        restored.Should().NotBeNull();
        restored!.DeviceUid.Should().Be("DEV-RT-001");
        restored.UserKey.Should().Be("user-rt");
        restored.Mode.Should().Be("incoming_only");
        restored.ValidUntil.Should().Be(validUntil);
    }

    #endregion
}
