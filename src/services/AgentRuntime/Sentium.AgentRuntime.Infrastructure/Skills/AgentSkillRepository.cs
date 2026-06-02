using Sentium.AgentRuntime.Core.Entities;
using Sentium.AgentRuntime.Core.Skills;
using Sentium.AgentRuntime.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Sentium.AgentRuntime.Infrastructure.Skills;

public sealed class AgentSkillRepository(AgentRuntimeDbContext context) : IAgentSkillRepository
{
    public async Task<IReadOnlyList<AgentSkill>> GetAllAsync(CancellationToken ct = default)
        => await context.AgentSkills
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

    public async Task<AgentSkill?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.AgentSkills.FindAsync([id], ct);

    public async Task<AgentSkill?> GetByNameAsync(string name, CancellationToken ct = default)
        => await context.AgentSkills
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == name, ct);

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
