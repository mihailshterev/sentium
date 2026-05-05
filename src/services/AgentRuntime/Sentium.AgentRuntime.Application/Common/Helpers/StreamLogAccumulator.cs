using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Dtos;

namespace Sentium.AgentRuntime.Application.Common.Helpers;

/// <summary>
/// Accumulates streaming log entries, merging consecutive tokens of the same
/// author + type into a single entry before persisting.
/// </summary>
public sealed class StreamLogAccumulator
{
    private readonly List<WorkflowLogEntry> _entries = [];

    public IReadOnlyList<WorkflowLogEntry> Entries => _entries;

    public void Add(string author, string text, string type)
    {
        if ((type == AgentUpdateTypes.Message || type == AgentUpdateTypes.Thought)
            && _entries.Count > 0
            && _entries[^1].Author == author
            && _entries[^1].Type == type)
        {
            _entries[^1] = _entries[^1] with { Text = _entries[^1].Text + text };
        }
        else
        {
            _entries.Add(new WorkflowLogEntry(author, text, type));
        }
    }
}
