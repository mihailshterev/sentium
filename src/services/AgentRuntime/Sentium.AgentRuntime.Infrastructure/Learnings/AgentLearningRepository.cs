using Microsoft.EntityFrameworkCore;
using Sentium.AgentRuntime.Core.Entities;
using Sentium.AgentRuntime.Core.Learnings;
using Sentium.AgentRuntime.Infrastructure.Data;

namespace Sentium.AgentRuntime.Infrastructure.Learnings;

public sealed class AgentLearningRepository(AgentRuntimeDbContext context) : IAgentLearningRepository
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
                l.ConversationId, l.CapturedAt, l.IsIngested, l.IsGlobal))
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
}
