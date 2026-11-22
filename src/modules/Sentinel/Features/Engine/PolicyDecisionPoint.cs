using Sentium.Sentinel.Contracts;

namespace Sentium.Sentinel.Features.Engine;

/// <summary>
/// In-process <see cref="IPolicyDecisionPoint"/>. Each evaluation runs in its own DI scope, so concurrent tool calls
/// never share the audit <see cref="Data.SentinelDbContext"/>. Fails closed: any fault outside the engine (which
/// already denies on a policy fault) is a deny too, so callers never need their own guard.
/// </summary>
internal sealed class PolicyDecisionPoint(IServiceScopeFactory scopeFactory, ILogger<PolicyDecisionPoint> logger) : IPolicyDecisionPoint
{
    private const string UnavailableReason = "Sentinel PDP is unavailable. Request denied for safety.";

    public async Task<PolicyEvaluationResponse> EvaluateAsync(PolicyEvaluationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            return await EvaluateCoreAsync(request, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Sentinel PDP evaluation failed for agent '{AgentId}' skill '{Skill}'. Failing closed.", request.AgentId, request.SkillName);
            return Deny(UnavailableReason, PolicyRiskLevel.Critical, "FailClosed");
        }
    }

    private async Task<PolicyEvaluationResponse> EvaluateCoreAsync(PolicyEvaluationRequest request, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetRequiredService<SentinelPolicyEngine>();

        var decision = await engine.EvaluateAsync(request, ct);

        return new PolicyEvaluationResponse
        {
            Allowed = decision.Allowed,
            Effect = decision.Effect,
            Reason = decision.Reason,
            Risk = decision.Risk,
            AuditId = decision.AuditId,
            Timestamp = decision.Timestamp,
            TriggeredPolicies = decision.TriggeredPolicies
        };
    }

    private static PolicyEvaluationResponse Deny(string reason, PolicyRiskLevel risk, string trigger)
    {
        return new()
        {
            Allowed = false,
            Effect = PolicyEffect.Deny,
            Reason = reason,
            Risk = risk,
            AuditId = Guid.Empty,
            Timestamp = DateTimeOffset.UtcNow,
            TriggeredPolicies = [trigger]
        };
    }
}
