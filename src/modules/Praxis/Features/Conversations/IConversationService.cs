using Sentium.Core.Results;

namespace Sentium.Praxis.Features.Conversations;

internal interface IConversationService
{
    /// <summary>
    /// Returns a page of the archived or the active conversations (pinned first, then by latest activity), optionally
    /// only those whose title or any message contains <paramref name="search"/> (case-insensitive).
    /// </summary>
    Task<PagedResponse<ConversationSummary>> GetConversationsAsync(
        int page,
        int pageSize,
        string? search = null,
        bool archived = false,
        CancellationToken ct = default);

    /// <summary>
    /// The conversation with its newest <paramref name="messageLimit"/> messages (clamped to the maximum page).
    /// </summary>
    Task<ConversationResponse?> GetConversationAsync(Guid conversationId, int messageLimit = ConversationService.DefaultMessageLimit, CancellationToken ct = default);

    /// <summary>
    /// Up to <paramref name="limit"/> messages older than <paramref name="beforeMessageId"/>, oldest first;
    /// <see langword="null"/> when the conversation has no such message.
    /// </summary>
    Task<MessagePageResponse?> GetMessagesBeforeAsync(Guid conversationId, Guid beforeMessageId, int limit, CancellationToken ct = default);

    /// <returns><see langword="false"/> when the conversation does not exist.</returns>
    Task<bool> SetPinnedAsync(Guid conversationId, bool pinned, CancellationToken ct = default);

    /// <summary>
    /// Archives (and unpins) or unarchives a conversation. A new message unarchives it too.
    /// </summary>
    /// <returns><see langword="false"/> when the conversation does not exist.</returns>
    Task<bool> SetArchivedAsync(Guid conversationId, bool archived, CancellationToken ct = default);

    /// <summary>
    /// Creates a conversation, optionally with a specific agent (a built-in specialist or one of the user's agents).
    /// An unknown agent or a control role is <see cref="ResultStatus.Invalid"/>.
    /// </summary>
    Task<Result<ConversationSummary>> CreateConversationAsync(CreateConversationRequest request, CancellationToken ct = default);

    Task<bool> DeleteConversationAsync(Guid conversationId, CancellationToken ct = default);

    /// <summary>
    /// Whether the conversation exists and still waits for a generated title.
    /// </summary>
    Task<bool> IsTitlePendingAsync(Guid conversationId, CancellationToken ct = default);

    /// <summary>
    /// The agent a chat turn runs as: the conversation's agent when it still exists for <paramref name="userId"/>,
    /// otherwise the general assistant.
    /// </summary>
    Task<string> ResolveChatAgentAsync(Guid? conversationId, Guid? userId, CancellationToken ct = default);

    /// <summary>
    /// Stores a generated title while the conversation is still pending (a manual rename wins).
    /// </summary>
    /// <returns><see langword="true"/> when the title was stored.</returns>
    Task<bool> TrySetGeneratedTitleAsync(Guid conversationId, string title, CancellationToken ct = default);

    /// <summary>
    /// Sets a user-chosen title and stops automatic titling.
    /// </summary>
    /// <returns><see langword="false"/> when the conversation does not exist.</returns>
    Task<bool> RenameConversationAsync(Guid conversationId, string title, CancellationToken ct = default);

    /// <summary>
    /// Binds the conversation to one of the current user's workspaces, or unbinds it with <see langword="null"/>.
    /// <see cref="ResultStatus.Invalid"/> for a workspace the user does not own.
    /// </summary>
    Task<Result<bool>> SetWorkspaceAsync(Guid conversationId, Guid? workspaceId, CancellationToken ct = default);

    /// <summary>
    /// The workspace of <paramref name="userId"/>'s conversation; <see langword="null"/> when it has none or is not theirs.
    /// </summary>
    Task<Guid?> GetWorkspaceIdAsync(Guid conversationId, Guid userId, CancellationToken ct = default);

    Task AddMessageAsync(
        Guid conversationId,
        string role,
        string content,
        string? enhancedPrompt = null,
        IReadOnlyList<MessageStep>? steps = null,
        IReadOnlyList<MessageAttachment>? attachments = null,
        CancellationToken ct = default);
}
