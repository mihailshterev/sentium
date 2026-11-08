namespace Sentium.Praxis.Dtos;

public sealed record ConversationResponse(
    Guid Id,
    string Title,
    string Model,
    DateTime CreatedAt,
    IReadOnlyList<MessageResponse> Messages,
    string? AgentName = null);

public sealed record ConversationSummary(
    Guid Id,
    string Title,
    string Model,
    DateTime CreatedAt,
    string? AgentName = null);
