using Sentium.AgentRuntime.Core.Entities;
using Sentium.AgentRuntime.Core.Skills;

namespace Sentium.AgentRuntime.Infrastructure.Skills;

public sealed class AgentSkillService(IAgentSkillRepository repository) : IAgentSkillService
{
    public async Task<IReadOnlyList<AgentSkillDto>> GetAllAsync(CancellationToken ct = default)
    {
        var skills = await repository.GetAllAsync(ct);
        return skills.Select(ToDto).ToList();
    }

    public async Task<AgentSkillDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var skill = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"Skill with ID {id} was not found.");
        return ToDto(skill);
    }

    public async Task<AgentSkillDto> CreateAsync(CreateAgentSkillRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = await repository.GetByNameAsync(request.Name, ct);
        if (existing is not null)
        {
            throw new InvalidOperationException($"A skill named '{request.Name}' already exists.");
        }

        var skill = new AgentSkill
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Instructions = request.Instructions,
            SkillType = request.SkillType,
            FileName = request.FileName,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await repository.AddAsync(skill, ct);
        return ToDto(skill);
    }

    public async Task UpdateAsync(Guid id, UpdateAgentSkillRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var skill = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"Skill with ID {id} was not found.");

        skill.Name = request.Name;
        skill.Description = request.Description;
        skill.Instructions = request.Instructions;
        skill.UpdatedAt = DateTimeOffset.UtcNow;

        await repository.UpdateAsync(skill, ct);
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
        => repository.DeleteAsync(id, ct);

    private static AgentSkillDto ToDto(AgentSkill s) => new(
        s.Id,
        s.Name,
        s.Description,
        s.Instructions,
        s.SkillType,
        s.FileName,
        s.CreatedAt,
        s.UpdatedAt);
}
