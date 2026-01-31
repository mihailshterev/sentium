namespace AgentRuntime.Core.Agents;

public interface IAgentRegistry
{
    Type? GetAgentType(string name);
    IEnumerable<string> GetRegisteredNames();
}
