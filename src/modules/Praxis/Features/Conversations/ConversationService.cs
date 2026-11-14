using Sentium.Core.Results;
using Sentium.Infrastructure.Caching;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Common;
using Sentium.Praxis.Features.Agents;

namespace Sentium.Praxis.Features.Conversations;

public sealed class ConversationService(
    IConversationRepository repository,
    IScopedCache cache,
    IAgentRoster agentRoster,
    ICurrentUser currentUser,
    ILogger<ConversationService> logger) : IConversationService
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
            token => LoadPageAsync(page, pageSize, term, token),
            CacheTag,
            ct);
    }

    public async Task<ConversationResponse?> GetConversationAsync(Guid conversationId, CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            $"{CacheTag}:{conversationId}",
            async token => await repository.GetConversationAsync(conversationId, token),
            CacheTag,
            ct);
    }

    public async Task<Result<ConversationSummary>> CreateConversationAsync(CreateConversationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string? agentName = null;
        if (!string.IsNullOrWhiteSpace(request.AgentName))
        {
            var agent = await agentRoster.FindAsync(request.AgentName, currentUser.UserId, ct);
            if (agent is null)
            {
                return Result<ConversationSummary>.Invalid($"There is no agent named '{request.AgentName.Trim()}' to chat with.");
            }

            // The general assistant is the default; store the canonical spelling of any other agent.
            agentName = string.Equals(agent.Name, AgentRole.GeneralAssistant, StringComparison.Ordinal) ? null : agent.Name;
        }

        var result = await repository.CreateConversationAsync(request with { AgentName = agentName }, ct);
        await cache.InvalidateTagAsync(CacheTag, ct);
        return Result<ConversationSummary>.Success(result);
    }

    public async Task<string> ResolveChatAgentAsync(Guid? conversationId, Guid? userId, CancellationToken ct = default)
    {
        if (conversationId is not { } id || await repository.GetAgentNameAsync(id, userId, ct) is not { } stored)
        {
            return AgentRole.GeneralAssistant;
        }

        if (await agentRoster.FindAsync(stored, userId, ct) is { } agent)
        {
            return agent.Name;
        }

        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning("Agent {AgentName} of conversation {ConversationId} no longer exists; using the general assistant.", stored, id);
        }

        return AgentRole.GeneralAssistant;
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

    public async Task AddMessageAsync(Guid conversationId, string role, string content, string? enhancedPrompt = null, IReadOnlyList<MessageStep>? steps = null, CancellationToken ct = default)
    {
        await repository.AddMessageAsync(conversationId, role, content, enhancedPrompt, steps, ct);
        await cache.RemoveAsync($"{CacheTag}:{conversationId}", ct);
    }

    private async ValueTask<PagedResponse<ConversationSummary>> LoadPageAsync(int page, int pageSize, string? term, CancellationToken ct)
    {
        var (items, total) = await repository.GetConversationsAsync(page, pageSize, term, ct);
        return PagedResponse<ConversationSummary>.Create(items, total, page, pageSize);
    }
}
