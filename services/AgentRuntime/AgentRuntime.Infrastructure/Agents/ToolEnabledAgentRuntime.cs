using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Tools;
using AgentRuntime.Infrastructure.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentRuntime.Infrastructure.Agents;

public sealed class ToolEnabledAgentRuntime : IAgentRuntime
{
    private readonly IChatClient Client;
    private readonly IReadOnlyList<IAgentTool> Tools;

    public ToolEnabledAgentRuntime(
        IChatClient client,
        IToolRegistry toolRegistry)
    {
        Client = client;
        Tools = toolRegistry.GetTools();
    }

    public async Task<string> RunAsync(string systemPrompt, string input, CancellationToken ct)
    {
        var agent = new ChatClientAgent(
            Client,
            instructions: systemPrompt,
            tools: Tools.Select(t => AIFunctionAdapter.ToAIFunction(t, ct)).ToList());

        var thread = await agent.GetNewThreadAsync(ct);

        var result = await agent.RunAsync(input, thread, cancellationToken: ct);
        return result.Messages.Last().Text;
    }
}
