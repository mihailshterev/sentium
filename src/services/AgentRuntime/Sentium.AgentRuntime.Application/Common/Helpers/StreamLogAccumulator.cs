using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Dtos;
using System.Text;

namespace Sentium.AgentRuntime.Application.Common.Helpers;

/// <summary>
/// Accumulates streaming log entries, merging consecutive tokens of the same
/// author + type into a single entry before persisting.
/// </summary>
public sealed class StreamLogAccumulator
{
    private readonly List<WorkflowLogEntry> _entries = [];

    private readonly StringBuilder _activeBuffer = new();

    public IReadOnlyList<WorkflowLogEntry> Entries
    {
        get
        {
            SyncActiveEntry();
            return _entries;
        }
    }

    public void Add(string author, string text, string type)
    {
        var isMergeable = type == AgentUpdateTypes.Message || type == AgentUpdateTypes.Thought;

        if (isMergeable && _entries.Count > 0 && _entries[^1].Author == author && _entries[^1].Type == type)
        {
            _activeBuffer.Append(text);
        }
        else
        {
            SyncActiveEntry();

            _activeBuffer.Clear();
            _activeBuffer.Append(text);

            _entries.Add(new WorkflowLogEntry(author, text, type));
        }
    }

    private void SyncActiveEntry()
    {
        if (_entries.Count > 0 && _activeBuffer.Length > _entries[^1].Text.Length)
        {
            _entries[^1] = _entries[^1] with { Text = _activeBuffer.ToString() };
        }
    }
}
