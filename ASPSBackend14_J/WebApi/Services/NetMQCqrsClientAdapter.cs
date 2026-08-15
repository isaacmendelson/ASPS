using Business.Messaging.Abstractions;
using Common.Messaging;

namespace WebApi.Services;

/// <summary>
/// Thin adapter exposing <see cref="Business.Messaging.Transport.NetMQ.NetMQCqrsClient"/>
/// (via the transport-agnostic <see cref="ICqrsClient"/>) through the legacy
/// <see cref="ICQRSClient"/> contract, so the 39 existing WebApi consumers keep working
/// unmodified during migration (ASPS-687, Messaging Refactoring Phase 4).
/// Business cannot reference WebApi, so this adapter — not the transport class itself —
/// is what bridges the two interfaces; it wraps the same singleton transport instance,
/// keeping NetMQCqrsClient the single canonical implementation (no duplicated socket logic).
/// Mechanical replacement of ICQRSClient → ICqrsClient across consumers is Phase 6 cleanup.
/// </summary>
public sealed class NetMQCqrsClientAdapter : ICQRSClient
{
    private readonly ICqrsClient _inner;

    public NetMQCqrsClientAdapter(ICqrsClient inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public Task<TResult> SendQueryAsync<TResult>(Query query) where TResult : QueryResult =>
        _inner.SendQueryAsync<TResult>(query, CancellationToken.None);

    public Task<TResult> SendCommandAsync<TResult>(Command command) where TResult : CommandResult =>
        _inner.SendCommandAsync<TResult>(command, CancellationToken.None);

    public void Dispose() => _inner.Dispose();
}
