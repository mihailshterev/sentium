using System.Collections.Concurrent;
using Sentium.AgentRuntime.Core.Agents;

namespace Sentium.AgentRuntime.Infrastructure.Agents;

/// <summary>
/// In-memory store for tool-approval continuations.
/// </summary>
public sealed class PendingApprovalStore : IPendingApprovalStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);
    private const int MaxEntries = 1000;

    private readonly ConcurrentDictionary<string, Entry> _pending = new();

    public void Add(string requestId, PendingApproval approval)
    {
        PruneExpired();
        EnforceCap();
        _pending[requestId] = new Entry(approval, DateTimeOffset.UtcNow);
    }

    public bool TryTake(string requestId, out PendingApproval? approval)
    {
        if (_pending.TryRemove(requestId, out var entry) && !IsExpired(entry))
        {
            approval = entry.Approval;
            return true;
        }

        approval = null;
        return false;
    }

    private static bool IsExpired(Entry entry) => DateTimeOffset.UtcNow - entry.CreatedAt > Ttl;

    private void PruneExpired()
    {
        foreach (var kvp in _pending)
        {
            if (IsExpired(kvp.Value))
            {
                _pending.TryRemove(kvp.Key, out _);
            }
        }
    }

    private void EnforceCap()
    {
        while (_pending.Count >= MaxEntries)
        {
            var oldestKey = string.Empty;
            var oldestAt = DateTimeOffset.MaxValue;
            var found = false;

            foreach (var kvp in _pending)
            {
                if (kvp.Value.CreatedAt < oldestAt)
                {
                    oldestKey = kvp.Key;
                    oldestAt = kvp.Value.CreatedAt;
                    found = true;
                }
            }

            if (!found || !_pending.TryRemove(oldestKey, out _))
            {
                break;
            }
        }
    }

    private readonly record struct Entry(PendingApproval Approval, DateTimeOffset CreatedAt);
}
