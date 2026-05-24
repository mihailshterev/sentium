namespace Sentium.Sentinel.Core.Policies;

public sealed record PolicyDecision
{
    public required bool Allowed { get; init; }
    public required PolicyEffect Effect { get; init; }
    public required string Reason { get; init; }
    public required PolicyRiskLevel Risk { get; init; }
    public required Guid AuditId { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<string> TriggeredPolicies { get; init; } = [];
    public string? AlignmentVerdict { get; init; }

    public static PolicyDecision Allow(Guid auditId, IReadOnlyList<string> policies) =>
        new()
        {
            Allowed = true,
            Effect = PolicyEffect.Allow,
            Reason = "All policy checks passed.",
            Risk = PolicyRiskLevel.Low,
            AuditId = auditId,
            TriggeredPolicies = policies
        };

    public static PolicyDecision Deny(
        string reason,
        Guid auditId,
        IReadOnlyList<string> policies,
        PolicyRiskLevel risk = PolicyRiskLevel.High,
        bool alert = false) =>
        new()
        {
            Allowed = false,
            Effect = alert ? PolicyEffect.DenyWithAlert : PolicyEffect.Deny,
            Reason = reason,
            Risk = risk,
            AuditId = auditId,
            TriggeredPolicies = policies
        };
}
