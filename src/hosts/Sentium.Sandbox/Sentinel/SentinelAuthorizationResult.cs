namespace Sentium.Sandbox.Sentinel;

internal sealed record SentinelAuthorizationResult
{
    public required bool IsAllowed { get; init; }
    public required string Reason { get; init; }
    public required Guid AuditId { get; init; }

    public static SentinelAuthorizationResult Allow(Guid auditId)
    {
        return new()
        {
            IsAllowed = true,
            Reason = "All policy checks passed.",
            AuditId = auditId
        };
    }

    public static SentinelAuthorizationResult Deny(string reason, Guid auditId = default)
    {
        return new()
        {
            IsAllowed = false,
            Reason = reason,
            AuditId = auditId
        };
    }
}
