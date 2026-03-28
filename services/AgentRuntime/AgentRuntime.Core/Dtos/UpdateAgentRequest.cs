namespace AgentRuntime.Core.Dtos;

public sealed record UpdateAgentRequest(Guid Id, string Name, string Description);
