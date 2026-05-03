using Sentium.Sentinel.Core.Events;
using Sentium.Sentinel.Core.Decisions;
using Sentium.Sentinel.Core.Policies;

namespace Sentium.Sentinel.Infrastructure.Policies;

public sealed class BlockOutboundNetworkPolicy : ISentinelPolicy
{
    public SentinelDecision Evaluate(SentinelEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        if (evt.Type == EventType.Network && evt.Action == TrafficDirection.Outbound)
        {
            return new SentinelDecision(
                Allowed: false,
                Reason: "Outbound network access is forbidden"
            );
        }

        return new SentinelDecision(true, "Not applicable");
    }
}
