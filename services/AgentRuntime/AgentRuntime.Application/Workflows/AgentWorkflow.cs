using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI;
using OllamaSharp;
using AgentRuntime.Core.Workflows;

namespace AgentRuntime.Application.Workflows;

public class AgentWorkflow : IAgentWorkflow
{
    public WorkflowType Type => WorkflowType.Predefined;

    // EXAMPLE
    public async Task<IReadOnlyList<string>> ExecuteAsync(string input, CancellationToken ct)
    {
        var uri = new Uri("http://localhost:11434");
        using (var ollama = new OllamaApiClient(uri))
        {
            ollama.SelectedModel = "gemma3:1b";

            var summaryAgent = new ChatClientAgent(
                ollama,
                new ChatClientAgentOptions
                {
                    Name = "You are a helpful assistant that summarizes text concisely.",
                    Description = "Analyze the network traffic and provide a concise summary of any suspicious activities.",
                });

            var sentinelAgent = new ChatClientAgent(
                ollama,
                new ChatClientAgentOptions
                {
                    Name = "You are a security analyst specialized in network traffic analysis.",
                    Description = "Evaluate the summarized network traffic and determine if there are any security threats or anomalies.",
                });

            var workflowAgent = AgentWorkflowBuilder.BuildSequential(summaryAgent, sentinelAgent).AsAgent();

            var result = await workflowAgent.RunAsync("Analyze the following network traffic data: ", cancellationToken: ct);
            return result.Messages.Select(m => m.Text).ToList();
        }
    }

    public Task<WorkflowResult> ExecuteAsync(WorkflowTrigger trigger, CancellationToken ct) => throw new NotImplementedException();
}
