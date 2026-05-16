using Sentium.Sandbox.Core.Models;

namespace Sentium.Sandbox.Application.Sentinel;

/// <summary>
/// Abstracts calls to the Sentinel Policy Decision Point so the Application layer
/// remains independent of any specific HTTP client implementation.
/// </summary>
public interface ISentinelGateway
{
    /// <summary>
    /// Asks the Sentinel PDP whether the given agent is permitted to execute code.
    /// Implementations must be fail-closed: any PDP communication failure must return a denial.
    /// </summary>
    Task<SentinelAuthorizationResult> AuthorizeExecutionAsync(ExecutionRequest request, CancellationToken ct);
}

public sealed record SentinelAuthorizationResult
{
    public required bool IsAllowed { get; init; }
    public required string Reason { get; init; }
    public required Guid AuditId { get; init; }

    public static SentinelAuthorizationResult Allow(Guid auditId) => new()
    {
        IsAllowed = true,
        Reason = "All policy checks passed.",
        AuditId = auditId
    };

    public static SentinelAuthorizationResult Deny(string reason, Guid auditId = default) => new()
    {
        IsAllowed = false,
        Reason = reason,
        AuditId = auditId
    };
}
