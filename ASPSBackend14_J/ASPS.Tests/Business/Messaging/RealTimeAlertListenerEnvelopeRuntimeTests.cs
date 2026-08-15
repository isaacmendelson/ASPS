using Business.Messaging;
using Common.Generated.Messaging.V1;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using System.Threading;

namespace ASPS.Tests.Business.Messaging;

public class RealTimeAlertListenerEnvelopeRuntimeTests
{
    [Fact]
    public async Task MalformedFirst_CorrectedRetryClaimsThenDuplicateDoesNotReachDomain()
    {
        using var host = ProductionListenerTestHost.Build(acceptLegacyV0: false);
        var alertProcessor = host.Services.GetRequiredService<AlertProcessor>();
        var token = host.Services.GetRequiredService<global::Business.Services.TokenStore>()
            .CreateToken("device-1", "user-1");
        var dispatchCount = 0;
        alertProcessor.DomainDispatchObserver =
            _ => Interlocked.Increment(ref dispatchCount);
        var envelope = CreateEnvelope(token.TokenValue);
        var malformed = JObject.Parse(envelope);
        malformed["payload"]!["alert"]!["Url"] = "https://wrong.example/";

        var malformedResult = (MessageEnvelopeV1)await alertProcessor.ProcessEnvelopeAsync(
            malformed.ToString(), malformed);
        malformedResult.Outcome!.Error!.Code.Should()
            .Be("validation.immutable_context_mismatch");
        Volatile.Read(ref dispatchCount).Should().Be(0);

        var corrected = JObject.Parse(envelope);
        var correctedResult = (MessageEnvelopeV1)await alertProcessor.ProcessEnvelopeAsync(
            corrected.ToString(), corrected);
        correctedResult.MessageType.Should().Be("url_scan.accepted");
        correctedResult.Payload.TryGetProperty("duplicate", out _).Should().BeFalse();
        await WaitForDispatchCountAsync(() => Volatile.Read(ref dispatchCount), 1);
        Volatile.Read(ref dispatchCount).Should().Be(1);

        var duplicate = JObject.Parse(envelope);
        var duplicateResult = (MessageEnvelopeV1)await alertProcessor.ProcessEnvelopeAsync(
            duplicate.ToString(), duplicate);
        duplicateResult.Payload.GetProperty("duplicate").GetBoolean().Should().BeTrue();
        await Task.Delay(100);
        Volatile.Read(ref dispatchCount).Should().Be(1);
    }

    private static async Task WaitForDispatchCountAsync(
        Func<int> getCount, int expected)
    {
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (getCount() != expected && DateTime.UtcNow < timeout)
            await Task.Delay(10);
    }

    private static string CreateEnvelope(string token)
    {
        var messageId = Guid.NewGuid();
        var serialized = JsonSerializer.Serialize(new MessageEnvelopeV1
        {
            MessageId = messageId,
            CorrelationId = Guid.NewGuid(),
            RequestId = Guid.NewGuid(),
            MessageType = "url_scan.request",
            SentAt = DateTimeOffset.UtcNow,
            Source = "desktop",
            Context = new MessageContextV1
            {
                DeviceId = "device-1",
                TabId = "12",
                Url = "https://example.com/"
            },
            Outcome = null,
            Payload = JsonSerializer.SerializeToElement(new
            {
                alert = new
                {
                    AlertId = Guid.NewGuid().ToString(),
                    AlertType = "UrlAlert",
                    DeviceInfo = new
                    {
                        DeviceUid = "device-1",
                        DeviceType = 1,
                        OperatingSystem = 1,
                        MACAddress = "00:11:22:33:44:55"
                    },
                    Timestamp = DateTime.UtcNow,
                    Priority = 1,
                    Token = token,
                    Url = "https://example.com/",
                    Trackers = Array.Empty<object>(),
                    IFrameDomains = Array.Empty<string>(),
                    UserAgent = "test",
                    TabId = "12"
                }
            })
        });
        var envelope = JObject.Parse(serialized);
        envelope["sentAt"] = DateTimeOffset.UtcNow
            .ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
        return envelope.ToString();
    }
}
