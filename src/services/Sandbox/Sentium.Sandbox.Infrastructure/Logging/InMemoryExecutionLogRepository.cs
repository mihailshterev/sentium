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

    public Task<(IReadOnlyList<ExecutionLogEntry> Items, int TotalCount)> GetPagedAsync(
        ExecutionLogQuery query,
        CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var filtered = ApplyFilters(_queue.Reverse(), query).ToList();

        var items = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult<(IReadOnlyList<ExecutionLogEntry>, int)>((items, filtered.Count));
    }

    public Task<ExecutionLogEntry?> GetByIdAsync(Guid jobId, CancellationToken ct = default)
        => Task.FromResult(_queue.FirstOrDefault(e => e.JobId == jobId));

    public Task<ExecutionLogStats> GetStatsAsync(CancellationToken ct = default)
    {
        var snapshot = _queue.ToList();

        var total = snapshot.Count;
        var denied = snapshot.Count(e => e.PolicyDenied);
        var succeeded = snapshot.Count(e => e.Succeeded && !e.PolicyDenied);
        var failed = total - denied - succeeded;

        return Task.FromResult(new ExecutionLogStats(total, succeeded, failed, denied));
    }

    private static IEnumerable<ExecutionLogEntry> ApplyFilters(IEnumerable<ExecutionLogEntry> source, ExecutionLogQuery query)
    {
        if (query.Language is { } language)
        {
            source = source.Where(e => e.Language == language);
        }

        source = query.Status switch
        {
            ExecutionStatusFilter.Succeeded => source.Where(e => e.Succeeded && !e.PolicyDenied),
            ExecutionStatusFilter.Failed => source.Where(e => !e.Succeeded && !e.PolicyDenied),
            ExecutionStatusFilter.Denied => source.Where(e => e.PolicyDenied),
            _ => source
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            source = source.Where(e =>
                e.AgentId.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                e.JobId.ToString().Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        return source;
    }
}
