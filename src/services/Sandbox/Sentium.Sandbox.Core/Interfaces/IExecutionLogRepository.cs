using Sentium.Sandbox.Core.Models;

namespace Sentium.Sandbox.Core.Interfaces;

/// <summary>
/// Stores and retrieves <see cref="ExecutionLogEntry"/> records so that operators can
/// inspect what the AI agents executed inside the sandbox.
/// </summary>
public interface IExecutionLogRepository
{
    /// <summary>
    /// Appends a completed execution to the store.
    /// </summary>
    Task AddAsync(ExecutionLogEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Returns a page of entries (newest first) matching the supplied filter, along with the
    /// total number of entries that match (ignoring paging).
    /// </summary>
    Task<(IReadOnlyList<ExecutionLogEntry> Items, int TotalCount)> GetPagedAsync(ExecutionLogQuery query, CancellationToken ct = default);

    /// <summary>
    /// Returns a single entry by its job id, or <c>null</c> if no such entry exists.
    /// </summary>
    Task<ExecutionLogEntry?> GetByIdAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>
    /// Returns aggregate outcome counts across all recorded executions.
    /// </summary>
    Task<ExecutionLogStats> GetStatsAsync(CancellationToken ct = default);
}
