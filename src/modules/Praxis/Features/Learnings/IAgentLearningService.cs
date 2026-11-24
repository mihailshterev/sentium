using Sentium.Core.Results;

namespace Sentium.Praxis.Features.Learnings;

internal interface IAgentLearningService
{
    /// <summary>
    /// Newest first.
    /// </summary>
    Task<PagedResponse<AgentLearningResponse>> GetLearningsAsync(string? agentName, int page, int pageSize, CancellationToken ct = default);

    Task<AgentLearningStats> GetStatsAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves the learning and queues it for embedding.
    /// </summary>
    Task<AgentLearningResponse> CaptureAsync(CaptureAgentLearningRequest request, CancellationToken ct = default);

    /// <summary>
    /// Also removes its vectors. <see langword="false"/> when the learning does not exist.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Re-ingests the learning in place of its old vectors. <see langword="null"/> when it does not exist.
    /// </summary>
    Task<AgentLearningResponse?> UpdateAsync(Guid id, UpdateAgentLearningRequest request, CancellationToken ct = default);

    /// <summary>
    /// Shared learnings plus <paramref name="userId"/>'s own, relevant to <paramref name="query"/>. Empty on failure:
    /// recall is best-effort and must never break an agent run.
    /// </summary>
    Task<IReadOnlyList<RecalledLearning>> RecallRelevantAsync(string query, Guid? userId, int limit = 5, CancellationToken ct = default);

    /// <summary>
    /// Embeds up to <paramref name="max"/> learnings whose embedding failed earlier, oldest first, stopping at the
    /// first failure.
    /// </summary>
    /// <returns>The number of learnings embedded.</returns>
    Task<int> RetryPendingIngestionAsync(int max, CancellationToken ct = default);
}
