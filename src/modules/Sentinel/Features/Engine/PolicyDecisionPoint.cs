using Sentium.Sentinel.Contracts;
using Sentium.Sentinel.Features.Policies;

namespace Sentium.Sentinel.Features.Engine;

/// <summary>
/// In-process <see cref="IPolicyDecisionPoint"/>. Each evaluation runs in its own DI scope (as the old
/// <c>POST policy/evaluate</c> request did), so concurrent tool calls never share the audit
/// <see cref="Data.SentinelDbContext"/>.
/// </summary>
public sealed class PolicyDecisionPoint(IServiceScopeFactory scopeFactory) : IPolicyDecisionPoint
{
    public async Task<PolicyEvaluationResponse> EvaluateAsync(PolicyEvaluationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.TryParse<ResourceType>(request.ResourceType, ignoreCase: true, out var resourceType))
        {
            return Deny($"Unknown resource type '{request.ResourceType}'. Valid values: {string.Join(", ", Enum.GetNames<ResourceType>())}");
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

    private static PolicyEvaluationResponse Deny(string reason)
    {
        return new()
        {
            Allowed = false,
            Effect = nameof(PolicyEffect.Deny),
            Reason = reason,
            Risk = nameof(PolicyRiskLevel.High),
            AuditId = Guid.Empty,
            Timestamp = DateTimeOffset.UtcNow,
            TriggeredPolicies = ["InvalidRequest"]
        };
    }
}
