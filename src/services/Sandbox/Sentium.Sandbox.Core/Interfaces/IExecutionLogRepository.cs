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
    /// Returns the most recent <paramref name="count"/> entries, newest first.
    /// </summary>
    Task<IReadOnlyList<ExecutionLogEntry>> GetRecentAsync(int count = 100, CancellationToken ct = default);
}
