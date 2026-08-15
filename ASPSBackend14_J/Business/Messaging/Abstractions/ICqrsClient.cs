using Common.Messaging;

namespace Business.Messaging.Abstractions;

/// <summary>
/// Transport-agnostic CQRS client — sends queries/commands and awaits their result.
/// Mirrors <see cref="WebApi.Services.ICQRSClient"/> but adds cancellation support
/// and lives outside WebApi so Business-hosted callers can depend on it too.
/// </summary>
public interface ICqrsClient : IDisposable
{
    Task<TResult> SendQueryAsync<TResult>(Query query, CancellationToken ct = default) where TResult : QueryResult;
    Task<TResult> SendCommandAsync<TResult>(Command command, CancellationToken ct = default) where TResult : CommandResult;
}
