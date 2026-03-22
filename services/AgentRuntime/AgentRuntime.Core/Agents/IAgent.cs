namespace AgentRuntime.Core.Agents;

public interface IAgent
{
    string Name { get; }
    string Instructions { get; }
}
