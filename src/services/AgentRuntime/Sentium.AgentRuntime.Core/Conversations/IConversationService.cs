using Sentium.AgentRuntime.Core.Dtos;

namespace Sentium.AgentRuntime.Core.Conversations;

public interface IConversationService
{
    Task<IReadOnlyList<ConversationSummary>> GetConversationsAsync(CancellationToken ct = default);
    Task<ConversationResponse> GetConversationAsync(Guid conversationId, CancellationToken ct = default);
    Task<ConversationSummary> CreateConversationAsync(CreateConversationRequest request, CancellationToken ct = default);
    Task DeleteConversationAsync(Guid conversationId, CancellationToken ct = default);
    Task AddMessageAsync(Guid conversationId, string role, string content, string? thought = null, IReadOnlyList<string>? toolCalls = null, CancellationToken ct = default);
}
