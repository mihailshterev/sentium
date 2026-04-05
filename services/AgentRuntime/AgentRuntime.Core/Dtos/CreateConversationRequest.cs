namespace AgentRuntime.Core.Dtos;

public sealed record CreateConversationRequest(
    string Title,
    string Model);
