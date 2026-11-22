namespace Sentium.Sentinel.Contracts;

public sealed record PolicyEvaluationResponse
{
    public required bool Allowed { get; init; }
    public required PolicyEffect Effect { get; init; }
    public required string Reason { get; init; }
    public required PolicyRiskLevel Risk { get; init; }
    public required Guid AuditId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public IReadOnlyList<string> TriggeredPolicies { get; init; } = [];
}
