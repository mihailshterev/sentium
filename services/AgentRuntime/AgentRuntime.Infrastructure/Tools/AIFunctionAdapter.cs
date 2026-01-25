using AgentRuntime.Core.Tools;
using Microsoft.Extensions.AI;

namespace AgentRuntime.Infrastructure.Tools;

public static class AIFunctionAdapter
{
    public static AITool ToAIFunction(IAgentTool tool, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tool);

        Task<string> Invoke(string input) => tool.ExecuteAsync(input, ct);

        return AIFunctionFactory.Create(Invoke, name: tool.Name, description: tool.Description);
    }
}
