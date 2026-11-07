using Sentium.Praxis.Dtos;

namespace Sentium.Praxis.Features.Conversations;

/// <summary>
/// Persistence-layer operations for conversations and their messages.
/// </summary>
public interface IConversationRepository
{
    /// <summary>
    /// Returns a page of conversation summaries (newest first) plus the total count.
    /// </summary>
    Task<(IReadOnlyList<ConversationSummary> Items, int TotalCount)> GetConversationsAsync(int page, int pageSize, CancellationToken ct = default);
    Task<ConversationResponse?> GetConversationAsync(Guid conversationId, CancellationToken ct = default);
    Task<ConversationSummary> CreateConversationAsync(CreateConversationRequest request, CancellationToken ct = default);
    Task<bool> DeleteConversationAsync(Guid conversationId, CancellationToken ct = default);
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
