namespace Sentium.Praxis.Features.Learnings;

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
internal sealed record LearningPromotionVerdict(bool Approved, string Reason, string SanitizedContent, Guid? DuplicateOfId = null);
