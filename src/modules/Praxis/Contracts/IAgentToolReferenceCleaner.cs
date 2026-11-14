namespace Sentium.Praxis.Contracts;

/// <summary>
/// Lets a tool source drop tools it no longer offers from the agents that reference them. Implemented by Praxis, which
/// owns the agents' tool lists and their caches.
/// </summary>
public interface IAgentToolReferenceCleaner
{
    /// <summary>
    /// Removes every tool whose name starts with <paramref name="prefix"/> (case-insensitive) from all agents' tool lists
    /// and built-in agent overrides, then evicts the affected agent caches. Returns the number of owners that changed.
    /// </summary>
    Task<int> RemoveToolsByPrefixAsync(string prefix, CancellationToken ct);
}
