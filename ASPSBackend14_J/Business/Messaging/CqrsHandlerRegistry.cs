using System.Collections.Concurrent;
using Common.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace Business.Messaging;

/// <summary>
/// Type-safe command/query dispatch registry. Replaces manual switch dispatch in CQRSGateway.
/// </summary>
public class CqrsHandlerRegistry
{
    // Map from CommandType/QueryType string → async dispatch function
    // The dispatch function takes (messageJson, IServiceScope) and returns serialized JSON result
    private readonly ConcurrentDictionary<string, Func<string, IServiceScope, Task<string>>> _handlers = new();

    private readonly JsonSerializerSettings _serializerSettings = new()
    {
        TypeNameHandling = TypeNameHandling.None,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
    };

    /// <summary>
    /// Register a command handler. THandler must have a HandleAsync(TCommand) method.
    /// </summary>
    public void RegisterCommand<TCommand, THandler>(string commandType)
        where TCommand : Command
    {
        _handlers[commandType] = async (json, scope) =>
        {
            var command = JsonConvert.DeserializeObject<TCommand>(json);
            if (command == null)
                return CreateErrorJson($"Invalid {commandType} format");

            var handler = scope.ServiceProvider.GetRequiredService<THandler>();
            var result = await InvokeHandleAsync(handler!, command);
            return JsonConvert.SerializeObject(result, _serializerSettings);
        };
    }

    /// <summary>
    /// Register a query handler.
    /// </summary>
    public void RegisterQuery<TQuery, THandler>(string queryType)
        where TQuery : Query
    {
        _handlers[queryType] = async (json, scope) =>
        {
            var query = JsonConvert.DeserializeObject<TQuery>(json);
            if (query == null)
                return CreateErrorJson($"Invalid {queryType} format");

            var handler = scope.ServiceProvider.GetRequiredService<THandler>();
            var result = await InvokeHandleAsync(handler!, query);
            return JsonConvert.SerializeObject(result, _serializerSettings);
        };
    }

    /// <summary>
    /// Register a query with a factory default (for queries where null deserialization should use `new TQuery()`).
    /// </summary>
    public void RegisterQueryWithDefault<TQuery, THandler>(string queryType)
        where TQuery : Query, new()
    {
        _handlers[queryType] = async (json, scope) =>
        {
            var query = JsonConvert.DeserializeObject<TQuery>(json) ?? new TQuery();
            var handler = scope.ServiceProvider.GetRequiredService<THandler>();
            var result = await InvokeHandleAsync(handler!, query);
            return JsonConvert.SerializeObject(result, _serializerSettings);
        };
    }

    /// <summary>
    /// Register a custom handler function for special cases (direct service access, non-standard patterns).
    /// </summary>
    public void RegisterCustom(string messageType, Func<string, IServiceScope, Task<string>> handler)
    {
        _handlers[messageType] = handler;
    }

    /// <summary>
    /// Dispatch a command or query by its type string.
    /// </summary>
    public async Task<string> DispatchAsync(string messageType, string messageJson, IServiceScope scope)
    {
        if (!_handlers.TryGetValue(messageType, out var handler))
            return CreateErrorJson($"Unknown message type: {messageType}");

        return await handler(messageJson, scope);
    }

    /// <summary>
    /// Check if a message type is registered.
    /// </summary>
    public bool IsRegistered(string messageType) => _handlers.ContainsKey(messageType);

    /// <summary>
    /// Get all registered message type names.
    /// </summary>
    public IReadOnlyCollection<string> RegisteredTypes => _handlers.Keys.ToList().AsReadOnly();

    private static string CreateErrorJson(string message)
    {
        return JsonConvert.SerializeObject(new { Success = false, Message = message });
    }

    private static async Task<object?> InvokeHandleAsync(object handler, object message)
    {
        // Find the HandleAsync method on the handler
        var method = handler.GetType().GetMethod("HandleAsync",
            new[] { message.GetType() });

        if (method == null)
        {
            // Try without specific type (might be base type parameter)
            var methods = handler.GetType().GetMethods()
                .Where(m => m.Name == "HandleAsync" && m.GetParameters().Length == 1)
                .ToList();

            method = methods.FirstOrDefault(m =>
                m.GetParameters()[0].ParameterType.IsAssignableFrom(message.GetType()));
        }

        if (method == null)
            throw new InvalidOperationException(
                $"Handler {handler.GetType().Name} does not have a HandleAsync method accepting {message.GetType().Name}");

        var task = (Task)method.Invoke(handler, new[] { message })!;
        await task;

        // Get the result from Task<T>
        var resultProperty = task.GetType().GetProperty("Result");
        return resultProperty?.GetValue(task);
    }
}
