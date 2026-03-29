namespace AgentRuntime.Core.Dtos;

public sealed record AgentResponse(Guid Id, string Name, string Description, DateTime CreatedAt, DateTime UpdatedAt);
