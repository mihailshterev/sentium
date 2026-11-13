using Microsoft.Extensions.AI;
using Sentium.Praxis.Common.Streaming;
using Sentium.Praxis.Features.Agents.Activity;

namespace Sentium.Praxis.Features.Orchestration;

internal static class AgentStreamEmitter
{
    /// <summary>
    /// Publishes an update's reasoning, tool calls and tool results to the run's live stream and its persisted log, in
    /// order, under <paramref name="author"/>. Tool frames carry their JSON payload as the entry text.
    /// </summary>
    public static void EmitActivity(
        IAgentStreamPublisher stream,
        StreamLogAccumulator streamLog,
        AgentActivityTranslator activity,
        string streamId,
        string author,
        IEnumerable<AIContent> contents)
    {
        foreach (var activityEvent in activity.Translate(contents))
        {
            stream.Emit(streamId, author, activityEvent.Payload, activityEvent.Type);
            streamLog.Add(author, activityEvent.Payload, activityEvent.Type);
        }
    }
}
