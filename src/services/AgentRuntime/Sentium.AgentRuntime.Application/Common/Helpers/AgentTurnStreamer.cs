using System.Text;
using Sentium.AgentRuntime.Application.Extensions;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Workflows;
using Sentium.Infrastructure.Messaging;
using Microsoft.Agents.AI;

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
            await AgentStreamEmitter.EmitReasoningAndToolsAsync(nats, streamLog, trigger.StreamId, author, update.Contents, ct);

            if (!string.IsNullOrEmpty(update.Text))
            {
                output.Append(update.Text);
                await nats.StreamAgentUpdateAsync(trigger.StreamId, author, update.Text, ct);
                streamLog.Add(author, update.Text, AgentUpdateTypes.Message);
            }
        }

        return output.ToString();
    }
}
