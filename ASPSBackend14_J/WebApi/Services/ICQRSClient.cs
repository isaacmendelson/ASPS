using Common.Messaging;

namespace WebApi.Services;

/// <summary>
/// Interface for CQRS Client - allows mocking in unit tests
/// </summary>
public interface ICQRSClient : IDisposable
{
    Task<TResult> SendQueryAsync<TResult>(Query query) where TResult : QueryResult;
    Task<TResult> SendCommandAsync<TResult>(Command command) where TResult : CommandResult;
}
