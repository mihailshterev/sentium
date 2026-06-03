using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.Infrastructure.Caching;
using Sentium.Shared.Results;

namespace Sentium.AgentRuntime.Application.Agents;

public sealed class AgentService(
    IAgentRepository repository,
    IScopedCache cache,
    IAgentRegistry registry) : IAgentService
{
    private const string CacheTag = "agents";

    public async ValueTask<Result<AgentResponse>> CreateAgentAsync(CreateAgentRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (IsReservedName(request.Name))
        {
            return Result<AgentResponse>.Conflict($"'{request.Name}' is a reserved built-in agent name. Please choose a different name.");
        }

        if (await repository.NameExistsAsync(request.Name, ct: ct))
        {
            return Result<AgentResponse>.Conflict($"An agent named '{request.Name}' already exists.");
        }

        var response = await repository.CreateAgentAsync(request, ct);
        await cache.InvalidateTagAsync(CacheTag, ct);
        return Result<AgentResponse>.Success(response);
    }

    public async ValueTask<IReadOnlyList<AgentResponse>> GetAgentsAsync(CancellationToken ct = default)
        => await cache.GetOrCreateAsync(
            $"{CacheTag}:all",
            async token => await repository.GetAgentsAsync(token),
            CacheTag,
            ct);

    public async ValueTask<AgentResponse?> GetAgentByIdAsync(Guid agentId, CancellationToken ct = default)
        => await cache.GetOrCreateAsync(
            $"{CacheTag}:{agentId}",
            async token => await repository.GetAgentByIdAsync(agentId, token),
            CacheTag,
            ct);

    public async ValueTask<Result<AgentResponse>> UpdateAgentAsync(Guid agentId, UpdateAgentRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (IsReservedName(request.Name))
        {
            return Result<AgentResponse>.Conflict($"'{request.Name}' is a reserved built-in agent name. Please choose a different name.");
        }

        if (await repository.NameExistsAsync(request.Name, excludeId: agentId, ct: ct))
        {
            return Result<AgentResponse>.Conflict($"An agent named '{request.Name}' already exists.");
        }

        var updated = await repository.UpdateAgentAsync(agentId, request, ct);
        if (!updated)
        {
            return Result<AgentResponse>.NotFound();
        }

        await cache.InvalidateTagAsync(CacheTag, ct);

        var response = await repository.GetAgentByIdAsync(agentId, ct);
        return response is null ? Result<AgentResponse>.NotFound() : Result<AgentResponse>.Success(response);
    }

    public async ValueTask<bool> DeleteAgentAsync(Guid agentId, CancellationToken ct = default)
    {
        var deleted = await repository.DeleteAgentAsync(agentId, ct);
        if (deleted)
        {
            await cache.InvalidateTagAsync(CacheTag, ct);
        }

        return deleted;
    }

    private bool IsReservedName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var trimmed = name.Trim();
        return registry.GetRegisteredNames().Any(n => string.Equals(n, trimmed, StringComparison.OrdinalIgnoreCase));
    }
}
