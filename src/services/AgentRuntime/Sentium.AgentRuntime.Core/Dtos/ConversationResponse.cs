namespace Sentium.AgentRuntime.Core.Dtos;

public sealed record ConversationResponse(
    Guid Id,
    string Title,
    string Model,
    DateTime CreatedAt,
    IReadOnlyList<MessageResponse> Messages);

public sealed record ConversationSummary(
    Guid Id,
    string Title,
    string Model,
    DateTime CreatedAt);
