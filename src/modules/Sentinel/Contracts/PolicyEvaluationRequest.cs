namespace Sentium.Sentinel.Contracts;

public sealed record PolicyEvaluationRequest
{
    public required string AgentId { get; init; }
    public required string SkillName { get; init; }
    public required ResourceType ResourceType { get; init; }
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
    /// The outbound content of a write, an execution or an external call, scanned by the content policies (sensitive-data
    /// egress, semantic intent). The audit trail keeps only its length and hash.
    /// </summary>
    public string? Payload { get; init; }

    /// <summary>
    /// Extra facts about the call (a language, a code length), recorded in the audit trail and scanned like the payload.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
