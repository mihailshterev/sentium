using Sentium.Praxis.Entities;
using Sentium.Praxis.Data;
using Sentium.Core.Results;
using Microsoft.EntityFrameworkCore;

namespace Sentium.Praxis.Features.Skills;

public sealed class AgentSkillRepository(PraxisDbContext context) : IAgentSkillRepository
{
    public async Task<IReadOnlyList<AgentSkill>> GetAllAsync(CancellationToken ct = default)
    {
        return await context.AgentSkills
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Take(PaginationQuery.MaxListCap)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AgentSkill>> GetForUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await context.AgentSkills
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(PaginationQuery.MaxListCap)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<AgentSkill> Items, int TotalCount)> GetPagedAsync(AgentSkillType? skillType, int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.AgentSkills.AsNoTracking();

        if (skillType is { } type)
        {
            query = query.Where(s => s.SkillType == type);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<AgentSkill?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.AgentSkills.FindAsync([id], ct);

    public async Task<AgentSkill?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        return await context.AgentSkills
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == name, ct);
    }

    public async Task AddAsync(AgentSkill skill, CancellationToken ct = default)
    {
        context.AgentSkills.Add(skill);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(AgentSkill skill, CancellationToken ct = default)
    {
        context.AgentSkills.Update(skill);
        await context.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var affected = await context.AgentSkills
            .Where(s => s.Id == id)
            .ExecuteDeleteAsync(ct);

        return affected > 0;
    }
}
