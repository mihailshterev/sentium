using System.Text;
using Sentium.AgentRuntime.Application.Extensions;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Workflows;
using Sentium.Infrastructure.Messaging;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Sentium.AgentRuntime.Application.Common.Helpers;

internal static class AgentTurnStreamer
{
    public static async Task<string> RunAsync(
        AIAgent agent,
        string input,
        AgentSession session,
        WorkflowTrigger trigger,
        string author,
        IEventBus nats,
        StreamLogAccumulator streamLog,
        CancellationToken ct)
    {
        var output = new StringBuilder();

        await foreach (var update in agent.RunStreamingAsync(input, session, cancellationToken: ct))
        {
            if (update.Contents.OfType<TextReasoningContent>().FirstOrDefault() is { } reasoning && !string.IsNullOrEmpty(reasoning.Text))
            {
                await nats.StreamAgentUpdateAsync(trigger.TriggerType, author, reasoning.Text, AgentUpdateTypes.Thought, ct);
                streamLog.Add(author, reasoning.Text, AgentUpdateTypes.Thought);
            }

            foreach (var call in update.Contents.OfType<FunctionCallContent>())
            {
                var toolLabel = $"Calling {call.Name}...";
                await nats.StreamAgentUpdateAsync(trigger.TriggerType, author, toolLabel, AgentUpdateTypes.Tool, ct);
                streamLog.Add(author, toolLabel, AgentUpdateTypes.Tool);
            }

            if (!string.IsNullOrEmpty(update.Text))
            {
                output.Append(update.Text);
                await nats.StreamAgentUpdateAsync(trigger.TriggerType, author, update.Text, ct);
                streamLog.Add(author, update.Text, AgentUpdateTypes.Message);
            }
        }

        return output.ToString();
    }
}
