using System.Collections.Concurrent;
using Sentium.Sandbox.Core.Interfaces;
using Sentium.Sandbox.Core.Models;

namespace Sentium.Sandbox.Infrastructure.Logging;

/// <summary>
/// Thread-safe in-memory execution log capped at <see cref="MaxCapacity"/> entries.
/// Oldest entries are evicted when the cap is reached.
/// </summary>
public sealed class InMemoryExecutionLogRepository : IExecutionLogRepository
{
    private const int MaxCapacity = 500;
    private readonly ConcurrentQueue<ExecutionLogEntry> _queue = new();

    public Task AddAsync(ExecutionLogEntry entry, CancellationToken ct = default)
    {
        _queue.Enqueue(entry);

        while (_queue.Count > MaxCapacity)
        {
            _queue.TryDequeue(out _);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ExecutionLogEntry>> GetRecentAsync(int count = 100, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ExecutionLogEntry>>(
            _queue.TakeLast(count).Reverse().ToList());
}
