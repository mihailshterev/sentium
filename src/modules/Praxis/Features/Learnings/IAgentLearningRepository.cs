using Sentium.Praxis.Entities;

namespace Sentium.Praxis.Features.Learnings;

/// <summary>
/// Defines the contract for managing and retrieving agent learning records.
/// </summary>
internal interface IAgentLearningRepository
{
    /// <summary>
    /// Retrieves a page of learning responses (newest first) plus the total count,
    /// optionally filtered by agent name.
    /// </summary>
    /// <param name="page">1-based page number.</param>
    Task<(IReadOnlyList<AgentLearningResponse> Items, int TotalCount)> GetAllAsync(string? agentName, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Gets statistical information regarding stored agent learnings.
    /// </summary>
    Task<AgentLearningStats> GetStatsAsync(CancellationToken ct = default);

    /// <summary>
    /// Finds a specific learning record by its unique identifier.
    /// </summary>
    Task<AgentLearning?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Adds a new learning record to the repository and persists the change.
    /// </summary>
    Task AddAsync(AgentLearning entity, CancellationToken ct = default);

    /// <summary>
    /// Saves any pending changes tracked by the repository to the underlying data store.
    /// </summary>
    Task SaveAsync(CancellationToken ct = default);

    /// <summary>
    /// Removes a specific learning record and persists the change.
    /// </summary>
    Task RemoveAsync(AgentLearning entity, CancellationToken ct = default);
}
