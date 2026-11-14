using Sentium.Core.Results;
using Sentium.Praxis.Features.Agents.Runtime;
using Sentium.Praxis.Features.Agents.ToolOverrides;

namespace Sentium.Praxis.Features.Agents;

internal sealed class BuiltInAgentService(IEnumerable<IAgent> nativeAgents, IAgentToolOverrideRepository overrides) : IBuiltInAgentService
{
    public async Task<IReadOnlyList<BuiltInAgentResponse>> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var byName = (await overrides.GetAllAsync(userId, ct)).ToDictionary(o => o.AgentName, StringComparer.Ordinal);

        return [.. BuiltIn().Select(agent => ToResponse(agent, byName.GetValueOrDefault(agent.Name)))];
    }

    public async Task<Result<BuiltInAgentResponse>> SetToolsAsync(string agentName, Guid userId, IReadOnlyList<string>? tools, CancellationToken ct = default)
    {
        if (Find(agentName) is not { } agent)
        {
            return Result<BuiltInAgentResponse>.NotFound();
        }

        await overrides.UpsertAsync(agent.Name, userId, tools, ct);
        return Result<BuiltInAgentResponse>.Success(ToResponse(agent, await overrides.GetAsync(agent.Name, userId, ct)));
    }

    public async Task<Result<BuiltInAgentResponse>> ResetToolsAsync(string agentName, Guid userId, CancellationToken ct = default)
    {
        if (Find(agentName) is not { } agent)
        {
            return Result<BuiltInAgentResponse>.NotFound();
        }

        await overrides.DeleteAsync(agent.Name, userId, ct);
        return Result<BuiltInAgentResponse>.Success(ToResponse(agent, null));
    }

    public async Task<IAgent> ApplyOverrideAsync(IAgent agent, Guid? userId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(agent);

        if (userId is not { } uid || AgentRole.IsControl(agent.Name))
        {
            return agent;
        }

        return await overrides.GetAsync(agent.Name, uid, ct) is { } custom ? new ConfiguredAgent(agent, custom.Tools) : agent;
    }

    private IEnumerable<IAgent> BuiltIn()
    {
        return nativeAgents
            .Where(a => !AgentRole.IsControl(a.Name))
            .DistinctBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a.Name == AgentRole.GeneralAssistant ? 0 : 1)
            .ThenBy(a => a.Name, StringComparer.Ordinal);
    }

    private IAgent? Find(string name) =>
        BuiltIn().FirstOrDefault(a => string.Equals(a.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static BuiltInAgentResponse ToResponse(IAgent agent, AgentToolOverrideRecord? custom) =>
        new(agent.Name, agent.Description, agent.Tools, custom is null ? agent.Tools : custom.Tools, custom is not null);
}
