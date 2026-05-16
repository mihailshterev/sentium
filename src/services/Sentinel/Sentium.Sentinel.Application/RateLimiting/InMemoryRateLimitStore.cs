using System.Collections.Concurrent;
using Sentium.Sentinel.Core.RateLimiting;

namespace Sentium.Sentinel.Application.RateLimiting;

/// <summary>
/// Thread-safe sliding-window rate limiter backed by in-memory timestamp queues.
/// </summary>
public sealed class InMemoryRateLimitStore : IRateLimitStore
{
    private readonly ConcurrentDictionary<string, Queue<long>> _windows = new(StringComparer.OrdinalIgnoreCase);

    public bool TryConsume(string agentId, TimeSpan window, int maxRequests)
    {
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var cutoff = now - window.Ticks;

        var queue = _windows.GetOrAdd(agentId, _ => new Queue<long>());

        lock (queue)
        {
            while (queue.Count > 0 && queue.Peek() < cutoff)
            {
                queue.Dequeue();
            }

            if (queue.Count >= maxRequests)
            {
                return false;
            }

            queue.Enqueue(now);
            return true;
        }
    }

    public int GetCurrentCount(string agentId, TimeSpan window)
    {
        if (!_windows.TryGetValue(agentId, out var queue))
        {
            return 0;
        }

        var cutoff = DateTimeOffset.UtcNow.UtcTicks - window.Ticks;

        lock (queue)
        {
            return queue.Count(t => t >= cutoff);
        }
    }
}
