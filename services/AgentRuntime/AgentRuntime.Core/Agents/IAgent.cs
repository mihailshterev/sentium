namespace AgentRuntime.Core.Agents;

/// <summary>
/// Defines the core contract for an autonomous agent within the runtime.
/// Represents a specialized persona with a specific identity and behavioral guidelines.
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Gets the unique identifier or display name of the agent.
    /// This is often used for keyed service resolution and logging.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the system instructions or "persona prompt" that defines how the agent
    /// should behave, its constraints, and its specific domain expertise.
    /// </summary>
    string Instructions { get; }
}
