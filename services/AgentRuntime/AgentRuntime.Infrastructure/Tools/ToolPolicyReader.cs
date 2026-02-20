using AgentRuntime.Core.Tools;
using AgentRuntime.Core.Tools.Attributes;

namespace AgentRuntime.Infrastructure.Tools;

public static class ToolPolicyReader
{
    public static AgentToolPolicyAttribute GetPolicy(IAgentTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return tool.GetType()
                   .GetCustomAttributes(typeof(AgentToolPolicyAttribute), inherit: true)
                   .Cast<AgentToolPolicyAttribute>()
                   .FirstOrDefault() ?? new AgentToolPolicyAttribute();
    }
}
