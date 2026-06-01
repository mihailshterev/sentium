using Sentium.AgentRuntime.Core.Conversations;
using Sentium.AgentRuntime.Core.Dtos;

namespace Sentium.AgentRuntime.Application.Conversations;

public sealed class ConversationService(IConversationRepository repository) : IConversationService
{
    public Task<IReadOnlyList<ConversationSummary>> GetConversationsAsync(CancellationToken ct = default)
        => repository.GetConversationsAsync(ct);

    public Task<ConversationResponse> GetConversationAsync(Guid conversationId, CancellationToken ct = default)
        => repository.GetConversationAsync(conversationId, ct);

    public Task<ConversationSummary> CreateConversationAsync(CreateConversationRequest request, CancellationToken ct = default)
        => repository.CreateConversationAsync(request, ct);

    public Task DeleteConversationAsync(Guid conversationId, CancellationToken ct = default)
        => repository.DeleteConversationAsync(conversationId, ct);

    public Task AddMessageAsync(Guid conversationId, string role, string content, string? enhancedPrompt = null, string? thought = null, IReadOnlyList<string>? toolCalls = null, CancellationToken ct = default)
        => repository.AddMessageAsync(conversationId, role, content, enhancedPrompt, thought, toolCalls, ct);
}
