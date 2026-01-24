using Sentinel.Core.Decisions;
using Sentinel.Core.Events;

namespace Sentinel.Core.Policies;

public interface ISentinelPolicy
{
    SentinelDecision Evaluate(SentinelEvent evt);
}
