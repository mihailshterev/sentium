using Microsoft.Extensions.AI;
using Sentium.Praxis.Common.Streaming;

namespace Sentium.Praxis.Features.Agents.Activity;

/// <summary>
/// Turns the contents of streamed agent updates into ordered activity events - reasoning deltas, tool calls (with
/// arguments) and tool results (with outcome and duration) - for the assistant SSE stream and the workflow timeline.
/// One instance per run: it times each tool from its call to its result.
/// </summary>
/// <remarks>
/// Function invocation runs between the update carrying a <see cref="FunctionCallContent"/> and the one carrying its
/// <see cref="FunctionResultContent"/>, so the gap is the tool's execution time. Answer text is not translated here
/// (read <c>update.Text</c>).
/// </remarks>
public sealed class AgentActivityTranslator(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider time = timeProvider ?? TimeProvider.System;
    private readonly Dictionary<string, long> started = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> durations = new(StringComparer.Ordinal);

    /// <summary>
    /// Measured tool durations in milliseconds, by call id.
    /// </summary>
    public IReadOnlyDictionary<string, long> Durations => durations;

    public IEnumerable<AgentActivityEvent> Translate(IEnumerable<AIContent> contents)
    {
        ArgumentNullException.ThrowIfNull(contents);

        foreach (var content in contents)
        {
            switch (content)
            {
                case TextReasoningContent reasoning when !string.IsNullOrEmpty(reasoning.Text):
                    yield return new AgentActivityEvent(AgentUpdateTypes.Thought, reasoning.Text);
                    break;

                case FunctionCallContent call when !string.IsNullOrEmpty(call.CallId):
                    if (started.TryAdd(call.CallId, time.GetTimestamp()))
                    {
                        yield return new AgentActivityEvent(
                            AgentUpdateTypes.ToolCall,
                            AgentActivityFormat.Serialize(new ToolCallFrame(call.CallId, call.Name, AgentActivityFormat.Arguments(call.Arguments))));
                    }

                    break;

                case FunctionResultContent result when !string.IsNullOrEmpty(result.CallId):
                    long? duration = null;
                    if (started.TryGetValue(result.CallId, out var start))
                    {
                        duration = (long)time.GetElapsedTime(start).TotalMilliseconds;
                        durations[result.CallId] = duration.Value;
                    }

                    yield return new AgentActivityEvent(
                        AgentUpdateTypes.ToolResult,
                        AgentActivityFormat.Serialize(new ToolResultFrame(result.CallId, AgentActivityFormat.Status(result), AgentActivityFormat.Result(result), duration)));
                    break;
            }
        }
    }
}
