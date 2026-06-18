using Sentium.AgentRuntime.Application.Extensions;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.Infrastructure.Messaging;
using Microsoft.Extensions.AI;

namespace Sentium.AgentRuntime.Application.Common.Helpers;

internal static class AgentStreamEmitter
{
    public static async Task EmitReasoningAndToolsAsync(
        IEventBus nats,
        StreamLogAccumulator streamLog,
        string streamId,
        string author,
        IEnumerable<AIContent> contents,
        CancellationToken ct)
    {
        if (contents.OfType<TextReasoningContent>().FirstOrDefault() is { } reasoning && !string.IsNullOrEmpty(reasoning.Text))
        {
            await nats.StreamAgentUpdateAsync(streamId, author, reasoning.Text, AgentUpdateTypes.Thought, ct);
            streamLog.Add(author, reasoning.Text, AgentUpdateTypes.Thought);
        }

        foreach (var call in contents.OfType<FunctionCallContent>())
        {
            var toolLabel = $"Calling {call.Name}...";
            await nats.StreamAgentUpdateAsync(streamId, author, toolLabel, AgentUpdateTypes.Tool, ct);
            streamLog.Add(author, toolLabel, AgentUpdateTypes.Tool);
        }
    }
}
