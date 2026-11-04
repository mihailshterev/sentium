using System.Text;
using Sentium.Praxis.Extensions;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Workflows;
using Sentium.Praxis.Features.Orchestration;
using Microsoft.Agents.AI;

namespace Sentium.Praxis.Common.Helpers;

internal static class AgentTurnStreamer
{
    public static async Task<string> RunAsync(
        AIAgent agent,
        string input,
        AgentSession session,
        WorkflowTrigger trigger,
        string author,
        IAgentStreamPublisher stream,
        StreamLogAccumulator streamLog,
        CancellationToken ct)
    {
        var output = new StringBuilder();

        await foreach (var update in agent.RunStreamingAsync(input, session, cancellationToken: ct))
        {
            AgentStreamEmitter.EmitReasoningAndTools(stream, streamLog, trigger.StreamId, author, update.Contents);

            if (!string.IsNullOrEmpty(update.Text))
            {
                output.Append(update.Text);
                stream.Emit(trigger.StreamId, author, update.Text);
                streamLog.Add(author, update.Text, AgentUpdateTypes.Message);
            }
        }

        return output.ToString();
    }
}
