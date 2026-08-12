using Business.Messaging;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ASPS.Tests.Business.Messaging;

public class RealTimeAlertListenerDiTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProductionRegistration_InjectsMessagingAcceptLegacyV0(bool enabled)
    {
        using var host = ProductionListenerTestHost.Build(enabled);

        var alertProcessor = host.Services.GetRequiredService<AlertProcessor>();

        alertProcessor.AcceptLegacyV0.Should().Be(enabled);
    }
}
