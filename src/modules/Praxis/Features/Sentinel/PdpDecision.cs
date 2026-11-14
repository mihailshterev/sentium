namespace Sentium.Praxis.Features.Sentinel;

internal sealed record PdpDecision
{
    public required bool Allowed { get; init; }
    public required string Effect { get; init; }
    public required string Reason { get; init; }
    public required string Risk { get; init; }
    public required Guid AuditId { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<string> TriggeredPolicies { get; init; } = [];

    public static PdpDecision DenyFallback(string reason)
    {
        return new()
        {
            Allowed = false,
            Effect = "Deny",
            Reason = reason,
            Risk = "Critical",
            AuditId = Guid.Empty,
            TriggeredPolicies = ["FailClosed"]
        };
    }
}
