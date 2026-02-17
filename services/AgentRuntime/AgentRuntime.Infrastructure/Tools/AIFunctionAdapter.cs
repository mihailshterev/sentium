using AgentRuntime.Core.Tools;
using Microsoft.Extensions.AI;

namespace AgentRuntime.Infrastructure.Tools;

public static class AIFunctionAdapter
{
    public static AITool ToAIFunction(IAgentTool tool, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tool);

        async Task<string> Invoke(string input) => await tool.ExecuteAsync(input, ct);

        return AIFunctionFactory.Create(
            method: Invoke,
            name: tool.Name,
            description: tool.Description
        );
    }
}
