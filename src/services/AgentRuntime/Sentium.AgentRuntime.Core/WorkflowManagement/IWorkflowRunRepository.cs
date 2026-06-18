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
    /// Returns a page of run summaries (newest first) plus the total count. Summaries omit the
    /// per-run log transcript; use <see cref="GetByIdAsync"/> for the full run including logs.
    /// </summary>
    Task<(IReadOnlyList<WorkflowRunSummaryResponse> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Returns the run with the given <paramref name="id"/>, or <see langword="null"/> if not found.
    /// </summary>
    Task<WorkflowRunResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
