namespace Sentium.Praxis.Features.Conversations;

public sealed record ConversationResponse(
    Guid Id,
    string Title,
    string Model,
    DateTime CreatedAt,
    IReadOnlyList<MessageResponse> Messages,
    string? AgentName = null);
