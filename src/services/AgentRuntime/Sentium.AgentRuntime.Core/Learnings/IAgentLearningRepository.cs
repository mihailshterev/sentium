using Sentium.AgentRuntime.Core.Entities;

namespace Sentium.AgentRuntime.Core.Learnings;

/// <summary>
/// Defines the contract for managing and retrieving agent learning records.
/// </summary>
public interface IAgentLearningRepository
{
    /// <summary>
    /// Retrieves a list of learning responses, optionally filtered by agent name.
    /// </summary>
    /// <param name="agentName">The optional name of the agent to filter by.</param>
    /// <param name="count">The maximum number of records to return. Defaults to 50.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A read-only list of <see cref="AgentLearningResponse"/> objects.</returns>
    Task<IReadOnlyList<AgentLearningResponse>> GetAllAsync(string? agentName = null, int count = 50, CancellationToken ct = default);

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
