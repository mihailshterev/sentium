using AgentRuntime.Core.Agents;

namespace AgentRuntime.Infrastructure.Agents;

public sealed class GeneralAssistant : IAgent
{
    public string Name => "GeneralAssistant";
    public string Instructions => "You are a helpful assistant. Use tools when necessary to answer user queries.";
}
