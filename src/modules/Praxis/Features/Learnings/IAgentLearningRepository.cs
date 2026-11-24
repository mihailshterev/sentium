using Sentium.Praxis.Entities;

namespace Sentium.Praxis.Features.Learnings;

internal interface IAgentLearningRepository
{
    /// <summary>
    /// Newest first.
    /// </summary>
    /// <param name="page">1-based.</param>
    Task<(IReadOnlyList<AgentLearningResponse> Items, int TotalCount)> GetAllAsync(string? agentName, int page, int pageSize, CancellationToken ct = default);

    Task<AgentLearningStats> GetStatsAsync(CancellationToken ct = default);

    Task<AgentLearning?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Saves immediately.
    /// </summary>
    Task AddAsync(AgentLearning entity, CancellationToken ct = default);

    Task SaveAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves immediately.
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
