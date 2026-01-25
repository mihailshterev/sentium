using AgentRuntime.Core.Agents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentRuntime.Infrastructure.Agents;

public sealed class OllamaAgentRuntime : IAgentRuntime
{
    private readonly IChatClient AgentClient;

    public OllamaAgentRuntime(IChatClient client)
    {
        AgentClient = client;
    }

    public async Task<string> RunAsync(
        string systemPrompt,
        string input,
        CancellationToken ct)
    {
        var agent = new ChatClientAgent(
            AgentClient,
            new ChatClientAgentOptions
            {
                Description = systemPrompt
            });

        var thread = await agent.GetNewThreadAsync(ct);
        var result = await agent.RunAsync(thread, cancellationToken: ct);
        return result.Messages.Last().Text;
    }
}
