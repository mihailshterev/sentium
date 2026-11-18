using System.Text.Json;

namespace Sentium.Probatio.Entities;

/// <summary>
/// A tool the agent should call. <paramref name="Arguments"/> is the subset of arguments that must match;
/// <see langword="null"/> means the arguments are not checked.
/// </summary>
internal sealed record ExpectedToolCall(string ToolName, IReadOnlyDictionary<string, JsonElement>? Arguments);
