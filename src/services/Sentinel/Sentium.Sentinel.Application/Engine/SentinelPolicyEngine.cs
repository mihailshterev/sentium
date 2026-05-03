using Sentium.Sentinel.Core.Decisions;
using Sentium.Sentinel.Core.Events;
using Sentium.Sentinel.Core.Policies;

namespace Sentium.Sentinel.Application.Engine;

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
