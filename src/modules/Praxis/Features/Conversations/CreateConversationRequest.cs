namespace Sentium.Praxis.Features.Conversations;

/// <summary>
/// Creates a conversation. Without a <paramref name="Title"/> the conversation starts as
/// <see cref="ConversationTitles.Placeholder"/> and is titled from its first prompt.
/// </summary>
public sealed record CreateConversationRequest(
    string? Title,
    string Model,
    string? AgentName = null);
