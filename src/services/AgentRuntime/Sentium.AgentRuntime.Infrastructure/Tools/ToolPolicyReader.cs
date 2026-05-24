using System.Collections.Concurrent;
using System.Reflection;
using Sentium.AgentRuntime.Core.Tools;
using Sentium.AgentRuntime.Core.Tools.Attributes;

namespace Sentium.AgentRuntime.Infrastructure.Tools;

public static class ToolPolicyReader
{
    private static readonly ConcurrentDictionary<Type, AgentToolPolicyAttribute> PolicyCache = new();
    private static readonly AgentToolPolicyAttribute DefaultPolicy = new();

    public static AgentToolPolicyAttribute GetPolicy(IAgentTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        var toolType = tool.GetType();

        return PolicyCache.GetOrAdd(toolType, type =>
        {
            return type.GetCustomAttribute<AgentToolPolicyAttribute>(inherit: true) ?? DefaultPolicy;
        });
    }
}
