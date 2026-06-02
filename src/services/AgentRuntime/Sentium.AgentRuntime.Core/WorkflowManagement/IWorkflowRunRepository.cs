using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.Entities;

namespace Sentium.AgentRuntime.Core.WorkflowManagement;

/// <summary>
/// Persists and queries workflow run records for the current user scope.
/// </summary>
public interface IWorkflowRunRepository
{
    /// <summary>
    /// Persists a completed workflow run.
    /// </summary>
    Task AddAsync(WorkflowRun run, CancellationToken ct = default);

    /// <summary>
    /// Returns the <paramref name="count"/> most recent runs, ordered newest first.
    /// </summary>
    Task<IReadOnlyList<WorkflowRunResponse>> GetRecentAsync(int count = 20, CancellationToken ct = default);

    /// <summary>
    /// Returns the run with the given <paramref name="id"/>, or <see langword="null"/> if not found.
    /// </summary>
    Task<WorkflowRunResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
