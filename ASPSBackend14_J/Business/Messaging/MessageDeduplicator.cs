namespace Business.Messaging;

/// <summary>
/// Atomically claims wire message IDs before domain side effects.
/// Entries expire and the in-memory set is bounded; the database unique index
/// on persisted MessageId remains the durable race/failover backstop.
/// </summary>
public sealed class MessageDeduplicator
{
    private readonly TimeSpan _retention;
    private readonly int _capacity;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly Dictionary<Guid, DateTimeOffset> _claims = new();
    private readonly object _gate = new();

    public MessageDeduplicator(
        TimeSpan retention,
        int capacity,
        Func<DateTimeOffset>? utcNow = null)
    {
        if (retention <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(retention));
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _retention = retention;
        _capacity = capacity;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public int Count
    {
        get
        {
            lock (_gate)
                return _claims.Count;
        }
    }

    public bool TryBegin(Guid messageId)
    {
        var now = _utcNow();
        lock (_gate)
        {
            RemoveExpired(now);
            if (_claims.ContainsKey(messageId))
                return false;

            while (_claims.Count >= _capacity)
            {
                var oldest = _claims.MinBy(pair => pair.Value).Key;
                _claims.Remove(oldest);
            }

            _claims.Add(messageId, now);
            return true;
        }
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var messageId in _claims
                     .Where(pair => now - pair.Value >= _retention)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _claims.Remove(messageId);
        }
    }
}
