namespace Sentium.AgentRuntime.Core.Learnings;

/// <summary>
/// Manages the lifecycle of agent learnings: capture, retrieval, deletion and
/// triggering of the RAG ingestion pipeline so learnings feed back into the
/// knowledge base for future recall.
/// </summary>
public interface IAgentLearningService
{
    /// <summary>
    /// Returns paginated learnings, optionally filtered by agent name.
    /// Results are ordered newest-first.
    /// </summary>
    Task<IReadOnlyList<AgentLearningResponse>> GetLearningsAsync(string? agentName = null, int count = 50, CancellationToken ct = default);

    /// <summary>
    /// Returns aggregate statistics across all learnings.
    /// </summary>
    Task<AgentLearningStats> GetStatsAsync(CancellationToken ct = default);

    /// <summary>
    /// Persists a new learning and enqueues it for vector ingestion.
    /// </summary>
    Task<AgentLearningResponse> CaptureAsync(CaptureAgentLearningRequest request, CancellationToken ct = default);

    /// <summary>
    /// Hard-deletes a learning and removes its vectors from the knowledge base.
    /// Returns <see langword="false"/> when no learning with the given <paramref name="id"/> exists.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Updates the content and tags of an existing learning.
    /// The old vectors are removed and the learning is re-ingested with the new content.
    /// Returns <see langword="null"/> when no learning with the given <paramref name="id"/> exists.
    /// </summary>
    Task<AgentLearningResponse?> UpdateAsync(Guid id, UpdateAgentLearningRequest request, CancellationToken ct = default);

    /// <summary>
    /// Semantic-searches the <c>agent_learnings</c> collection for learnings relevant to
    /// <paramref name="query"/>, scoped to shared/global learnings plus the given user's own
    /// private learnings. Returns an empty list on failure so callers can treat learning recall
    /// as best-effort (it must never break an agent run).
    /// </summary>
    Task<IReadOnlyList<RecalledLearning>> RecallRelevantAsync(string query, Guid? userId, int limit = 5, CancellationToken ct = default);
}
