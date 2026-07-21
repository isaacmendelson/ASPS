using Xunit;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Common.Models.Alerts;
using Common.Entities;
using Common.Enums;
using System;

namespace ASPS.Tests;

/// <summary>
/// Unit tests for tab lifecycle alert routing in RealTimeAlertListener (FR-029).
///
/// The listener's ProcessAlertAsync dispatches on AlertType string:
///   "TabClosedAlert"   → JsonConvert.DeserializeObject&lt;TabClosedAlert&gt;
///   "TabChangedAlert"  → JsonConvert.DeserializeObject&lt;TabChangedAlert&gt;
///
/// These tests verify that:
///   1. Both wire formats deserialize correctly.
///   2. The AlertType field in each JSON matches the switch key the listener uses.
///   3. Entity counterparts (TabClosedAlertEntity, TabChangedAlertEntity) hold
///      the expected properties for DB persistence.
/// </summary>
public class RealTimeAlertListenerTabLifecycleTests
{
    #region TabClosedAlert deserialization

    [Fact]
    public void TabClosedAlert_ValidJson_ShouldDeserializeCorrectly()
    {
        // Arrange — wire format forwarded by the desktop agent from the Chrome extension
        var json = @"{
            ""AlertType"": ""TabClosedAlert"",
            ""Priority"": 2,
            ""Timestamp"": ""2026-07-01T10:00:00Z"",
            ""Token"": ""test-token-tab-closed"",
            ""DeviceInfo"": {
                ""DeviceUid"": ""TAB-DEVICE-001"",
                ""OperatingSystem"": 0
            },
            ""TabId"": ""42"",
            ""Url"": ""https://suspicious-bank.com/login""
        }";

        // Act
        var alert = JsonConvert.DeserializeObject<TabClosedAlert>(json);

