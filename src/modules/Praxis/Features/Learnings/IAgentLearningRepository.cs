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

    /// <summary>
    /// Up to <paramref name="max"/> learnings, oldest first, whose embedding failed and can still be retried. Every
    /// user's, regardless of the caller.
    /// </summary>
    Task<IReadOnlyList<AgentLearning>> GetPendingIngestionAsync(int max, CancellationToken ct = default);

    /// <summary>
    /// Counts one recall of each learning in <paramref name="ids"/>.
    /// </summary>
    Task RecordRecallAsync(IReadOnlyCollection<Guid> ids, DateTimeOffset recalledAt, CancellationToken ct = default);

    /// <summary>
    /// Every user's learnings neither recalled nor captured since <paramref name="cutoff"/>.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetUnusedSinceAsync(DateTimeOffset cutoff, CancellationToken ct = default);
}
