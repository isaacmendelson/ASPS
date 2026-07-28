using Common.Generated.Messaging.V1;
using Common.Models.Alerts;
using Common.Entities;
using FluentAssertions;

namespace ASPS.Tests.Common.Messaging;

public class MessageIdentityPropagationTests
{
    [Fact]
    public void DeviceAlert_RetainsImmutableEnvelopeIdentityForAsyncAnalysis()
    {
        var identity = new MessageIdentityV1(
            Guid.Parse("4b2a90c9-4b50-40e8-8969-f594b9fde602"),
            Guid.Parse("6b7a9fa7-e3f0-4c5c-86cc-3914f42b262f"),
            Guid.Parse("7afe6cba-7916-40e1-91dc-666f40f760db"),
            "device-1", "12", "https://example.com/");
        var alert = new UrlAlert { MessagingIdentity = identity };

        alert.MessagingIdentity.Should().BeSameAs(identity);
    }

    [Theory]
    [InlineData(nameof(DeviceAlertEntity.MessageId))]
    [InlineData(nameof(DeviceAlertEntity.CorrelationId))]
    [InlineData(nameof(DeviceAlertEntity.RequestId))]
    [InlineData(nameof(DeviceAlertEntity.SchemaVersion))]
    [InlineData(nameof(DeviceAlertEntity.CanonicalUrl))]
    public void DeviceAlertPersistence_HasEnvelopeIdentityColumn(string propertyName)
    {
        typeof(DeviceAlertEntity).GetProperty(propertyName).Should().NotBeNull();
    }

    [Fact]
    public void ResultFactory_PreservesWorkflowIdentityAndCreatesNewMessageId()
    {
        var identity = new MessageIdentityV1(
            Guid.Parse("4b2a90c9-4b50-40e8-8969-f594b9fde602"),
            Guid.Parse("6b7a9fa7-e3f0-4c5c-86cc-3914f42b262f"),
            Guid.Parse("7afe6cba-7916-40e1-91dc-666f40f760db"),
            "device-1", "12", "https://example.com/");

        var result = MessageEnvelopeFactoryV1.CreateSuccess(identity, new { riskScore = 72 });

        result.MessageId.Should().NotBe(identity.MessageId);
        result.CorrelationId.Should().Be(identity.CorrelationId);
        result.RequestId.Should().Be(identity.RequestId);
        result.Context.Url.Should().Be(identity.Url);
        result.Outcome!.Status.Should().Be("success");
    }
}