        // Assert
        alert.Should().NotBeNull();
        alert!.AlertType.Should().Be("TabClosedAlert");
        alert.TabId.Should().Be("42");
        alert.Url.Should().Be("https://suspicious-bank.com/login");
        alert.Token.Should().Be("test-token-tab-closed");
        alert.Priority.Should().Be(Priority.High);
        alert.DeviceInfo.DeviceUid.Should().Be("TAB-DEVICE-001");
    }

    [Fact]
    public void TabClosedAlert_MinimalJson_ShouldDeserializeWithDefaultValues()
    {
        // Arrange — only required fields; optional fields get C# defaults
        var json = @"{
            ""AlertType"": ""TabClosedAlert"",
            ""Priority"": 0,
            ""DeviceInfo"": { ""DeviceUid"": ""TAB-DEVICE-002"", ""OperatingSystem"": 0 },
            ""TabId"": """",
            ""Url"": """"
        }";

        // Act
        var alert = JsonConvert.DeserializeObject<TabClosedAlert>(json);

        // Assert
        alert.Should().NotBeNull();
        alert!.TabId.Should().BeEmpty();
        alert.Url.Should().BeEmpty();
        alert.Priority.Should().Be(Priority.Low);
    }

    [Fact]
    public void TabClosedAlert_AlertType_MatchesListenerSwitchKey()
    {
        // The listener routes on the literal string "TabClosedAlert"
        var json = @"{
            ""AlertType"": ""TabClosedAlert"",
            ""DeviceInfo"": { ""DeviceUid"": ""D1"", ""OperatingSystem"": 0 },
            ""TabId"": ""7"", ""Url"": ""https://example.com""
        }";

        var alert = JsonConvert.DeserializeObject<TabClosedAlert>(json);

        alert!.AlertType.Should().Be("TabClosedAlert");
    }

    #endregion

    #region TabChangedAlert deserialization

    [Fact]
    public void TabChangedAlert_ValidJson_ShouldDeserializeCorrectly()
    {
        // Arrange
        var json = @"{
            ""AlertType"": ""TabChangedAlert"",
            ""Priority"": 2,
            ""Timestamp"": ""2026-07-01T11:00:00Z"",
            ""Token"": ""test-token-tab-changed"",
            ""DeviceInfo"": {
                ""DeviceUid"": ""TAB-DEVICE-003"",
                ""OperatingSystem"": 0
            },
            ""TabId"": ""99"",
            ""Url"": ""https://bank.example.com/account"",
            ""IsSensitiveWebsite"": true,
            ""IsLoggedIn"": true
        }";

        // Act
        var alert = JsonConvert.DeserializeObject<TabChangedAlert>(json);

        // Assert
        alert.Should().NotBeNull();
        alert!.AlertType.Should().Be("TabChangedAlert");
        alert.TabId.Should().Be("99");
        alert.Url.Should().Be("https://bank.example.com/account");
        alert.IsSensitiveWebsite.Should().BeTrue();
        alert.IsLoggedIn.Should().BeTrue();
        alert.Priority.Should().Be(Priority.High);
        alert.DeviceInfo.DeviceUid.Should().Be("TAB-DEVICE-003");
        alert.Token.Should().Be("test-token-tab-changed");
    }

    [Fact]
    public void TabChangedAlert_NullableFields_DeserializeAsNull()
    {
        // Arrange — IsSensitiveWebsite and IsLoggedIn are deliberately null
        var json = @"{
            ""AlertType"": ""TabChangedAlert"",
            ""Priority"": 1,
            ""DeviceInfo"": { ""DeviceUid"": ""TAB-DEVICE-004"", ""OperatingSystem"": 0 },
            ""TabId"": ""12"",
            ""Url"": ""https://example.com"",
            ""IsSensitiveWebsite"": null,
            ""IsLoggedIn"": null
        }";

        // Act
        var alert = JsonConvert.DeserializeObject<TabChangedAlert>(json);

        // Assert
        alert.Should().NotBeNull();
        alert!.IsSensitiveWebsite.Should().BeNull();
        alert.IsLoggedIn.Should().BeNull();
    }

    [Fact]
    public void TabChangedAlert_AlertType_MatchesListenerSwitchKey()
    {
        // The listener routes on the literal string "TabChangedAlert"
        var json = @"{
            ""AlertType"": ""TabChangedAlert"",
            ""DeviceInfo"": { ""DeviceUid"": ""D2"", ""OperatingSystem"": 0 },
            ""TabId"": ""3"", ""Url"": ""https://safe.com""
        }";

        var alert = JsonConvert.DeserializeObject<TabChangedAlert>(json);

        alert!.AlertType.Should().Be("TabChangedAlert");
    }

    #endregion

    #region Entity model tests (DB persistence counterparts)

    [Fact]
    public void TabClosedAlertEntity_CanSetAndReadProperties()
    {
        // Act
        var entity = new TabClosedAlertEntity
        {
            AlertType = "TabClosedAlert",
            TabId = "55",
            Url = "https://phishing.example.com",
            DeviceUid = "ENT-DEVICE-001",
            Priority = Priority.High
        };

        // Assert
        entity.TabId.Should().Be("55");
        entity.Url.Should().Be("https://phishing.example.com");
        entity.AlertType.Should().Be("TabClosedAlert");
        entity.TypeName.Should().Be("TabClosedAlert");
        entity.Priority.Should().Be(Priority.High);
    }

    [Fact]
    public void TabChangedAlertEntity_CanSetAndReadProperties()
    {
        // Act
        var entity = new TabChangedAlertEntity
        {
            AlertType = "TabChangedAlert",
            TabId = "88",
            Url = "https://bank.example.com",
            IsSensitiveWebsite = true,
            IsLoggedIn = false,
            DeviceUid = "ENT-DEVICE-002",
            Priority = Priority.Medium
        };

        // Assert
        entity.TabId.Should().Be("88");
        entity.Url.Should().Be("https://bank.example.com");
        entity.IsSensitiveWebsite.Should().BeTrue();
        entity.IsLoggedIn.Should().BeFalse();
        entity.TypeName.Should().Be("TabChangedAlert");
        entity.Priority.Should().Be(Priority.Medium);
    }

    [Fact]
    public void TabChangedAlertEntity_DefaultConstruction_NullableFieldsAreNull()
    {
        var entity = new TabChangedAlertEntity();

        entity.IsSensitiveWebsite.Should().BeNull();
        entity.IsLoggedIn.Should().BeNull();
        entity.TabId.Should().BeNull();
        entity.Url.Should().BeNull();
    }

    #endregion

    #region Routing key verification (JObject parsing — same path as the listener)

    [Theory]
    [InlineData("TabClosedAlert")]
    [InlineData("TabChangedAlert")]
    public void AlertType_JObjectParsed_MatchesListenerRoutingSwitch(string alertType)
    {
        // Arrange — the listener calls JObject.Parse(message) first, then reads AlertType
        var json = $@"{{
            ""AlertType"": ""{alertType}"",
            ""Priority"": 1,
            ""DeviceInfo"": {{ ""DeviceUid"": ""D1"", ""OperatingSystem"": 0 }},
            ""TabId"": ""1"",
            ""Url"": ""https://example.com""
        }}";

        // Act
        var jObject = JObject.Parse(json);
        var parsedType = jObject["AlertType"]?.ToString();

        // Assert
        parsedType.Should().Be(alertType);
    }

    #endregion
}
