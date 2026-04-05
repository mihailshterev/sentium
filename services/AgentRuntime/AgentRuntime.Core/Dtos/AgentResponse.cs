namespace AgentRuntime.Core.Dtos;

public sealed record AgentResponse(
    Guid Id,
    string Name,
    string Description,
    string Model,
    DateTime CreatedAt,
    DateTime UpdatedAt);
