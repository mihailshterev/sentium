using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Dtos;
using AgentRuntime.Core.Entities;
using AgentRuntime.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentRuntime.Infrastructure.Agents;

public sealed class AgentManager(AgentRuntimeDbContext db) : IAgentManager
{
    public async Task<AgentResponse> CreateAgentAsync(CreateAgentRequest request, CancellationToken ct = default)
    {
        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Agents.Add(agent);
        await db.SaveChangesAsync(ct);

        return new AgentResponse(agent.Id, agent.Name, agent.Description, agent.CreatedAt, agent.UpdatedAt);
    }

    public async Task<IReadOnlyList<AgentResponse>> GetAgentsAsync(CancellationToken ct = default)
    {
        return await db.Agents
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AgentResponse(a.Id, a.Name, a.Description, a.CreatedAt, a.UpdatedAt))
            .ToListAsync(ct);
    }
}
