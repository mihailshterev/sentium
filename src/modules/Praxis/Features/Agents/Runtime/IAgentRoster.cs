
namespace Sentium.Praxis.Features.Agents.Runtime;

/// <summary>
/// The agents a user can work with directly: the built-in specialists (not the control roles that steer squads)
/// and the user's own custom agents.
/// </summary>
public interface IAgentRoster
{
    Task<IReadOnlyList<AgentRosterEntry>> GetAsync(Guid? userId, CancellationToken ct = default);

    /// <summary>
    /// The roster entry named <paramref name="name"/> (case-insensitive), with its canonical spelling, or
    /// <see langword="null"/> when the user has no such agent.
    /// </summary>
    Task<AgentRosterEntry?> FindAsync(string name, Guid? userId, CancellationToken ct = default);
}
