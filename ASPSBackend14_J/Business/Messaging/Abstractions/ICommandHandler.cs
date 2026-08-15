using Common.Messaging;

namespace Business.Messaging.Abstractions;

/// <summary>
/// Handles a single <typeparamref name="TCommand"/> and produces a <typeparamref name="TResult"/>.
/// </summary>
public interface ICommandHandler<TCommand, TResult>
    where TCommand : Command
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default);
}
