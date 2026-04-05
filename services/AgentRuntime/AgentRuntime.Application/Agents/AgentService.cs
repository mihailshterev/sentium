using Microsoft.Extensions.Caching.Hybrid;
using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Dtos;

namespace AgentRuntime.Application.Agents;

public sealed class AgentService(IAgentManager manager, HybridCache cache) : IAgentService
{
    private const string CacheTag = "agents";

    public async ValueTask<AgentResponse> CreateAgentAsync(CreateAgentRequest request, CancellationToken ct = default)
    {
        var response = await manager.CreateAgentAsync(request, ct);

        await cache.RemoveByTagAsync(CacheTag, ct);

        return response;
    }

    public async ValueTask<IReadOnlyList<AgentResponse>> GetAgentsAsync(CancellationToken ct = default)
    {
        var cacheKey = $"{CacheTag}:all";

        return await cache.GetOrCreateAsync(
            cacheKey,
            async token => await manager.GetAgentsAsync(token),
            tags: [CacheTag],
            cancellationToken: ct
        );
    }

    public async ValueTask<AgentResponse> GetAgentByIdAsync(Guid agentId, CancellationToken ct = default)
    {
        var cacheKey = $"{CacheTag}:{agentId}";

        return await cache.GetOrCreateAsync(
            cacheKey,
            async token => await manager.GetAgentByIdAsync(agentId, token),
            tags: [CacheTag],
            cancellationToken: ct
        );
    }

    public async ValueTask UpdateAgentAsync(Guid agentId, UpdateAgentRequest request, CancellationToken ct = default)
    {
        await manager.UpdateAgentAsync(agentId, request, ct);
        await cache.RemoveByTagAsync(CacheTag, ct);
    }

    public async ValueTask DeleteAgentAsync(Guid agentId, CancellationToken ct = default)
    {
        await manager.DeleteAgentAsync(agentId, ct);
        await cache.RemoveByTagAsync(CacheTag, ct);
    }
}
