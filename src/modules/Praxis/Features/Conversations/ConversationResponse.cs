namespace Sentium.Praxis.Features.Conversations;

internal sealed record ConversationResponse(
    Guid Id,
    string Title,
    string Model,
    DateTime CreatedAt,
    IReadOnlyList<MessageResponse> Messages,
    string? AgentName = null);
