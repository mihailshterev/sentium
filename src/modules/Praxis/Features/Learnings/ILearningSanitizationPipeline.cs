namespace Sentium.Praxis.Features.Learnings;

/// <summary>
/// Gatekeeper that decides whether an agent-captured learning may be promoted to <em>global</em>
/// (shared with every user's agents). It must not trust the agent's request blindly: a learning may
/// only become global if it is (1) free of user-specific identifiers and (2) a genuinely generalizable
/// architectural or execution pattern - not a personal fact - and is not a near-duplicate of existing
/// global knowledge.
/// </summary>
internal interface ILearningSanitizationPipeline
{
    /// <summary>
    /// Evaluates a candidate learning for global promotion. Implementations are <strong>fail-closed</strong>:
    /// on any error or timeout the verdict is <c>Approved = false</c> so the learning is kept private
    /// rather than wrongly shared.
    /// </summary>
    Task<LearningPromotionVerdict> EvaluateForGlobalAsync(string content, string tags, string agentName, CancellationToken ct = default);
}
