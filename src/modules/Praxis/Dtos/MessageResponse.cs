namespace Sentium.Praxis.Dtos;

public sealed record MessageResponse(
    Guid Id,
    string Role,
    string Content,
    DateTime Timestamp,
    string? EnhancedPrompt = null,
    string? Thought = null,
    IReadOnlyList<string>? ToolCalls = null);
