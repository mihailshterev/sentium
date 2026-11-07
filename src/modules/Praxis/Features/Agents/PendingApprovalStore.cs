using System.Collections.Concurrent;

namespace Sentium.Praxis.Features.Agents;

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

    public bool TryTake(string requestId, Guid? ownerId, out PendingApproval? approval)
    {
        approval = null;

        if (!_pending.TryGetValue(requestId, out var entry) || entry.Approval.UserId != ownerId)
        {
            return false;
        }

        if (!_pending.TryRemove(new KeyValuePair<string, Entry>(requestId, entry)) || IsExpired(entry))
        {
            return false;
        }

        approval = entry.Approval;
        return true;
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
