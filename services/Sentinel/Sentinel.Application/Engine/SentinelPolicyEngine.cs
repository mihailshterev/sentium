using Sentinel.Core.Decisions;
using Sentinel.Core.Events;
using Sentinel.Core.Policies;

namespace Sentinel.Application.Engine;

public sealed class SentinelPolicyEngine
{
    private readonly IReadOnlyList<ISentinelPolicy> Policies;

    public SentinelPolicyEngine(IEnumerable<ISentinelPolicy> policies)
    {
        Policies = policies.ToList();
    }

    public SentinelDecision Evaluate(SentinelEvent evt)
    {
        foreach (var policy in Policies)
        {
            var decision = policy.Evaluate(evt);
            if (!decision.Allowed)
            {
                return decision;
            }
        }

        return new SentinelDecision(true, "Allowed");
    }
}
