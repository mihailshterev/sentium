using System.Collections.Concurrent;
using Sentium.AgentRuntime.Core.Agents;

namespace Sentium.AgentRuntime.Infrastructure.Agents;

public sealed class PendingApprovalStore : IPendingApprovalStore
{
    private readonly ConcurrentDictionary<string, PendingApproval> _pending = new();

    public void Add(string requestId, PendingApproval approval) => _pending[requestId] = approval;

    public bool TryTake(string requestId, out PendingApproval? approval) => _pending.TryRemove(requestId, out approval);
}
