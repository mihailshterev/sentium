using Sentium.Core.Results;

namespace Sentium.Praxis.Features.Conversations;

/// <summary>
/// Application-layer operations for conversations and their messages.
/// </summary>
public interface IConversationService
{
    /// <summary>
    /// Returns a page of conversation summaries (newest first), optionally only those whose title contains
    /// <paramref name="search"/> (case-insensitive).
    /// </summary>
    Task<PagedResponse<ConversationSummary>> GetConversationsAsync(int page, int pageSize, string? search = null, CancellationToken ct = default);

    Task<ConversationResponse?> GetConversationAsync(Guid conversationId, CancellationToken ct = default);
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
    /// Appends a message (with optional enhanced prompt and ordered activity steps) to a conversation.
    /// </summary>
    Task AddMessageAsync(Guid conversationId, string role, string content, string? enhancedPrompt = null, IReadOnlyList<MessageStep>? steps = null, CancellationToken ct = default);
}
