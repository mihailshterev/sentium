namespace Sentium.Praxis.Features.Conversations;

/// <param name="LastActivityAt">When the last message was added (the creation time for an older conversation).</param>
internal sealed record ConversationSummary(
    Guid Id,
    string Title,
    string Model,
    DateTime CreatedAt,
    string? AgentName = null,
    Guid? WorkspaceId = null,
    string? WorkspaceName = null,
    DateTime? LastActivityAt = null,
    bool IsPinned = false,
    bool IsArchived = false);
