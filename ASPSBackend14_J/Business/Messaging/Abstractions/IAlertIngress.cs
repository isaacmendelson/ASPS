using Microsoft.Extensions.Hosting;

namespace Business.Messaging.Abstractions;

/// <summary>
/// Abstraction for alert ingress — listens for device alerts and dispatches them.
/// Implementations are hosted services that run in the background.
/// </summary>
public interface IAlertIngress : IHostedService, IDisposable
{
    // No public methods — it's a listener that dispatches via DomainEventPublisher.
    // Configuration (port, CURVE) is constructor-injected.
}
