using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Common.Streaming;
using Sentium.Praxis.Features.Agents.Activity;

namespace Sentium.Praxis.Features.Agents;

/// <summary>
/// Streams one agent run to an <see cref="IAgentUpdateSink"/>: for each update, its activity (reasoning, tool calls,
/// tool results) and then its answer text. Keeps what callers need after the run, or after it was cancelled: the full
/// answer and the raw contents. One instance per run, because <see cref="Activity"/> times tools across updates.
/// </summary>
public sealed class AgentRunStream(IAgentUpdateSink sink)
{
    private readonly StringBuilder text = new();
    private readonly List<AIContent> contents = [];

    public AgentActivityTranslator Activity { get; } = new();

    public string Text => text.ToString();

    public IReadOnlyList<AIContent> Contents => contents;

    public async Task RunAsync(IAsyncEnumerable<AgentResponseUpdate> updates, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(updates);

        await foreach (var update in updates.WithCancellation(ct))
        {
            contents.AddRange(update.Contents);

            foreach (var activityEvent in Activity.Translate(update.Contents))
            {
                await sink.WriteUpdateAsync(activityEvent.Type, activityEvent.Payload, ct);
            }

            if (!string.IsNullOrEmpty(update.Text))
            {
                text.Append(update.Text);
                await sink.WriteUpdateAsync(AgentUpdateTypes.Message, update.Text, ct);
            }
        }
    }
}
