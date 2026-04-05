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
            Model = request.Model,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Agents.Add(agent);
        await context.SaveChangesAsync(ct);

        return new AgentResponse(agent.Id, agent.Name, agent.Description, agent.Model, agent.CreatedAt, agent.UpdatedAt);
    }

    public async Task<IReadOnlyList<AgentResponse>> GetAgentsAsync(CancellationToken ct = default)
    {
        return await context.Agents
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AgentResponse(a.Id, a.Name, a.Description, a.Model, a.CreatedAt, a.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<AgentResponse> GetAgentByIdAsync(Guid agentId, CancellationToken ct = default)
    {
        var response = await context.Agents
            .AsNoTracking()
            .Where(a => a.Id == agentId)
            .Select(a => new AgentResponse(a.Id, a.Name, a.Description, a.Model, a.CreatedAt, a.UpdatedAt))
            .FirstOrDefaultAsync(ct);

        return response ?? throw new KeyNotFoundException($"Agent with ID {agentId} not found.");
    }

    public async Task UpdateAgentAsync(Guid agentId, UpdateAgentRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var affectedRows = await context.Agents
            .Where(a => a.Id == agentId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.Name, request.Name)
                .SetProperty(a => a.Description, request.Description)
                .SetProperty(a => a.Model, request.Model)
                .SetProperty(a => a.UpdatedAt, DateTime.UtcNow),
            ct);

        if (affectedRows == 0)
        {
            throw new KeyNotFoundException($"Agent with ID {agentId} not found.");
        }
    }

    public async Task DeleteAgentAsync(Guid agentId, CancellationToken ct = default)
    {
        var affectedRows = await context.Agents
            .Where(a => a.Id == agentId)
            .ExecuteDeleteAsync(ct);

        if (affectedRows == 0)
        {
            throw new KeyNotFoundException($"Agent with ID {agentId} not found.");
        }
    }
}
