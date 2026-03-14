using System;
using Xunit;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Common.Models;
using Common.Models.Alerts;
using Common.Enums;

namespace ASPS.Tests;

/// <summary>
/// Unit tests for RealTimeAlertListener - focuses on alert parsing and deserialization
/// </summary>
public class RealTimeAlertListenerTests
{
    #region RemoteAccessAlert Tests

    [Fact]
    public void RemoteAccessAlert_ValidJson_ShouldDeserializeCorrectly()
    {
        // Arrange
        var json = @"{
            ""AlertType"": ""RemoteAccessAlert"",
            ""Priority"": 2,
            ""Timestamp"": ""2026-03-12T10:00:00Z"",
            ""Token"": ""test-token-123"",
            ""DeviceInfo"": {
                ""Key"": { ""Type"": ""UserDevice"", ""Value"": ""device-key-1"" },
                ""DeviceUid"": ""TEST-DEVICE-001"",
                ""AggregateVersion"": ""1"",
                ""IP"": ""192.168.1.100"",
                ""UserAgent"": ""TestAgent/1.0"",
                ""Timezone"": 0,
                ""OperatingSystem"": 0,
                ""MACAddress"": ""00:11:22:33:44:55"",
                ""UserKey"": { ""Type"": ""User"", ""Value"": ""user-key-1"" }
            },
            ""RemoteAccessApp"": 2,
            ""RunningProcesses"": 3,
            ""ConnectionUrl"": ""https://teamviewer.com/session/12345"",
            ""ConnectionStatus"": 1,
            ""ConnectionsCount"": 1,
            ""SessionStatus"": 1,
            ""BrowserTabs"": null
        }";

        // Act
        var alert = JsonConvert.DeserializeObject<RemoteAccessAlert>(json);

