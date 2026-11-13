using Sentium.Praxis.Entities;
using Sentium.Praxis.Features.Workflows;

namespace Sentium.Praxis.Features.WorkflowManagement;

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
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Runs per page.</param>
    /// <param name="search">
    /// Optional text matched (case-insensitive) against the run title and the name of the workflow that ran; runs recorded
    /// before titles existed are matched against their trigger payload (the scenario) instead.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task<(IReadOnlyList<WorkflowRunSummaryResponse> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null, CancellationToken ct = default);

    /// <summary>
    /// Returns the run with the given <paramref name="id"/>, or <see langword="null"/> if not found.
    /// </summary>
    Task<WorkflowRunResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Sets a user-chosen title on the run with the given <paramref name="id"/>.
    /// </summary>
    /// <returns><see langword="false"/> when the run does not exist (or belongs to someone else).</returns>
    Task<bool> RenameAsync(Guid id, string title, CancellationToken ct = default);
}
