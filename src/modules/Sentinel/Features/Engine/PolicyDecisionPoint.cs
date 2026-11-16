using Sentium.Sentinel.Contracts;
using Sentium.Sentinel.Features.Policies;

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
        if (!Enum.TryParse<ResourceType>(request.ResourceType, ignoreCase: true, out var resourceType))
        {
            return Deny($"Unknown resource type '{request.ResourceType}'. Valid values: {string.Join(", ", Enum.GetNames<ResourceType>())}", PolicyRiskLevel.High, "InvalidRequest");
        }

        var policyRequest = new PolicyRequest
        {
            AgentId = request.AgentId,
            SkillName = request.SkillName,
            ResourceType = resourceType,
            ResourceId = request.ResourceId,
            Action = request.Action,
            OriginalUserPrompt = request.OriginalUserPrompt,
            CorrelationId = request.CorrelationId,
            UserId = request.UserId,
            Metadata = request.Metadata
        };

        await using var scope = scopeFactory.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetRequiredService<SentinelPolicyEngine>();

        var decision = await engine.EvaluateAsync(policyRequest, ct);

        return new PolicyEvaluationResponse
        {
            Allowed = decision.Allowed,
            Effect = decision.Effect.ToString(),
            Reason = decision.Reason,
            Risk = decision.Risk.ToString(),
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
            Effect = nameof(PolicyEffect.Deny),
            Reason = reason,
            Risk = risk.ToString(),
            AuditId = Guid.Empty,
            Timestamp = DateTimeOffset.UtcNow,
            TriggeredPolicies = [trigger]
        };
    }
}
