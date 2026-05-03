using Sentium.Sentinel.Core.Decisions;
using Sentium.Sentinel.Core.Events;

namespace Sentium.Sentinel.Core.Policies;

public interface ISentinelPolicy
{
    SentinelDecision Evaluate(SentinelEvent evt);
}
