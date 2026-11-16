namespace Sentium.Sentinel.Contracts;

public sealed record PolicyEvaluationRequest
{
    public const string PayloadKey = "payload";

    public required string AgentId { get; init; }
    public required string SkillName { get; init; }
    public required string ResourceType { get; init; }
    public required string ResourceId { get; init; }
    public required string Action { get; init; }
    public required string OriginalUserPrompt { get; init; }
    public required string CorrelationId { get; init; }

    /// <summary>
    /// The user the agent acts for: recorded in the audit trail and the scope of the rate limit.
    /// <see langword="null"/> for unattended system work.
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Inspected by the content policies (sensitive-data egress, semantic intent). Under <see cref="PayloadKey"/> it
    /// carries the outbound payload, which the audit trail stores only as a length and hash.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
