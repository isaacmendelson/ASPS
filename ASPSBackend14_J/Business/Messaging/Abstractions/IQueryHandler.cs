using Common.Messaging;

namespace Business.Messaging.Abstractions;

/// <summary>
/// Handles a single <typeparamref name="TQuery"/> and produces a <typeparamref name="TResult"/>.
/// </summary>
public interface IQueryHandler<TQuery, TResult>
    where TQuery : Query
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
}
