namespace Sentium.AgentRuntime.Core.Learnings;

/// <summary>
/// Gatekeeper that decides whether an agent-captured learning may be promoted to <em>global</em>
/// (shared with every user's agents). It must not trust the agent's request blindly: a learning may
/// only become global if it is (1) free of user-specific identifiers and (2) a genuinely generalizable
/// architectural or execution pattern - not a personal fact - and is not a near-duplicate of existing
/// global knowledge.
/// </summary>
public interface ILearningSanitizationPipeline
{
    /// <summary>
    /// Evaluates a candidate learning for global promotion. Implementations are <strong>fail-closed</strong>:
    /// on any error or timeout the verdict is <c>Approved = false</c> so the learning is kept private
    /// rather than wrongly shared.
    /// </summary>
    Task<LearningPromotionVerdict> EvaluateForGlobalAsync(string content, string tags, string agentName, CancellationToken ct = default);
}

/// <summary>
/// Outcome of <see cref="ILearningSanitizationPipeline.EvaluateForGlobalAsync"/>.
/// </summary>
/// <param name="Approved">Whether the learning may be stored globally.</param>
/// <param name="Reason">Human-readable explanation, surfaced back to the agent.</param>
/// <param name="SanitizedContent">
/// The content to persist. Equals the original input unless a stage produced an abstracted version.
/// </param>
/// <param name="DuplicateOfId">
/// When set, an equivalent global learning already exists; no new global record should be created.
/// </param>
public sealed record LearningPromotionVerdict(bool Approved, string Reason, string SanitizedContent, Guid? DuplicateOfId = null);
