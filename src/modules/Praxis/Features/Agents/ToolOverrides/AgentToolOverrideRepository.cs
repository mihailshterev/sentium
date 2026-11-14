using Microsoft.EntityFrameworkCore;
using Sentium.Praxis.Data;
using Sentium.Praxis.Entities;

namespace Sentium.Praxis.Features.Agents.ToolOverrides;

internal sealed class AgentToolOverrideRepository(PraxisDbContext context) : IAgentToolOverrideRepository
{
    public async Task<AgentToolOverrideRecord?> GetAsync(string agentName, Guid userId, CancellationToken ct = default)
    {
        return await context.AgentToolOverrides
            .AsNoTracking()
            .Where(o => o.UserId == userId && o.AgentName == agentName)
            .Select(o => new AgentToolOverrideRecord(o.AgentName, o.Tools))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<AgentToolOverrideRecord>> GetAllAsync(Guid userId, CancellationToken ct = default)
    {
        return await context.AgentToolOverrides
            .AsNoTracking()
            .Where(o => o.UserId == userId)
            .Select(o => new AgentToolOverrideRecord(o.AgentName, o.Tools))
            .ToListAsync(ct);
    }

    public async Task UpsertAsync(string agentName, Guid userId, IReadOnlyList<string>? tools, CancellationToken ct = default)
    {
        List<string>? list = tools is null ? null : [.. tools.Distinct(StringComparer.OrdinalIgnoreCase)];

        var existing = await context.AgentToolOverrides
            .FirstOrDefaultAsync(o => o.UserId == userId && o.AgentName == agentName, ct);

        if (existing is null)
        {
            context.AgentToolOverrides.Add(new AgentToolOverride
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AgentName = agentName,
                Tools = list,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.Tools = list;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string agentName, Guid userId, CancellationToken ct = default)
    {
        await context.AgentToolOverrides
            .Where(o => o.UserId == userId && o.AgentName == agentName)
            .ExecuteDeleteAsync(ct);
    }
}
