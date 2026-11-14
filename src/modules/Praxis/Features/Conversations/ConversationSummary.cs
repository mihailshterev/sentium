namespace Sentium.Praxis.Features.Conversations;

public sealed record ConversationSummary(
    Guid Id,
    string Title,
    string Model,
    DateTime CreatedAt,
    string? AgentName = null);
