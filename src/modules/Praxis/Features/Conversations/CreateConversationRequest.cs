namespace Sentium.Praxis.Features.Conversations;

/// <summary>
/// Creates a conversation. Without a <paramref name="Title"/> the conversation starts as
/// <see cref="ConversationTitles.Placeholder"/> and is titled from its first prompt. A <paramref name="WorkspaceId"/>
/// binds it to one of the user's workspaces from the start.
/// </summary>
internal sealed record CreateConversationRequest(
    string? Title,
    string Model,
    string? AgentName = null,
    Guid? WorkspaceId = null);
