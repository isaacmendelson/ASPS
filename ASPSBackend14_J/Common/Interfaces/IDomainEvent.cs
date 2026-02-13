namespace Common.Interfaces;

public interface IDomainEvent
{
    DateTime Timestamp { get; }
    string EventType { get; }
}

public interface IDomainEventHandler
{
    Task Handle(IDomainEvent evt);
    Type[] GetHandleableEvents();
}

public interface IBackgroundTask
{
    void Start();
    void Stop();
}
