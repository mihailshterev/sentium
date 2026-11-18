using System.Text.Json;
using Sentium.Probatio.Entities;

namespace Sentium.Probatio.Features.Evaluations;

/// <summary>
/// Answers every tool call of one repetition with the case's canned result for that tool, or a neutral result,
/// and records the call (ADR 0003).
/// </summary>
internal sealed class CannedToolStub(IReadOnlyList<CannedToolResult> cannedResults)
{
    public const string NeutralResult = "Done. The tool returned no data.";

    private readonly Lock gate = new();
    private readonly List<RecordedToolCall> calls = [];

    public IReadOnlyList<RecordedToolCall> Calls
    {
        get
        {
            lock (gate)
            {
                return [.. calls];
            }
        }
    }

    public ValueTask<string> AnswerAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var result = cannedResults.FirstOrDefault(r => string.Equals(r.ToolName, toolName, StringComparison.OrdinalIgnoreCase))?.Result
            ?? NeutralResult;

        lock (gate)
        {
            calls.Add(new RecordedToolCall(toolName, JsonSerializer.SerializeToElement(arguments), result));
        }

        return ValueTask.FromResult(result);
    }
}
