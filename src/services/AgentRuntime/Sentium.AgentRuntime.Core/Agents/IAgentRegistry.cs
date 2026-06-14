namespace Sentium.AgentRuntime.Core.Agents;

/// <summary>
/// Lookup of statically registered agents by name: their CLR type and system instructions.
/// </summary>
public interface IAgentRegistry
{
    /// <summary>
    /// Returns the agent's CLR type, or <c>null</c> if no agent is registered under <paramref name="name"/>.
    /// </summary>
    Type? GetAgentType(string name);
    string GetInstructions(string name);
    IEnumerable<string> GetRegisteredNames();
}
