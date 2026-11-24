
namespace Sentium.Praxis.Features.Conversations;

/// <summary>
/// Persistence-layer operations for conversations and their messages.
/// </summary>
internal interface IConversationRepository
{
    /// <summary>
    /// Returns a page of the archived or the active conversations (pinned first, then by latest activity) plus the
    /// total count, optionally only those whose title or any message contains <paramref name="search"/>
    /// (case-insensitive).
    /// </summary>
    Task<(IReadOnlyList<ConversationSummary> Items, int TotalCount)> GetConversationsAsync(
        int page,
        int pageSize,
        string? search = null,
        bool archived = false,
        CancellationToken ct = default);

    /// <summary>
    /// The conversation with its newest <paramref name="messageLimit"/> messages.
    /// </summary>
    Task<ConversationResponse?> GetConversationAsync(Guid conversationId, int messageLimit, CancellationToken ct = default);

    /// <summary>
    /// Up to <paramref name="limit"/> messages older than <paramref name="beforeMessageId"/>, oldest first;
    /// <see langword="null"/> when the conversation has no such message.
    /// </summary>
    Task<MessagePageResponse?> GetMessagesBeforeAsync(Guid conversationId, Guid beforeMessageId, int limit, CancellationToken ct = default);

    /// <returns><see langword="false"/> when the conversation does not exist.</returns>
    Task<bool> SetPinnedAsync(Guid conversationId, bool pinned, CancellationToken ct = default);

    /// <summary>
    /// Archives (and unpins) or unarchives a conversation.
    /// </summary>
    /// <returns><see langword="false"/> when the conversation does not exist.</returns>
    Task<bool> SetArchivedAsync(Guid conversationId, bool archived, CancellationToken ct = default);
    Task<ConversationSummary> CreateConversationAsync(CreateConversationRequest request, CancellationToken ct = default);
    Task<bool> DeleteConversationAsync(Guid conversationId, CancellationToken ct = default);

    /// <summary>
    /// Whether the conversation exists and still waits for a generated title.
    /// </summary>
    Task<bool> IsTitlePendingAsync(Guid conversationId, CancellationToken ct = default);

    /// <summary>
    /// The agent of <paramref name="userId"/>'s conversation, or <see langword="null"/> (general assistant, or not
    /// their conversation). Filters on the owner explicitly: a Sovereign bypasses the query filter.
    /// </summary>
    Task<string?> GetAgentNameAsync(Guid conversationId, Guid? userId, CancellationToken ct = default);

    /// <summary>
    /// Stores a generated title, but only while the conversation is still pending - a concurrent manual rename wins.
    /// </summary>
    /// <returns><see langword="true"/> when the title was stored.</returns>
    Task<bool> TrySetGeneratedTitleAsync(Guid conversationId, string title, CancellationToken ct = default);

    /// <summary>
    /// Sets a user-chosen title and stops automatic titling.
    /// </summary>
    /// <returns><see langword="false"/> when the conversation does not exist.</returns>
    Task<bool> RenameConversationAsync(Guid conversationId, string title, CancellationToken ct = default);

    /// <returns><see langword="false"/> when the conversation does not exist.</returns>
    Task<bool> SetWorkspaceAsync(Guid conversationId, Guid? workspaceId, CancellationToken ct = default);

    /// <summary>
    /// The workspace of <paramref name="userId"/>'s conversation; <see langword="null"/> when it has none or is not theirs.
    /// </summary>
    Task<Guid?> GetWorkspaceIdAsync(Guid conversationId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Filters on the owner explicitly: a Sovereign bypasses the query filter and must not bind another member's workspace.
    /// </summary>
    Task<bool> WorkspaceIsOwnedByAsync(Guid workspaceId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Appends a message (with optional enhanced prompt, activity steps and attached files) to a conversation, which
    /// becomes its latest activity and unarchives it.
    /// </summary>
    Task AddMessageAsync(
        Guid conversationId,
        string role,
        string content,
        string? enhancedPrompt = null,
        IReadOnlyList<MessageStep>? steps = null,
        IReadOnlyList<MessageAttachment>? attachments = null,
        CancellationToken ct = default);

    /// <summary>
    /// Whether <paramref name="conversationId"/> exists and belongs to <paramref name="userId"/>. Filters explicitly so the
    /// check also holds in system scopes that bypass the per-user query filter.
    /// </summary>
    Task<bool> IsOwnedByAsync(Guid conversationId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Returns up to <paramref name="limit"/> of the most recent messages of a conversation owned by <paramref name="userId"/>,
    /// oldest first. Empty when the conversation does not exist or belongs to someone else.
    /// </summary>
    Task<IReadOnlyList<MessageResponse>> GetRecentMessagesAsync(Guid conversationId, Guid userId, int limit, CancellationToken ct = default);

    /// <summary>
    /// The running summary of <paramref name="userId"/>'s conversation, or <see langword="null"/> when it has none (or
    /// is not theirs).
    /// </summary>
    Task<HistorySummary?> GetHistorySummaryAsync(Guid conversationId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Up to <paramref name="limit"/> messages, oldest first, that are older than the newest
    /// <paramref name="recentMessages"/> and newer than <paramref name="after"/> (the summary's coverage).
    /// </summary>
    Task<IReadOnlyList<MessageResponse>> GetMessagesBeyondWindowAsync(
        Guid conversationId,
        Guid userId,
        DateTime? after,
        int recentMessages,
        int limit,
        CancellationToken ct = default);

    Task SaveHistorySummaryAsync(Guid conversationId, Guid userId, HistorySummary summary, CancellationToken ct = default);
}
