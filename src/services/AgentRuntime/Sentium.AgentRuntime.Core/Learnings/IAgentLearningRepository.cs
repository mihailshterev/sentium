using Sentium.AgentRuntime.Core.Entities;

namespace Sentium.AgentRuntime.Core.Learnings;

/// <summary>
/// Defines the contract for managing and retrieving agent learning records.
/// </summary>
public interface IAgentLearningRepository
{
    /// <summary>
    /// Retrieves a page of learning responses (newest first) plus the total count,
    /// optionally filtered by agent name.
    /// </summary>
    /// <param name="agentName">The optional name of the agent to filter by.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Number of records per page.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A page of <see cref="AgentLearningResponse"/> objects with the total count.</returns>
    Task<(IReadOnlyList<AgentLearningResponse> Items, int TotalCount)> GetAllAsync(string? agentName, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Gets statistical information regarding stored agent learnings.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>An <see cref="AgentLearningStats"/> object containing metrics like total and pending counts.</returns>
    Task<AgentLearningStats> GetStatsAsync(CancellationToken ct = default);

    /// <summary>
    /// Finds a specific learning record by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the learning record.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The <see cref="AgentLearning"/> entity if found; otherwise, null.</returns>
    Task<AgentLearning?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Adds a new learning record to the repository and persists the change.
    /// </summary>
    /// <param name="entity">The learning entity to add.</param>
    /// <param name="ct">The cancellation token.</param>
    Task AddAsync(AgentLearning entity, CancellationToken ct = default);

    /// <summary>
    /// Saves any pending changes tracked by the repository to the underlying data store.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    Task SaveAsync(CancellationToken ct = default);

    /// <summary>
    /// Removes a specific learning record and persists the change.
    /// </summary>
    /// <param name="entity">The learning entity to remove.</param>
    /// <param name="ct">The cancellation token.</param>
    Task RemoveAsync(AgentLearning entity, CancellationToken ct = default);
}
