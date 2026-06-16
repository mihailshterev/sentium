using Sentium.AgentRuntime.Core.Conversations;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.Infrastructure.Caching;
using Sentium.Shared.Results;

namespace Sentium.AgentRuntime.Application.Conversations;

public sealed class ConversationService(
    IConversationRepository repository,
    IScopedCache cache) : IConversationService
{
    private const string CacheTag = "conversations";

    public async Task<PagedResponse<ConversationSummary>> GetConversationsAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = new PaginationQuery { Page = page, PageSize = pageSize };
        (page, pageSize) = query.Normalize();

        return await cache.GetOrCreateAsync(
            $"{CacheTag}:page:{page}:{pageSize}",
            async token =>
            {
                var (items, total) = await repository.GetConversationsAsync(page, pageSize, token);
                return PagedResponse<ConversationSummary>.Create(items, total, page, pageSize);
            },
            CacheTag,
            ct);
    }

    public async Task<ConversationResponse?> GetConversationAsync(Guid conversationId, CancellationToken ct = default)
        => await cache.GetOrCreateAsync(
            $"{CacheTag}:{conversationId}",
            async token => await repository.GetConversationAsync(conversationId, token),
            CacheTag,
            ct);

    public async Task<ConversationSummary> CreateConversationAsync(CreateConversationRequest request, CancellationToken ct = default)
    {
        var result = await repository.CreateConversationAsync(request, ct);
        await cache.InvalidateTagAsync(CacheTag, ct);
        return result;
    }

    public async Task<bool> DeleteConversationAsync(Guid conversationId, CancellationToken ct = default)
    {
        var deleted = await repository.DeleteConversationAsync(conversationId, ct);
        if (deleted)
        {
            await cache.InvalidateTagAsync(CacheTag, ct);
        }

        return deleted;
    }

    public async Task AddMessageAsync(Guid conversationId, string role, string content, string? enhancedPrompt = null, string? thought = null, IReadOnlyList<string>? toolCalls = null, CancellationToken ct = default)
    {
        await repository.AddMessageAsync(conversationId, role, content, enhancedPrompt, thought, toolCalls, ct);
        await cache.RemoveAsync($"{CacheTag}:{conversationId}", ct);
    }
}