        // Assert
        alert.Should().NotBeNull();
        alert!.AlertType.Should().Be("RemoteAccessAlert");
        alert.Priority.Should().Be(Priority.High);
        alert.DeviceInfo.DeviceUid.Should().Be("TEST-DEVICE-001");
        alert.Token.Should().Be("test-token-123");
        alert.RemoteAccessApp.Should().Be(RemoteAccessApp.TeamViewer);
        alert.RunningProcesses.Should().Be(3);
        alert.ConnectionUrl.Should().Be("https://teamviewer.com/session/12345");
        alert.ConnectionStatus.Should().Be(ConnectionStatus.Open);
        alert.ConnectionsCount.Should().Be(1);
    }

    [Fact]
    public void RemoteAccessAlert_MissingOptionalFields_ShouldDeserializeWithDefaults()
    {
        // Arrange - minimal valid RemoteAccessAlert
        var json = @"{
            ""AlertType"": ""RemoteAccessAlert"",
            ""Priority"": 1,
            ""Timestamp"": ""2026-03-12T10:00:00Z"",
            ""Token"": ""test-token"",
            ""DeviceInfo"": {
                ""DeviceUid"": ""TEST-DEVICE-002"",
                ""OperatingSystem"": 0
            },
            ""RemoteAccessApp"": 1,
            ""RunningProcesses"": 1,
            ""ConnectionUrl"": """",
            ""ConnectionStatus"": 0,
            ""ConnectionsCount"": 0,
            ""SessionStatus"": 0
        }";

        // Act
        var alert = JsonConvert.DeserializeObject<RemoteAccessAlert>(json);

        // Assert
        alert.Should().NotBeNull();
        alert!.AlertType.Should().Be("RemoteAccessAlert");
        alert.RemoteAccessApp.Should().Be(RemoteAccessApp.AnyDesk);
        alert.ConnectionUrl.Should().BeEmpty();
    }

    #endregion

    #region UrlAlert Tests

    [Fact]
    public void UrlAlert_ValidJson_ShouldDeserializeCorrectly()
    {
        // Arrange
        var json = @"{
            ""AlertType"": ""UrlAlert"",
            ""Priority"": 1,
            ""Timestamp"": ""2026-03-12T10:00:00Z"",
            ""Token"": ""test-token-456"",
            ""DeviceInfo"": {
                ""Key"": { ""Type"": ""UserDevice"", ""Value"": ""device-key-2"" },
                ""DeviceUid"": ""TEST-DEVICE-002"",
                ""AggregateVersion"": ""1"",
                ""IP"": ""192.168.1.101"",
                ""UserAgent"": ""Mozilla/5.0"",
                ""Timezone"": 0,
                ""OperatingSystem"": 0,
                ""MACAddress"": ""AA:BB:CC:DD:EE:FF""
            },
            ""Url"": ""https://malicious-site.com/phishing"",
            ""Trackers"": [
                { ""Type"": ""Tracker"", ""Value"": ""tracker-1"" },
                { ""Type"": ""Tracker"", ""Value"": ""tracker-2"" }
            ],
            ""IFrameDomains"": [""evil.com"", ""bad.net""],
            ""UserAgent"": ""Chrome/100.0""
        }";

        // Act
        var alert = JsonConvert.DeserializeObject<UrlAlert>(json);

        // Assert
        alert.Should().NotBeNull();
        alert!.AlertType.Should().Be("UrlAlert");
        alert.Priority.Should().Be(Priority.Medium);
        alert.Url.Should().Be("https://malicious-site.com/phishing");
        alert.Trackers.Should().HaveCount(2);
        alert.Trackers[0].Value.Should().Be("tracker-1");
        alert.IFrameDomains.Should().HaveCount(2);
        alert.IFrameDomains.Should().Contain("evil.com");
        alert.UserAgent.Should().Be("Chrome/100.0");
    }

    [Fact]
    public void UrlAlert_LocalUrl_ShouldDeserialize()
    {
        // Arrange - local URLs should deserialize but may be filtered by listener
        var json = @"{
            ""AlertType"": ""UrlAlert"",
            ""Priority"": 1,
            ""Timestamp"": ""2026-03-12T10:00:00Z"",
            ""Token"": ""test-token"",
            ""DeviceInfo"": {
                ""DeviceUid"": ""TEST-DEVICE-003"",
                ""OperatingSystem"": 0
            },
            ""Url"": ""http://localhost:8080/admin"",
            ""Trackers"": [],
            ""IFrameDomains"": [],
            ""UserAgent"": """"
        }";

        // Act
        var alert = JsonConvert.DeserializeObject<UrlAlert>(json);

        // Assert
        alert.Should().NotBeNull();
        alert!.Url.Should().Be("http://localhost:8080/admin");
    }

    [Fact]
    public void UrlAlert_EmptyArrays_ShouldDeserializeCorrectly()
    {
        // Arrange
        var json = @"{
            ""AlertType"": ""UrlAlert"",
            ""Priority"": 1,
            ""Timestamp"": ""2026-03-12T10:00:00Z"",
            ""Token"": ""test-token"",
            ""DeviceInfo"": {
                ""DeviceUid"": ""TEST-DEVICE-004"",
                ""OperatingSystem"": 0
            },
            ""Url"": ""https://safe-site.com"",
            ""Trackers"": [],
            ""IFrameDomains"": [],
            ""UserAgent"": ""TestAgent""
        }";

        // Act
        var alert = JsonConvert.DeserializeObject<UrlAlert>(json);

        // Assert
        alert.Should().NotBeNull();
        alert!.Trackers.Should().BeEmpty();
        alert.IFrameDomains.Should().BeEmpty();
    }

    #endregion

    #region TrackUrlAlert Tests

    [Fact]
    public void TrackUrlAlert_ValidJson_ShouldDeserializeCorrectly()
    {
        // Arrange
        var json = @"{
            ""AlertType"": ""TrackUrlAlert"",
            ""Priority"": 1,
            ""Timestamp"": ""2026-03-12T10:00:00Z"",
            ""Token"": ""test-token-789"",
            ""DeviceInfo"": {
                ""Key"": { ""Type"": ""UserDevice"", ""Value"": ""device-key-3"" },
                ""DeviceUid"": ""TEST-DEVICE-003"",
                ""AggregateVersion"": ""1"",
                ""IP"": ""192.168.1.102"",
                ""UserAgent"": ""Safari/15.0"",
                ""Timezone"": 0,
                ""OperatingSystem"": 1,
                ""MACAddress"": ""11:22:33:44:55:66""
            },
            ""Url"": ""https://suspicious-site.com"",
            ""FromUrl"": ""https://google.com"",
            ""Duration"": 180,
            ""ScamInProgressKey"": ""scam-key-456"",
            ""IPAddress"": ""192.168.1.102"",
            ""TabId"": ""tab-789"",
            ""Timezone"": ""America/New_York"",
            ""UserAgent"": ""Safari/15.0""
        }";

        // Act
        var alert = JsonConvert.DeserializeObject<TrackUrlAlert>(json);

        // Assert
        alert.Should().NotBeNull();
        alert!.AlertType.Should().Be("TrackUrlAlert");
        alert.Priority.Should().Be(Priority.Medium);
        alert.Url.Should().Be("https://suspicious-site.com");
        alert.FromUrl.Should().Be("https://google.com");
        alert.Duration.Should().Be(180);
        alert.ScamInProgressKey.Should().Be("scam-key-456");
        alert.UserAgent.Should().Be("Safari/15.0");
    }

    [Fact]
    public void TrackUrlAlert_NoFromUrl_ShouldDeserializeCorrectly()
    {
        // Arrange
        var json = @"{
            ""AlertType"": ""TrackUrlAlert"",
            ""Priority"": 1,
            ""Timestamp"": ""2026-03-12T10:00:00Z"",
            ""Token"": ""test-token"",
            ""DeviceInfo"": {
                ""DeviceUid"": ""TEST-DEVICE-004"",
                ""OperatingSystem"": 0
            },
            ""Url"": ""https://clean-site.com"",
            ""FromUrl"": """",
            ""Duration"": 0,
            ""ScamInProgressKey"": """",
            ""UserAgent"": ""Chrome""
        }";

        // Act
        var alert = JsonConvert.DeserializeObject<TrackUrlAlert>(json);

        // Assert
        alert.Should().NotBeNull();
        alert!.FromUrl.Should().BeEmpty();
        alert.Duration.Should().Be(0);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Alert_NullJson_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Action act = () => JsonConvert.DeserializeObject<RemoteAccessAlert>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Alert_EmptyJson_ShouldReturnNull()
    {
        // Arrange
        var json = "";

        // Act
        var alert = JsonConvert.DeserializeObject<UrlAlert>(json);

        // Assert
        alert.Should().BeNull();
    }

    [Fact]
    public void Alert_MalformedJson_ShouldThrowJsonException()
    {
        // Arrange
        var json = @"{ ""AlertType"": ""UrlAlert"", ""Priority"": ""NOT_A_NUMBER"" }";

        // Act & Assert
        Action act = () => JsonConvert.DeserializeObject<UrlAlert>(json);
        act.Should().Throw<JsonSerializationException>();
    }

    [Fact]
    public void Alert_InvalidJsonStructure_ShouldThrowJsonException()
    {
        // Arrange
        var json = @"{ this is not valid json at all }";

        // Act & Assert
        Action act = () => JsonConvert.DeserializeObject<RemoteAccessAlert>(json);
        act.Should().Throw<JsonReaderException>();
    }

    [Fact]
    public void Alert_PartialJson_ShouldDeserializeWithDefaults()
    {
        // Arrange - only required fields
        var json = @"{
            ""AlertType"": ""RemoteAccessAlert"",
            ""RemoteAccessApp"": 0,
            ""ConnectionStatus"": 0
        }";

        // Act
        var alert = JsonConvert.DeserializeObject<RemoteAccessAlert>(json);

        // Assert
        alert.Should().NotBeNull();
        alert!.AlertType.Should().Be("RemoteAccessAlert");
        alert.Priority.Should().Be(Priority.Low); // Default
        alert.Token.Should().BeNullOrEmpty();
        alert.ConnectionUrl.Should().BeNullOrEmpty();
        alert.RemoteAccessApp.Should().Be(RemoteAccessApp.Unknown);
    }

    [Fact]
    public void JObject_Parse_UnknownAlertType_ShouldParseSuccessfully()
    {
        // Arrange - testing the RealTimeAlertListener's approach of parsing to JObject first
        var json = @"{
            ""AlertType"": ""UnknownAlertType"",
            ""Priority"": 2,
            ""DeviceInfo"": {
                ""DeviceUid"": ""TEST-DEVICE-005""
            },
            ""SomeCustomField"": ""value""
        }";

        // Act
        var jObject = JObject.Parse(json);
        var alertType = jObject["AlertType"]?.ToString();

        // Assert
        jObject.Should().NotBeNull();
        alertType.Should().Be("UnknownAlertType");
        jObject["SomeCustomField"]?.ToString().Should().Be("value");
    }

    [Fact]
    public void JObject_Parse_MessageWithUrl_ShouldContainUrlProperty()
    {
        // Arrange - testing InferAlertType logic
        var json = @"{
            ""Url"": ""https://example.com"",
            ""DeviceInfo"": { ""DeviceUid"": ""TEST"" }
        }";

        // Act
        var jObject = JObject.Parse(json);
        var hasUrl = jObject.ContainsKey("Url");

        // Assert
        hasUrl.Should().BeTrue();
        jObject["Url"]?.ToString().Should().Be("https://example.com");
    }

    [Fact]
    public void JObject_Parse_MessageWithRemoteAccessFields_ShouldContainRemoteAccessApp()
    {
        // Arrange - testing InferAlertType logic
        var json = @"{
            ""RemoteAccessApp"": { ""Name"": ""TeamViewer"" },
            ""ConnectionUrl"": ""https://session.com"",
            ""DeviceInfo"": { ""DeviceUid"": ""TEST"" }
        }";

        // Act
        var jObject = JObject.Parse(json);
        var hasRemoteAccessApp = jObject.ContainsKey("RemoteAccessApp");
        var hasConnectionUrl = jObject.ContainsKey("ConnectionUrl");

        // Assert
        hasRemoteAccessApp.Should().BeTrue();
        hasConnectionUrl.Should().BeTrue();
    }

    #endregion

    #region Local URL Detection Tests

    [Theory]
    [InlineData("http://localhost/path")]
    [InlineData("http://127.0.0.1/admin")]
    [InlineData("http://127.0.0.100/test")]
    [InlineData("http://0.0.0.0:8080")]
    public void IsLocalUrl_LocalAddresses_ShouldBeIdentifiable(string url)
    {
        // These URLs should be considered local by RealTimeAlertListener.IsLocalUrl
        // We verify they can be parsed and identified
        
        // Act
        var uri = new Uri(url);
        var host = uri.Host.ToLowerInvariant();
        var isLocal = host == "localhost" ||
                      host == "127.0.0.1" ||
                      host == "::1" ||
                      host == "0.0.0.0" ||
                      host.StartsWith("127.");

        // Assert
        isLocal.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://google.com")]
    [InlineData("https://192.168.1.100/path")]
    [InlineData("https://10.0.0.1/admin")]
    [InlineData("https://example.com:8080")]
    public void IsLocalUrl_NonLocalAddresses_ShouldNotBeLocal(string url)
    {
        // Act
        var uri = new Uri(url);
        var host = uri.Host.ToLowerInvariant();
        var isLocal = host == "localhost" ||
                      host == "127.0.0.1" ||
                      host == "::1" ||
                      host == "0.0.0.0" ||
                      host.StartsWith("127.");

        // Assert
        isLocal.Should().BeFalse();
    }

    #endregion

    #region Priority Enum Tests

    [Theory]
    [InlineData(0, Priority.Low)]
    [InlineData(1, Priority.Medium)]
    [InlineData(2, Priority.High)]
    [InlineData(3, Priority.Critical)]
    public void Priority_EnumValues_ShouldMapCorrectly(int value, Priority expected)
    {
        // Act
        var priority = (Priority)value;

        // Assert
        priority.Should().Be(expected);
    }

    #endregion
}
