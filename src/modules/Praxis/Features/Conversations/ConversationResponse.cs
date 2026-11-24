namespace Sentium.Praxis.Features.Conversations;

/// <param name="Messages">The most recent messages, oldest first.</param>
/// <param name="HasEarlierMessages">Whether older messages exist; load them page by page from the oldest one shown.</param>
internal sealed record ConversationResponse(
    Guid Id,
    string Title,
    string Model,
    DateTime CreatedAt,
    IReadOnlyList<MessageResponse> Messages,
    string? AgentName = null,
    Guid? WorkspaceId = null,
    string? WorkspaceName = null,
    bool HasEarlierMessages = false,
    bool IsPinned = false,
    bool IsArchived = false);
