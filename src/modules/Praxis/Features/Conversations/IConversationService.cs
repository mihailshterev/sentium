using Sentium.Praxis.Dtos;
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
    Task<ConversationSummary> CreateConversationAsync(CreateConversationRequest request, CancellationToken ct = default);
    Task<bool> DeleteConversationAsync(Guid conversationId, CancellationToken ct = default);

    /// <summary>
    /// Whether the conversation exists and still waits for a generated title.
    /// </summary>
    Task<bool> IsTitlePendingAsync(Guid conversationId, CancellationToken ct = default);

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
    /// Appends a message (with optional enhanced prompt, thought, and tool calls) to a conversation.
    /// </summary>
    Task AddMessageAsync(Guid conversationId, string role, string content, string? enhancedPrompt = null, string? thought = null, IReadOnlyList<string>? toolCalls = null, CancellationToken ct = default);
}
