using System.Text.Json;
using Microsoft.Extensions.AI;
using Sentium.AgentRuntime.Core.Tools;

namespace Sentium.AgentRuntime.Infrastructure.Tools;

public static class AIFunctionAdapter
{
    public static AITool ToAIFunction(IAgentTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        async Task<string> Invoke(AIFunctionArguments arguments, CancellationToken runtimeCt)
        {
            string inputForTool;

            if (arguments.Count == 0)
            {
                inputForTool = string.Empty;
            }
            else if (arguments.Count == 1 && arguments.Values.First() is string singleString)
            {
                inputForTool = singleString;
            }
            else
            {
                inputForTool = JsonSerializer.Serialize(arguments);
            }

            return await tool.ExecuteAsync(inputForTool, runtimeCt);
        }

        return AIFunctionFactory.Create(
            method: Invoke,
            name: tool.Name,
            description: tool.Description
        );
    }
}
