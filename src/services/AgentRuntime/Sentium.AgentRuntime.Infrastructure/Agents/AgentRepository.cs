using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.Entities;
using Sentium.AgentRuntime.Infrastructure.Data;
using Sentium.AgentRuntime.Infrastructure.Projections;
using Microsoft.EntityFrameworkCore;

namespace Sentium.AgentRuntime.Infrastructure.Agents;

public sealed class AgentRepository(AgentRuntimeDbContext context) : IAgentRepository
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

        return await GetAgentByIdAsync(agent.Id, ct) ?? throw new InvalidOperationException($"Agent {agent.Id} could not be loaded immediately after creation.");
    }

    public async Task<IReadOnlyList<AgentResponse>> GetAgentsAsync(CancellationToken ct = default)
    {
        return await context.Agents
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Select(AgentProjections.ToResponse())
            .ToListAsync(ct);
    }

    public async Task<AgentResponse?> GetAgentByNameAsync(string name, CancellationToken ct = default)
    {
        return await context.Agents
            .AsNoTracking()
            .Where(a => a.Name.ToLower() == name.ToLower())
            .Select(AgentProjections.ToResponse())
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default)
    {
        var normalized = name?.ToLower();
        return context.Agents.AnyAsync(a => a.Name.ToLower() == normalized && (excludeId == null || a.Id != excludeId.Value), ct);
    }

    public async Task<AgentResponse?> GetAgentByIdAsync(Guid agentId, CancellationToken ct = default)
    {
        return await context.Agents
            .AsNoTracking()
            .Where(a => a.Id == agentId)
            .Select(AgentProjections.ToResponse())
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> UpdateAgentAsync(Guid agentId, UpdateAgentRequest request, CancellationToken ct = default)
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

        return affectedRows > 0;
    }

    public async Task<bool> DeleteAgentAsync(Guid agentId, CancellationToken ct = default)
    {
        var affectedRows = await context.Agents
            .Where(a => a.Id == agentId)
            .ExecuteDeleteAsync(ct);

        return affectedRows > 0;
    }

    public Task<int> ResetAgentsModelAsync(string modelName, string defaultModel, CancellationToken ct = default)
    {
        return context.Agents
            .Where(a => a.Model == modelName)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Model, defaultModel)
                .SetProperty(a => a.UpdatedAt, DateTime.UtcNow),
            ct);
    }
}
