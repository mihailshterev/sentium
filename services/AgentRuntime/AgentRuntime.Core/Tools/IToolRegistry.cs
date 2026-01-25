namespace AgentRuntime.Core.Tools;

public interface IToolRegistry
{
    IReadOnlyList<IAgentTool> GetTools();
}
