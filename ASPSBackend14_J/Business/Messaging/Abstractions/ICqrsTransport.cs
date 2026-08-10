using Microsoft.Extensions.Hosting;

namespace Business.Messaging.Abstractions;

/// <summary>
/// Abstraction for CQRS transport server-side — listens for commands/queries and dispatches them.
/// </summary>
public interface ICqrsTransport : IHostedService, IDisposable
{
    // No public methods — it's a listener that dispatches via CqrsHandlerRegistry.
}
