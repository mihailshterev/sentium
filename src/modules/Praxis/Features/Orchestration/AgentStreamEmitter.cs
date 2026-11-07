using Sentium.Praxis.Extensions;
using Sentium.Praxis.Features.Agents;
using Microsoft.Extensions.AI;

namespace Sentium.Praxis.Features.Orchestration;

internal static class AgentStreamEmitter
{
    public static void EmitReasoningAndTools(
        IAgentStreamPublisher stream,
        StreamLogAccumulator streamLog,
        string streamId,
        string author,
        IEnumerable<AIContent> contents)
    {
        if (contents.OfType<TextReasoningContent>().FirstOrDefault() is { } reasoning && !string.IsNullOrEmpty(reasoning.Text))
        {
            stream.Emit(streamId, author, reasoning.Text, AgentUpdateTypes.Thought);
            streamLog.Add(author, reasoning.Text, AgentUpdateTypes.Thought);
        }

        foreach (var call in contents.OfType<FunctionCallContent>())
        {
            var toolLabel = $"Calling {call.Name}...";
            stream.Emit(streamId, author, toolLabel, AgentUpdateTypes.Tool);
            streamLog.Add(author, toolLabel, AgentUpdateTypes.Tool);
        }
    }
}
