using System.Collections.Concurrent;

namespace Business.Services;

/// <summary>
/// In-memory sliding window rate limiter for NetMQ token endpoints.
/// Thread-safe via ConcurrentDictionary.
/// </summary>
public class RateLimiter
{
    private readonly ConcurrentDictionary<string, List<DateTime>> _requests = new();
    private DateTime _lastCleanup = DateTime.UtcNow;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Check if a request is allowed under the rate limit.
    /// </summary>
    /// <param name="key">Identifier (e.g., DeviceUid or "endpoint:DeviceUid")</param>
    /// <param name="maxRequests">Maximum requests allowed in the window</param>
    /// <param name="window">Time window</param>
    /// <returns>True if allowed, false if rate limited</returns>
    public bool IsAllowed(string key, int maxRequests, TimeSpan window)
    {
        var now = DateTime.UtcNow;
        var cutoff = now - window;

        var timestamps = _requests.GetOrAdd(key, _ => new List<DateTime>());

        lock (timestamps)
        {
            // Remove expired entries
            timestamps.RemoveAll(t => t < cutoff);

            if (timestamps.Count >= maxRequests)
            {
                return false;
            }

            timestamps.Add(now);
        }

        // Periodic cleanup of stale keys
        if (now - _lastCleanup > _cleanupInterval)
        {
            _lastCleanup = now;
            CleanupStaleEntries(cutoff);
        }

        return true;
    }

    private void CleanupStaleEntries(DateTime cutoff)
    {
        foreach (var key in _requests.Keys)
        {
            if (_requests.TryGetValue(key, out var timestamps))
            {
                lock (timestamps)
                {
                    timestamps.RemoveAll(t => t < cutoff);
                    if (timestamps.Count == 0)
                    {
                        _requests.TryRemove(key, out _);
                    }
                }
            }
        }
    }
}
