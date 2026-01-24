using Sentinel.Core.Events;
using Sentinel.Core.Decisions;
using Sentinel.Core.Policies;

namespace Sentinel.Infrastructure.Policies;

public sealed class BlockOutboundNetworkPolicy : ISentinelPolicy
{
    public SentinelDecision Evaluate(SentinelEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        if (evt.Type == EventType.Network && evt.Action == EventType.Outbound)
        {
            return new SentinelDecision(
                Allowed: false,
                Reason: "Outbound network access is forbidden"
            );
        }

        return new SentinelDecision(true, "Not applicable");
    }
}
