namespace WebApi.Services;

public interface INetMQClientService
{
    Task<TResult> SendCommandAsync<TCommand, TResult>(TCommand command)
        where TCommand : Common.Messaging.Command
        where TResult : Common.Messaging.CommandResult;

    Task<TResult> SendQueryAsync<TQuery, TResult>(TQuery query)
        where TQuery : Common.Messaging.Query
        where TResult : Common.Messaging.QueryResult;
}
