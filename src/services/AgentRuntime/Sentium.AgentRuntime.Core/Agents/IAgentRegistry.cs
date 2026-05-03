namespace Sentium.AgentRuntime.Core.Agents;

public interface IAgentRegistry
{
    Type? GetAgentType(string name);
    string GetInstructions(string name);
    IEnumerable<string> GetRegisteredNames();
}
