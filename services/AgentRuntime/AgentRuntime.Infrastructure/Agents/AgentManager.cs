using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Dtos;
using AgentRuntime.Core.Entities;
using AgentRuntime.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentRuntime.Infrastructure.Agents;

public sealed class AgentManager(AgentRuntimeDbContext context) : IAgentManager
{
    public async Task<AgentResponse> CreateAgentAsync(CreateAgentRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Agents.Add(agent);
        await context.SaveChangesAsync(ct);

        return new AgentResponse(agent.Id, agent.Name, agent.Description, agent.CreatedAt, agent.UpdatedAt);
    }

    public async Task<IReadOnlyList<AgentResponse>> GetAgentsAsync(CancellationToken ct = default)
    {
        return await context.Agents
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AgentResponse(a.Id, a.Name, a.Description, a.CreatedAt, a.UpdatedAt))
            .ToListAsync(ct);
    }
}
