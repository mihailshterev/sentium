using Sentium.Praxis.Entities;
using Sentium.Infrastructure.Caching;
using Sentium.Core.Results;

namespace Sentium.Praxis.Features.Skills;

internal sealed class AgentSkillService(
    IAgentSkillRepository repository,
    IScopedCache cache) : IAgentSkillService
{
    private const string CacheTag = "skills";

    public async Task<IReadOnlyList<AgentSkillDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            $"{CacheTag}:all",
            LoadAllAsync,
            CacheTag,
            ct);
    }

    public async Task<PagedResponse<AgentSkillDto>> GetPagedAsync(AgentSkillType? skillType, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = new PaginationQuery { Page = page, PageSize = pageSize }.Normalize();

        return await cache.GetOrCreateAsync(
            $"{CacheTag}:page:{skillType?.ToString() ?? "all"}:{page}:{pageSize}",
            token => LoadPageAsync(skillType, page, pageSize, token),
            CacheTag,
            ct);
    }

    public async Task<AgentSkillDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            $"{CacheTag}:{id}",
            token => LoadByIdAsync(id, token),
            CacheTag,
            ct);
    }

    public async Task<Result<AgentSkillDto>> CreateAsync(CreateAgentSkillRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = await repository.GetByNameAsync(request.Name, ct);
        if (existing is not null)
        {
            return Result<AgentSkillDto>.Conflict($"A skill named '{request.Name}' already exists.");
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
        await cache.InvalidateTagAsync(CacheTag, ct);
        return Result<AgentSkillDto>.Success(ToDto(skill));
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateAgentSkillRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var skill = await repository.GetByIdAsync(id, ct);
        if (skill is null)
        {
            return false;
        }

        skill.Name = request.Name;
        skill.Description = request.Description;
        skill.Instructions = request.Instructions;
        skill.UpdatedAt = DateTimeOffset.UtcNow;

        await repository.UpdateAsync(skill, ct);
        await cache.InvalidateTagAsync(CacheTag, ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var deleted = await repository.DeleteAsync(id, ct);
        if (deleted)
        {
            await cache.InvalidateTagAsync(CacheTag, ct);
        }

        return deleted;
    }

    private async ValueTask<IReadOnlyList<AgentSkillDto>> LoadAllAsync(CancellationToken ct)
    {
        var skills = await repository.GetAllAsync(ct);
        return skills.Select(ToDto).ToList();
    }

    private async ValueTask<PagedResponse<AgentSkillDto>> LoadPageAsync(AgentSkillType? skillType, int page, int pageSize, CancellationToken ct)
    {
        var (skills, total) = await repository.GetPagedAsync(skillType, page, pageSize, ct);
        return PagedResponse<AgentSkillDto>.Create([.. skills.Select(ToDto)], total, page, pageSize);
    }

    private async ValueTask<AgentSkillDto?> LoadByIdAsync(Guid id, CancellationToken ct)
    {
        var skill = await repository.GetByIdAsync(id, ct);
        return skill is null ? null : ToDto(skill);
    }

    private static AgentSkillDto ToDto(AgentSkill s)
    {
        return new(
            s.Id,
            s.Name,
            s.Description,
            s.Instructions,
            s.SkillType,
            s.FileName,
            s.CreatedAt,
            s.UpdatedAt);
    }
}
