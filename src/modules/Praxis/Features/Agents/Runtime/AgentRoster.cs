
namespace Sentium.Praxis.Features.Agents.Runtime;

/// <summary>
/// Custom agents are looked up for the explicit user, never through the ambient query filter: a Sovereign (and a
/// system-scoped agent run) bypasses that filter and would otherwise see every member's agents.
/// </summary>
internal sealed class AgentRoster(IEnumerable<IAgent> nativeAgents, IAgentRepository agentRepository) : IAgentRoster
{
    public async Task<IReadOnlyList<AgentRosterEntry>> GetAsync(Guid? userId, CancellationToken ct = default)
    {
        var builtIn = BuiltIn().ToList();
        var custom = await agentRepository.GetAgentsForUserAsync(userId, ct);

        return
        [
            .. builtIn,
            .. custom
                .Where(a => !builtIn.Any(b => string.Equals(b.Name, a.Name, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .Select(a => new AgentRosterEntry(a.Name, a.Description, AgentKind.Custom, string.IsNullOrWhiteSpace(a.Model) ? null : a.Model))
        ];
    }

    public async Task<AgentRosterEntry?> FindAsync(string name, Guid? userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Trim();
        var builtIn = BuiltIn().FirstOrDefault(a => string.Equals(a.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        if (builtIn is not null || AgentRole.IsControl(trimmed))
        {
            return builtIn;
        }

        var custom = await agentRepository.GetAgentByNameForUserAsync(trimmed, userId, ct);
        return custom is null
            ? null
            : new AgentRosterEntry(custom.Name, custom.Description, AgentKind.Custom, string.IsNullOrWhiteSpace(custom.Model) ? null : custom.Model);
    }

    private IEnumerable<AgentRosterEntry> BuiltIn()
    {
        return nativeAgents
            .Where(a => !AgentRole.IsControl(a.Name))
            .DistinctBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a.Name == AgentRole.GeneralAssistant ? 0 : 1)
            .ThenBy(a => a.Name, StringComparer.Ordinal)
            .Select(a => new AgentRosterEntry(a.Name, a.Description, AgentKind.BuiltIn, Model: null));
    }
}
