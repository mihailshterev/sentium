using AgentRuntime.Core.Tools;

namespace AgentRuntime.Infrastructure.Tools;

public sealed class ToolRegistry : IToolRegistry
{
    private readonly IReadOnlyList<IAgentTool> Tools;

    public ToolRegistry(IEnumerable<IAgentTool> tools)
    {
        Tools = tools.ToList();
    }

    public IReadOnlyList<IAgentTool> GetTools() => Tools;
}
