using Business.Messaging;
using FluentAssertions;
using Newtonsoft.Json.Linq;

namespace ASPS.Tests.Business.Messaging;

public class MessagingCompatibilityTests
{
    [Fact]
    public void Negotiate_CommonV1_SelectsV1WithoutLegacy()
    {
        MessagingCompatibility.Negotiate(
                [1], new MessagingCompatibilityOptions { AcceptLegacyV0 = true })
            .Should().Be(new MessagingNegotiationResult(1, false));
    }

    [Fact]
    public void Negotiate_NoCommonMajor_RejectsWhenLegacyFlagIsOff()
    {
        var action = () => MessagingCompatibility.Negotiate(
            [0], new MessagingCompatibilityOptions { AcceptLegacyV0 = false });

        action.Should().Throw<MessagingCompatibilityException>()
            .Where(error => error.Code == "protocol.unsupported_schema_version");
    }

    [Fact]
    public void Negotiate_LegacyRequiresExplicitFlagAndCannotTargetTab()
    {
        var result = MessagingCompatibility.Negotiate(
            [0], new MessagingCompatibilityOptions { AcceptLegacyV0 = true });

        result.Should().Be(new MessagingNegotiationResult(0, true));
        result.CanPerformTabTargetedAction.Should().BeFalse();
    }

    [Fact]
    public void AdaptLegacyIngress_RemovesTabTargetBeforeActionHandling()
    {
        var alert = JObject.Parse("""{"AlertType":"UrlAlert","TabId":"42"}""");

        MessagingCompatibility.AdaptLegacyIngress(
            alert, new MessagingCompatibilityOptions { AcceptLegacyV0 = true });

        alert["TabId"]!.Type.Should().Be(JTokenType.Null);
        alert["LegacyAdapted"]!.Value<bool>().Should().BeTrue();
    }

    [Fact]
    public void AdaptLegacyIngress_FlagOffRejectsBeforeActionHandling()
    {
        var alert = JObject.Parse("""{"AlertType":"UrlAlert","TabId":"42"}""");

        var action = () => MessagingCompatibility.AdaptLegacyIngress(
            alert, new MessagingCompatibilityOptions { AcceptLegacyV0 = false });

        action.Should().Throw<MessagingCompatibilityException>();
        alert["TabId"]!.Value<string>().Should().Be("42");
    }
}
