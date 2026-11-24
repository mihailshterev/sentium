using Microsoft.EntityFrameworkCore;
using Sentium.Praxis.Entities;
using Sentium.Praxis.Data;

namespace Sentium.Praxis.Features.Learnings;

internal sealed class AgentLearningRepository(PraxisDbContext context) : IAgentLearningRepository
{
    public async Task<(IReadOnlyList<AgentLearningResponse> Items, int TotalCount)> GetAllAsync(string? agentName, int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.AgentLearnings.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(agentName))
        {
            query = query.Where(l => l.AgentName == agentName);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.CapturedAt)
            .ThenByDescending(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new AgentLearningResponse(
                l.Id, l.AgentName, l.Content, l.Tags,
                l.ConversationId, l.CapturedAt, l.IsIngested, l.IsGlobal,
                l.RecallCount, l.LastRecalledAt))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<AgentLearningStats> GetStatsAsync(CancellationToken ct = default)
    {
        var total = await context.AgentLearnings.CountAsync(ct);
        var pending = await context.AgentLearnings.CountAsync(l => !l.IsIngested, ct);
        var global = await context.AgentLearnings.CountAsync(l => l.IsGlobal, ct);

        var byAgent = await context.AgentLearnings
            .AsNoTracking()
            .GroupBy(l => l.AgentName)
            .Select(g => new { AgentName = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.AgentName, x => x.Count, ct);

        return new AgentLearningStats(total, pending, global, byAgent);
    }

    public async Task<AgentLearning?> FindAsync(Guid id, CancellationToken ct = default)
        => await context.AgentLearnings.FindAsync([id], ct);

    public async Task AddAsync(AgentLearning entity, CancellationToken ct = default)
    {
        context.AgentLearnings.Add(entity);
        await context.SaveChangesAsync(ct);
    }

    public async Task SaveAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);

    public async Task RemoveAsync(AgentLearning entity, CancellationToken ct = default)
    {
        context.AgentLearnings.Remove(entity);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AgentLearning>> GetPendingIngestionAsync(int max, CancellationToken ct = default)
    {
        // Ownerless private learnings are never embedded (they have no retrieval scope), so they are not pending.
        return await context.AgentLearnings
            .IgnoreQueryFilters()
            .Where(l => !l.IsIngested && (l.IsGlobal || l.UserId != null))
            .OrderBy(l => l.CapturedAt)
            .Take(max)
            .ToListAsync(ct);
    }

    public async Task RecordRecallAsync(IReadOnlyCollection<Guid> ids, DateTimeOffset recalledAt, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return;
        }

        await context.AgentLearnings
            .IgnoreQueryFilters()
            .Where(l => ids.Contains(l.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.RecallCount, l => l.RecallCount + 1)
                .SetProperty(l => l.LastRecalledAt, recalledAt), ct);
    }

    public async Task<IReadOnlyList<Guid>> GetUnusedSinceAsync(DateTimeOffset cutoff, CancellationToken ct = default)
    {
        return await context.AgentLearnings
            .IgnoreQueryFilters()
            .Where(l => (l.LastRecalledAt ?? l.CapturedAt) < cutoff)
            .Select(l => l.Id)
            .ToListAsync(ct);
    }
}
