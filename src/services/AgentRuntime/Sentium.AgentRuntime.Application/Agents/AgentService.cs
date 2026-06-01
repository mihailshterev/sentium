using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.Infrastructure.Caching;

namespace Sentium.AgentRuntime.Application.Agents;

public sealed class AgentService(IAgentRepository repository, IScopedCache cache) : IAgentService
{
    private const string CacheTag = "agents";

    public async ValueTask<AgentResponse> CreateAgentAsync(CreateAgentRequest request, CancellationToken ct = default)
    {
        var response = await repository.CreateAgentAsync(request, ct);
        await cache.InvalidateTagAsync(CacheTag, ct);
        return response;
    }

    public async ValueTask<IReadOnlyList<AgentResponse>> GetAgentsAsync(CancellationToken ct = default)
        => await cache.GetOrCreateAsync(
            $"{CacheTag}:all",
            async token => await repository.GetAgentsAsync(token),
            CacheTag,
            ct);

    public async ValueTask<AgentResponse> GetAgentByIdAsync(Guid agentId, CancellationToken ct = default)
        => await cache.GetOrCreateAsync(
            $"{CacheTag}:{agentId}",
            async token => await repository.GetAgentByIdAsync(agentId, token),
            CacheTag,
            ct);

    public async ValueTask UpdateAgentAsync(Guid agentId, UpdateAgentRequest request, CancellationToken ct = default)
    {
        await repository.UpdateAgentAsync(agentId, request, ct);
        await cache.InvalidateTagAsync(CacheTag, ct);
    }

    public async ValueTask DeleteAgentAsync(Guid agentId, CancellationToken ct = default)
    {
        await repository.DeleteAgentAsync(agentId, ct);
        await cache.InvalidateTagAsync(CacheTag, ct);
    }
}
