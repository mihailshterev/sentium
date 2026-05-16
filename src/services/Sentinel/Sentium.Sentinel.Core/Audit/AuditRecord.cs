using Sentium.Sentinel.Core.Policies;

namespace Sentium.Sentinel.Core.Audit;

public sealed record AuditRecord
{
    public required Guid Id { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string AgentId { get; init; }
    public required string SkillName { get; init; }
    public required ResourceType ResourceType { get; init; }
    public required string ResourceId { get; init; }
    public required string Action { get; init; }
    public required string UserPromptHash { get; init; }
    public required string CorrelationId { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
    public required bool Allowed { get; init; }
    public required PolicyEffect Effect { get; init; }
    public required string Reason { get; init; }
    public required PolicyRiskLevel Risk { get; init; }
    public required IReadOnlyList<string> TriggeredPolicies { get; init; }
    public required long EvaluationDurationMs { get; init; }
    public string? AlignmentVerdict { get; init; }
}
