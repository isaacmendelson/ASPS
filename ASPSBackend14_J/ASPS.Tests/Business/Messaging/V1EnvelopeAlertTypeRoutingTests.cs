using Business.Messaging;
using Common.Generated.Messaging.V1;
using Common.Models;
using Common.Models.Alerts;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using System.Threading;

namespace ASPS.Tests.Business.Messaging;

/// <summary>
/// ASPS-732: ProcessEnvelopeAsync must accept v1 envelopes for every alert family the desktop
/// agent sends (url_scan, track_url, tab_closed, tab_changed, remote_access) — not just
/// url_scan.request — and route payload.alert to the matching entity type via the existing
/// ProcessAlertAsync discriminator pipeline.
/// </summary>
public class V1EnvelopeAlertTypeRoutingTests
{
    [Theory]
    [InlineData("url_scan.request", "url_scan.accepted", typeof(UrlAlert))]
    [InlineData("track_url.request", "track_url.accepted", typeof(TrackUrlAlert))]
    [InlineData("tab_closed.request", "tab_closed.accepted", typeof(TabClosedAlert))]
    [InlineData("tab_changed.request", "tab_changed.accepted", typeof(TabChangedAlert))]
    public async Task ProcessEnvelopeAsync_UrlBasedAlertFamily_RoutesToMatchingEntityAndAccepts(
        string messageType, string expectedAccepted, Type expectedEntityType)
    {
        using var host = ProductionListenerTestHost.Build(acceptLegacyV0: false);
        var alertProcessor = host.Services.GetRequiredService<AlertProcessor>();
        var token = host.Services.GetRequiredService<global::Business.Services.TokenStore>()
            .CreateToken("device-1", "user-1");

        DeviceAlert? dispatchedAlert = null;
        alertProcessor.DomainDispatchObserver = evt => Volatile.Write(ref dispatchedAlert, evt.Alert);

        var envelope = CreateUrlBasedEnvelope(messageType, token.TokenValue);
        var wire = JObject.Parse(envelope);

        var result = (MessageEnvelopeV1)await alertProcessor.ProcessEnvelopeAsync(envelope, wire);

        result.MessageType.Should().Be(expectedAccepted);
        result.Outcome.Should().BeNull();

        await WaitForAsync(() => Volatile.Read(ref dispatchedAlert) != null);
        dispatchedAlert.Should().BeOfType(expectedEntityType);
        dispatchedAlert!.AlertType.Should().Be(expectedEntityType.Name);
    }

    [Fact]
    public async Task ProcessEnvelopeAsync_RemoteAccessRequest_RoutesToRemoteAccessAlertAndAccepts()
    {
        using var host = ProductionListenerTestHost.Build(acceptLegacyV0: false);
        var alertProcessor = host.Services.GetRequiredService<AlertProcessor>();
        var token = host.Services.GetRequiredService<global::Business.Services.TokenStore>()
            .CreateToken("device-1", "user-1");

        DeviceAlert? dispatchedAlert = null;
        alertProcessor.DomainDispatchObserver = evt => Volatile.Write(ref dispatchedAlert, evt.Alert);

        var envelope = CreateRemoteAccessEnvelope(token.TokenValue, contextTabId: "999");
        var wire = JObject.Parse(envelope);

        var result = (MessageEnvelopeV1)await alertProcessor.ProcessEnvelopeAsync(envelope, wire);

        result.MessageType.Should().Be("remote_access.accepted");
        result.Outcome.Should().BeNull();

        await WaitForAsync(() => Volatile.Read(ref dispatchedAlert) != null);
        dispatchedAlert.Should().BeOfType<RemoteAccessAlert>();
        dispatchedAlert!.AlertType.Should().Be(nameof(RemoteAccessAlert));
    }

    [Fact]
    public async Task ProcessEnvelopeAsync_RemoteAccessRequest_DeviceIdMismatch_ReturnsImmutableContextError()
    {
        using var host = ProductionListenerTestHost.Build(acceptLegacyV0: false);
        var alertProcessor = host.Services.GetRequiredService<AlertProcessor>();
        var token = host.Services.GetRequiredService<global::Business.Services.TokenStore>()
            .CreateToken("device-1", "user-1");

        var envelope = CreateRemoteAccessEnvelope(token.TokenValue, contextTabId: null, contextDeviceId: "device-other");
        var wire = JObject.Parse(envelope);

        var result = (MessageEnvelopeV1)await alertProcessor.ProcessEnvelopeAsync(envelope, wire);

        result.MessageType.Should().Be("remote_access.error");
        result.Outcome!.Error!.Code.Should().Be("validation.immutable_context_mismatch");
    }

    [Fact]
    public async Task ProcessEnvelopeAsync_TrackUrlRequest_UrlMismatch_ReturnsImmutableContextErrorWithFamilyPrefix()
    {
        using var host = ProductionListenerTestHost.Build(acceptLegacyV0: false);
        var alertProcessor = host.Services.GetRequiredService<AlertProcessor>();
        var token = host.Services.GetRequiredService<global::Business.Services.TokenStore>()
            .CreateToken("device-1", "user-1");

        var envelope = CreateUrlBasedEnvelope("track_url.request", token.TokenValue);
        var mismatched = JObject.Parse(envelope);
        mismatched["payload"]!["alert"]!["Url"] = "https://wrong.example/";

        var result = (MessageEnvelopeV1)await alertProcessor.ProcessEnvelopeAsync(mismatched.ToString(), mismatched);

        result.MessageType.Should().Be("track_url.error");
        result.Outcome!.Error!.Code.Should().Be("validation.immutable_context_mismatch");
    }

