namespace Common.Messaging;

public abstract class BaseMessage
{
    public Guid MessageId { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string MessageType { get; set; } = string.Empty;
}

public abstract class Command : BaseMessage
{
    protected Command()
    {
        MessageType = "Command";
    }
}

public abstract class CommandResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
}

public abstract class Query : BaseMessage
{
    protected Query()
    {
        MessageType = "Query";
    }
}

public abstract class QueryResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
}
