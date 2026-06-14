using Sentium.AgentRuntime.Core.Dtos;

namespace Sentium.AgentRuntime.Core.Conversations;

/// <summary>
/// Application-layer operations for conversations and their messages.
/// </summary>
public interface IConversationService
{
    Task<IReadOnlyList<ConversationSummary>> GetConversationsAsync(CancellationToken ct = default);
    Task<ConversationResponse?> GetConversationAsync(Guid conversationId, CancellationToken ct = default);
    Task<ConversationSummary> CreateConversationAsync(CreateConversationRequest request, CancellationToken ct = default);
    Task<bool> DeleteConversationAsync(Guid conversationId, CancellationToken ct = default);
    /// <summary>
    /// Appends a message (with optional enhanced prompt, thought, and tool calls) to a conversation.
    /// </summary>
    Task AddMessageAsync(Guid conversationId, string role, string content, string? enhancedPrompt = null, string? thought = null, IReadOnlyList<string>? toolCalls = null, CancellationToken ct = default);
}
