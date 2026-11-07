using Sentium.Praxis.Dtos;

namespace Sentium.Praxis.Features.Conversations;

/// <summary>
/// Persistence-layer operations for conversations and their messages.
/// </summary>
public interface IConversationRepository
{
    /// <summary>
    /// Returns a page of conversation summaries (newest first) plus the total count, optionally only those whose title
    /// contains <paramref name="search"/> (case-insensitive).
    /// </summary>
    Task<(IReadOnlyList<ConversationSummary> Items, int TotalCount)> GetConversationsAsync(int page, int pageSize, string? search = null, CancellationToken ct = default);
    Task<ConversationResponse?> GetConversationAsync(Guid conversationId, CancellationToken ct = default);
    Task<ConversationSummary> CreateConversationAsync(CreateConversationRequest request, CancellationToken ct = default);
    Task<bool> DeleteConversationAsync(Guid conversationId, CancellationToken ct = default);

    /// <summary>
    /// Whether the conversation exists and still waits for a generated title.
    /// </summary>
    Task<bool> IsTitlePendingAsync(Guid conversationId, CancellationToken ct = default);

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

    /// <summary>
    /// Appends a message (with optional enhanced prompt, thought, and tool calls) to a conversation.
    /// </summary>
    Task AddMessageAsync(Guid conversationId, string role, string content, string? enhancedPrompt = null, string? thought = null, IReadOnlyList<string>? toolCalls = null, CancellationToken ct = default);

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
}