    [Fact]
    public async Task ProcessEnvelopeAsync_UnknownMessageType_ReturnsUnsupportedMessageTypeError()
    {
        using var host = ProductionListenerTestHost.Build(acceptLegacyV0: false);
        var alertProcessor = host.Services.GetRequiredService<AlertProcessor>();
        var token = host.Services.GetRequiredService<global::Business.Services.TokenStore>()
            .CreateToken("device-1", "user-1");

        // MessageEnvelopeValidator itself rejects unknown message types before ProcessEnvelopeAsync's
        // own family switch runs — confirms the validator's expanded MessageTypes set still rejects
        // anything outside the five known families.
        var envelope = CreateUrlBasedEnvelope("unknown_family.request", token.TokenValue);
        var wire = JObject.Parse(envelope);

        var result = (MessageEnvelopeV1)await alertProcessor.ProcessEnvelopeAsync(envelope, wire);

        result.Outcome!.Error!.Code.Should().Be("protocol.unknown_message_type");
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < timeout)
            await Task.Delay(10);
    }

    private static string CreateUrlBasedEnvelope(string messageType, string token)
    {
        object alert = messageType switch
        {
            "url_scan.request" => new
            {
                AlertId = Guid.NewGuid().ToString(),
                AlertType = "UrlAlert",
                DeviceInfo = new { DeviceUid = "device-1", DeviceType = 1, OperatingSystem = 1, MACAddress = "00:11:22:33:44:55" },
                Timestamp = DateTime.UtcNow,
                Priority = 1,
                Token = token,
                Url = "https://example.com/",
                Trackers = Array.Empty<object>(),
                IFrameDomains = Array.Empty<string>(),
                UserAgent = "test",
                TabId = "12"
            },
            "track_url.request" => new
            {
                AlertId = Guid.NewGuid().ToString(),
                AlertType = "TrackUrlAlert",
                DeviceInfo = new { DeviceUid = "device-1", DeviceType = 1, OperatingSystem = 1, MACAddress = "00:11:22:33:44:55" },
                Timestamp = DateTime.UtcNow,
                Priority = 1,
                Token = token,
                Url = "https://example.com/",
                FromUrl = "https://referrer.example/",
                Duration = 5,
                UserAgent = "test",
                TabId = "12"
            },
            "tab_closed.request" => new
            {
                AlertId = Guid.NewGuid().ToString(),
                AlertType = "TabClosedAlert",
                DeviceInfo = new { DeviceUid = "device-1", DeviceType = 1, OperatingSystem = 1, MACAddress = "00:11:22:33:44:55" },
                Timestamp = DateTime.UtcNow,
                Priority = 1,
                Token = token,
                Url = "https://example.com/",
                TabId = "12"
            },
            "tab_changed.request" => new
            {
                AlertId = Guid.NewGuid().ToString(),
                AlertType = "TabChangedAlert",
                DeviceInfo = new { DeviceUid = "device-1", DeviceType = 1, OperatingSystem = 1, MACAddress = "00:11:22:33:44:55" },
                Timestamp = DateTime.UtcNow,
                Priority = 1,
                Token = token,
                Url = "https://example.com/",
                TabId = "12",
                IsSensitiveWebsite = false,
                IsLoggedIn = false
            },
            _ => new
            {
                AlertId = Guid.NewGuid().ToString(),
                AlertType = "UrlAlert",
                DeviceInfo = new { DeviceUid = "device-1", DeviceType = 1, OperatingSystem = 1, MACAddress = "00:11:22:33:44:55" },
                Timestamp = DateTime.UtcNow,
                Priority = 1,
                Token = token,
                Url = "https://example.com/",
                TabId = "12"
            }
        };

        var serialized = JsonSerializer.Serialize(new MessageEnvelopeV1
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            RequestId = Guid.NewGuid(),
            MessageType = messageType,
            SentAt = DateTimeOffset.UtcNow,
            Source = "desktop",
            Context = new MessageContextV1 { DeviceId = "device-1", TabId = "12", Url = "https://example.com/" },
            Outcome = null,
            Payload = JsonSerializer.SerializeToElement(new { alert })
        });
        var envelope = JObject.Parse(serialized);
        envelope["sentAt"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
        return envelope.ToString();
    }

    private static string CreateRemoteAccessEnvelope(string token, string? contextTabId, string contextDeviceId = "device-1")
    {
        var alert = new
        {
            AlertId = Guid.NewGuid().ToString(),
            AlertType = "RemoteAccessAlert",
            DeviceInfo = new { DeviceUid = "device-1", DeviceType = 1, OperatingSystem = 1, MACAddress = "00:11:22:33:44:55" },
            Timestamp = DateTime.UtcNow,
            Priority = 1,
            Token = token,
            RemoteAccessApp = 1,
            RunningProcesses = 2,
            ConnectionUrl = "anydesk://session/123",
            ConnectionStatus = 1,
            ConnectionsCount = 1,
            SessionStatus = 1,
            Direction = "incoming"
        };

        var serialized = JsonSerializer.Serialize(new MessageEnvelopeV1
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            RequestId = Guid.NewGuid(),
            MessageType = "remote_access.request",
            SentAt = DateTimeOffset.UtcNow,
            Source = "desktop",
            // Context.Url/TabId are unrelated to a RemoteAccessAlert's own fields (it has no Url/TabId) —
            // proves only DeviceId is cross-checked for this family.
            Context = new MessageContextV1 { DeviceId = contextDeviceId, TabId = contextTabId, Url = "https://example.com/unrelated" },
            Outcome = null,
            Payload = JsonSerializer.SerializeToElement(new { alert })
        });
        var envelope = JObject.Parse(serialized);
        envelope["sentAt"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
        return envelope.ToString();
    }
}
