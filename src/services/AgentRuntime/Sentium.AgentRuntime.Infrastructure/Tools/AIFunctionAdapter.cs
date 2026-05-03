using Sentium.AgentRuntime.Core.Tools;
using Microsoft.Extensions.AI;

namespace Sentium.AgentRuntime.Infrastructure.Tools;

public static class AIFunctionAdapter
{
    public static AITool ToAIFunction(IAgentTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        async Task<string> Invoke(string input, CancellationToken runtimeCt) => await tool.ExecuteAsync(input, runtimeCt);

        return AIFunctionFactory.Create(
            method: Invoke,
            name: tool.Name,
            description: tool.Description
        );
    }
}
