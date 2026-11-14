namespace Sentium.Praxis.Features.Agents.Activity;

/// <summary>
/// Payload of a <c>tool_result</c> frame.
/// </summary>
internal sealed record ToolResultFrame(string CallId, string Status, string? Result, long? DurationMs);
