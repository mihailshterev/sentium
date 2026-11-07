using Sentium.Praxis.Dtos;
using Sentium.Infrastructure.Caching;
using Sentium.Core.Results;
using Sentium.Praxis.Common;

namespace Sentium.Praxis.Features.Conversations;

public sealed class ConversationService(
    IConversationRepository repository,
    IScopedCache cache) : IConversationService
{
    private const string CacheTag = "conversations";

    public async Task<PagedResponse<ConversationSummary>> GetConversationsAsync(int page, int pageSize, string? search = null, CancellationToken ct = default)
    {
        var query = new PaginationQuery { Page = page, PageSize = pageSize };
        (page, pageSize) = query.Normalize();
        var term = TextSearch.Normalize(search);
        var key = term is null
            ? $"{CacheTag}:page:{page}:{pageSize}"
            : $"{CacheTag}:page:{page}:{pageSize}:q:{term.ToLowerInvariant()}";

        return await cache.GetOrCreateAsync(
            key,
            async token =>
            {
                var (items, total) = await repository.GetConversationsAsync(page, pageSize, term, token);
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

    public Task<bool> IsTitlePendingAsync(Guid conversationId, CancellationToken ct = default)
        => repository.IsTitlePendingAsync(conversationId, ct);

    public async Task<bool> TrySetGeneratedTitleAsync(Guid conversationId, string title, CancellationToken ct = default)
    {
        var updated = await repository.TrySetGeneratedTitleAsync(conversationId, title, ct);
        if (updated)
        {
            await cache.InvalidateTagAsync(CacheTag, ct);
        }

        return updated;
    }

    public async Task<bool> RenameConversationAsync(Guid conversationId, string title, CancellationToken ct = default)
    {
        var renamed = await repository.RenameConversationAsync(conversationId, title.Trim(), ct);
        if (renamed)
        {
            await cache.InvalidateTagAsync(CacheTag, ct);
        }

        return renamed;
    }

    public async Task AddMessageAsync(Guid conversationId, string role, string content, string? enhancedPrompt = null, string? thought = null, IReadOnlyList<string>? toolCalls = null, CancellationToken ct = default)
    {
        await repository.AddMessageAsync(conversationId, role, content, enhancedPrompt, thought, toolCalls, ct);
        await cache.RemoveAsync($"{CacheTag}:{conversationId}", ct);
    }
}
