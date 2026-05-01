using AgentRuntime.Core.Agents;

namespace AgentRuntime.Application.Agents.Native;

/// <summary>
/// A versatile native agent designed to handle broad user requests.
/// Acts as the fallback or primary point of contact for general inquiries
/// and coordinates tool usage for multi-step tasks.
/// </summary>
public sealed class GeneralAssistant : IAgent
{
    /// <inheritdoc />
    /// <value>"GeneralAssistant"</value>
    public string Name => "GeneralAssistant";

    /// <inheritdoc />
    /// <remarks>
    /// This agent is configured with a broad persona, allowing it to adapt to various
    /// topics while maintaining the capability to invoke registered <see cref="IAgentTool"/>
    /// implementations when domain-specific data is required.
    /// </remarks>
    public string Instructions => "You are a helpful assistant. Use tools when necessary to answer user queries.";
}
