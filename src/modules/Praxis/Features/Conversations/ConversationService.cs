using Sentium.Core.Results;
using Sentium.Infrastructure.Caching;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Common;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Agents.Runtime;

namespace Sentium.Praxis.Features.Conversations;

internal sealed class ConversationService(
    IConversationRepository repository,
    IScopedCache cache,
    IAgentRoster agentRoster,
    ICurrentUser currentUser,
    ILogger<ConversationService> logger) : IConversationService
{
    public const int DefaultMessageLimit = 100;
    public const int MaxMessageLimit = 500;

    private const string CacheTag = "conversations";

    public async Task<PagedResponse<ConversationSummary>> GetConversationsAsync(
        int page,
        int pageSize,
        string? search = null,
        bool archived = false,
        CancellationToken ct = default)
    {
        var query = new PaginationQuery { Page = page, PageSize = pageSize };
        (page, pageSize) = query.Normalize();
        var term = TextSearch.Normalize(search);
        var key = $"{CacheTag}:page:{page}:{pageSize}:archived:{archived}";
        if (term is not null)
        {
            key += $":q:{term.ToLowerInvariant()}";
        }

        return await cache.GetOrCreateAsync(
            key,
            token => LoadPageAsync(page, pageSize, term, archived, token),
            CacheTag,
            ct);
    }

    public async Task<ConversationResponse?> GetConversationAsync(Guid conversationId, int messageLimit = DefaultMessageLimit, CancellationToken ct = default)
    {
        var limit = Math.Clamp(messageLimit, 1, MaxMessageLimit);
        return await cache.GetOrCreateAsync(
            $"{CacheTag}:{conversationId}:{limit}",
            token => LoadConversationAsync(conversationId, limit, token),
            CacheTag,
            ct);
    }

    public Task<MessagePageResponse?> GetMessagesBeforeAsync(Guid conversationId, Guid beforeMessageId, int limit, CancellationToken ct = default)
    {
        return repository.GetMessagesBeforeAsync(conversationId, beforeMessageId, Math.Clamp(limit, 1, MaxMessageLimit), ct);
    }

    public async Task<bool> SetPinnedAsync(Guid conversationId, bool pinned, CancellationToken ct = default)
    {
        var updated = await repository.SetPinnedAsync(conversationId, pinned, ct);
        if (updated)
        {
            await cache.InvalidateTagAsync(CacheTag, ct);
        }

        return updated;
    }

    public async Task<bool> SetArchivedAsync(Guid conversationId, bool archived, CancellationToken ct = default)
    {
        var updated = await repository.SetArchivedAsync(conversationId, archived, ct);
        if (updated)
        {
            await cache.InvalidateTagAsync(CacheTag, ct);
        }

        return updated;
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

        if (request.WorkspaceId is { } workspaceId && !await IsOwnWorkspaceAsync(workspaceId, ct))
        {
            return Result<ConversationSummary>.Invalid("There is no such workspace.");
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

    public async Task<Result<bool>> SetWorkspaceAsync(Guid conversationId, Guid? workspaceId, CancellationToken ct = default)
    {
        if (workspaceId is { } id && !await IsOwnWorkspaceAsync(id, ct))
        {
            return Result<bool>.Invalid("There is no such workspace.");
        }

        if (!await repository.SetWorkspaceAsync(conversationId, workspaceId, ct))
        {
            return Result<bool>.NotFound();
        }

        await cache.InvalidateTagAsync(CacheTag, ct);
        return Result<bool>.Success(true);
    }

    public Task<Guid?> GetWorkspaceIdAsync(Guid conversationId, Guid userId, CancellationToken ct = default)
        => repository.GetWorkspaceIdAsync(conversationId, userId, ct);

    public async Task AddMessageAsync(
        Guid conversationId,
        string role,
        string content,
        string? enhancedPrompt = null,
        IReadOnlyList<MessageStep>? steps = null,
        IReadOnlyList<MessageAttachment>? attachments = null,
        CancellationToken ct = default)
    {
        await repository.AddMessageAsync(conversationId, role, content, enhancedPrompt, steps, attachments, ct);

        // The message changes the conversation and its place in the list.
        await cache.InvalidateTagAsync(CacheTag, ct);
    }

    private async Task<bool> IsOwnWorkspaceAsync(Guid workspaceId, CancellationToken ct)
    {
        return currentUser.UserId is { } userId && await repository.WorkspaceIsOwnedByAsync(workspaceId, userId, ct);
    }

    private async ValueTask<PagedResponse<ConversationSummary>> LoadPageAsync(int page, int pageSize, string? term, bool archived, CancellationToken ct)
    {
        var (items, total) = await repository.GetConversationsAsync(page, pageSize, term, archived, ct);
        return PagedResponse<ConversationSummary>.Create(items, total, page, pageSize);
    }

    private async ValueTask<ConversationResponse?> LoadConversationAsync(Guid conversationId, int messageLimit, CancellationToken ct)
    {
        return await repository.GetConversationAsync(conversationId, messageLimit, ct);
    }
}
