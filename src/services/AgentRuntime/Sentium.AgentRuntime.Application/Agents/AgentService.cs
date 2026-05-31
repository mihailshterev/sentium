using Microsoft.Extensions.Caching.Hybrid;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.Infrastructure.Security;

namespace Sentium.AgentRuntime.Application.Agents;

public sealed class AgentService(IAgentManager manager, HybridCache cache, ICurrentUser currentUser) : IAgentService
{
    private const string CacheTag = "agents";
    private string Scope => currentUser.IsSovereign ? "sovereign" : currentUser.UserId?.ToString() ?? "anon";

    public async ValueTask<AgentResponse> CreateAgentAsync(CreateAgentRequest request, CancellationToken ct = default)
    {
        var response = await manager.CreateAgentAsync(request, ct);

        await cache.RemoveByTagAsync(CacheTag, ct);

        return response;
    }

    public async ValueTask<IReadOnlyList<AgentResponse>> GetAgentsAsync(CancellationToken ct = default)
    {
        var cacheKey = $"{CacheTag}:all:{Scope}";

        return await cache.GetOrCreateAsync(
            cacheKey,
            async token => await manager.GetAgentsAsync(token),
            tags: [CacheTag],
            cancellationToken: ct
        );
    }

    public async ValueTask<AgentResponse> GetAgentByIdAsync(Guid agentId, CancellationToken ct = default)
    {
        var cacheKey = $"{CacheTag}:{agentId}:{Scope}";

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
