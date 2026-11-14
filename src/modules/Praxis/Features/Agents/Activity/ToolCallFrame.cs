namespace Sentium.Praxis.Features.Agents.Activity;

/// <summary>
/// Payload of a <c>tool_call</c> frame.
/// </summary>
internal sealed record ToolCallFrame(string CallId, string Name, string? Arguments);
