namespace AgentRuntime.Core.Dtos;

public sealed record MessageResponse(
    Guid Id,
    string Role,
    string Content,
    DateTime Timestamp,
    string? Thought = null,
    IReadOnlyList<string>? ToolCalls = null);
