using Sentium.Sentinel.Contracts;

namespace Sentium.Praxis.Features.Sentinel;

/// <summary>
/// Submits authorization requests to the Sentinel PDP (in-process, through <see cref="IPolicyDecisionPoint"/>).
/// All agent tool invocations that touch protected resources must go through this client.
/// </summary>
public sealed class SentinelClient(IPolicyDecisionPoint policyDecisionPoint, ILogger<SentinelClient> logger)
{
    /// <summary>
    /// Submits a full policy evaluation request and returns the PDP decision.
    /// </summary>
    /// <param name="request">The full evaluation context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <see cref="PdpDecision"/> from Sentinel; a deny if the evaluation itself fails.</returns>
    public async Task<PdpDecision> EvaluateAsync(PdpRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var response = await policyDecisionPoint.EvaluateAsync(new PolicyEvaluationRequest
            {
                AgentId = request.AgentId,
                SkillName = request.SkillName,
                ResourceType = request.ResourceType,
                ResourceId = request.ResourceId,
                Action = request.Action,
                OriginalUserPrompt = request.OriginalUserPrompt,
                CorrelationId = request.CorrelationId,
                Metadata = request.Metadata
            }, ct);

            return new PdpDecision
            {
                Allowed = response.Allowed,
                Effect = response.Effect,
                Reason = response.Reason,
                Risk = response.Risk,
                AuditId = response.AuditId,
                Timestamp = response.Timestamp,
                TriggeredPolicies = response.TriggeredPolicies
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Sentinel PDP evaluation failed for agent '{AgentId}' skill '{Skill}'. Failing closed.", request.AgentId, request.SkillName);

            return PdpDecision.DenyFallback("Sentinel PDP is unavailable. Request denied for safety.");
        }
    }

    /// <summary>
    /// Convenience method: returns <see langword="true"/> only if the PDP explicitly allows the request.
    /// Use <see cref="EvaluateAsync"/> when you need the full decision payload.
    /// </summary>
    public async Task<bool> IsAllowedAsync(PdpRequest request, CancellationToken ct)
    {
        var decision = await EvaluateAsync(request, ct);
        return decision.Allowed;
    }
}

public sealed record PdpRequest
{
    public required string AgentId { get; init; }
    public required string SkillName { get; init; }
    public required string ResourceType { get; init; }
    public required string ResourceId { get; init; }
    public required string Action { get; init; }
    public required string OriginalUserPrompt { get; init; }
    public required string CorrelationId { get; init; }

    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

public sealed record PdpDecision
{
    public required bool Allowed { get; init; }
    public required string Effect { get; init; }
    public required string Reason { get; init; }
    public required string Risk { get; init; }
    public required Guid AuditId { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<string> TriggeredPolicies { get; init; } = [];

    public static PdpDecision DenyFallback(string reason) => new()
    {
        Allowed = false,
        Effect = "Deny",
        Reason = reason,
        Risk = "Critical",
        AuditId = Guid.Empty,
        TriggeredPolicies = ["FailClosed"]
    };
}
