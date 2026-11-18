using System.Text.Json;

namespace Sentium.Probatio.Entities;

/// <summary>
/// A tool call the agent made and the stub result it was given; the tool itself never ran (ADR 0003).
/// </summary>
internal sealed record RecordedToolCall(string ToolName, JsonElement Arguments, string Result);
