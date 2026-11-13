using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Data;

namespace Sentium.Praxis.Features.Agents;

/// <summary>
/// Strips tools that an external source no longer offers from every agent's tool list and built-in override (across all
/// owners, so query filters are ignored), in one SaveChanges, then evicts the affected agent caches.
/// </summary>
public sealed class AgentToolReferenceCleaner(PraxisDbContext db, HybridCache cache) : IAgentToolReferenceCleaner
{
    public async Task<int> RemoveToolsByPrefixAsync(string prefix, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(prefix);

        bool HasPrefix(string tool) => tool.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

        var agents = await db.Agents.IgnoreQueryFilters().Where(a => a.Tools != null).ToListAsync(ct);
        var overrides = await db.AgentToolOverrides.IgnoreQueryFilters().Where(o => o.Tools != null).ToListAsync(ct);

        var owners = new HashSet<Guid>();
        foreach (var agent in agents.Where(a => a.Tools!.Any(HasPrefix)))
        {
            agent.Tools = [.. agent.Tools!.Where(t => !HasPrefix(t))];
            owners.Add(agent.UserId);
        }

        foreach (var toolOverride in overrides.Where(o => o.Tools!.Any(HasPrefix)))
        {
            toolOverride.Tools = [.. toolOverride.Tools!.Where(t => !HasPrefix(t))];
            owners.Add(toolOverride.UserId);
        }

        if (owners.Count == 0)
        {
            return 0;
        }

        await db.SaveChangesAsync(ct);

        foreach (var scope in owners.Select(o => o.ToString()).Append("sovereign"))
        {
            await cache.RemoveByTagAsync($"{AgentService.CacheTag}:{scope}", ct);
        }

        return owners.Count;
    }
}
